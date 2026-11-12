using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Mikrofin.EMoney.CashDeskSdk;
using Mikrofin.EMoney.CashDeskSdk.Messaging;
using Microsoft.Extensions.Configuration;

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, args) =>
{
    args.Cancel = true;
    cts.Cancel();
};

var configuration = BuildConfiguration();
var endpoint = ResolveEndpoint(configuration);
var payments = new ConcurrentDictionary<Guid, PaymentDetailsResponse>();
var observedCompletions = new ConcurrentDictionary<Guid, DateTimeOffset>();
var stornedPayments = new ConcurrentDictionary<Guid, byte>();

await using var client = new CashDeskClient(
    new CashDeskClientOptions(endpoint)
    {
        DiagnosticLogger = message => Console.WriteLine($"[SDK] {message}")
    });

Console.WriteLine($"Using endpoint {endpoint}");

client.CashierLoginSucceeded += (_, payload) =>
{
    Console.WriteLine($"Logged in as {payload.Cashier.UserName}");
    DumpPayload("cashier.login.success", payload);
};

client.CashierLoginFailed += (_, payload) =>
{
    Console.WriteLine($"Login failed: {payload.Code} - {payload.Message}");
    DumpPayload("cashier.login.error", payload);
};

client.PaymentCreated += (_, payload) =>
{
    payments[payload.Payment.Id] = payload.Payment;
    Console.WriteLine($"Payment created: {payload.Payment.Id}");
    DumpPayload("payment.created", payload);
};

client.PaymentCompleted += (_, payload) =>
{
    var receivedAt = DateTimeOffset.UtcNow;
    payments[payload.Payment.Id] = payload.Payment;
    observedCompletions[payload.Payment.Id] = receivedAt;
    Console.WriteLine($"Payment {payload.Payment.Id} completed");
    if (CanOfferStorno(payload.Payment, receivedAt, receivedAt))
        Console.WriteLine($"Storniraj plaćanje: storno {payload.Payment.Id}");
    DumpPayload("payment.completed", payload);
};

client.PaymentCanceled += (_, payload) =>
{
    payments[payload.Payment.Id] = payload.Payment;
    Console.WriteLine($"Payment {payload.Payment.Id} canceled");
    DumpPayload("payment.canceled", payload);
};

client.PaymentStornoResultReceived += (_, payload) =>
{
    payments[payload.Payment.Id] = payload.Payment;
    var storno = payload.Storno;
    if (storno.IsCompleted)
    {
        stornedPayments[storno.PaymentId] = 0;
        Console.WriteLine($"Payment {storno.PaymentId} storned (reference: {storno.StornoReference}).");
    }
    else
    {
        Console.WriteLine($"Storno {storno.StornoReference} for payment {storno.PaymentId}: {storno.State}.");
        if (!string.IsNullOrWhiteSpace(storno.Error))
            Console.WriteLine($"Storno error: {storno.Error}");
        Console.WriteLine("Unresolved bank or wallet states require administrator reconciliation or retry.");
    }
    DumpPayload("payment.storno.result", payload);
};

client.TransactionCreateFailed += (_, payload) =>
{
    Console.WriteLine($"Transaction creation failed: {payload.Code} - {payload.Message}");
    DumpPayload("transaction.create.error", payload);
};

client.CashInCreated += (_, payload) =>
{
    Console.WriteLine($"CashIn created: {payload.CashIn.Id}");
    DumpPayload("cashIn.created", payload);
};

client.CashInCompleted += (_, payload) =>
{
    Console.WriteLine($"CashIn {payload.CashIn.Id} completed");
    DumpPayload("cashIn.completed", payload);
};

client.CashInCanceled += (_, payload) =>
{
    Console.WriteLine($"CashIn {payload.CashIn.Id} canceled");
    DumpPayload("cashIn.canceled", payload);
};

client.CashOutPaidByUser += (_, payload) =>
{
    Console.WriteLine($"CashOut {payload.CashOut.Id} paid by User {payload.UserId}");
    DumpPayload("cashout.paid", payload);
};

client.CashOutCreated += (_, payload) =>
{
    Console.WriteLine($"CashOut created: {payload.CashOut.Id}");
    DumpPayload("cashOut.created", payload);
};

