# SmartCampus

Project Work ITS Bari · Novembre 2026.

Prodotto per **trovare e prenotare gli spazi del campus senza impegni incompatibili** e rendere più comprensibile il loro utilizzo effettivo.

> Tutti i dati sono **sintetici** e prodotti dal gruppo. Nessun dato personale reale, nessun segreto nel repository.

## Architettura

| Componente | Cartella | Porta locale | Risponde alla domanda | Contenuto |
|---|---|---|---|---|
| Web (Angular) | `frontend/` | 4200 | *Cosa vede l'utente?* | Schermate per studenti e personale |
| Gateway (YARP) | `src/Gateway/` | 5000 | *A quale servizio va la richiesta?* | Unico ingresso pubblico, routing, CORS |
| Identity | `src/Services/Identity/` | 5101 | *Chi sei e cosa puoi fare?* | Login, ruoli, emissione JWT |
| Rooms | `src/Services/Rooms/` | 5102 | *Quali spazi esistono e come sono fatti?* | Edifici, aule, attrezzature, orari |
| Bookings | `src/Services/Bookings/` | 5103 | *Chi userà quale spazio e quando?* | Prenotazioni senza conflitti, disponibilità |
| Attendance | `src/Services/Attendance/` | 5104 | *Lo spazio è stato usato davvero?* | Check-in, mancate presentazioni, report |

Ogni servizio ha **il proprio database SQL Server** e comunica con gli altri solo tramite API o eventi (RabbitMQ in locale).

- **Cosa fa ogni servizio, spiegato semplice:** [docs/servizi.md](docs/servizi.md)
- Dettagli tecnici: [docs/architettura.md](docs/architettura.md)
- Base per le user stories: [docs/handoff-architettura.md](docs/handoff-architettura.md) · bozza: [docs/user-stories-bozza.md](docs/user-stories-bozza.md)

```
src/
  BuildingBlocks/
    SmartCampus.ServiceDefaults/   health check, ProblemDetails, OpenAPI comuni
    SmartCampus.Contracts/         SOLO eventi scambiati tra servizi
  Gateway/
  Services/{Identity,Rooms,Bookings,Attendance}/
tests/
  {Servizio}/SmartCampus.{Servizio}.UnitTests/
  {Servizio}/SmartCampus.{Servizio}.IntegrationTests/
  SmartCampus.EndToEnd.Tests/
frontend/
docs/
```

## Prerequisiti

- .NET SDK 10
- Node.js 24 + npm
- Docker Desktop

## Avvio

### Tutto in Docker

```bash
cp .env.example .env
docker compose up --build
```

- Frontend: http://localhost:4200
- Gateway: http://localhost:5000 (es. `GET /api/rooms/`)
- RabbitMQ management: http://localhost:15672

### Sviluppo (servizi in locale, infrastruttura in Docker)

```bash
docker compose up -d sqlserver rabbitmq
dotnet run --project src/Services/Rooms/SmartCampus.Rooms.Api
dotnet run --project src/Gateway/SmartCampus.Gateway
cd frontend && npm install && npm start
```

Le connection string locali vanno negli **user-secrets**, non in `appsettings.json`:

```bash
dotnet user-secrets init --project src/Services/Rooms/SmartCampus.Rooms.Api
dotnet user-secrets set "ConnectionStrings:Database" "Server=localhost,1433;User Id=sa;Password=<la tua>;TrustServerCertificate=True;Database=SmartCampus_Rooms" --project src/Services/Rooms/SmartCampus.Rooms.Api
```

In sviluppo Angular inoltra `/api/*` al gateway tramite `frontend/proxy.conf.json`.

## Test

```bash
dotnet test SmartCampus.slnx
cd frontend && npx ng test --watch=false
```

Ogni servizio ha i propri unit test (regole di dominio) e test di integrazione (API + SQL Server reale con Testcontainers). `SmartCampus.EndToEnd.Tests` verifica i flussi tra più servizi.

## Contribuire

Convenzioni per lingua (**codice in inglese**, documentazione e testi utente in italiano), issue, branch, commit e pull request (collegati automaticamente al GitHub Project) in [CONTRIBUTING.md](CONTRIBUTING.md).

## Cloud

Il sistema è progettato per il cloud (container stateless, configurazione esterna, health check `/health/live` e `/health/ready`, un database per servizio). La corrispondenza prevista con Azure è descritta in [docs/architettura.md](docs/architettura.md). **Il deploy non è stato eseguito**: la dimostrazione è locale.
