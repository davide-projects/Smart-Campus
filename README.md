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
| Bookings | `src/Services/Bookings/` | 5103 | *Chi userà quale spazio e quando? Lo ha usato davvero?* | Prenotazioni singole e di gruppo senza conflitti, disponibilità, check-in, report |

Ogni servizio ha **il proprio database SQL Server** e comunica con gli altri solo tramite API o eventi (RabbitMQ in locale).

- **Cosa fa ogni servizio, spiegato semplice:** [docs/servizi.md](docs/servizi.md)
- **Catalogo endpoint HTTP** (route attive, pianificate e istruzioni per aggiungerne): [docs/endpoints.md](docs/endpoints.md)
- Dettagli tecnici: [docs/architettura.md](docs/architettura.md)
- Base per le user stories: [docs/handoff-architettura.md](docs/handoff-architettura.md) · bozza: [docs/user-stories-bozza.md](docs/user-stories-bozza.md)

```
src/
  BuildingBlocks/
    SmartCampus.ServiceDefaults/   health check, ProblemDetails, OpenAPI comuni
    SmartCampus.Contracts/         SOLO eventi scambiati tra servizi
  Gateway/
  Services/{Identity,Rooms,Bookings}/
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

Ogni servizio usa un database dedicato, anche quando i database risiedono sulla stessa istanza SQL Server. Per lo sviluppo locale, configura la connection string negli **user-secrets del singolo progetto**: tutti usano la chiave `ConnectionStrings:Database`, ma ciascuno punta al proprio database. Non inserire credenziali in `appsettings.json` e non committarle.

Per SQL Server locale avviato con Docker Compose (`localhost,1433`, utente `sa`), imposta la connection string per ogni progetto. Sostituisci `<password>` con la password locale di SQL Server; se usi un altro server o utente, aggiorna i relativi campi. Gli ID degli user-secrets sono definiti nei progetti, quindi non serve eseguire `dotnet user-secrets init`.

```bash
dotnet user-secrets set "ConnectionStrings:Database" "Server=localhost,1433;User Id=sa;Password=<password>;TrustServerCertificate=True;Database=SmartCampus_Identity" --project src/Services/Identity/SmartCampus.Identity.Api
dotnet user-secrets set "ConnectionStrings:Database" "Server=localhost,1433;User Id=sa;Password=<password>;TrustServerCertificate=True;Database=SmartCampus_Rooms" --project src/Services/Rooms/SmartCampus.Rooms.Api
dotnet user-secrets set "ConnectionStrings:Database" "Server=localhost,1433;User Id=sa;Password=<password>;TrustServerCertificate=True;Database=SmartCampus_Bookings" --project src/Services/Bookings/SmartCampus.Bookings.Api
```

Rooms valida i JWT emessi da Identity: imposta la stessa chiave di firma di almeno 32 byte in entrambi i progetti.

```bash
dotnet user-secrets set "Jwt:SigningKey" "<stessa chiave casuale di almeno 32 byte usata da Identity>" --project src/Services/Rooms/SmartCampus.Rooms.Api
```

Rooms pubblica gli eventi e Bookings li consuma tramite RabbitMQ. Per avviare entrambi con `dotnet run`, imposta gli user-secrets di ciascun progetto con gli stessi valori di `RABBITMQ_USER` e `RABBITMQ_PASSWORD` presenti in `.env`:

```bash
dotnet user-secrets set "RabbitMq:Host" "localhost" --project src/Services/Rooms/SmartCampus.Rooms.Api
dotnet user-secrets set "RabbitMq:Port" "5672" --project src/Services/Rooms/SmartCampus.Rooms.Api
dotnet user-secrets set "RabbitMq:VirtualHost" "/" --project src/Services/Rooms/SmartCampus.Rooms.Api
dotnet user-secrets set "RabbitMq:Username" "<utente>" --project src/Services/Rooms/SmartCampus.Rooms.Api
dotnet user-secrets set "RabbitMq:Password" "<password>" --project src/Services/Rooms/SmartCampus.Rooms.Api

dotnet user-secrets set "RabbitMq:Host" "localhost" --project src/Services/Bookings/SmartCampus.Bookings.Api
dotnet user-secrets set "RabbitMq:Username" "<utente>" --project src/Services/Bookings/SmartCampus.Bookings.Api
dotnet user-secrets set "RabbitMq:Password" "<password>" --project src/Services/Bookings/SmartCampus.Bookings.Api
```

Senza queste impostazioni i servizi provano a collegarsi con `guest`/`guest`, ma il container RabbitMQ usa l'utente configurato da `RABBITMQ_USER`; la connessione locale fallisce. In Docker Compose host e credenziali sono già passati ai servizi tramite variabili d'ambiente (`RabbitMq__Host`, `RabbitMq__Port`, `RabbitMq__VirtualHost`, `RabbitMq__Username`, `RabbitMq__Password`).

Gli user-secrets sono separati per progetto: se lavori su più servizi, esegui i comandi per ognuno. In Docker Compose gli host dei database sono `sqlserver` e le tre connection string con database distinti sono già configurate in `docker-compose.yml`; per avviare lo stack completo imposta in `.env` le credenziali locali richieste da `.env.example`. La password `MSSQL_SA_PASSWORD` deve avere almeno 8 caratteri e includere almeno 3 categorie tra lettere maiuscole, minuscole, numeri e simboli, altrimenti SQL Server si arresta durante l'inizializzazione. Compose controlla che SQL Server accetti una query prima di avviare Identity, Rooms e Bookings.

Attualmente Rooms e Bookings leggono `ConnectionStrings:Database` e applicano le migrazioni EF Core all'avvio. Identity userà la stessa chiave quando verrà implementata la sua persistenza.

Per creare una migrazione dopo aver modificato il modello:

```bash
dotnet tool restore
dotnet ef migrations add NomeMigrazione --project src/Services/Rooms/SmartCampus.Rooms.Api --output-dir Data/Migrations
```

Il tool `dotnet-ef` è versionato in `.config/dotnet-tools.json`: `dotnet tool restore` lo installa per il repository, senza installazioni globali. La migrazione iniziale di Rooms include anche i dati sintetici di sviluppo (edifici, spazi, attrezzature, orari).

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
