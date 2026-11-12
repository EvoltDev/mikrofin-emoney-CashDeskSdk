# Mikrofin EMoney CashDesk SDK – Detaljna dokumentacija

## Šta je Mikrofin EMoney CashDesk SDK?

Mikrofin.EMoney.CashDeskSdk je .NET biblioteka koja omogućava vašoj aplikaciji
da se poveže na Mikrofin EMoney Core API putem WebSocket konekcije i obavlja rad
sa blagajnom (Cash Desk).

Pomoću ovog SDK-a možete:
- slati poruke uz pomoc vec definisanih metoda prema Mikrofin EMoney Core API-u:
  - LoginAsync
  - CreatePaymentAsync
  - CancelPaymentAsync
  - CreateCashInAsync
  - CancelCashInAsync
  - CreateCashOutAsync
  - CompleteCashOutAsync
  - CancelCashOutAsync

- primati jasno definisane događaje koje integracija može
  pretvoriti u UI obavijesti ili poslovnu logiku:
  - CashierLoginSucceeded
  - CashierLoginFailed
  - PaymentCreated
  - TransactionCreateFailed
  - PaymentCompleted
  - CashInCreated
  - CashInCompleted
  - CashOutCreated
  - CashOutPaidByUser
  - CashOutCompleted
  - GeneralErrorReceived

SDK sakriva kompletnu WebSocket komunikaciju, tako da vi radite samo sa jasnim C# metodama i događajima.

## Kako SDK radi?
1.	SDK uspostavlja jednu stalnu WebSocket vezu prema EMoney API-ju
2.	Vi šaljete zahtjeve metodama kao što su:
   - LoginAsync
   - CreatePaymentAsync
   - CancelPaymentAsync
   - CreateCashInAsync
   - CancelCashInAsync
   - CreateCashOutAsync
   - CompleteCashOutAsync
   - CancelCashOutAsync
3.	Server vam vraća odgovore kroz događaje (events), npr.:
   - PaymentCreated
   - PaymentCompleted
   - TransactionCreateFailed
   - CashInCreated
   - CashInCompleted
   - CashOutCreated
   - CashOutPaidByUser
   - CashOutCompleted
4.	Te događaje možete koristiti za:
   - prikaz QR koda,
   - obavijesti korisniku,
   - pokretanje poslovne logike u aplikaciji.


## Tehnički zahtjevi

Prije nego počnete, potrebno je sljedeće:

Softverski zahtjevi
  - .NET 6.0 ili noviji
  - Ili bilo koje runtime okruženje koje podržava .NET Standard 2.0

Sistemski zahtjevi
  - Kreiran blagajnički (cashier) račun u EMoney Core sistemu
  - Pristup WebSocket endpointu:
    - Lokalno:

      `ws://localhost:5000/ws/cashdesk`
    - Dev / Test

      `wss://test-api-emoney.mfsoftware.com/ws/cashdesk`
    - Prod

      `wss://api.totopay.ba/ws/cashdesk`

## Instalacija SDK-a
Opcija 1: preko .NET CLI (NuGet paket)

```bash
dotnet add package Mikrofin.EMoney.CashDeskSdk
```

Opcija 2: ručno u .csproj fajlu

```xml
<ItemGroup>
    <PackageReference Include="Mikrofin.EMoney.CashDeskSdk" Version="x.y.z" />
</ItemGroup>
```

## Kreiranje i konfiguracija klijenta
Prvi korak u kodu je konfiguracija CashDeskClient-a.
```csharp
var options = new CashDeskClientOptions(
    new Uri("wss://<your-host>/ws/cashdesk"))
{
    KeepAliveInterval = TimeSpan.FromSeconds(30),
    ReceiveBufferSize = 32 * 1024,
    DiagnosticLogger = message =>
        logger.LogInformation("[CashDeskSdk] {Message}", message)
};

await using var client = new CashDeskClient(options);
```
### Obavezni parametri
- `Endpoint` – puni `ws://` ili `wss://` Mikrofin EMoney Core API URL izloženog CashDesk endpointa.

### Dodatne postavke
- `KeepAliveInterval` – period slanja pingova (default 30 sek).
- `ReceiveBufferSize` – veličina buffera za prijem frame-ova (default 32 KB).
- `Headers` – kolekcija custom zaglavlja (trenutno nije neophodna; koristite samo ako vam gateway naknadno zatraži dodatne header-e).
- `DiagnosticLogger` – callback za logovanje lifecycle događaja (spajanje, diskonekcija).


