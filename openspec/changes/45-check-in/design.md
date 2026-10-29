# Design

## Context

Motivazione e ambito: vedi `proposal.md`. Requisiti: `specs/bookings`, `specs/frontend`.

Stato attuale rilevante:

- **Modello di Bookings:**
  - `Reservation`: `Status` `Confirmed`/`Cancelled` come stringa, `CancelledAt`, `RowVersion`.
  - `ReservationMember`: chiave `ReservationId` + `UserId`, indice su `UserId`.
  - Migrazioni su `main`: `InitialCreate` (`20261027095732`) e `AddBuildingAndOrganizerNames` (`20261028114713`).
  - `AttendanceRecord` esiste solo nei documenti (`docs/handoff-architettura.md`, 6.2; `docs/architettura.md`).
- **Endpoint:**
  - `Features/Reservations/ReservationEndpoints.cs` mappa `POST /`, `GET /mine`, `DELETE /{id:guid}` e il conteggio Staff nel gruppo `/api/bookings`, con `RequireAuthorization()`.
  - Le regole sono funzioni pure in `ReservationRules`, che ricevono tutto come parametro (anche «adesso») e sono coperte da `ReservationRulesTests`. I rifiuti sono `ReservationRejection`, resi con `ToProblem`.
  - La cancellazione usa `CancellationDecision`, perché ha tre esiti: consentita, già cancellata senza modifiche, rifiutata.
  - La scrittura della cancellazione è un `ExecuteUpdateAsync` condizionato; con 0 righe si rilegge la prenotazione e si decide di nuovo.
- **Ruolo:** `user.IsInRole(StaffRole)` con `StaffRole = "Staff"` in `ReservationEndpoints`, `RoleClaimType = "role"`.
- **Tempo:** `TimeProvider` iniettato. Nei test di integrazione c'è `TestClock` (`Set`/`Reset`) di `BookingsApiFixture`, che crea anche token con il ruolo scelto (`CreateClientFor(userId, name, role)`).
- **Gateway:** la route `bookings` (`/api/bookings/**`, tutti i metodi, policy `Default`) inoltra già `POST /api/bookings/{id}/check-in`.
- **Frontend:**
  - `components/my-bookings/my-bookings.ts` costruisce le voci (`ReservationView`) da `getMine()` e salva in `loadedAt` l'ora del dispositivo al caricamento, usata per `canCancel`.
  - `MatSnackBar` è già usata per «Prenotazione cancellata.».
  - `authInterceptor` chiude la sessione su ogni `401` di `/api/`.

## Goals / Non-Goals

**Goals:**

- Registrare il check-in in modo sicuro (organizzatore, membri, `Staff`), una volta sola per prenotazione, con la finestra A6 per gli studenti.
- Esporre lo stato «usata» in `GET /api/bookings/mine` e mostrarlo nella pagina «Le mie prenotazioni».
- Impedire la cancellazione di una prenotazione usata.
- Preparare il modello per il no-show (US-15) senza implementarlo.

**Non-Goals:**

- Segnare il no-show, report di utilizzo, annullare un check-in, pagine per lo `Staff`, eventi di integrazione.

## Decisions

### 1. Route e risposta (decisione dell'utente)

`MapPost("/{id:guid}/check-in", CheckInAsync)` in `ReservationEndpoints`, nello stesso gruppo autenticato:
- `WithName("CheckInReservation")`, `Produces<CheckInResponse>(200)`, `ProducesProblem` per `401`, `403`, `404`, `422`;
- `CheckInResponse(DateTimeOffset CheckedInAt)` serializzato come `{ "checkedInAt": ... }`, con `RecordedAt` del record appena inserito;
- nessun corpo nella richiesta: chi fa il check-in è il claim `sub`, l'istante è `TimeProvider.GetUtcNow()`;
- il vincolo `:guid` lascia al routing il `404` senza corpo per un `id` non GUID, come per la `DELETE`.

La route è quella già prevista dall'handoff (6.2) e dal flusso F4.