client.CashOutCompleted += (_, payload) =>
{
    Console.WriteLine($"CashOut {payload.CashOut.Id} completed");
    DumpPayload("cashOut.completed", payload);
};

client.CashOutCanceled += (_, payload) =>
{
    Console.WriteLine($"CashOut {payload.CashOut.Id} canceled");
    DumpPayload("cashOut.canceled", payload);
};

client.GeneralErrorReceived += (_, payload) =>
{
    Console.WriteLine($"General error: {payload.Code} - {payload.Message}");
    DumpPayload("cashdesk.error", payload);
};

client.ConnectionClosed += (_, args) =>
{
    Console.WriteLine($"Connection closed. Status={args.Status}, Description={args.Description}");
};

try
{
    await client.ConnectAsync(cts.Token);
    await LoginFromConsoleAsync(client, cts.Token);

    Console.WriteLine("Commands: 'login', 'createPayment', 'cancelPayment <id>' (pending), 'storno <payment id>' (eligible completed payment), or Ctrl+C to quit.");
    while (!cts.Token.IsCancellationRequested)
    {
        var line = await ReadLineAsync(cts.Token).ConfigureAwait(false);
        if (line == null)
        {
            break;
        }

        if (line.Equals("login", StringComparison.OrdinalIgnoreCase))
        {
            await LoginFromConsoleAsync(client, cts.Token);
            continue;
        }

        if (line.StartsWith("create", StringComparison.OrdinalIgnoreCase))
        {
            if (line.EndsWith("Payment", StringComparison.OrdinalIgnoreCase))
            {
                await CreatePaymentFromConsoleAsync(client, cts.Token);
                continue;
            }
            if (line.EndsWith("CashIn", StringComparison.OrdinalIgnoreCase))
            {
                await CreateCashInFromConsoleAsync(client, cts.Token);
                continue;
            }
            if (line.EndsWith("CashOut", StringComparison.OrdinalIgnoreCase))
            {
                await CreateCashOutFromConsoleAsync(client, cts.Token);
                continue;
            }

        }

        if (line.StartsWith("complete", StringComparison.OrdinalIgnoreCase))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string? idInput = parts.Length > 1 ? parts[1] : null;
            if (idInput == null)
            {
                Console.Write("Enter cashOut ID to complte: ");
                idInput = Console.ReadLine();
            }

            if (Guid.TryParse(idInput, out var id))
            {
                await client.CompleteCashOutAsync(id, cts.Token);
                Console.WriteLine($"Complete cashOut request sent for {id}");
                continue;
            }

        }

        if (line.StartsWith("storno ", StringComparison.OrdinalIgnoreCase))
        {
            var idInput = line.Split(' ', StringSplitOptions.RemoveEmptyEntries).ElementAtOrDefault(1);
            if (!Guid.TryParse(idInput, out var paymentId))
                Console.WriteLine("Invalid payment GUID.");
            else if (!payments.TryGetValue(paymentId, out var payment))
                Console.WriteLine("This sample process has no record of that payment. Restarting the sample clears its payment history; complete a new payment while it is running.");
            else if (stornedPayments.ContainsKey(paymentId))
                Console.WriteLine("This sample already received a Completed storno result for that payment.");
            else if (payment.Status != PaymentStatus.Successful)
                Console.WriteLine($"Payment status is {payment.Status}; storno is offered only for successful payments.");
            else if (payment.CompletedAt is null && !observedCompletions.ContainsKey(paymentId))
                Console.WriteLine("Completion time is unavailable: this sample process did not receive payment.completed for that payment.");
            else if (!CanOfferStorno(payment, DateTimeOffset.UtcNow,
                         observedCompletions.TryGetValue(paymentId, out var receivedAt) ? receivedAt : null))
                Console.WriteLine("The locally known 30-minute storno window has passed. CoreApi makes the final eligibility decision.");
            else
            {
                await client.StornoPaymentAsync(paymentId, cts.Token);
                Console.WriteLine($"Storno request sent for {paymentId}; awaiting operation state.");
            }
            continue;
        }

        if (line.StartsWith("cancel", StringComparison.OrdinalIgnoreCase))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var command = parts[0];
            string? idInput = parts.Length > 1 ? parts[1] : null;
            if (idInput == null)
            {
                Console.Write("Enter payment or cashIn or cashOut ID to cancel: ");
                idInput = Console.ReadLine();
            }

            if (Guid.TryParse(idInput, out var id))
            {
                if (command.Equals("cancelPayment", StringComparison.OrdinalIgnoreCase))
                {
                    if (payments.TryGetValue(id, out var knownPayment) && knownPayment.Status != PaymentStatus.Pending)
                    {
                        Console.WriteLine("payment.cancel applies only to pending payments. Use storno for an eligible completed payment.");
                        continue;
                    }
                    await client.CancelPaymentAsync(id, cts.Token);
                    Console.WriteLine($"Cancel Payment request sent for {id}");
                    continue;
                }
                if (command.Equals("cancelCashIn", StringComparison.OrdinalIgnoreCase))
                {
                    await client.CancelCashInAsync(id, cts.Token);
                    Console.WriteLine($"Cancel CashIn request sent for {id}");
                    continue;
                }
                if (command.Equals("cancelCashOut", StringComparison.OrdinalIgnoreCase))
                {
                    await client.CancelCashOutAsync(id, cts.Token);
                    Console.WriteLine($"Cancel CashOut request sent for {id}");
                    continue;
                }
            }
            else
            {
                Console.WriteLine("Invalid GUID.");
            }

            continue;
        }

        Console.WriteLine("Unknown command.");
    }
}
catch (OperationCanceledException)
{
    Console.WriteLine("Shutting down...");
}
catch (Exception ex)
{
    Console.WriteLine($"Unhandled exception: {ex}");
}
finally
{
    cts.Dispose();
}

