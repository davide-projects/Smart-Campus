# Architettura

> Spiegazione non tecnica di ogni servizio: [servizi.md](servizi.md).
> Nomi di servizi, entità, campi, eventi e route sono in **inglese** (vedi [CONTRIBUTING.md](../CONTRIBUTING.md#lingua)); il testo della documentazione è in italiano.

## Criterio di suddivisione

I servizi sono divisi per **responsabilità (bounded context)**, non per entità: i dati che devono restare coerenti nella stessa transazione stanno nello stesso servizio. In particolare la prevenzione dei conflitti di prenotazione avviene tutta dentro **Bookings**, su un solo database.

## Servizi

### Identity
- **Responsabilità:** autenticazione e ruoli (`Student`, `Staff`); emette JWT.
- **Dati:** `User` (sintetici), con `Role`.
- **Note cloud:** emette token standard, così in produzione si può sostituire con Entra ID senza modificare gli altri servizi.

### Rooms
- **Responsabilità:** anagrafica di ciò che esiste ed è prenotabile.
- **Dati:** `Building`, `Room` (`Type`, `Capacity`, `IsBookable`), `Equipment`, `OpeningHours`.
- **Pubblica:** `RoomCreated`, `RoomUpdated`, `RoomDeactivated`.

### Bookings
- **Responsabilità:** prenotare senza conflitti, cancellare, calcolare la disponibilità.
- **Dati:** `Reservation` (con `RowVersion`), `BookableRoom` (copia locale aggiornata dagli eventi del servizio Rooms).
- **Invarianti:** nessuna sovrapposizione tra prenotazioni confermate dello stesso spazio; prenotazione dentro l'orario di apertura; spazio attivo; `End > Start`.
- **Prevenzione conflitti (SQL Server):** in un'unica transazione
  1. `SELECT ... FROM BookableRooms WITH (UPDLOCK, ROWLOCK) WHERE Id = @roomId`, che serializza le prenotazioni dello stesso spazio;
  2. verifica delle sovrapposizioni (`Start < @end AND End > @start`);
  3. `INSERT` e commit; in caso di conflitto restituisce `409 Conflict`.
- **Prova:** test di integrazione con richieste parallele sullo stesso slot contro SQL Server reale (Testcontainers): ne deve riuscire esattamente una.
- **Pubblica:** `ReservationConfirmed`, `ReservationCancelled`.

### Attendance
- **Responsabilità:** utilizzo effettivo e report per il personale.
- **Dati:** `AttendanceRecord` (`CheckIn`, `NoShow`), `ReservationProjection` (proiezione delle prenotazioni costruita dagli eventi).
- **Regola di interpretazione:** il report distingue *prenotato e usato*, *prenotato e non usato*, *nessuna informazione*. L'assenza di dati non indica che lo spazio fosse libero.

### API Gateway
Routing `/api/{service}/**`, validazione JWT, CORS. Nessuna logica di dominio.

### Frontend
SPA Angular che comunica solo con il gateway.

## Principi cloud-ready

| Principio | Applicazione |
|---|---|
| Container | Un `Dockerfile` per ogni servizio; servizi stateless |
| Configurazione esterna | Variabili d'ambiente / user-secrets; nessun segreto versionato |
| Database per servizio | 4 database SQL Server separati (`SmartCampus_Identity`, `SmartCampus_Rooms`, `SmartCampus_Bookings`, `SmartCampus_Attendance`) |
| Messaggistica | MassTransit: RabbitMQ in locale, Azure Service Bus in cloud |
| Eventi affidabili | Outbox pattern (MassTransit + EF Core) |
| Health check | `/health/live`, `/health/ready` |

## Corrispondenza con Azure (progettata, non eseguita)

| Locale | Azure |
|---|---|
| Container dei servizi e del gateway | Azure Container Apps |
| SQL Server in container | Azure SQL Database (uno per servizio, serverless o elastic pool) |
| RabbitMQ | Azure Service Bus |
| `.env` / user-secrets | Azure Key Vault + Managed Identity |
| Frontend nginx | Azure Static Web Apps |

## Incrementi previsti

1. Rooms + Bookings + Gateway + Frontend: prenotazione senza conflitti con test di concorrenza.
2. Identity: ruoli reali.
3. Attendance + eventi: check-in e report.
