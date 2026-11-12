using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mikrofin.EMoney.CashDeskSdk.Messaging;

internal static class CashDeskMessageTypes
{
    public const string CashierLogin = "cashier.login";
    public const string CashierLoginSuccess = "cashier.login.success";
    public const string CashierLoginError = "cashier.login.error";
    public const string PaymentCreate = "payment.create";
    public const string PaymentCreated = "payment.created";
    public const string TransactionCreateError = "transaction.create.error";
    public const string PaymentCancel = "payment.cancel";
    public const string PaymentStorno = "payment.storno";
    public const string PaymentStornoResult = "payment.storno.result";
    public const string PaymentCanceled = "payment.canceled";
    public const string PaymentCompleted = "payment.completed";
    public const string CashInCreate = "cashIn.create";
    public const string CashInCreated = "cashIn.created";
    public const string CashInCancel = "cashIn.cancel";
    public const string CashInCanceled = "cashIn.canceled";
    public const string CashInCompleted = "cashIn.completed";
    public const string CashOutCreate = "cashOut.create";
    public const string CashOutCreated = "cashOut.created";
    public const string CashOutCancel = "cashOut.cancel";
    public const string CashOutCanceled = "cashOut.canceled";
    public const string CashOutComplete = "cashOut.complete";
    public const string CashOutCompleted = "cashOut.completed";
    public const string CashOutPaidByUser = "cashout.paid";
    public const string GeneralError = "cashdesk.error";
}

internal sealed class CashDeskOutgoingEnvelope<T>
{
    public CashDeskOutgoingEnvelope()
    {
    }

    public CashDeskOutgoingEnvelope(string type, T payload)
    {
        Type = type;
        Payload = payload;
    }

    public string Type { get; set; } = string.Empty;
    public T Payload { get; set; } = default!;
}

internal sealed class CashDeskIncomingMessage
{
    public string Type { get; set; } = string.Empty;
    public JsonElement Payload { get; set; }
}

public sealed class CashierLoginRequest
{
    public CashierLoginRequest(string accountId, string userName, string password)
    {
        AccountId = accountId;
        UserName = userName;
        Password = password;
    }

    public string AccountId { get; }
    public string UserName { get; }
    public string Password { get; }
}

public sealed class CashDeskPaymentLineItemRequest
{
    public CashDeskPaymentLineItemRequest(string name, decimal unitPrice, int quantity = 1)
    {
        Name = name;
        UnitPrice = unitPrice;
        Quantity = quantity;
    }

    public string Name { get; }
    public decimal UnitPrice { get; }
    public int Quantity { get; }
}

public sealed class CashDeskPaymentMetadata
{
    public CashDeskPaymentMetadata(string key, string value, bool displayToUser = false)
    {
        Key = key;
        Value = value;
        DisplayToUser = displayToUser;
    }

    public string Key { get; }
    public string Value { get; }
    public bool DisplayToUser { get; }
}

public sealed class CashDeskPaymentCreateRequest
{
    public CashDeskPaymentCreateRequest(
        decimal totalAmount,
        string currency,
        IReadOnlyList<CashDeskPaymentLineItemRequest> lineItems,
        IReadOnlyList<CashDeskPaymentMetadata> paymentMetadata)
    {
        TotalAmount = totalAmount;
        Currency = currency;
        LineItems = lineItems;
        PaymentMetadata = paymentMetadata;
    }

    public decimal TotalAmount { get; }
    public string Currency { get; }
    public IReadOnlyList<CashDeskPaymentLineItemRequest> LineItems { get; }
    public IReadOnlyList<CashDeskPaymentMetadata> PaymentMetadata { get; }
}

public sealed class CashDeskPaymentCancelRequest
{
    public CashDeskPaymentCancelRequest(Guid paymentId)
    {
        PaymentId = paymentId;
    }

    public Guid PaymentId { get; }
}

public sealed class CashDeskPaymentStornoRequest
{
    public CashDeskPaymentStornoRequest(Guid paymentId) => PaymentId = paymentId;
    public Guid PaymentId { get; }
}

public sealed class CashDeskCashInCreateRequest
{
    public CashDeskCashInCreateRequest(
        decimal totalAmount,
        string currency)
    {
        TotalAmount = totalAmount;
        Currency = currency;
    }

    public decimal TotalAmount { get; }
    public string Currency { get; }
}

public sealed class CashDeskCashOutCreateRequest
{
    public CashDeskCashOutCreateRequest(
        decimal totalAmount,
        string currency)
    {
        TotalAmount = totalAmount;
        Currency = currency;
    }

    public decimal TotalAmount { get; }
    public string Currency { get; }
}

public sealed class CashDeskCashOutCompleteRequest
{
    public CashDeskCashOutCompleteRequest(Guid cashOutId)
    {
        CashOutId = cashOutId;
    }

    public Guid CashOutId { get; }
}

public sealed class CashDeskCashInCancelRequest
{
    public CashDeskCashInCancelRequest(Guid cashInId)
    {
        CashInId = cashInId;
    }

    public Guid CashInId { get; }
}

public sealed class CashDeskCashOutCancelRequest
{
    public CashDeskCashOutCancelRequest(Guid cashOutId)
    {
        CashOutId = cashOutId;
    }