static void DumpPayload(string label, object payload)
{
    var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
    {
        WriteIndented = true
    });
    Console.WriteLine($"{label} payload:\n{json}");
}

static bool CanOfferStorno(PaymentDetailsResponse payment, DateTimeOffset now, DateTimeOffset? observedCompletion)
{
    var completedAt = payment.CompletedAt ?? observedCompletion;
    return payment.Status == PaymentStatus.Successful &&
           completedAt is { } time &&
           now >= time && now - time < TimeSpan.FromMinutes(30);
}

static async Task CreatePaymentFromConsoleAsync(CashDeskClient client, CancellationToken cancellationToken)
{
    Console.Write("Amount: ");
    var amountInput = Console.ReadLine();
    if (!decimal.TryParse(amountInput, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount) || amount <= 0)
    {
        Console.WriteLine("Invalid amount.");
        return;
    }

    Console.Write("Currency [BAM]: ");
    var currency = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(currency))
    {
        currency = "BAM";
    }

    var metadata = new List<CashDeskPaymentMetadata>();
    Console.Write("Add metadata entries? (y/N): ");
    var metadataAnswer = Console.ReadLine();
    if (!string.IsNullOrWhiteSpace(metadataAnswer) &&
        metadataAnswer.StartsWith("y", StringComparison.OrdinalIgnoreCase))
    {
        while (true)
        {
            Console.Write("Metadata key (leave blank to finish): ");
            var key = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(key))
            {
                break;
            }

            Console.Write("Value: ");
            var value = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(value))
            {
                Console.WriteLine("Value cannot be empty.");
                continue;
            }

            Console.Write("Display to user? (y/N): ");
            var displayAnswer = Console.ReadLine();
            var displayToUser = !string.IsNullOrWhiteSpace(displayAnswer) &&
                                displayAnswer.StartsWith("y", StringComparison.OrdinalIgnoreCase);

            metadata.Add(new CashDeskPaymentMetadata(key, value, displayToUser));
        }
    }

    var lineItems = new List<CashDeskPaymentLineItemRequest>();
    Console.Write("Add line items? (y/N): ");
    var answer = Console.ReadLine();
    if (!string.IsNullOrWhiteSpace(answer) && answer.StartsWith("y", StringComparison.OrdinalIgnoreCase))
    {
        while (true)
        {
            Console.Write("Item name (leave blank to finish): ");
            var itemName = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(itemName))
            {
                break;
            }

            Console.Write("Unit price: ");
            var priceInput = Console.ReadLine();
            if (!decimal.TryParse(priceInput, NumberStyles.Any, CultureInfo.InvariantCulture, out var unitPrice) ||
                unitPrice <= 0)
            {
                Console.WriteLine("Invalid price, skipping item.");
                continue;
            }

            Console.Write("Quantity [1]: ");
            var qtyInput = Console.ReadLine();
            if (!int.TryParse(qtyInput, out var quantity) || quantity <= 0)
            {
                quantity = 1;
            }

            lineItems.Add(new CashDeskPaymentLineItemRequest(itemName, unitPrice, quantity));
        }
    }

    await client.CreatePaymentAsync(
        new CashDeskPaymentCreateRequest(
            amount,
            currency,
            lineItems,
            metadata
        ),
        cancellationToken
    );
}

