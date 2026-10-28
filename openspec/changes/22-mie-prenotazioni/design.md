# Design

## Context

Motivazione e ambito: vedi `proposal.md`. Requisiti: `specs/rooms`, `specs/bookings`, `specs/frontend`.

Stato attuale rilevante:

- **Rooms** accoda `RoomCreated` e `RoomUpdated` con l'EF Core Bus Outbox in tre punti: creazione (`RoomEndpoints.CreateAsync`, tramite `RoomEventFactory.ToCreatedEvent`), modifica (`RoomEndpoints.UpdateAsync`, che costruisce `RoomUpdated` a mano) e pubblicazione unica degli spazi seed (`RoomSeedEventPublisher`). Nessuno dei tre carica oggi l'edificio: creazione e modifica controllano solo che `BuildingId` esista.
- **Contratti:** `RoomCreated(RoomId, Name, Capacity, IsBookable)` e `RoomUpdated(..., IsActive)` hanno `OpeningHours` come proprietà `init` con valore predefinito, aggiunta dopo i parametri posizionali.
- **Bookings** consuma gli eventi con `RoomEventsConsumer` (upsert idempotente su `BookableRoom`, un messaggio alla volta). `BookableRoom` non ha l'edificio; `Reservation` ha solo `OrganizerId`. Gli indici per «le mie prenotazioni» esistono già: `Reservations (OrganizerId, Start)` e `ReservationMembers (UserId)`.
- **PR #86** (su `main`) ha aggiunto `POST /api/bookings`, `CurrentUser.TryGetUserId` (legge `sub` dal JWT senza verificarne la firma, che verifica il Gateway) e la serializzazione degli enum come stringhe. Crea solo prenotazioni singole (`Participants = 1`, nessun membro).
- **Identity** emette il claim `name` con il nome visualizzato (`docs/identity.md`); `DisplayName` è lungo al massimo 128 caratteri. `Building.Name` in Rooms è lungo al massimo 100 caratteri.
- **Frontend:** pagine in `components/<nome>/`, `authGuard` per le pagine riservate, voci di menu in `LINKS` di `components/shared/navbar/navbar.ts` con il campo `access`. Nessuna locale italiana registrata e nessuna pipe di data in uso.

## Goals / Non-Goals

**Goals:**

- Mostrare edificio e organizzatore leggendo solo il database di Bookings, senza chiamate sincrone ad altri servizi.
- Cambiare i contratti degli eventi in modo additivo.
- Una sola query per utente, servita dagli indici esistenti.

**Non-Goals:**

- Evento per gli edifici (`BuildingUpdated`) o per gli utenti (cambio di nome visualizzato).
- Versionare gli eventi o risolvere il limite noto sull'ordine dei messaggi rielaborati (`docs/architettura.md`).
- Seconda validazione del JWT in Bookings.

## Decisions

### 1. Edificio copiato in Bookings tramite gli eventi di Rooms (decisione dell'utente)

Rooms aggiunge `BuildingName` a `RoomCreated` e `RoomUpdated`; Bookings lo salva in `BookableRoom.BuildingName`.

- *Scartata:* chiamare Rooms a ogni `GET /api/bookings/mine`. Lega la disponibilità della pagina a quella di Rooms e aggiunge una chiamata tra servizi che oggi non esiste.
- *Scartata:* mandare solo `BuildingId` e un evento separato per gli edifici. Rooms non ha ancora la gestione degli edifici (anagrafiche pianificate in `docs/handoff-architettura.md`, 6.1): sarebbe un nuovo evento e un nuovo consumer per un caso che oggi non si verifica.

### 2. Forma del campo nel contratto

`BuildingName` si aggiunge come proprietà `init`, come `OpeningHours`, non come parametro posizionale.

- Motivo: le chiamate esistenti a `new RoomCreated(...)` e `new RoomUpdated(...)` (Rooms e test di Bookings) restano valide e il cambiamento è additivo per chi deserializza.
- *Scartata:* parametro posizionale. Obbliga a cambiare ogni costruttore, compresi i test di Bookings che non c'entrano con l'edificio.
- Tipo `string?` con valore predefinito `null`: un evento accodato prima della modifica (o senza il campo) si legge senza errori. Il consumer di Bookings aggiorna l'edificio solo se il campo è valorizzato: con `null` lascia quello già salvato, così un evento vecchio rimasto in coda non cancella un edificio noto. Rooms valorizza sempre il campo, perché ogni spazio ha un edificio.

In Rooms la costruzione dei due eventi passa da `RoomEventFactory` (si aggiunge `ToUpdatedEvent`), così il nome dell'edificio si valorizza in un solo posto. Creazione e modifica leggono il nome dell'edificio dove oggi controllano solo che esista; la pubblicazione seed include l'edificio nella query degli spazi.