## Tipičan tok integracije

1. Instancirati `CashDeskClient` (prethodno poglavlje)
2. pretplatiti se na događaje
```csharp
client.CashierLoginSucceeded += (_, payload) =>
{
    Console.WriteLine($"Cashier {payload.Cashier.UserName} logged in.");
};

client.PaymentCompleted += (_, payload) =>
{
    Console.WriteLine($"Payment {payload.Payment.Id} completed.");
};
client.CashInCompleted += (_, payload) =>
{
    Console.WriteLine($"CashIn {payload.CashIn.Id} completed.");
};
client.CashOutCompleted += (_, payload) =>
{
    Console.WriteLine($"CashOut {payload.CashOut.Id} completed.");
};
```
2. Uspostaviti WebSocket konekciju
```csharp
await client.ConnectAsync(ct);
```
3. Prijaviti blagajnika (Login)
```csharp
await client.LoginAsync(
    new CashierLoginRequest(accountId, username, password), ct);
```
4. Po potrebi pozivati:
   - `CreatePaymentAsync` sa linijama artikala, valutom i metapodacima.
   - `CancelPaymentAsync(paymentId)` za otkazivanje placanja.
   - `CreateCashInAsync` sa iznosom, valutom.
   - `CancelCashInAsync(cashInId)` za otkazivanje cashIna.
   - `CreateCashOutAsync` sa iznosom, valutom.
   - `CompleteCashOutAsync(cashOutId)` za potvrdu placanja.
   - `CancelCashOutAsync(cashOutId)` za otkazivanje cashOuta.


```csharp
await client.CreatePaymentAsync(
    new CashDeskPaymentCreateRequest(
        totalAmount: 25.00m,
        currency: "BAM",
        lineItems: new[]
        {
            new CashDeskPaymentLineItemRequest("Brasno", 25.00m)
        },
        paymentMetadata: Array.Empty<CashDeskPaymentMetadata>()),
    cancellationToken);
```

```csharp
await client.CancelPaymentAsync(paymentId, cts.Token);
```

```csharp
await client.CreateCashInAsync(
    new CashDeskCashInCreateRequest(
        totalAmount: 25.00m,
        currency: "BAM",
        cancellationToken);
```

```csharp
await client.CancelCashInAsync(cashInId, cts.Token);
```

```csharp
await client.CreateCashOutAsync(
    new CashDeskCashOutCreateRequest(
        totalAmount: 25.00m,
        currency: "BAM",
        cancellationToken);
```

```csharp
await client.CancelCashOutAsync(cashOutId, cts.Token);
```

5. Rukovati događajima koje server šalje (npr. `PaymentCreated`, `PaymentCompleted`, `cashInCreated`, ...).
6. Na gašenje aplikacije ili gubitak konekcije pozvati `DisconnectAsync` i
   `DisposeAsync` (ili koristiti `await using` kao u primjerima).
```csharp
await client.DisconnectAsync();
```

## Događaji

