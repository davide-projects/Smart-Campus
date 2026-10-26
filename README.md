# SmartCampus

Project Work ITS Bari · Novembre 2026.

Prodotto per **trovare e prenotare gli spazi del campus senza impegni incompatibili** e rendere più comprensibile il loro utilizzo effettivo.

> Tutti i dati sono **sintetici** e prodotti dal gruppo. Nessun dato personale reale, nessun segreto nel repository.

## Architettura

| Componente | Cartella | Porta locale | Responsabilità |
|---|---|---|---|
| Frontend (Angular) | `frontend/` | 4200 | Interfaccia per studenti e personale |
| API Gateway (YARP) | `src/Gateway/` | 5000 | Unico ingresso pubblico, routing, CORS |
| Identity | `src/Services/Identity/` | 5101 | Autenticazione, ruoli, emissione JWT |
| Catalogo | `src/Services/Catalogo/` | 5102 | Edifici, spazi, attrezzature, orari |
| Prenotazioni | `src/Services/Prenotazioni/` | 5103 | Prenotazioni senza conflitti, disponibilità |
| Utilizzo | `src/Services/Utilizzo/` | 5104 | Check-in, no-show, report per il personale |

Ogni servizio ha **il proprio database SQL Server** e comunica con gli altri solo tramite API o eventi (RabbitMQ in locale). Dettagli in [docs/architettura.md](docs/architettura.md); per scrivere le user stories vedi [docs/handoff-architettura.md](docs/handoff-architettura.md).

```
src/
  BuildingBlocks/
    SmartCampus.ServiceDefaults/   health check, ProblemDetails, OpenAPI comuni
    SmartCampus.Contracts/         SOLO eventi scambiati tra servizi
  Gateway/
  Services/{Identity,Catalogo,Prenotazioni,Utilizzo}/
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
- Gateway: http://localhost:5000 (es. `GET /api/catalogo/`)
- RabbitMQ management: http://localhost:15672

### Sviluppo (servizi in locale, infrastruttura in Docker)

```bash
docker compose up -d sqlserver rabbitmq
dotnet run --project src/Services/Catalogo/SmartCampus.Catalogo.Api
dotnet run --project src/Gateway/SmartCampus.Gateway
cd frontend && npm install && npm start
```

Le connection string locali vanno negli **user-secrets**, non in `appsettings.json`:

```bash
dotnet user-secrets init --project src/Services/Catalogo/SmartCampus.Catalogo.Api
dotnet user-secrets set "ConnectionStrings:Database" "Server=localhost,1433;User Id=sa;Password=<la tua>;TrustServerCertificate=True;Database=SmartCampus_Catalogo" --project src/Services/Catalogo/SmartCampus.Catalogo.Api
```

In sviluppo Angular inoltra `/api/*` al gateway tramite `frontend/proxy.conf.json`.

## Test

```bash
dotnet test SmartCampus.slnx
cd frontend && npx ng test --watch=false
```

Ogni servizio ha i propri unit test (regole di dominio) e test di integrazione (API + SQL Server reale con Testcontainers). `SmartCampus.EndToEnd.Tests` verifica i flussi tra più servizi.

## Contribuire

Convenzioni per issue, branch, commit e pull request (collegati automaticamente al GitHub Project) in [CONTRIBUTING.md](CONTRIBUTING.md).

## Cloud

Il sistema è progettato per il cloud (container stateless, configurazione esterna, health check `/health/live` e `/health/ready`, un database per servizio). La corrispondenza prevista con Azure è descritta in [docs/architettura.md](docs/architettura.md). **Il deploy non è stato eseguito**: la dimostrazione è locale.