### 3. Nome dell'organizzatore salvato alla creazione (decisione dell'utente)

`Reservation.OrganizerName` viene valorizzato nella `POST /api/bookings` dal claim `name` del token, letto con un metodo accanto a `CurrentUser.TryGetUserId` (stessa decodifica del payload, nessuna verifica della firma, come da `docs/gateway.md`).

- È uno *snapshot*: se il nome visualizzato cambia in Identity, le prenotazioni esistenti mostrano quello vecchio.
- *Scartata:* chiedere il nome a Identity a ogni lettura. Identity non ha un'API per cercare altri utenti e servirebbe una chiamata tra servizi.
- *Scartata:* copia locale degli utenti alimentata da eventi di Identity. Identity non pubblica eventi; sarebbe un'infrastruttura nuova per un solo campo.
- Claim `name` assente o vuoto: la `POST` risponde `401` con lo stesso titolo e messaggio della #86 per `sub` mancante («Accesso richiesto», «Effettua l'accesso per prenotare.»), prima di qualsiasi lock. Identity emette sempre il claim, quindi il caso riguarda solo token non emessi da Identity.

### 4. Una migrazione EF in Bookings

Il task #23 aggiunge in una sola migrazione `BookableRooms.BuildingName` (`nvarchar(100)`, come `Building.Name` in Rooms) e `Reservations.OrganizerName` (`nvarchar(128)`, come `DisplayName` in Identity). Una migrazione sola perché le due colonne arrivano nella stessa PR. Entrambe le colonne sono nullable e la migrazione non riempie le righe esistenti: le prenotazioni già salvate restano con `OrganizerName = NULL`, gli spazi già copiati con `BuildingName = NULL` finché non ricevono un `RoomUpdated`. L'API restituisce `null` e il frontend mostra «Organizzatore non disponibile» e «—».

### 5. Query di `GET /api/bookings/mine`