| Događaj                 | Opis                                                      | Payload (ključna polja)                                                                                                                                             |
|-------------------------|-----------------------------------------------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `CashierLoginSucceeded` | Server je prihvatio `cashier.login`.                      | `Cashier` (AccountId, UserName, Location…), opciona polja `PendingPayment` + `PaymentDeepLink`, `PendingCashIn` + `CashInDeepLink`, `PendingCashOut` + `CashOutDeepLink` (zavisno od tipa pending transakcije). |
| `CashierLoginFailed`    | Prijava odbijena.                                         | `CashDeskErrorPayload` – `Code` (npr. `InvalidCredentials`, `UserLocked`) i `Message`. Nijedno polje nije `null`; koristi ih za prikaz operateru ili za audit log.  |
| `PaymentCreated`        | `payment.create` uspješan.                                | `PaymentCreatedPayload` – `Payment` (`PaymentDetailsResponse`: Id, Amount, Currency, Status, Metadata, LineItems), obavezni `PaymentDeepLink`.                      |
| `TransactionCreateFailed` | Kreiranje transakcije odbijeno (`transaction.create.error`). | `TransactionCreateErrorPayload` – nasljeđuje `CashDeskErrorPayload`. Opciona polja: `PendingPayment` + `PaymentDeepLink`, `PendingCashIn` + `CashInDeepLink`, `PendingCashOut` + `CashOutDeepLink` (zavisno od tipa transakcije), uz `Code` i `Message`. |
| `PaymentCompleted`      | Korisnik je završio plaćanje (`payment.completed`).       | `PaymentCompletedPayload` – `Payment` (iste strukture kao iznad) i `UserId` koji je inicirao završetak.                                                             |
| `PaymentStornoResultReceived` | Rezultat `payment.storno`. | `PaymentStornoResultPayload` – `Storno` (`StornoReference`, `PaymentId`, `Amount`, `State`, `Error`, `BankSteps`) i osvježeni `Payment`. Uspjeh samo ako je `Storno.IsCompleted` true. |
| `GeneralErrorReceived`  | Šalje se `cashdesk.error` za sve ostale greške protokola. | `CashDeskErrorPayload` – `Code`, `Message`. Koristite za prikaz korisniku ili logovanje; može značiti da je server odbio komandu zbog stanja uređaja.               |
| `ConnectionClosed`      | Konekcija zatvorena sa bilo koje strane.                  | `ConnectionClosedEventArgs` – `Status` (npr. `NormalClosure`, `AbnormalClosure`), `Description` (poruka servera ili izuzetak). Korisno za prikaz i za retry logiku. |
| `CashInCreated`         | `cashIn.create` uspješan.                                 | `CashInCreatedPayload` – `CashIn` (`CashInDetailsResponse`: Id, Amount, Currency, Status, Location, CreatedAt), obavezni `CashInDeepLink`.                          |
| `CashInCompleted`       | Korisnik je završio cashIn (`cashIn.completed`).          | `CashInCompletedPayload` – `CashIn` (iste strukture kao iznad) i `UserId` koji je inicirao završetak.                                                               |
| `CashOutCreated`        | `cashOut.create` uspješan.                                | `CashOutCreatedPayload` – `CashOut` (`CashOutDetailsResponse`: Id, Amount, Currency, Status, Location, CreatedAt), obavezni `CashOutDeepLink`.                      |
| `CashOutPaidByUser`     | cashOut uplacen od strane usera                           | `CashOutPaidByUserPayload` – `CashOut` (`CashOutDetailsResponse`: Id, Amount, Currency, Status, Location, CreatedAt) i `UserId` koji je inicirao uplatu.            |
| `CashOutCompleted`      | Korisnik je završio cashOut (`cashOut.completed`).        | `CashInCompletedPayload` – `CashOut` (iste strukture kao iznad) i `UserId` koji je inicirao završetak.                                                              |

### Obavezna polja po zahtjevima

- `CashierLoginRequest`:
  - `AccountId` - string (obavezno)
  - `UserName` - string (obavezno)
  - `Password` - string (obavezno)

- `CashDeskPaymentCreateRequest`:
  - `TotalAmount` - decimal (obavezan)
  - `Currency` - string (default `BAM`).
  - `LineItems` - `CashDeskPaymentLineItemRequest` (opcionalan)
    - `Name` - string (obavezan)
    - `UnitPrice` - decimal (obavezan)
    - `Quantity` - int (default 1)
  - `PaymentMetadata` - `CashDeskPaymentMetadata` (može biti prazna lista)
    - `Key` - string
    - `Value` - string
    - `DisplayToUser` - bool (default `false`).
- `CashDeskPaymentCancelRequest`:
  - `PaymentId` - Guid (obavezan)

- `CashDeskCashInCreateRequest`:
  - `TotalAmount` - decimal (obavezan)
  - `Currency` - string (default `BAM`).

- `CashDeskCashInCancelRequest`:
  - `CashInId` - Guid (obavezan)

- `CashDeskCashOutCreateRequest`:
  - `TotalAmount` - decimal (obavezan)
  - `Currency` - string (default `BAM`).

- `CashDeskCashOutCompleteRequest`:
  - `CashOutId` - Guid (obavezan)

- `CashDeskCashOutCancelRequest`:
  - `CashOutId` - Guid (obavezan)

### Struktura tipova