- *Scartata:* `POST /api/bookings/{id}/attendance` con il tipo nel corpo. Le regole di check-in e no-show (chi, quando) sono diverse e l'handoff prevede già due route distinte.
- *Scartati:* `204` e `201`. Con `200` e `checkedInAt` il frontend aggiorna la voce senza ricaricare l'elenco (decisione 8). `201` richiederebbe una risorsa di presenza con una `GET`, che non esiste.

### 2. Entità `AttendanceRecord` e migrazione (decisione dell'utente)

- `Data/Entities/AttendanceRecord.cs` contiene:
  - `Id` (`Guid`);
  - `ReservationId` (`Guid`) e la navigazione `Reservation`;
  - `Type` (`AttendanceType`);
  - `RecordedAt` (`DateTimeOffset`);
  - `RecordedBy` (`Guid`): claim `sub` di chi registra, studente o `Staff`.
- `Data/Entities/Enums/AttendanceType.cs`: `enum AttendanceType { CheckIn, NoShow }`. Si salva come stringa con `HasMaxLength(20)`, come `ReservationStatus`.
- `Reservation.AttendanceRecords` (`ICollection<AttendanceRecord>`), usata dalle query di check-in, `/mine` e cancellazione.
- `Data/Configurations/AttendanceRecordConfiguration.cs`:
  - tabella `AttendanceRecords`;
  - FK verso `Reservations` con `OnDelete(Cascade)`, come `ReservationMembers`;
  - **indice unico su `ReservationId`** (decisione 4).
- `DbSet<AttendanceRecord> AttendanceRecords` in `BookingDbContext` e migrazione `AddAttendanceRecords`, che crea solo tabella e indice, senza dati da trasformare.

- *Scartata:* colonna `CheckedInAt` su `Reservations`. Il no-show (US-15) e il report (US-17) hanno bisogno di chi e quando per entrambi i tipi, e l'handoff descrive già l'entità separata.

### 3. Regola pura e ordine dei controlli (decisione dell'utente)

Nuova funzione in `ReservationRules`:

```
ReservationRejection? ValidateCheckIn(
    Reservation reservation, Guid userId, bool isMember, bool isStaff,
    AttendanceType? existingRecord, DateTimeOffset now)
```

Restituisce `null` se il check-in è consentito, altrimenti il rifiuto. Non serve un tipo come `CancellationDecision`, perché il check-in non ha un esito «nessuna modifica». Le costanti `CheckInBefore` e `CheckInAfter` valgono entrambe `TimeSpan.FromMinutes(15)` (A6).

Ordine nell'endpoint:

1. `sub` non GUID → `401` «Accesso richiesto» / «Effettua l'accesso per fare il check-in.».
2. Prenotazione inesistente → `404` «Prenotazione non trovata» / «La prenotazione richiesta non esiste.».
3. Né organizzatore, né membro, né `Staff` → `403` «Check-in non consentito» / «Non puoi fare il check-in di questa prenotazione.».
4. `Status = Cancelled` → `422` «Check-in non consentito» / «Non è possibile fare il check-in di una prenotazione cancellata.».
5. Record `NoShow` → `422` «Check-in non consentito» / «La prenotazione è già stata segnata come non usata.».
6. Record `CheckIn` → `422` «Check-in non consentito» / «Il check-in di questa prenotazione è già stato registrato.».
7. Solo se non `Staff`: `now < Start − 15 min` oppure `now > Start + 15 min` → `422` «Check-in non consentito» / «Il check-in è possibile da 15 minuti prima a 15 minuti dopo l'inizio.».
8. Inserimento del record (decisione 4) → `200`.

L'autorizzazione viene prima, come nella cancellazione, così un estraneo non scopre lo stato di una prenotazione altrui. Gli stati definitivi vengono prima della finestra perché spiegano meglio il rifiuto. La prenotazione si legge con il lock della decisione 4; appartenenza come membro e `Type` dell'eventuale record si leggono subito dopo, nella stessa transazione.

### 4. Concorrenza: lock sulla riga della prenotazione e indice unico (decisione dell'utente)

Il check-in gira in una transazione esplicita, dentro `db.Database.CreateExecutionStrategy().ExecuteAsync(...)` come la `POST`, con `ChangeTracker.Clear()` all'inizio di ogni tentativo:

1. Lettura della prenotazione con `FromSql($"SELECT * FROM Reservations WITH (UPDLOCK, ROWLOCK) WHERE Id = {id}")`. Il lock resta fino al commit.
2. Sotto il lock: lettura di appartenenza come membro e dell'eventuale record di presenza, poi controlli 2–7 (decisione 3).
3. `Add` del record `CheckIn`, `SaveChangesAsync` e commit → `200`.

Due garanzie distinte:

- **Check-in contro check-in.** Le richieste sulla stessa prenotazione si mettono in fila sul lock. Chi arriva dopo legge il record già registrato e riceve `422` «già registrato».
- **Indice unico su `ReservationId`.** Garantisce un solo record di presenza (`CheckIn` o `NoShow`) anche per scritture che non prendono il lock, per esempio un futuro no-show della US-15 scritto diversamente. Se l'`INSERT` lo viola (`DbUpdateException` con `SqlException` `2601`/`2627`), si fa rollback, si rilegge e si risponde con il `422` del record trovato: «già registrato» o «no-show». Mai `500`.

**Check-in contro cancellazione.** La `DELETE` è un `UPDATE` condizionato (`Status = Confirmed`, `Start > now`, nessun `CheckIn`; decisione 7) e prende anch'essa il lock di scrittura sulla riga della prenotazione, incompatibile con l'`UPDLOCK`:

- **Il check-in prende il lock per primo.** L'`UPDATE` aspetta il commit e trova il check-in. Aggiorna 0 righe, rilegge e risponde `422` «Non è possibile cancellare una prenotazione già usata.».
- **L'`UPDATE` arriva per primo.** Il check-in aspetta, poi legge `Cancelled` e risponde `422` «Non è possibile fare il check-in di una prenotazione cancellata.».

In entrambi i casi non può esistere una prenotazione `Cancelled` con un record `CheckIn`. Con un nuovo tentativo dopo un errore transitorio successivo al commit, la rilettura trova il record e lo studente riceve `422` «già registrato» invece di `200`: caso raro e accettato.

**Perché:** è l'unica alternativa che elimina la gara in entrambe le direzioni senza cambiare la forma della `DELETE`. Il costo è una transazione esplicita, la stessa già usata dalla `POST`. L'indice resta come difesa nel database.

- *Scartata (a):* accettare il caso raro. Una prenotazione cancellata ma usata confonderebbe `/mine` e il report della US-17 (cancellata o usata?) e violerebbe la regola «una prenotazione usata non si cancella».
- *Scartata (b):* `INSERT ... SELECT ... WHERE EXISTS (Reservations con Status = Confirmed)` senza lock. Riduce la finestra di gara, ma in `READ COMMITTED` la lettura della prenotazione e l'`UPDATE` della `DELETE` possono ancora intrecciarsi.
- *Scartata:* solo l'indice unico, senza lock. Basta tra due check-in, ma non vede la cancellazione concorrente.
- *Scartata:* indice unico su (`ReservationId`, `Type`). Lascia coesistere un `CheckIn` e un `NoShow`.
- *Scartata:* `sp_getapplock` su `reservation:{id}`. La `DELETE` non lo prenderebbe, quindi non serializza contro la cancellazione.

**Da verificare nel test 1.7:** l'esito «il check-in prende il lock per primo» presuppone che SQL Server valuti la condizione «nessun `CheckIn`» dell'`UPDATE` dopo aver ottenuto il lock sulla riga della prenotazione. Il test di concorrenza ripetuto lo verifica su SQL Server reale. Se fallisse, la `DELETE` deve leggere la riga con `UPDLOCK, ROWLOCK` in una transazione prima dell'`UPDATE`, con lo stesso comportamento osservabile; il motivo va scritto nella PR.