- Una query su `Reservations` filtrata con `OrganizerId == userId || Members.Any(m => m.UserId == userId)`, con proiezione su nome dello spazio e dell'edificio (da `BookableRoom`), `OrganizerName` e `IsGroup = Members.Any()`. Gli indici esistenti coprono entrambi i rami.
- Divisione in sezioni con l'istante corrente da `TimeProvider` (già registrato nel servizio), così i test controllano il tempo: future = `Confirmed` con `End > now`, passate = `Confirmed` con `End <= now`, cancellate = `Cancelled`.
- Ordinamenti: `upcoming` per inizio crescente, `past` e `cancelled` per inizio decrescente.
- Risposta: `{ upcoming, past, cancelled }`, ogni voce con `id`, `roomId`, `roomName`, `buildingName`, `start`, `end`, `isGroup`, `organizerName`, `isOrganizer` (`OrganizerId == userId`). Nessuna paginazione né limite (decisione dell'utente): le future sono poche per costruzione (anticipo massimo 14 giorni nella #86, nessuna sovrapposizione per INV-8); passate e cancellate crescono nel tempo, ma con i dati sintetici della demo restano contenute. Se diventassero tante, la paginazione si aggiunge in una storia successiva.
- *Scartata:* tre endpoint separati o un parametro `?section=`. Più chiamate dal frontend per una pagina che mostra le tre sezioni insieme.
- L'utente si identifica come nella POST (`CurrentUser.TryGetUserId`); senza `sub` valido la risposta è `401`.
- Il Gateway non cambia: la route `bookings` con policy `Default` copre la nuova route.

### 6. Frontend

- Pagina in `components/my-bookings/`, modello in `models/booking.model.ts`, chiamata in `services/bookings.service.ts`. Nessun componente in `components/shared/`: la pagina non introduce componenti comuni.
- Rotta `/my-bookings` con `authGuard` e titolo «Le mie prenotazioni»; voce «Le mie prenotazioni» in `LINKS` con `access: 'user'`, quindi visibile a `Student` e `Staff` e nel menu mobile.
- Sezioni in un `mat-tab-group` con le schede «Future», «Passate», «Cancellate»; all'arrivo è selezionata «Future». Le schede di Angular Material gestiscono già tastiera e ARIA. Un messaggio per ogni scheda vuota; solo quello di «Future» ha il collegamento a `/rooms`.
- Organizzatore solo sulle voci con `isGroup`: «Tu» se `isOrganizer`, altrimenti il nome, oppure «Organizzatore non disponibile» se `organizerName` è `null`. `isOrganizer` ha la precedenza sul nome mancante.
- Giorno e orario sempre nell'ora del campus (`Europe/Rome`, come la #86), nel formato «giovedì 5 novembre 2026, 10:00–11:00». Giorno e orario si formattano con `Intl.DateTimeFormat('it-IT', { timeZone: 'Europe/Rome', ... })`, verificato dai test dei due scenari di formato.
  - *Scartata:* `DatePipe`. Accetta solo uno scostamento fisso e sbaglierebbe al cambio dell'ora legale; richiederebbe anche di registrare la locale `it` (`registerLocaleData`, `LOCALE_ID`), che con `Intl` non serve.
  - *Scartata:* una libreria di date (per esempio `date-fns-tz` o Luxon). Aggiunge una dipendenza per una sola formattazione che `Intl` copre già in tutti i browser supportati.
  - *Scartata:* il fuso del browser. Uno studente con il dispositivo in un altro fuso vedrebbe orari diversi da quelli dell'orario di apertura dello spazio.
- La risposta non contiene enum: la sezione di appartenenza basta a distinguere confermate e cancellate.

### 7. Ordine dei task

1. **#90 Rooms:** contratto ed eventi. Non dipende da altro.
2. **#23 Bookings:** parte dopo il merge di #90 (usa il nuovo contratto); riusa `POST /api/bookings` e `CurrentUser` della PR #86, già su `main`.
3. **#24 Frontend:** dopo #23, che fissa la forma della risposta.

## Risks / Trade-offs

- [Spazi già presenti in Bookings senza edificio: Rooms pubblica gli eventi seed una sola volta e un database Bookings esistente non li riceve di nuovo] → `BuildingName` resta `null` finché lo spazio non riceve un `RoomUpdated`; il frontend mostra «—». Su un database nuovo il problema non si presenta, perché gli eventi seed portano già l'edificio.
- [Prenotazioni create tra il merge di #86 e quello di #23 senza nome dell'organizzatore] → `OrganizerName` resta `null` e il frontend mostra «Organizzatore non disponibile». Il seed di Bookings non contiene prenotazioni, quindi il caso riguarda solo database di sviluppo.
- [Rinomina di un edificio senza modificare i suoi spazi: Bookings mostra il nome vecchio finché ogni spazio non riceve un `RoomUpdated`] → limite noto, da scrivere in `docs/architettura.md`. Oggi Rooms non ha un'API per modificare gli edifici, quindi il caso non si verifica; non si introduce un evento `BuildingUpdated`.
- [Nome dell'organizzatore non aggiornato se cambia in Identity] → limite noto, coerente con la scelta dello snapshot; da scrivere in `docs/architettura.md`.
- [Elenco senza paginazione: passate e cancellate crescono nel tempo] → accettato per la demo; da riconsiderare se i dati crescono.
- [Bookings non verifica la firma del JWT: chi raggiunge direttamente la porta di Bookings in locale può scegliere `sub` e `name`] → limite già noto in `docs/gateway.md`; in Docker Compose solo il Gateway è esposto.
- [Prenotazioni di gruppo e cancellate non riproducibili dall'applicazione: la PR #86 crea solo prenotazioni singole, i membri arrivano con US-05 e la cancellazione con US-08] → gruppi e cancellate si verificano con dati inseriti dai test di integrazione (#23) e con risposte simulate nei test del frontend (#24); la verifica manuale con Docker Compose copre solo le prenotazioni singole e lo dichiara nella PR.
- [Consistenza eventuale: dopo una modifica dello spazio l'edificio in Bookings si aggiorna dopo qualche istante] → limite già dichiarato (I-4).

## Migration Plan

- Rooms: nessuna migrazione del database; cambia solo il contenuto degli eventi.
- Bookings: la migrazione si applica all'avvio, come oggi (`MigrateAsync` in `Program.cs`). Rollback: la migrazione inversa elimina le due colonne.
- Ordine di rilascio in locale: indifferente con `docker compose up --build`, perché i servizi ripartono insieme; in ogni caso un Bookings aggiornato che riceve eventi senza `BuildingName` lascia invariato l'edificio di quello spazio (`NULL` se non era mai stato ricevuto).
- Documentazione da aggiornare: `docs/endpoints.md` (nuova route), `docs/architettura.md` (campi degli eventi, `BookableRoom`, `Reservation`, limiti noti), `docs/handoff-architettura.md` 6.1 e 6.2 (eventi e campi), `docs/servizi.md` se cita i campi della copia locale, `docs/gateway.md` (prova manuale di `GET /api/bookings/mine`).
- Archivio: dopo il merge delle PR di #90, #23 e #24, la change si archivia con `/opsx:archive` dal branch del task «Archivio OpenSpec» (#89).
