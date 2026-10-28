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
- **Dati:** `Building`, `Room` (`Type`, `Capacity`, `Floor`, `IsBookable`, `IsActive`) e `OpeningHours` in SQL Server; catalogo `Equipment` e associazioni `RoomEquipment` in MongoDB.
- **Ricerca:** `GET /api/rooms?type=&minCapacity=&buildingId=&equipmentIds=` restituisce solo gli spazi attivi e, per ogni spazio, le attrezzature assegnate con ID e nome. `equipmentIds` è ripetibile e con più valori richiede tutte le attrezzature selezionate. `IsActive = false` significa dismesso: resta in archivio ma non compare mai nella ricerca. `IsBookable = false` resta invece visibile, ma non prenotabile.
- **Gestione:** `POST /api/rooms` e `PUT /api/rooms/{id}` sono riservati al ruolo `Staff`; dati dello spazio e orari sono scritti in SQL Server, attrezzature in MongoDB.
- **Scelta MongoDB:** `Equipment` è il catalogo e `RoomEquipment` conserva, per ogni spazio, l'elenco degli ID delle attrezzature. La forma documentale supporta la sostituzione atomica dell'assegnazione e il filtro con indice multikey, mantenendo SQL Server per i dati relazionali e l'outbox.
- **Orari:** nel primo incremento è prevista una fascia per spazio e giorno; i giorni chiusi sono assenti e le chiusure straordinarie fuori ambito. `RoomCreated` e `RoomUpdated` includono lo snapshot degli orari.
- **Seed e migrazione:** i dati sintetici di edifici, spazi e orari sono inclusi nella migrazione iniziale per la demo locale e vengono applicati anche a ogni nuovo database Rooms; non sono seed limitati all'ambiente Development. Le attrezzature di esempio sono seed idempotenti in MongoDB. Durante il passaggio dal vecchio schema, Rooms copia prima catalogo e associazioni dalle tabelle SQL in MongoDB e solo dopo le rimuove con una migrazione. All'avvio Rooms pubblica gli eventi una sola volta tramite outbox.
- **Pubblica:** `RoomCreated` (`RoomId`, `Name`, `Capacity`, `IsBookable`, `OpeningHours`, `BuildingName`), `RoomUpdated` (gli stessi campi più `IsActive`), `RoomDeactivated` (`RoomId`), accodati con EF Core Bus Outbox nella stessa transazione dei dati. `BuildingName` è il nome dell'edificio dello spazio al momento dell'accodamento: Rooms lo valorizza sempre (creazione, modifica e pubblicazione seed), ma nel contratto è facoltativo e vale `null` negli eventi che non lo contengono.