**Presupposto: `READ_COMMITTED_SNAPSHOT` disattivato.** È la situazione di oggi: né `Program.cs` né le migrazioni di Bookings lo attivano. Con RCSI attivo, la sottoquery `NOT EXISTS` su `AttendanceRecords` dell'`UPDATE` leggerebbe la versione dei dati all'inizio dell'istruzione: non vedrebbe un check-in confermato mentre l'`UPDATE` aspettava il lock, e la gara tornerebbe possibile. Il ripiego descritto sopra (la `DELETE` legge la riga con `UPDLOCK` in una transazione prima dell'`UPDATE`) funziona anche con RCSI, perché `UPDLOCK` legge l'ultima versione confermata. Va quindi applicato anche se un giorno si attiva RCSI.

### 5. Chi è membro e chi è `Staff`

- **Membro:** esiste `ReservationMember` con `UserId` = `sub` per quella prenotazione.
- **`Staff`:** `user.IsInRole(StaffRole)`, come per la cancellazione. Lo `Staff` salta solo il controllo 7; cancellata, no-show e già registrato valgono anche per lui.

Prima della US-05 (PR #126) nessuna API crea membri: i test li inseriscono direttamente con `ReservationMember`, come `CancelReservationTests`.

### 6. `checkedInAt` in `GET /api/bookings/mine` (decisione dell'utente)

`MyReservationItem` riceve un ultimo parametro `DateTimeOffset? CheckedInAt`. Nella proiezione esistente (una sola query) si aggiunge:

```
reservation.AttendanceRecords
    .Where(a => a.Type == AttendanceType.CheckIn)
    .Select(a => (DateTimeOffset?)a.RecordedAt)
    .FirstOrDefault()
```

EF Core la traduce in una sottoquery per riga, coperta dall'indice unico su `ReservationId`. Liste, ordinamenti e gli altri campi non cambiano.

### 7. Cancellazione di una prenotazione usata (decisione dell'utente)

- **Regola:** `ValidateCancellation` riceve un parametro in più, `bool hasCheckIn`. L'ordine diventa:
  1. `403` per chi non è organizzatore né `Staff`;
  2. già cancellata → `204` senza modifiche;
  3. con check-in → `422` «Cancellazione non consentita» / «Non è possibile cancellare una prenotazione già usata.»;
  4. `Start <= now` → `422` «già iniziata o passata».

  La lettura iniziale della `DELETE` aggiunge `AttendanceRecords.Any(a => a.Type == CheckIn)`.
- **Scrittura atomica:** la `WHERE` dell'`ExecuteUpdateAsync` aggiunge `!r.AttendanceRecords.Any(a => a.Type == AttendanceType.CheckIn)`. Se il check-in arriva tra la lettura e l'`UPDATE`, l'`UPDATE` aggiorna 0 righe, la rilettura trova il check-in e risponde `422` «già usata» (scenario «Check-in registrato tra la lettura e la scrittura»). Il ciclo esistente a due giri resta valido: dopo un `UPDATE` senza righe la rilettura non può più dare «consentita».
- **Requisiti:** la regola nuova è il requisito ADDED «Nessuna cancellazione di una prenotazione usata». «Cancellazione di una prenotazione», «Cancellazione solo prima dell'inizio» e «Cancellazioni contemporanee» sono MODIFIED perché descrivono le condizioni della cancellazione riuscita e del `422` sull'orario, che ora escludono le prenotazioni usate. «Chi può cancellare una prenotazione» e «Cancellazione ripetuta» non cambiano: il loro controllo viene prima del nuovo.
- **Frontend:** «Cancella» non compare sulle voci con `checkedInAt` (MODIFIED «Azione «Cancella» nelle prenotazioni future»).

### 8. Frontend (decisioni dell'utente)

- **Modello:** `checkedInAt: string | null` in `MyReservationItem` e `CheckInResponse { checkedInAt: string }` in `models/booking.model.ts`. Funzione pura `isInCheckInWindow(item, now)` nello stesso file, con la stessa formula del servizio e unit test sui bordi.
- **Servizio:** `BookingsService.checkIn(id): Observable<CheckInResponse>` con `POST /api/bookings/{id}/check-in`, corpo vuoto, `encodeURIComponent(id)` come `cancel()`.
- **Voce:** `ReservationView` riceve:
  - `used` (`checkedInAt !== null`), che mostra «Usata» nelle schede «Future» e «Passate» come elemento con `tabindex="-1"`;
  - `showCheckIn`: scheda «Future» e `checkedInAt === null`;
  - `canCheckIn`: `showCheckIn` e dentro la finestra;
  - `checkInLabel`: «Check-in per <spazio>, <reservationTimeLabel>».

  `canCancel` aggiunge la condizione `checkedInAt === null`.
- **Ora della finestra:** un signal `now`, aggiornato al caricamento e da `interval(30_000)` con `takeUntilDestroyed`, che si ferma alla distruzione del componente. Lo usano solo `canCheckIn` e il testo di spiegazione. `canCancel` continua a usare `loadedAt`, così il comportamento di «Cancella» resta quello della US-08.
  - *Scartato:* un timeout calcolato sul prossimo bordo di finestra. È più preciso ma più complesso da gestire con più voci; 30 secondi bastano, e il servizio resta l'arbitro.
- **Pulsante fuori finestra:** `matButton` con `disabledInteractive` e `[disabled]="!canCheckIn"`, così resta raggiungibile con Tab. Sotto c'è un paragrafo visibile con `id` unico per voce e il testo «Il check-in è possibile da 15 minuti prima a 15 minuti dopo l'inizio.», collegato con `aria-describedby`. Il gestore del clic controlla comunque `canCheckIn` e lo stato di invio, così il pulsante disabilitato non invia nulla.
  - *Scartati:* `matTooltip`, che non si vede su touch, e il pulsante disabilitato senza `disabledInteractive`, che non riceve il focus.
- **Invio:** nessuna conferma. Un signal con l'`id` della voce in invio disabilita il pulsante e impedisce la seconda richiesta.
- **Dopo il `200`:**
  - la voce si aggiorna in locale: il signal `reservations` sostituisce l'elemento con `checkedInAt` della risposta, senza `getMine()`;
  - `MatSnackBar.open('Check-in registrato.', undefined, { duration: 5000 })`;
  - il focus va sull'etichetta «Usata» della voce dopo il rendering (`afterNextRender` o un `viewChildren` per `id`), perché il pulsante che aveva il focus sparisce.
  - *Scartato:* ricaricare `/mine` come dopo la cancellazione. Qui la voce non cambia scheda, e la risposta contiene già l'unico dato che cambia.
- **Errori:** signal `checkInErrors` per `id` di voce, mostrato nella card con `role="alert"`:
  - `403`, `404` e `422` → `detail` della ProblemDetails;
  - altri errori o nessuna risposta → «Non è stato possibile registrare il check-in. Riprova più tardi.»;
  - `401` → nessun avviso, l'`authInterceptor` chiude la sessione.

  L'avviso si svuota al tentativo successivo.

### 9. Documentazione

- `docs/endpoints.md`:
  - nuova riga `POST /api/bookings/{id}/check-in`: accesso organizzatore, membro o `Staff`, finestra, `200` con `checkedInAt`, codici e messaggi;
  - `checkedInAt` nella riga di `GET /api/bookings/mine`;
  - nuovo `422` nella riga della `DELETE`.
- `docs/handoff-architettura.md`:
  - 6.2: route implementata, finestra A6 al posto di «X, Y da concordare», `Staff` senza finestra, un solo record di presenza per prenotazione, prenotazione usata non cancellabile, messaggi nella tabella degli errori;
  - 6.2, tabella «API previste»: colonna di accesso della riga `POST /api/bookings/{id}/check-in` da «organizzatore o membro» a «organizzatore, membro o Staff» (riga 164 di oggi);
  - flusso F4 (riga 251 di oggi): «verifica organizzatore, membro o Staff + finestra temporale (non per lo Staff)»;
  - I-9: finestra risolta con A6, no-show ancora aperto per la US-15.
- `docs/architettura.md` (riga 47 di oggi): check-in consentito a organizzatore, membri e Staff.
- `docs/servizi.md` (riga 64 di oggi): check-in fatto dall'organizzatore, da un membro o dallo Staff.
- `docs/gateway.md`: passo di prova manuale del check-in.

### 10. Coesistenza con la PR #119 (#43) e con la change `16-prenotare-per-un-gruppo` (PR #126)

- **Migrazione (PR #119):**
  - La #119 aggiunge `20261028150650_AddRoomType` e modifica `BookingDbContextModelSnapshot.cs` (colonna `Type` di `BookableRooms`). Anche #46 aggiunge una migrazione e modifica lo snapshot.
  - Se #46 genera la propria migrazione prima del merge della #119, c'è un conflitto sullo snapshot e, a seconda dei timestamp, una migrazione che non vede l'altra.
  - Regola per #46: partire da `main` dopo il merge della #119. Se invece la #119 viene unita dopo, eliminare e rigenerare `AddAttendanceRecords` in rebase (`dotnet ef migrations remove` e `add`), così il timestamp è successivo a `20261028150650` e lo snapshot contiene entrambe le modifiche.
  - Nessuna sovrapposizione di tabelle o colonne.
- **Documenti (PR #119):** modifica le tabelle «API previste» ed entità di `docs/handoff-architettura.md` 6.2 e aggiunge una riga a `docs/endpoints.md`. Conflitti solo testuali, da risolvere in rebase.
- **Nomi dei requisiti (PR #126):** la change 16 aggiunge in `bookings` requisiti sui membri e modifica «Creazione di una prenotazione singola»; in `frontend` modifica «Conferma della prenotazione» e «Messaggi di rifiuto della prenotazione». Questa change modifica requisiti diversi:
  - in `bookings`: «Contenuto di ogni voce», «Cancellazione di una prenotazione», «Cancellazione solo prima dell'inizio», «Cancellazioni contemporanee»;
  - in `frontend`: «Azione «Cancella» nelle prenotazioni future».

  Nessuno è toccato dalla change 16, quindi le due change si archiviano in qualunque ordine. Il requisito «Permessi dei membri su una prenotazione di gruppo» della 16 si appoggia a «Chi può cancellare una prenotazione», che questa change non modifica.
- **File condivisi (PR #126):**
  - la #17 aggiunge funzioni a `ReservationRules` e test a `ReservationRulesTests`;
  - la #18 aggiunge `memberIds` a `CreateReservationRequest` in `models/booking.model.ts`.

  Questa change aggiunge `ValidateCheckIn`, cambia la firma di `ValidateCancellation` e aggiunge `checkedInAt` in `MyReservationItem`: metodi e interfacce diversi, conflitti solo di posizione. Se la #17 aggiunge chiamate a `ValidateCancellation`, vanno allineate alla firma nuova.

## Risks / Trade-offs

- [Orologio del dispositivo diverso da quello del servizio, oppure fino a 30 secondi di ritardo del timer] → il servizio è l'arbitro, risponde `422` «fuori finestra» e la card mostra il `detail`.
- [Gara tra un check-in e una cancellazione nei 15 minuti prima dell'inizio] → la risolve il lock sulla riga nel check-in insieme all'`UPDATE` condizionato della `DELETE` (decisione 4). Il test di concorrenza ripetuto (tasks 1.7) verifica che non esista mai una prenotazione `Cancelled` con un record `CheckIn`.
- [Il check-in tiene un lock sulla riga della prenotazione per la durata della transazione] → la transazione è breve (una lettura e un inserimento) e blocca solo quella prenotazione; la `POST` blocca le righe di `BookableRooms`, non quelle di `Reservations`.
- [Lo `Staff` può registrare un check-in su qualunque prenotazione confermata, anche futura o passata da tempo, e da quel momento non si può più cancellare] → conseguenza delle decisioni 5 (`Staff` senza finestra) e 7 (prenotazione usata non cancellabile); `RecordedBy` conserva chi l'ha fatto.
- [Dopo il check-in la voce è aggiornata in locale] → se nel frattempo è cambiato altro (per esempio una cancellazione dello `Staff`), si vede solo al caricamento successivo della pagina.
- [Nessuna API crea ancora record `NoShow`] → la regola si testa inserendo il record direttamente nel database, come i membri.

## Migration Plan

- Database: nuova migrazione `AddAttendanceRecords` in Bookings, applicata all'avvio come le precedenti. Crea solo tabella e indice unico, senza dati da trasformare. Rollback: `Down` elimina la tabella. Ordine rispetto alla PR #119: decisione 10.
- Nessuna modifica di configurazione (user-secrets, `docker-compose.yml`, rotte del Gateway).
- Documentazione nel task #46 (decisione 9).
- Archivio: dopo il merge delle PR di #46 e #47, con `/opsx:archive` dal branch del task «Archivio OpenSpec» (#129).

## Open Questions

Nessuna.