static async Task CreateCashInFromConsoleAsync(CashDeskClient client, CancellationToken cancellationToken)
{
    Console.Write("Amount: ");
    var amountInput = Console.ReadLine();
    if (!decimal.TryParse(amountInput, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount) || amount <= 0)
    {
        Console.WriteLine("Invalid amount.");
        return;
    }

    Console.Write("Currency [BAM]: ");
    var currency = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(currency))
    {
        currency = "BAM";
    }

    await client.CreateCashInAsync(
        new CashDeskCashInCreateRequest(
            amount,
            currency
        ),
        cancellationToken
    );
}

static async Task CreateCashOutFromConsoleAsync(CashDeskClient client, CancellationToken cancellationToken)
{
    Console.Write("Amount: ");
    var amountInput = Console.ReadLine();
    if (!decimal.TryParse(amountInput, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount) || amount <= 0)
    {
        Console.WriteLine("Invalid amount.");
        return;
    }

    Console.Write("Currency [BAM]: ");
    var currency = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(currency))
    {
        currency = "BAM";
    }

    await client.CreateCashOutAsync(
        new CashDeskCashOutCreateRequest(
            amount,
            currency
        ),
        cancellationToken
    );
}

static async Task LoginFromConsoleAsync(CashDeskClient client, CancellationToken cancellationToken)
{
    var accountId = ReadRequiredValue("Account ID");
    var userName = ReadRequiredValue("Username");
    var password = ReadRequiredPassword();

    await client.LoginAsync(new CashierLoginRequest(accountId, userName, password), cancellationToken);
}

static Uri ResolveEndpoint(IConfiguration configuration)
{
    var endpointFromConfig = configuration["CashDesk:Endpoint"];
    if (!string.IsNullOrWhiteSpace(endpointFromConfig))
    {
        if (Uri.TryCreate(endpointFromConfig, UriKind.Absolute, out var configUri))
        {
            return configUri;
        }

        Console.WriteLine(
            $"Invalid CashDesk:Endpoint '{endpointFromConfig}', falling back to ws://localhost:5000/ws/cashdesk.");
    }

    return new Uri("ws://localhost:5000/ws/cashdesk");
}

static IConfiguration BuildConfiguration()
{
    var environment = GetEnvironmentName();
    return new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
        .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: false)
        .Build();
}

static string GetEnvironmentName()
{
    return Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
           ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
           ?? "Production";
}

static string ReadRequiredValue(string label)
{
    while (true)
    {
        Console.Write($"{label}: ");
        var input = Console.ReadLine();
        if (!string.IsNullOrWhiteSpace(input))
        {
            return input;
        }

        Console.WriteLine($"{label} cannot be empty.");
    }
}

static string ReadRequiredPassword()
{
    while (true)
    {
        Console.Write("Password: ");
        var input = ReadPassword();
        if (!string.IsNullOrWhiteSpace(input))
        {
            return input;
        }

        Console.WriteLine("Password cannot be empty.");
    }
}

static async Task<string?> ReadLineAsync(CancellationToken cancellationToken)
{
    var readTask = Task.Run(Console.ReadLine);
    var cancelTask = Task.Delay(Timeout.Infinite, cancellationToken);
    var completedTask = await Task.WhenAny(readTask, cancelTask).ConfigureAwait(false);
    if (completedTask == readTask)
    {
        return await readTask.ConfigureAwait(false);
    }

    return null;
}

static string ReadPassword()
{
    var builder = new StringBuilder();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter)
        {
            Console.WriteLine();
            break;
        }

        if (key.Key == ConsoleKey.Backspace)
        {
            if (builder.Length > 0)
            {
                builder.Length--;
                Console.Write("\b \b");
            }

            continue;
        }

        builder.Append(key.KeyChar);
        Console.Write('*');
    }

    return builder.ToString();
}