- `PaymentDetailsResponse` (server response) sadrži:
  - `Id` - Guid
  - `Amount` - decimal
  - `Currency` - string
  - `Status` - enum (`Pending`, `Successful`, `Canceled`)
  - `Location` - `LocationInfo`
    - `Name` - string
    - `Address` - string
  - `CreatedAt` - `JsonElement` (server šalje datum u ISO formatu)
  - `LineItems` (lista `PaymentLineItemResponse`)
    - `Id` - Guid
    - `Name` - string
    - `Quantity` - int
    - `UnitPrice` - decimal
    - `Amount` - decimal
  - `Metadata` (lista `PaymentMetadataResponse`)
    - `Key` - string
    - `Value` - string
    - `DisplayToUser` - bool

- `CashInDetailsResponse` (server response) sadrži:
  - `Id` - Guid
  - `Amount` - decimal
  - `Currency` - string
  - `Status` - enum (`Pending`, `Completed`, `Canceled`)
  - `Location` - `LocationInfo`
    - `Name` - string
    - `Address` - string
  - `CreatedAt` - `JsonElement` (server šalje datum u ISO formatu)

- `CashOutDetailsResponse` (server response) sadrži:
  - `Id` - Guid
  - `Amount` - decimal
  - `Currency` - string
  - `Status` - enum (`Pending`, `UserPaid`, `Completed`, `Canceled`)
  - `Location` - `LocationInfo`
    - `Name` - string
    - `Address` - string
  - `CreatedAt` - `JsonElement` (server šalje datum u ISO formatu)

Koristite ova polja direktno ili mapirajte na vlastite DTO klasse. SDK već koristi `System.Text.Json` sa `JsonNamingPolicy.CamelCase`, tako da su nazivi polja identični onome što vidite u JSON payload-ima.

## Rukovanje greškama

- Svaki poziv (`ConnectAsync`, `LoginAsync`, `CreatePaymentAsync`, `CreateCashInAsync`, `CreateCashOutAsync`, ...) prihvata
  `CancellationToken` – koristite ga za timeoute i graceful shutdown.
- U slučaju izuzetka u `ReceiveLoop` SDK poziva `ConnectionClosed` sa razlogom.
- Ako primite `CashierLoginFailed`, tipično treba ponovo pitati korisnika za
  kredencijale ili blokirati dalji rad.
- `TransactionCreateFailed` može vratiti `PendingPayment` + `PaymentDeepLink`,
  `PendingCashIn` + `CashInDeepLink` ili `PendingCashOut` + `CashOutDeepLink`,
  što vam omogućava prikaz korisniku šta je ostalo otvoreno i direktno
  usmjeravanje na odgovarajući deep link.

## Testiranje i okruženja

- **Lokalno**: pokrenite sample API na `http://localhost:5000`, podesite
  `appsettings.json` na `ws://localhost:5000/ws/cashdesk`.
- **Development**: `DOTNET_ENVIRONMENT=Development` + `appsettings.Development.json`
  gdje definišete `wss://test-api-emoney.mfsoftware.com/ws/cashdesk` ili drugi URL.
- **Production**: koristite `wss://api.totopay.ba/ws/cashdesk` (TLS) URL koji obezbjeđuje vaš API gateway;
  provjerite da li treba dodatna autentifikacija na nivou zaglavlja.
- **Automatski testovi**: možete koristiti testove iz Mikrofin.EMoney.CashDeskSdk.Tests.

## Resursi

Za završeno plaćanje pozovite `await client.StornoPaymentAsync(paymentId)`. Komanda šalje `payment.storno` preko postojeće autentifikovane `/ws/cashdesk` veze. `CancelPaymentAsync` je samo za pending plaćanja. Ponovljeni storno zahtjev vraća pohranjeno stanje operacije; neriješena bankovna ili wallet stanja zahtijevaju administratorsko usklađivanje. Akciju ponudite samo za uspješno plaćanje unutar 30 minuta od `CompletedAt`, uz CoreApi kao konačni autoritet za validaciju.

- `Mikrofin.EMoney.CashDeskSdk.Sample` – konzolna aplikacija koja demonstrira
  sve funkcionalnosti (prijava, kreiranje uplata, praćenje događaja).
- Github repo: https://github.com/EvoltDev/Mikrofin.EMoney.CashDeskSdk/tree/main/Mikrofin.EMoney.CashDeskSdk.Sample
