using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mikrofin.EMoney.CashDeskSdk.Messaging;

namespace Mikrofin.EMoney.CashDeskSdk.Tests;

public class CashDeskMessageContractsTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    [Fact]
    public void PaymentStornoRequest_SerializesPaymentId()
    {
        var id = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var json = JsonSerializer.Serialize(new CashDeskPaymentStornoRequest(id), SerializerOptions);
        Assert.Equal("{\"paymentId\":\"11111111-1111-1111-1111-111111111111\"}", json);
    }

    [Theory]
    [InlineData("4", true)]
    [InlineData("\"completed\"", true)]
    [InlineData("0", false)]
    [InlineData("2", false)]
    public void PaymentStornoResult_OnlyCompletedIsSuccess(string stateJson, bool completed)
    {
        var json = "{\"storno\":{\"stornoReference\":\"22222222-2222-2222-2222-222222222222\",\"paymentId\":\"11111111-1111-1111-1111-111111111111\",\"amount\":12.5,\"state\":" + stateJson + ",\"error\":\"Bank pending\",\"bankSteps\":[{\"bankOrderNumber\":\"123\",\"state\":2,\"error\":\"Rejected\"}]},\"payment\":{\"id\":\"11111111-1111-1111-1111-111111111111\",\"status\":7}}";
        var result = JsonSerializer.Deserialize<PaymentStornoResultPayload>(json, SerializerOptions);
        Assert.NotNull(result);
        Assert.Equal(completed, result.Storno.IsCompleted);
        Assert.Equal("Bank pending", result.Storno.Error);
        Assert.Equal(PaymentStornoBankStepState.Rejected, Assert.Single(result.Storno.BankSteps).State);
        Assert.Equal(PaymentStatus.Storned, result.Payment.Status);
    }

    [Fact]
    public void CashDeskPaymentCreateRequest_PreservesValues()
    {
        var lineItems = new List<CashDeskPaymentLineItemRequest>
        {
            new("Item 1", 10m, 2)
        };
        var metadata = new List<CashDeskPaymentMetadata>
        {
            new("orderId", "12345", true)
        };

        var request = new CashDeskPaymentCreateRequest(
            totalAmount: 20m,
            currency: "BAM",
            lineItems: lineItems,
            paymentMetadata: metadata);

        Assert.Equal(20m, request.TotalAmount);
        Assert.Equal("BAM", request.Currency);
        Assert.Same(lineItems, request.LineItems);
        Assert.Same(metadata, request.PaymentMetadata);
    }

    [Fact]
    public void CashDeskPaymentLineItemRequest_AssignsProperties()
    {
        var lineItem = new CashDeskPaymentLineItemRequest("Test", 5m, 3);

        Assert.Equal("Test", lineItem.Name);
        Assert.Equal(5m, lineItem.UnitPrice);
        Assert.Equal(3, lineItem.Quantity);
    }

    [Fact]
    public void CashierLoginSuccessPayload_Deserializes_WithNewDeepLinkFields()
    {
        const string json = """
                            {
                              "cashier": {
                                "id": "11111111-1111-1111-1111-111111111111",
                                "userName": "cashier",
                                "locationId": "22222222-2222-2222-2222-222222222222",
                                "locationName": "Main",
                                "locationAddress": "Street 1"
                              },
                              "pendingPayment": {
                                "id": "33333333-3333-3333-3333-333333333333",
                                "amount": 12.5,
                                "currency": "BAM",
                                "status": "pending",
                                "location": {
                                  "name": "Main",
                                  "address": "Street 1"
                                },
                                "createdAt": "2026-01-01T00:00:00Z"
                              },
                              "paymentDeepLink": "app://payment/33333333-3333-3333-3333-333333333333",
                              "pendingCashIn": {
                                "id": "44444444-4444-4444-4444-444444444444",
                                "amount": 10,
                                "currency": "BAM",
                                "status": "pending",
                                "location": {
                                  "name": "Main",
                                  "address": "Street 1"
                                },
                                "createdAt": "2026-01-01T00:01:00Z"
                              },
                              "cashInDeepLink": "app://cashin/44444444-4444-4444-4444-444444444444",
                              "pendingCashOut": {
                                "id": "55555555-5555-5555-5555-555555555555",
                                "amount": 5,
                                "currency": "BAM",
                                "status": "pending",
                                "location": {
                                  "name": "Main",
                                  "address": "Street 1"
                                },
                                "createdAt": "2026-01-01T00:02:00Z"
                              },
                              "cashOutDeepLink": "app://cashout/55555555-5555-5555-5555-555555555555"
                            }
                            """;

        var payload = JsonSerializer.Deserialize<CashierLoginSuccessPayload>(json, SerializerOptions);

        Assert.NotNull(payload);
        Assert.Equal("app://payment/33333333-3333-3333-3333-333333333333", payload.PaymentDeepLink);
        Assert.Equal("app://cashin/44444444-4444-4444-4444-444444444444", payload.CashInDeepLink);
        Assert.Equal("app://cashout/55555555-5555-5555-5555-555555555555", payload.CashOutDeepLink);
        Assert.NotNull(payload.PendingPayment);
        Assert.NotNull(payload.PendingCashIn);
        Assert.NotNull(payload.PendingCashOut);
    }

    [Fact]
    public void CashierLoginSuccessPayload_Deserializes_WithoutNewDeepLinkFields()
    {
        const string json = """
                            {
                              "cashier": {
                                "id": "11111111-1111-1111-1111-111111111111",
                                "userName": "cashier",
                                "locationId": "22222222-2222-2222-2222-222222222222",
                                "locationName": "Main",
                                "locationAddress": "Street 1"
                              },
                              "pendingPayment": {
                                "id": "33333333-3333-3333-3333-333333333333",
                                "amount": 12.5,
                                "currency": "BAM",
                                "status": "pending",
                                "location": {
                                  "name": "Main",
                                  "address": "Street 1"
                                },
                                "createdAt": "2026-01-01T00:00:00Z"
                              }
                            }
                            """;

        var payload = JsonSerializer.Deserialize<CashierLoginSuccessPayload>(json, SerializerOptions);

        Assert.NotNull(payload);
        Assert.NotNull(payload.PendingPayment);
        Assert.Null(payload.PaymentDeepLink);
        Assert.Null(payload.PendingCashIn);
        Assert.Null(payload.CashInDeepLink);
        Assert.Null(payload.PendingCashOut);
        Assert.Null(payload.CashOutDeepLink);
    }

    [Fact]
    public void TransactionCreateErrorPayload_Deserializes_WithNewDeepLinkFields()
    {
        const string json = """
                            {
                              "code": "ExistingPendingTransaction",
                              "message": "A transaction is already pending.",
                              "pendingPayment": {
                                "id": "33333333-3333-3333-3333-333333333333",
                                "amount": 12.5,
                                "currency": "BAM",
                                "status": "pending",
                                "location": {
                                  "name": "Main",
                                  "address": "Street 1"
                                },
                                "createdAt": "2026-01-01T00:00:00Z"
                              },
                              "paymentDeepLink": "app://payment/33333333-3333-3333-3333-333333333333",
                              "pendingCashIn": {
                                "id": "44444444-4444-4444-4444-444444444444",
                                "amount": 10,
                                "currency": "BAM",
                                "status": "pending",
                                "location": {
                                  "name": "Main",
                                  "address": "Street 1"
                                },
                                "createdAt": "2026-01-01T00:01:00Z"
                              },
                              "cashInDeepLink": "app://cashin/44444444-4444-4444-4444-444444444444",
                              "pendingCashOut": {
                                "id": "55555555-5555-5555-5555-555555555555",
                                "amount": 5,
                                "currency": "BAM",
                                "status": "pending",
                                "location": {
                                  "name": "Main",
                                  "address": "Street 1"
                                },
                                "createdAt": "2026-01-01T00:02:00Z"
                              },
                              "cashOutDeepLink": "app://cashout/55555555-5555-5555-5555-555555555555"
                            }
                            """;

        var payload = JsonSerializer.Deserialize<TransactionCreateErrorPayload>(json, SerializerOptions);

        Assert.NotNull(payload);
        Assert.Equal("ExistingPendingTransaction", payload.Code);
        Assert.Equal("A transaction is already pending.", payload.Message);
        Assert.Equal("app://payment/33333333-3333-3333-3333-333333333333", payload.PaymentDeepLink);
        Assert.Equal("app://cashin/44444444-4444-4444-4444-444444444444", payload.CashInDeepLink);
        Assert.Equal("app://cashout/55555555-5555-5555-5555-555555555555", payload.CashOutDeepLink);
        Assert.NotNull(payload.PendingPayment);
        Assert.NotNull(payload.PendingCashIn);
        Assert.NotNull(payload.PendingCashOut);
    }

    [Fact]
    public void TransactionCreateErrorPayload_Deserializes_WithoutNewDeepLinkFields()
    {
        const string json = """
                            {
                              "code": "ExistingPendingTransaction",
                              "message": "A transaction is already pending.",
                              "pendingPayment": {
                                "id": "33333333-3333-3333-3333-333333333333",
                                "amount": 12.5,
                                "currency": "BAM",
                                "status": "pending",
                                "location": {
                                  "name": "Main",
                                  "address": "Street 1"
                                },
                                "createdAt": "2026-01-01T00:00:00Z"
                              }
                            }
                            """;

        var payload = JsonSerializer.Deserialize<TransactionCreateErrorPayload>(json, SerializerOptions);

        Assert.NotNull(payload);
        Assert.Equal("ExistingPendingTransaction", payload.Code);
        Assert.Equal("A transaction is already pending.", payload.Message);
        Assert.NotNull(payload.PendingPayment);
        Assert.Null(payload.PaymentDeepLink);
        Assert.Null(payload.PendingCashIn);
        Assert.Null(payload.CashInDeepLink);
        Assert.Null(payload.PendingCashOut);
        Assert.Null(payload.CashOutDeepLink);
    }
}
