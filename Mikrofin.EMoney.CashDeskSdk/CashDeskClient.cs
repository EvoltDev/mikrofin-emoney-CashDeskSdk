using System;
using System.Buffers;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Mikrofin.EMoney.CashDeskSdk.Messaging;

namespace Mikrofin.EMoney.CashDeskSdk;

/// <summary>
///     High-level client that wraps the cash desk WebSocket protocol and exposes strongly typed events and commands.
/// </summary>
public sealed class CashDeskClient : IAsyncDisposable
{
    private readonly CashDeskClientOptions options;
    private readonly JsonSerializerOptions serializerOptions;
    private readonly SemaphoreSlim sendLock = new(1, 1);
    private ClientWebSocket? socket;
    private CancellationTokenSource? receiveLoopCts;
    private Task? receiveLoopTask;

    public CashDeskClient(CashDeskClientOptions options, JsonSerializerOptions? serializerOptions = null)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.serializerOptions = serializerOptions ?? new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        };
    }

    public event EventHandler<CashierLoginSuccessPayload>? CashierLoginSucceeded;
    public event EventHandler<CashDeskErrorPayload>? CashierLoginFailed;
    public event EventHandler<PaymentCreatedPayload>? PaymentCreated;
    public event EventHandler<PaymentCompletedPayload>? PaymentCompleted;
    public event EventHandler<PaymentCanceledPayload>? PaymentCanceled;
    public event EventHandler<PaymentStornoResultPayload>? PaymentStornoResultReceived;
    public event EventHandler<TransactionCreateErrorPayload>? TransactionCreateFailed;
    public event EventHandler<CashInCreatedPayload>? CashInCreated;
    public event EventHandler<CashInCompletedPayload>? CashInCompleted;
    public event EventHandler<CashInCanceledPayload>? CashInCanceled;
    public event EventHandler<CashOutCreatedPayload>? CashOutCreated;
    public event EventHandler<CashOutCompletedPayload>? CashOutCompleted;
    public event EventHandler<CashOutCanceledPayload>? CashOutCanceled;
    public event EventHandler<CashOutPaidByUserPayload>? CashOutPaidByUser;
    public event EventHandler<CashDeskErrorPayload>? GeneralErrorReceived;
    public event EventHandler<ConnectionClosedEventArgs>? ConnectionClosed;

    public bool IsConnected => socket is { State: WebSocketState.Open };

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (IsConnected)
        {
            return;
        }

        await DisconnectAsync().ConfigureAwait(false);

        socket = new ClientWebSocket();
        socket.Options.KeepAliveInterval = options.KeepAliveInterval;
        foreach (var header in options.Headers)
        {
            socket.Options.SetRequestHeader(header.Key, header.Value);
        }

        await socket.ConnectAsync(options.Endpoint, cancellationToken).ConfigureAwait(false);
        Log($"Connected to {options.Endpoint}.");

        receiveLoopCts = new CancellationTokenSource();
        receiveLoopTask = Task.Run(() => ReceiveLoopAsync(receiveLoopCts.Token));
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        receiveLoopCts?.Cancel();
        if (receiveLoopTask is not null)
        {
            try
            {
                await receiveLoopTask.ConfigureAwait(false);
            }
            catch when (receiveLoopCts?.IsCancellationRequested == true)
            {
                // swallow cancellation coming from disposal
            }
        }

        if (socket is { State: WebSocketState.Open or WebSocketState.CloseReceived })
        {
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnect", cancellationToken).ConfigureAwait(false);
        }

        socket?.Dispose();
        socket = null;
        receiveLoopCts?.Dispose();
        receiveLoopCts = null;
    }

    public Task LoginAsync(CashierLoginRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }
        return SendAsync(CashDeskMessageTypes.CashierLogin, request, cancellationToken);
    }

    public Task CreatePaymentAsync(CashDeskPaymentCreateRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }
        return SendAsync(CashDeskMessageTypes.PaymentCreate, request, cancellationToken);
    }

    public Task CancelPaymentAsync(Guid paymentId, CancellationToken cancellationToken = default)
    {
        return SendAsync(CashDeskMessageTypes.PaymentCancel, new CashDeskPaymentCancelRequest(paymentId), cancellationToken);
    }

    public Task StornoPaymentAsync(Guid paymentId, CancellationToken cancellationToken = default)
    {
        if (paymentId == Guid.Empty) throw new ArgumentException("A payment ID is required.", nameof(paymentId));
        return SendAsync(CashDeskMessageTypes.PaymentStorno, new CashDeskPaymentStornoRequest(paymentId), cancellationToken);
    }

    public Task CreateCashInAsync(CashDeskCashInCreateRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }
        return SendAsync(CashDeskMessageTypes.CashInCreate, request, cancellationToken);
    }

    public Task CreateCashOutAsync(CashDeskCashOutCreateRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }
        return SendAsync(CashDeskMessageTypes.CashOutCreate, request, cancellationToken);
    }

    public Task CompleteCashOutAsync(Guid cashOutId, CancellationToken cancellationToken = default)
    {
        return SendAsync(CashDeskMessageTypes.CashOutComplete, new CashDeskCashOutCompleteRequest(cashOutId), cancellationToken);
    }

    public Task CancelCashInAsync(Guid cashInId, CancellationToken cancellationToken = default)
    {
        return SendAsync(CashDeskMessageTypes.CashInCancel, new CashDeskCashInCancelRequest(cashInId), cancellationToken);
    }

    public Task CancelCashOutAsync(Guid cashOutId, CancellationToken cancellationToken = default)
    {
        return SendAsync(CashDeskMessageTypes.CashOutCancel, new CashDeskCashOutCancelRequest(cashOutId), cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
        sendLock.Dispose();
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        if (socket == null)
        {
            return;
        }

        var buffer = ArrayPool<byte>.Shared.Rent(Math.Max(options.ReceiveBufferSize, 4 * 1024));
        var memoryStream = new MemoryStream();

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var segment = new ArraySegment<byte>(buffer);
                WebSocketReceiveResult result;

                do
                {
                    result = await socket.ReceiveAsync(segment, cancellationToken).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await HandleServerCloseAsync(result).ConfigureAwait(false);
                        return;
                    }

                    memoryStream.Write(segment.Array!, segment.Offset, result.Count);
                } while (!result.EndOfMessage);

                var payload = Encoding.UTF8.GetString(memoryStream.GetBuffer(), 0, (int)memoryStream.Length);
                memoryStream.SetLength(0);

                ProcessIncomingPayload(payload);
            }
        }
        catch (OperationCanceledException)
        {
            // expected during shutdown
        }
        catch (Exception ex)
        {
            Log($"Receive loop faulted: {ex}");
            ConnectionClosed?.Invoke(this, new ConnectionClosedEventArgs(null, ex.Message));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            memoryStream.Dispose();
        }
    }

    private void ProcessIncomingPayload(string json)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<CashDeskIncomingMessage>(json, serializerOptions);
            if (envelope == null || string.IsNullOrWhiteSpace(envelope.Type))
            {
                Log("Received an empty envelope from server.");
                return;
            }

            switch (envelope.Type)
            {
                case CashDeskMessageTypes.CashierLoginSuccess:
                    Dispatch(envelope, CashierLoginSucceeded);
                    break;
                case CashDeskMessageTypes.CashierLoginError:
                    Dispatch(envelope, CashierLoginFailed);
                    break;
                case CashDeskMessageTypes.PaymentCreated:
                    Dispatch(envelope, PaymentCreated);
                    break;
                case CashDeskMessageTypes.PaymentCompleted:
                    Dispatch(envelope, PaymentCompleted);
                    break;
                case CashDeskMessageTypes.PaymentCanceled:
                    Dispatch(envelope, PaymentCanceled);
                    break;
                case CashDeskMessageTypes.PaymentStornoResult:
                    Dispatch(envelope, PaymentStornoResultReceived);
                    break;
                case CashDeskMessageTypes.TransactionCreateError:
                    Dispatch(envelope, TransactionCreateFailed);
                    break;
                case CashDeskMessageTypes.CashInCreated:
                    Dispatch(envelope, CashInCreated);
                    break;
                case CashDeskMessageTypes.CashInCompleted:
                    Dispatch(envelope, CashInCompleted);
                    break;
                case CashDeskMessageTypes.CashInCanceled:
                    Dispatch(envelope, CashInCanceled);
                    break;
                case CashDeskMessageTypes.CashOutCreated:
                    Dispatch(envelope, CashOutCreated);
                    break;
                case CashDeskMessageTypes.CashOutPaidByUser:
                    Dispatch(envelope, CashOutPaidByUser);
                    break;
                case CashDeskMessageTypes.CashOutCompleted:
                    Dispatch(envelope, CashOutCompleted);
                    break;
                case CashDeskMessageTypes.CashOutCanceled:
                    Dispatch(envelope, CashOutCanceled);
                    break;
                case CashDeskMessageTypes.GeneralError:
                    Dispatch(envelope, GeneralErrorReceived);
                    break;
                default:
                    Log($"Ignoring unsupported message type '{envelope.Type}'.");
                    break;
            }
        }
        catch (Exception ex)
        {
            Log($"Failed to process server message: {ex}");
        }
    }

    private void Dispatch<TPayload>(CashDeskIncomingMessage envelope, EventHandler<TPayload>? handler)
    {
        if (handler == null)
        {
            return;
        }

        try
        {
            var payload = envelope.Payload.Deserialize<TPayload>(serializerOptions);
            if (payload != null)
            {
                handler.Invoke(this, payload);
            }
        }
        catch (JsonException jsonException)
        {
            Log($"Unable to deserialize payload for '{envelope.Type}': {jsonException.Message}");
        }
    }

    private async Task HandleServerCloseAsync(WebSocketReceiveResult result)
    {
        var status = result.CloseStatus;
        var description = result.CloseStatusDescription;
        Log($"Server closed the connection ({status}) {description}");
        ConnectionClosed?.Invoke(this, new ConnectionClosedEventArgs(status, description));

        if (socket is { State: WebSocketState.CloseReceived })
        {
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Acknowledged", CancellationToken.None).ConfigureAwait(false);
        }
    }

    private async Task SendAsync<TPayload>(string messageType, TPayload payload, CancellationToken cancellationToken)
    {
        if (payload == null)
        {
            throw new ArgumentNullException(nameof(payload));
        }
        var activeSocket = socket ?? throw new InvalidOperationException("The client is not connected. Call ConnectAsync first.");

        var envelope = new CashDeskOutgoingEnvelope<TPayload>(messageType, payload);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(envelope, serializerOptions);
        var segment = new ArraySegment<byte>(bytes);
        await sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await activeSocket.SendAsync(segment, WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            sendLock.Release();
        }
    }

    private void Log(string message)
    {
        options.DiagnosticLogger?.Invoke(message);
    }
}

public sealed class ConnectionClosedEventArgs : EventArgs
{
    public ConnectionClosedEventArgs(WebSocketCloseStatus? status, string? description)
    {
        Status = status;
        Description = description;
    }

    public WebSocketCloseStatus? Status { get; }
    public string? Description { get; }
}
