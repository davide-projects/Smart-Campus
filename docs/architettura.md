# Architettura

> Spiegazione non tecnica di ogni servizio: [servizi.md](servizi.md).
> Nomi di servizi, entità, campi, eventi e route sono in **inglese** (vedi [CONTRIBUTING.md](../CONTRIBUTING.md#lingua)); il testo della documentazione è in italiano.

## Criterio di suddivisione

I servizi sono divisi per **responsabilità (bounded context)**, non per entità: i dati che devono restare coerenti nella stessa transazione stanno nello stesso servizio. In particolare la prevenzione dei conflitti di prenotazione avviene tutta dentro **Bookings**, su un solo database.

Su indicazione del docente, la gestione delle presenze (check-in, mancate presentazioni, report) è stata **accorpata in Bookings**, eliminando il servizio separato *Attendance*: semplifica il sistema e toglie la sincronizzazione delle prenotazioni tramite eventi.

## Servizi

### Identity
- **Responsabilità:** autenticazione e ruoli (`Student`, `Staff`); emette JWT.
- **Dati:** `User` (sintetici), con `Role`.
- **Note cloud:** emette token standard, così in produzione si può sostituire con Entra ID senza modificare gli altri servizi.

### Rooms
- **Responsabilità:** anagrafica di ciò che esiste ed è prenotabile.
- **Dati:** `Building`, `Room` (`Type`, `Capacity`, `Floor`, `IsBookable`, `IsActive`), `Equipment`, `OpeningHours`.
- **Ricerca:** `GET /api/rooms?type=&minCapacity=&buildingId=` restituisce solo gli spazi attivi. `IsActive = false` significa dismesso: resta in archivio ma non compare mai nella ricerca. `IsBookable = false` resta invece visibile, ma non prenotabile.
- **Gestione:** `POST /api/rooms` e `PUT /api/rooms/{id}` sono riservati al ruolo `Staff`; aggiornano anche attrezzature e orari.
- **Orari:** nel primo incremento è prevista una fascia per spazio e giorno; i giorni chiusi sono assenti e le chiusure straordinarie fuori ambito. `RoomCreated` e `RoomUpdated` includono lo snapshot degli orari.
- **Seed:** i dati sintetici sono inclusi nella migrazione iniziale per la demo locale e vengono applicati anche a ogni nuovo database Rooms; non sono seed limitati all'ambiente Development. All'avvio Rooms ne pubblica gli eventi una sola volta tramite outbox.
- **Pubblica:** `RoomCreated` (`RoomId`, `Name`, `Capacity`, `IsBookable`, `OpeningHours`, `BuildingName`), `RoomUpdated` (gli stessi campi più `IsActive`), `RoomDeactivated` (`RoomId`), accodati con EF Core Bus Outbox nella stessa transazione dei dati. `BuildingName` è il nome dell'edificio dello spazio al momento dell'accodamento: Rooms lo valorizza sempre (creazione, modifica e pubblicazione seed), ma nel contratto è facoltativo e vale `null` negli eventi che non lo contengono.

### Bookings
- **Responsabilità:** prenotare senza conflitti (singolarmente o in gruppo), cancellare, calcolare la disponibilità, registrare check-in e mancate presentazioni, produrre i report di utilizzo.
- **Dati:**
  - `Reservation` (`OrganizerId`, `RoomId`, `Start`, `End`, `Participants`, `Status`, `RowVersion`);
  - `ReservationMember` (`ReservationId`, `UserId`): gli altri studenti di una prenotazione di gruppo;
  - `BookableRoom`: copia locale aggiornata dagli eventi del servizio Rooms;
  - `AttendanceRecord` (`Type`: `CheckIn` / `NoShow`).
- **Invarianti:**
  - nessuna sovrapposizione tra prenotazioni confermate dello stesso spazio;
  - **nessun utente** (organizzatore o membro) in due prenotazioni confermate sovrapposte, anche in spazi diversi;
  - `Participants` = organizzatore + membri, ≤ `Capacity`; i membri hanno ruolo `Student`;
  - prenotazione dentro l'orario di apertura; spazio attivo; `End > Start`.
- **Prevenzione conflitti sullo spazio (SQL Server):** in un'unica transazione
  1. `SELECT ... FROM BookableRooms WITH (UPDLOCK, ROWLOCK) WHERE Id = @roomId`, che serializza le prenotazioni dello stesso spazio;
  2. verifica delle sovrapposizioni (`Start < @end AND End > @start`);
  3. `INSERT` e commit; in caso di conflitto restituisce `409 Conflict`.
- **Prevenzione conflitti sulle persone:** il lock sullo spazio non basta, perché la stessa persona potrebbe prenotare due spazi diversi nello stesso istante. **Meccanismo da definire.**
- **Prova:** test di integrazione con richieste parallele sullo stesso slot contro SQL Server reale (Testcontainers): ne deve riuscire esattamente una.
- **Check-in:** consentito all'organizzatore e ai membri; basta un check-in per segnare la prenotazione come usata.
- **Regola di interpretazione dei report:** distinguono *prenotato e usato*, *prenotato e non usato*, *nessuna informazione*. L'assenza di dati non indica che lo spazio fosse libero.
- **Consuma:** `RoomCreated`, `RoomUpdated`, `RoomDeactivated` con un consumer idempotente (upsert su `BookableRoom`), un messaggio alla volta (`ConcurrentMessageLimit = 1`) per rispettare l'ordine di arrivo.
- **Limite noto:** l'ordine è garantito solo tra messaggi elaborati normalmente; un messaggio fallito e rielaborato più tardi potrebbe applicare uno stato vecchio. Per escluderlo servirebbe una versione o un `OccurredAt` negli eventi.

### API Gateway
Instrada `/api/{service}/**`, valida i JWT e applica le policy di accesso delle route YARP. La validazione condivisa usa `AddSmartCampusJwtBearer` di `ServiceDefaults`; Rooms valida di nuovo il token e richiede il ruolo `Staff` sulle route di scrittura.

### Frontend
SPA Angular che comunica solo con il gateway.

## Opzione da valutare: audit dei check-in su MongoDB

> **Stato: con riserva, non implementato.** Non è ancora deciso se realizzarlo.

- **Idea:** registrare su MongoDB uno storico degli eventi di check-in (*audit*), separato dal database SQL Server di Bookings.
- **Da decidere prima di implementarlo:** cosa si registra, chi lo consulta, quale valore aggiunge rispetto a `AttendanceRecord` su SQL Server.
- **Impatto se adottato:** un container MongoDB in `docker-compose.yml`, una seconda tecnologia di persistenza da configurare, testare e documentare.
- **Vincolo della traccia:** la doppia persistenza non è imposta e ogni scelta va motivata rispetto al bisogno.

## Principi cloud-ready

| Principio | Applicazione |
|---|---|
| Container | Un `Dockerfile` per ogni servizio; servizi stateless |
| Configurazione esterna | Variabili d'ambiente / user-secrets; nessun segreto versionato |
| Autenticazione | Identity emette i JWT; Gateway e Rooms condividono `AddSmartCampusJwtBearer` in `ServiceDefaults`; Rooms rivalida il token sulle route Staff |
| Database per servizio | 3 database SQL Server separati (`SmartCampus_Identity`, `SmartCampus_Rooms`, `SmartCampus_Bookings`) |
| Messaggistica | MassTransit 8.5.12 in Rooms e Bookings: versione unica scelta per mantenere l'allineamento e usare la licenza open source; RabbitMQ in locale, Azure Service Bus in cloud |
| Eventi affidabili | Outbox pattern (MassTransit + EF Core) in Rooms |
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

1. Rooms + Bookings + Gateway + Frontend: prenotazione singola e di gruppo senza conflitti, con test di concorrenza.
2. Identity: ruoli reali.
3. Check-in, mancate presentazioni e report (in Bookings).