### Bookings
- **Responsabilità:** prenotare senza conflitti (singolarmente o in gruppo), cancellare, calcolare la disponibilità, registrare check-in e mancate presentazioni, produrre i report di utilizzo.
- **Dati:**
  - `Reservation` (`OrganizerId`, `OrganizerName`, `RoomId`, `Start`, `End`, `Participants`, `Status`, `RowVersion`): `OrganizerName` è il nome visualizzato letto dal claim `name` del token alla creazione;
  - `ReservationMember` (`ReservationId`, `UserId`): gli altri studenti di una prenotazione di gruppo;
  - `BookableRoom` (`RoomId`, `Name`, `BuildingName`, `Capacity`, `IsBookable`, `IsActive`, orari di apertura): copia locale aggiornata dagli eventi del servizio Rooms; `BuildingName` si aggiorna solo con eventi che lo contengono;
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
- **Prevenzione conflitti sulle persone:** il lock sullo spazio non basta, perché la stessa persona potrebbe prenotare due spazi diversi nello stesso istante. Nella stessa transazione, dopo il lock sullo spazio, Bookings prende un lock `sp_getapplock` sulla persona (risorsa `user:{id}`, `@LockOwner = 'Transaction'`): si libera da solo al commit o al rollback e, se non arriva entro 10 secondi, la richiesta termina con `409`. Dettagli e alternative scartate in [servizi.md](servizi.md#come-bookings-evita-le-sovrapposizioni-inv-1-e-inv-8).
- **Prova:** test di integrazione con richieste parallele sullo stesso slot contro SQL Server reale (Testcontainers): ne deve riuscire esattamente una.
- **Check-in:** consentito all'organizzatore e ai membri; basta un check-in per segnare la prenotazione come usata.
- **Regola di interpretazione dei report:** distinguono *prenotato e usato*, *prenotato e non usato*, *nessuna informazione*. L'assenza di dati non indica che lo spazio fosse libero.
- **Consuma:** `RoomCreated`, `RoomUpdated`, `RoomDeactivated` con un consumer idempotente (upsert su `BookableRoom`), un messaggio alla volta (`ConcurrentMessageLimit = 1`) per rispettare l'ordine di arrivo.
- **Limite noto:** l'ordine è garantito solo tra messaggi elaborati normalmente; un messaggio fallito e rielaborato più tardi potrebbe applicare uno stato vecchio. Per escluderlo servirebbe una versione o un `OccurredAt` negli eventi.
- **Limiti noti di «le mie prenotazioni» (US-07):**
  - il nome dell'organizzatore è uno *snapshot*: se il nome visualizzato cambia in Identity, le prenotazioni esistenti mostrano quello vecchio;
  - la rinomina di un edificio non arriva a Bookings finché ogni suo spazio non riceve un `RoomUpdated`: Rooms non ha un evento per gli edifici;
  - le prenotazioni create prima del salvataggio del nome dell'organizzatore e gli spazi copiati prima dell'arrivo di `BuildingName` restano con `NULL` (la migrazione non riempie le righe esistenti) e l'API restituisce `null`.

### API Gateway
Instrada `/api/{service}/**`, valida i JWT e applica le policy di accesso delle route YARP. La validazione condivisa usa `AddSmartCampusJwtBearer` di `ServiceDefaults`; Rooms e Bookings validano di nuovo il token (Rooms richiede il ruolo `Staff` sulle route di scrittura, Bookings un utente autenticato su tutte le sue route).

### Frontend
SPA Angular che comunica solo con il gateway.

## Principi cloud-ready

| Principio | Applicazione |
|---|---|
| Container | Un `Dockerfile` per ogni servizio; servizi stateless |
| Configurazione esterna | Variabili d'ambiente / user-secrets; nessun segreto versionato |
| Autenticazione | Identity emette i JWT; Gateway, Rooms e Bookings condividono `AddSmartCampusJwtBearer` in `ServiceDefaults`; Rooms rivalida il token sulle route Staff e Bookings su tutte le sue route |
| Database per servizio | Identity e Bookings usano SQL Server; Rooms usa SQL Server per edifici, spazi, orari e outbox, e MongoDB per catalogo e assegnazioni delle attrezzature |
| Messaggistica | MassTransit 8.5.12 in Rooms e Bookings: versione unica scelta per mantenere l'allineamento e usare la licenza open source; RabbitMQ in locale, Azure Service Bus in cloud |
| Eventi affidabili | Outbox pattern (MassTransit + EF Core) in Rooms |
| Health check | `/health/live`, `/health/ready` |

## Corrispondenza con Azure (progettata, non eseguita)

| Locale | Azure |
|---|---|
| Container dei servizi e del gateway | Azure Container Apps |
| SQL Server in container | Azure SQL Database (uno per servizio, serverless o elastic pool) |
| MongoDB in container | MongoDB Atlas (cluster gestito, ad esempio su Azure) |
| RabbitMQ | Azure Service Bus |
| `.env` / user-secrets | Azure Key Vault + Managed Identity |
| Frontend nginx | Azure Static Web Apps |

## Incrementi previsti

1. Rooms + Bookings + Gateway + Frontend: prenotazione singola e di gruppo senza conflitti, con test di concorrenza.
2. Identity: ruoli reali.
3. Check-in, mancate presentazioni e report (in Bookings).
