# Mikrofin EMoney CashDesk SDK Sample

Ova konzolna aplikacija demonstrira kako koristiti `Mikrofin.EMoney.CashDeskSdk`. Konfigurisite
Websocket endpoint putem `appsettings*.json`:

1. Prilagodite `Mikrofin.EMoney.CashDeskSdk.Sample/appsettings.json` za lokalni razvoj.
2. Mozete kreirati i prepisane fajlove za razlicita okruzenja npr. `appsettings.Development.json` za DEV okruzenje
3. Postavite varijablu DOTNET_ENVIRONMENT na zeljeno okruzenje npr. `DOTNET_ENVIRONMENT=Development` kada pokrecete aplikaciju kako bi aplikacija pokupila odgovarajuci fajl.


Pokrenite sample aplikaciju i unesite traženi `accountId`, `username` i `password` kada aplikacija
zatraži komandu `login` da se prijavite kao blagajnik.

Komande:
- `login`
- `createPayment`
- `createCashIn`
- `createCashOut`
- `completeCashOut <cashOutId>`
- `cancelPayment <paymentId>`
- `storno <paymentId>`
- `cancelCashIn <cashInId>`
- `cancelCashOut <cashOutId>`

Pokretanje sample projekta:
```bash
dotnet run --project Mikrofin.EMoney.CashDeskSdk.Sample
```

Sample prikazuje sve događaje (`payment.created`, `payment.completed`, `cashIn.created`, `cashIn.completed`, `cashOut.created`, `cashOut.completed`, greške, itd.).
Ako želite prijaviti blagajnika iskoristite komandu `login` i unesite potrebne podatke
Ako želite kreirati placanje iskorisite komandu `createPayment` i unsite potrebne podatke
Ako želite kreirati cashIn iskorisite komandu `createCashIn` i unsite potrebne podatke
Ako želite kreirati cashOut iskorisite komandu `createCashOut` i unsite potrebne podatke
Ako želite potvrditi neki cashOut, iskoristite komandu `completeCashOut <cashOutId>`
Ako želite otkazati neku uplatu, iskoristite komandu `cancelPayment <paymentId>`
Za završeno plaćanje koristite `storno <paymentId>`. `payment.cancel` važi samo za pending plaćanja. Sample koristi `completedAt` ako je dostupan; inače računa 30 minuta od prijema `payment.completed` događaja. CoreApi konačno provjerava uslove. Odgovor `payment.storno.result` prikazuje stanje operacije i osvježeno plaćanje. Samo stanje `Completed` označava plaćanje storniranim. Neriješena bankovna ili wallet stanja zahtijevaju administratorsko usklađivanje.
Ako želite otkazati cashIn, iskoristite komandu `cancelCashIn <cashInId>`
Ako želite otkazati cashOut, iskoristite komandu `cancelCashOut <cashOutId>`
(npr. `cancelPayment e2d1...`, `cancelCashIn e2d1...`, `cancelCashOut e2d1...`).
