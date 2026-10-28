# Design

## Context

Motivazione e ambito: vedi `proposal.md`. Requisiti: `specs/bookings`, `specs/frontend`.

Stato attuale rilevante:

- **Bookings (PR #86, su `main`):** `POST /api/bookings` è in `Features/Reservations/ReservationEndpoints.cs`. Le regole sono funzioni pure in `ReservationRules.cs` (`ValidateRequest` sulla sola richiesta, `ValidateAgainstRoom` sullo spazio, più i rifiuti `RoomConflict`, `OrganizerConflict` e `UserLockTimeout`), coperte da `ReservationRulesTests` (unit test). `CreateReservationTests` (Testcontainers) copre `201` con la fascia occupata nella disponibilità, sovrapposizione parziale, adiacenza, stessa persona in un altro spazio, fuori orario, `401` e `404`, una richiesta alla volta. Il commento della classe rimanda la concorrenza al task #14.
- **Transazione di creazione:** dentro la execution strategy di EF Core (`EnableRetryOnFailure`): regole sulla richiesta prima della transazione → lock `UPDLOCK, ROWLOCK` sulla riga di `BookableRooms` → `404` se manca → `sp_getapplock` su `user:{id}` (`Exclusive`, `@LockOwner = 'Transaction'`, `@LockTimeout = 10000`) → regole sullo spazio → INV-1 → INV-8 (organizzatore o membro) → insert e commit.
- **Fixture di test:** `BookingsApiFixture` avvia un SQL Server 2022 in container e Bookings con `WebApplicationFactory`; le classi della collezione `Bookings API` condividono il container e girano una dopo l'altra. La CI esegue `dotnet test SmartCampus.slnx` su `ubuntu-latest`, dove Docker è disponibile.
- **Frontend:** il dettaglio dello spazio (`components/room-detail/`) mostra «Prenota spazio» disabilitato con `title` «Prenotazione non ancora disponibile (US-04)» sugli spazi prenotabili e «Non prenotabile» sugli altri. Le rotte sono in `app.routes.ts`; `authGuard` (`services/auth.guards.ts`) porta al login con `returnUrl`. `authInterceptor` aggiunge il token alle chiamate `/api/` e, su un `401`, chiama `AuthService.expireSession()`, che chiude la sessione e porta al login con `reason=expired` e `returnUrl`. Non esiste ancora un servizio Angular per Bookings.
- **US-03 (#11, aperto):** porterà nel dettaglio dello spazio la scelta del giorno e la griglia delle fasce libere e occupate da `GET /api/bookings/availability`.

## Goals / Non-Goals

**Goals:**

- Dimostrare su SQL Server reale, in modo ripetibile e in CI, che i due lock garantiscono INV-1 e INV-8 con richieste contemporanee.
- Permettere allo studente di prenotare dal dettaglio dello spazio e di vedere subito conferma o motivo del rifiuto, usando i messaggi del servizio senza duplicarne le regole.

**Non-Goals:**

- Cambiare codice, messaggi o contratto di `POST /api/bookings`: la PR #86 resta com'è.
- Fare del frontend l'arbitro delle regole: il modulo limita le scelte (giorno da oggi a oggi + 14, orari dentro l'apertura) solo per guidare lo studente, ma durata, passato e ogni altro rifiuto restano del servizio e dei suoi messaggi (criterio 6).
- Prenotazioni di gruppo, capienza, fasce libere nel `409`, validazione del JWT in Bookings (#91).

## Decisions

### 1. INV-1 e INV-8 con due lock nella stessa transazione (decisione dell'utente, già implementata nella #86)

La transazione prende prima il lock sulla riga dello spazio (`UPDLOCK, ROWLOCK` su `BookableRooms`), poi il lock applicativo sulla persona con `sp_getapplock` (`@Resource = 'user:{id}'`, `@LockMode = 'Exclusive'`, `@LockOwner = 'Transaction'`, `@LockTimeout = 10000`); solo dopo controlla le sovrapposizioni e inserisce. Il meccanismo è descritto in `docs/servizi.md` («Come Bookings evita le sovrapposizioni (INV-1 e INV-8)»).

- **Perché il lock sullo spazio:** serializza le richieste sullo stesso spazio; la seconda legge le prenotazioni solo dopo il commit della prima e vede la fascia occupata (`409`). Non serve un livello di isolamento `SERIALIZABLE` su tutta la tabella.
- **Perché il lock sulla persona:** il lock sullo spazio non basta per INV-8, perché la stessa persona può chiedere due spazi diversi nello stesso istante e le due transazioni non si incontrerebbero mai. `sp_getapplock` serializza le richieste della stessa persona senza tabelle né migrazioni; legato alla transazione, si libera da solo al commit, al rollback o se la connessione cade.
- **Timeout di 10 secondi → `409` «Prenotazione in corso»:** un'attesa senza limite terrebbe occupata una connessione se una transazione della stessa persona restasse bloccata; dopo 10 secondi la richiesta termina senza salvare nulla e lo studente può riprovare. Il lock sullo spazio non ha un timeout proprio: le transazioni sono brevi e vale il timeout dei comandi di EF Core.
- **Nessun deadlock tra i due lock:** ogni transazione prende al massimo un lock di spazio e un lock di persona, sempre in quest'ordine. Due richieste della stessa persona su spazi diversi tengono ciascuna il proprio spazio e attendono la persona; chi ha la persona non chiede altri spazi, quindi non si forma un ciclo. Con le prenotazioni di gruppo (US-05) una transazione prenderà più lock di persona: dovranno essere presi in ordine fisso (per esempio per id crescente), da scrivere nella change di US-05.
- *Scartata:* vincolo nel database. SQL Server non ha un vincolo nativo di non sovrapposizione tra intervalli; simularlo con una tabella di fasce da 30 minuti e un indice univoco su (spazio, fascia) e (persona, fascia) richiederebbe una tabella in più, da tenere allineata a ogni creazione e cancellazione, e una migrazione.
- *Scartata:* solo il lock sullo spazio. Garantisce INV-1 ma lascia aperto INV-8 con due richieste contemporanee della stessa persona su spazi diversi: entrambe vedrebbero la persona libera e verrebbero confermate.

### 2. Test di concorrenza (#14)

Nuova classe `ConcurrentReservationTests` nella collezione `Bookings API` (stesso container, nessuna nuova fixture).

- **Stesso spazio:** 20 utenti diversi (`sub` diversi, così INV-8 non interferisce) inviano la stessa richiesta libera; ogni richiesta usa un proprio `HttpClient` e parte da un segnale comune (per esempio una `TaskCompletionSource` attesa da tutte), poi `Task.WhenAll`. Verifica: esattamente un `201`, 19 `409` con titolo «Spazio già prenotato», e nel database una sola prenotazione `Confirmed` per quello spazio e quella fascia.
- **Stessa persona:** lo stesso utente invia in parallelo due richieste per la stessa fascia su due spazi diversi, entrambi liberi. Verifica: un `201`, un `409`, una sola prenotazione `Confirmed` di quell'utente nella fascia. Il `409` atteso è «Hai già una prenotazione»; il test accetta anche «Prenotazione in corso» solo se l'attesa supera i 10 secondi, che con due richieste non deve succedere (un risultato diverso è un fallimento da analizzare, non da ignorare).
- **Ripetibilità:** ogni scenario si ripete più volte nella stessa esecuzione su spazi nuovi (5 ripetizioni), e la PR riporta tre esecuzioni consecutive di `dotnet test` verdi. Spazi e utenti nuovi per ogni ripetizione evitano dipendenze dall'ordine dei test.
- **Timeout del lock sulla persona:** un test tiene `sp_getapplock` su `user:{id}` da una connessione separata, in una transazione aperta per più di 10 secondi, e verifica che la `POST` di quell'utente riceva `409` «Prenotazione in corso» senza creare nulla. Copre lo scenario della spec che oggi non ha un test di integrazione.
- **Membro di gruppo (INV-8):** un test inserisce direttamente nel database una prenotazione confermata con un `ReservationMember` e verifica che il membro riceva `409` «Hai già una prenotazione» chiedendo un altro spazio nella stessa fascia. La #86 copre solo l'organizzatore e oggi nessuna API crea membri (US-05).
- *Scartata:* provider in memoria o mock del database. Non hanno i lock di SQL Server e non dimostrerebbero nulla (vincolo in `docs/handoff-architettura.md`, sezione 2).

### 3. Pagina di prenotazione (#15)

- **Rotta:** `/rooms/:id/book` in `app.routes.ts`, con `canActivate: [authGuard]` e titolo «Prenota spazio»; componente in `components/room-booking/`. Una pagina e non un dialog: il ritorno dal login con `returnUrl` riporta esattamente al modulo, e la pagina è collegabile.
- **Dettaglio dello spazio:** il pulsante «Prenota spazio» diventa un collegamento a `['/rooms', id, 'book']` sugli spazi prenotabili; il ramo «Non prenotabile» resta invariato. Il pulsante resta visibile anche a chi non ha fatto l'accesso: è `authGuard` a portarlo al login.
- **Dati dello spazio:** la pagina carica lo spazio con `RoomsService.getRoomById` per mostrarne il nome e gli orari di apertura; `404` → «Spazio non trovato» come nel dettaglio.
- **Limiti delle scelte (decisione dell'utente):** il `MatDatepicker` ha `min` = oggi e `max` = oggi + 14 giorni, calcolati come date di `Europe/Rome`, e un `matDatepickerFilter` che esclude i giorni senza orario di apertura. Le opzioni di inizio e fine sono le mezz'ore dentro l'orario di apertura del giorno scelto (inizio fino a mezz'ora prima della chiusura, fine da mezz'ora dopo l'apertura); al cambio di giorno gli orari non più validi si azzerano. Scelto l'inizio, la fine propone solo le mezz'ore successive, e si azzera se un nuovo inizio la supera (decisione dell'utente); il limite delle 4 ore resta del servizio (`422`). Il campo «Giorno» è di sola lettura e si compila dal calendario (decisione dell'utente): `NativeDateAdapter` legge il testo con `Date.parse` in formato USA, e «05/11/2026» diventerebbe l'11 maggio. *Scartato:* un adapter che legge le date in formato italiano, più codice per un caso che il calendario evita. Gli orari possono differire da quelli usati da Bookings finché la copia locale non riceve l'evento di Rooms (consistenza eventuale, I-4): in quel caso vale il `422` del servizio. Il modulo non verifica la prenotabilità: se lo spazio non è prenotabile il servizio risponde `422` «Questo spazio non è prenotabile.».
- **Chiamata:** `BookingsService.create()` in `services/bookings.service.ts` e i tipi della richiesta e della risposta in `models/booking.model.ts`. La change `22-mie-prenotazioni` (task #24) prevede gli stessi file: chi arriva per secondo aggiunge il proprio metodo e i propri tipi senza duplicare il servizio.
- **Moduli Angular Material:** form reattivo; giorno con `MatDatepicker` e `provideNativeDateAdapter()` con locale `it-IT` sulla sola pagina; ora di inizio e di fine con `MatSelect` sulle mezz'ore; motivo con `matInput` e `maxlength="200"`. Sono componenti già inclusi in `@angular/material`: nessuna nuova dipendenza.
  - *Scartata:* `input type="date"` e `type="time"` nativi. Il selettore dell'ora nativo accetta anche minuti diversi da :00 e :30 e varia molto tra browser mobili.
  - *Scartata:* una libreria di date (Luxon, date-fns-tz). Una dipendenza per due conversioni che `Intl` copre già.
- **Ora del campus:** la richiesta si costruisce dalla data e dall'ora scelte come ora di `Europe/Rome`, calcolando lo scostamento di quel giorno con `Intl.DateTimeFormat('en-US', { timeZone: 'Europe/Rome', timeZoneName: 'longOffset' })` (es. `GMT+01:00`), così ora legale e fuso del dispositivo non spostano la fascia. La conferma usa il formato della change 22 («giovedì 5 novembre 2026, 10:00–11:00», `Intl.DateTimeFormat('it-IT', { timeZone: 'Europe/Rome', ... })`) con una funzione di formattazione fuori dal componente, riusabile dalla pagina «Le mie prenotazioni».
- **Esiti:** `201` → la conferma sostituisce il modulo, con il nome dello spazio da `roomName` della risposta. `409`/`422` → `detail` della ProblemDetails in un elemento con `role="alert"`, modulo invariato. Il collegamento alla disponibilità compare solo se il `detail` è «Lo spazio è già prenotato in questa fascia.»: il titolo «Spazio già prenotato» sarebbe equivalente, ma il servizio non espone un codice di errore e il frontend non deve indovinarlo da altro. Il collegamento porta a `/rooms/:id`, dove la #11 mostra la disponibilità, senza il giorno nell'URL (decisione dell'utente): se la #11 introdurrà un parametro per aprire la disponibilità su un giorno, il collegamento si rivede con la #11. Dopo la conferma, «Torna allo spazio» apre `/rooms/:id` e «Prenota un'altra fascia» azzera il modulo e lo ripropone. Altri errori → messaggio generico. Il pulsante di conferma resta disabilitato durante l'invio, così un doppio clic non crea due richieste (il servizio rifiuterebbe comunque la seconda con `409`).
- **`401`:** la pagina imposta il `detail`, ma `authInterceptor` chiude subito la sessione e porta al login con `returnUrl`: lo studente vede l'avviso di sessione scaduta, come in tutte le altre pagine. Non si cambia l'intercettore.

### 4. Ordine dei task e dipendenza da US-03

1. **#13 Bookings:** fatto (PR #86).
2. **#14 Bookings:** solo test e documentazione di stato; non dipende da altro.
3. **#15 Frontend:** parte dopo il merge di #11 (US-03), perché la verifica del criterio 1 («la fascia risulta occupata per tutti») e il collegamento dopo il conflitto usano la vista della disponibilità nel dettaglio dello spazio. Anche il pulsante «Vedi disponibilità» del dettaglio appartiene a #11, non a questa storia.

### 5. Coesistenza con la change `22-mie-prenotazioni`

Entrambe le change introducono come ADDED le capability `bookings` e `frontend`. I requisiti hanno nomi diversi e le due `## Purpose` sono identiche, quindi l'archivio della prima crea le spec e quello della seconda aggiunge i propri requisiti, in qualunque ordine. Il task #23 della change 22 modificherà la `POST /api/bookings` per salvare il claim `name` (requisito «Nome dell'organizzatore salvato alla creazione», con un `401` in più se il claim manca): è un comportamento aggiuntivo che non contraddice i requisiti di questa change; risposta `201` e messaggi restano invariati.

## Risks / Trade-offs

- [Test di concorrenza che passano per caso, se le richieste non si sovrappongono davvero nel tempo] → partenza comune delle richieste, 20 client indipendenti e ripetizioni nella stessa esecuzione; la verifica sul numero di righe nel database non dipende dai tempi.
- [Test lenti o instabili in CI per il timeout di 10 secondi] → un solo test aspetta il timeout; gli altri non lo raggiungono. Se in CI risultasse instabile, si segnala nella PR prima di allentare le verifiche.
- [Il collegamento alla disponibilità dipende dal testo del `detail`] → **limite noto accettato**: il servizio non espone un codice di errore, quindi se il messaggio cambiasse in Bookings il collegamento sparirebbe senza errori. Un test del frontend fissa il testo atteso, e un cambio del messaggio passa da una change che aggiorna anche questa spec. La PR del #15 lo dichiara tra i limiti noti.
- [Bookings non verifica la firma del JWT: chi raggiunge la porta di Bookings in locale può prenotare a nome di un altro] → limite noto (`docs/gateway.md`), tracciato in #91; in Docker Compose solo il Gateway è esposto.
- [Consistenza eventuale della copia degli spazi: subito dopo una modifica in Rooms la prenotazione può essere validata sugli orari precedenti] → limite già dichiarato (I-4).
- [#15 bloccato finché #11 non è unito] → #14 procede in parallelo; se #11 slitta, la storia non si chiude.

## Migration Plan

- Nessuna migrazione del database e nessuna modifica di configurazione (user-secrets, `docker-compose.yml`, Gateway).
- Documentazione: nella PR di pianificazione (#96) l'utente allinea `docs/handoff-architettura.md`, `docs/architettura.md` e `docs/servizi.md` su INV-8. Il #14 porta a ✅ la riga «Test di concorrenza prenotazioni» in `docs/handoff-architettura.md` (sezione 2) e aggiorna il commento di `CreateReservationTests` che rimanda al #14. `docs/endpoints.md` non cambia.
- Archivio: dopo il merge delle PR di #14 e #15, la change si archivia con `/opsx:archive` dal branch del task «Archivio OpenSpec» (#97).