    public Guid CashOutId { get; }
}
public sealed class CashierLoginSuccessPayload
{
    public CashDeskCashierInfo Cashier { get; set; } = new();
    public PaymentDetailsResponse? PendingPayment { get; set; }
    public string? PaymentDeepLink { get; set; }
    public CashInDetailsResponse? PendingCashIn { get; set; }
    public string? CashInDeepLink { get; set; }
    public CashOutDetailsResponse? PendingCashOut { get; set; }
    public string? CashOutDeepLink { get; set; }
}

public sealed class CashDeskCashierInfo
{
    public Guid Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public Guid LocationId { get; set; }
    public string LocationName { get; set; } = string.Empty;
    public string LocationAddress { get; set; } = string.Empty;
}

public sealed class PaymentCreatedPayload
{
    public PaymentDetailsResponse Payment { get; set; } = new();
    public string PaymentDeepLink { get; set; } = string.Empty;
}

public sealed class PaymentCompletedPayload
{
    public PaymentDetailsResponse Payment { get; set; } = new();
    public Guid UserId { get; set; }
}

public sealed class PaymentCanceledPayload
{
    public PaymentDetailsResponse Payment { get; set; } = new();
}

public sealed class PaymentStornoResultPayload
{
    public PaymentStornoDetails Storno { get; set; } = new();
    public PaymentDetailsResponse Payment { get; set; } = new();
}

public sealed class PaymentStornoDetails
{
    public Guid StornoReference { get; set; }
    public Guid PaymentId { get; set; }
    public decimal Amount { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PaymentStornoState State { get; set; }
    public string? Error { get; set; }
    public IReadOnlyList<PaymentStornoBankStep> BankSteps { get; set; } = Array.Empty<PaymentStornoBankStep>();
    public bool IsCompleted => State == PaymentStornoState.Completed;
}

public sealed class PaymentStornoBankStep
{
    public string BankOrderNumber { get; set; } = string.Empty;
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PaymentStornoBankStepState State { get; set; }
    public string? Error { get; set; }
}

public enum PaymentStornoState
{
    Processing = 0,
    BankRejected = 1,
    NeedsReconciliation = 2,
    WalletPending = 3,
    Completed = 4
}

public enum PaymentStornoBankStepState
{
    Pending = 0,
    Sending = 1,
    Rejected = 2,
    Completed = 3
}

public sealed class CashInCreatedPayload
{
    public CashInDetailsResponse CashIn { get; set; } = new();
    public string CashInDeepLink { get; set; } = string.Empty;
}

public sealed class CashInCompletedPayload
{
    public CashInDetailsResponse CashIn { get; set; } = new();
    public Guid UserId { get; set; }
}

public sealed class CashInCanceledPayload
{
    public CashInDetailsResponse CashIn { get; set; } = new();
}

public sealed class CashOutCreatedPayload
{
    public CashOutDetailsResponse CashOut { get; set; } = new();
    public string CashOutDeepLink { get; set; } = string.Empty;
}

public sealed class CashOutPaidByUserPayload
{
    public CashOutDetailsResponse CashOut { get; set; } = new();
    public Guid UserId { get; set; }
}

public sealed class CashOutCompletedPayload
{
    public CashOutDetailsResponse CashOut { get; set; } = new();
    public Guid UserId { get; set; }
}

public sealed class CashOutCanceledPayload
{
    public CashOutDetailsResponse CashOut { get; set; } = new();
}

public class CashDeskErrorPayload
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public sealed class TransactionCreateErrorPayload : CashDeskErrorPayload
{
    public PaymentDetailsResponse? PendingPayment { get; set; }
    public string? PaymentDeepLink { get; set; }
    public CashOutDetailsResponse? PendingCashOut { get; set; }
    public string? CashOutDeepLink { get; set; }
    public CashInDetailsResponse? PendingCashIn { get; set; }
    public string? CashInDeepLink { get; set; }
}

public sealed class PaymentDetailsResponse
{
    public Guid Id { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PaymentStatus Status { get; set; }
    public LocationInfo Location { get; set; } = new();
    public JsonElement CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public IReadOnlyList<PaymentLineItemResponse> LineItems { get; set; } = Array.Empty<PaymentLineItemResponse>();
    public IReadOnlyList<PaymentMetadataResponse> Metadata { get; set; } = Array.Empty<PaymentMetadataResponse>();
}

public sealed class CashOutDetailsResponse
{
    public Guid Id { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public CashOutStatus Status { get; set; }
    public LocationInfo Location { get; set; }
    public JsonElement CreatedAt { get; set; }
}

public sealed class CashInDetailsResponse
{
    public Guid Id { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public CashInStatus Status { get; set; }
    public LocationInfo Location { get; set; }
    public JsonElement CreatedAt { get; set; }
}

public sealed class LocationInfo
{
    public string? Name { get; set; }
    public string? Address { get; set; }
}

public sealed class PaymentLineItemResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; set; }
}

public sealed class PaymentMetadataResponse
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public bool DisplayToUser { get; set; }
}

public enum PaymentStatus : byte
{
    Pending = 1,
    Successful = 2,
    Canceled = 3,
    TimedOut = 4,
    Failed = 5,
    StornoProcessing = 6,
    Storned = 7,
}

public enum CashOutStatus : byte
{
    Pending = 0,
    UserPaid = 1,
    Completed = 2,
    Canceled = 3
}

public enum CashInStatus : byte
{
    Pending = 0,
    Completed = 1,
    Canceled = 2
}
