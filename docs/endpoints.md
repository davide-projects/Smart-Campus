# Catalogo degli endpoint

Questo documento è l'elenco condiviso degli endpoint HTTP di SmartCampus. Aggiornalo quando aggiungi o modifichi una route: registra solo endpoint implementati nella sezione del relativo servizio e usa il metodo HTTP e il percorso esatti. Le proposte non ancora implementate vanno nella sezione **Pianificati**, non nell'elenco degli endpoint attivi.

## Come leggere il catalogo

- **Percorso pubblico**: URL da chiamare passando dal Gateway, senza host e porta.
- I servizi sono elencati in ordine alfabetico; dentro ogni servizio le route sono ordinate per metodo e percorso.
- `{id}` indica un parametro nel percorso. I parametri dopo `?` sono query string.
- `—` significa che non sono previsti parametri o che il dettaglio non è ancora definito.
- Le route applicative passano dal Gateway, che valida il token e applica le regole di accesso (vedi [gateway.md](gateway.md)). Gli endpoint di salute sono esposti dal singolo servizio e non fanno parte del routing `/api` del Gateway.
- **Accesso**: `anonimo` = nessun token; `autenticato` = serve `Authorization: Bearer <jwt>` di un utente `Student` o `Staff` (senza token o con token non valido la risposta è `401`); `Staff` = serve un token con ruolo `Staff` (con un ruolo diverso la risposta è `403`).

## Endpoint implementati

### Bookings

Base pubblica: `/api/bookings`

Bookings verifica da sé il token (firma, issuer, audience, scadenza): tutte le route qui sotto rispondono `401` senza un token valido anche se il servizio viene chiamato direttamente, senza passare dal Gateway. Gli endpoint di salute restano anonimi.

| Metodo | Percorso | Accesso | Parametri | Descrizione |
|---|---|---|---|---|
| `DELETE` | `/api/bookings/{id}` | organizzatore o Staff | `id`: GUID della prenotazione | Cancella la prenotazione: `Status` diventa `Cancelled` e `CancelledAt` l'istante della richiesta; la riga resta e la fascia torna subito libera in disponibilità e prenotabile. Risponde `204` senza corpo. Una prenotazione già cancellata, chiesta dall'organizzatore o da `Staff`, riceve `204` senza modifiche (idempotente, `CancelledAt` invariato, anche dopo l'inizio). `401` senza token o con un token non valido (dall'autenticazione di Bookings), oppure con titolo «Accesso richiesto» e messaggio «Effettua l'accesso per cancellare la prenotazione.» con un token valido senza `sub` GUID; `403` «Cancellazione non consentita» / «Non puoi cancellare questa prenotazione.» per chi non è organizzatore né `Staff` (membri compresi), anche su una prenotazione già cancellata o passata; `404` «Prenotazione non trovata» / «La prenotazione richiesta non esiste.»; `422` «Cancellazione non consentita» / «Non è possibile cancellare una prenotazione già iniziata o passata.» se l'inizio è uguale o precedente all'istante della richiesta, anche per `Staff`. Con un `id` non GUID la route non corrisponde e risponde `404` senza corpo. |
| `GET` | `/api/bookings` | Staff | Query: `roomId` (GUID dello spazio), `date` (`AAAA-MM-GG`, giorno in ora del campus) | Elenca le prenotazioni dello spazio che toccano quel giorno, in ordine di orario, con `id`, `start`, `end`, `organizerId`, `participants`, `memberIds` (id degli altri studenti), `status` (`Confirmed` o `Cancelled`) e `cancelledAt`: le cancellate restano nell'elenco e si riconoscono dallo stato. Risponde `404` se lo spazio non esiste. Il ruolo `Staff` è controllato sia dal Gateway sia da Bookings: un utente senza quel ruolo riceve `403`, senza token `401`. |
| `GET` | `/api/bookings/availability/search` | autenticato | Query: `date` (`AAAA-MM-GG`), `start` e `end` (`HH:mm`, nella stessa giornata, ora del campus), `type` (facoltativo: `Classroom`, `Laboratory` o `StudyRoom`), `minCapacity` (facoltativa, intero ≥ 1) | Elenca gli spazi attivi e prenotabili aperti per tutta la fascia e senza prenotazioni confermate che la tocchino, in ordine di nome, con `roomId`, `name`, `buildingName`, `type` e `capacity`. Le prenotazioni adiacenti (che finiscono quando la fascia comincia o cominciano quando finisce) non bloccano lo spazio; quelle cancellate non contano. Con `type`, gli spazi di cui Bookings non conosce ancora il tipo restano fuori. Risponde `400` se `end` non è successiva a `start` o se `minCapacity` è minore di 1. |
| `GET` | `/api/bookings/rooms/{roomId}/future-reservations-count` | Staff | `roomId`: GUID dello spazio | Conta le prenotazioni confermate non ancora terminate di uno spazio (comprese quelle in corso), da mostrare al personale prima della dismissione; risponde `404` se lo spazio non esiste. Con un ruolo diverso da `Staff` risponde `403`. |
| `GET` | `/api/bookings/mine` | autenticato | — | Le prenotazioni dell'utente del token (claim `sub`) come organizzatore o come membro, in qualunque stato. Risponde `200` con `{ "upcoming": [], "past": [], "cancelled": [] }`: `upcoming` contiene le confermate non ancora terminate, comprese quelle in corso, per inizio crescente; `past` le confermate già terminate (fine uguale o precedente all'istante della richiesta) per inizio decrescente; `cancelled` tutte le cancellate, future o passate, per inizio decrescente. Ogni voce ha `id`, `roomId`, `roomName`, `buildingName`, `start`, `end` (ISO 8601 con fuso), `isGroup` (almeno un membro), `organizerName`, `isOrganizer`; `buildingName` e `organizerName` valgono `null` quando Bookings non li conosce. Nessuna paginazione: le liste sono complete. Senza prenotazioni risponde `200` con le tre liste vuote. `401` senza token o con un token non valido (firma errata, scaduto, senza firma), restituito dall'autenticazione di Bookings prima dell'endpoint; `401` con titolo «Accesso richiesto» e messaggio «Effettua l'accesso per vedere le tue prenotazioni.» con un token valido senza un `sub` GUID. |
| `POST` | `/api/bookings/` | autenticato | Body JSON: `roomId` (GUID), `start`, `end` (date-ora ISO 8601 con fuso, es. `2026-11-04T10:00:00+01:00`), `purpose` (facoltativo, max 200 caratteri) | Crea una prenotazione confermata a nome dell'utente del token (claim `sub`) e salva il suo nome visualizzato (claim `name`) come nome dell'organizzatore, così com'è al momento della creazione. Risponde `201` con `id`, `roomId`, `roomName`, `start`, `end`, `participants`, `status`; `401` senza token, con un token senza `sub` valido o senza claim `name` (o con `name` vuoto o più lungo di 128 caratteri), senza creare la prenotazione; `404` se lo spazio non esiste; `409` se lo spazio è già prenotato in quella fascia, se l'utente ha già un'altra prenotazione sovrapposta o se un'altra sua prenotazione è ancora in elaborazione; `422` se la richiesta viola una regola (fine non dopo l'inizio, inizio nel passato, orari non sulla mezz'ora, durata oltre 4 ore, anticipo oltre 14 giorni, spazio non attivo o non prenotabile, giorni diversi, fuori dall'orario di apertura). |

### Identity

Base pubblica: `/api/identity`

Il contratto del token (claim, durata, configurazione) e gli utenti sintetici di sviluppo sono descritti in [identity.md](identity.md).

| Metodo | Percorso | Accesso | Parametri | Descrizione |
|---|---|---|---|---|
| `GET` | `/api/identity/` | autenticato | — | Endpoint informativo; conferma che il servizio Identity risponde. |
| `GET` | `/api/identity/me` | autenticato | Intestazione `Authorization: Bearer <jwt>` | Restituisce il profilo dell'utente autenticato (`id`, `username`, `displayName`, `role`), letto dal database. Risponde `401` se il token è assente, scaduto o non valido. |
| `POST` | `/api/identity/login` | anonimo | Body JSON: `username`, `password` | Verifica le credenziali e restituisce il token JWT (`accessToken`, `tokenType`, `expiresAt`) insieme al profilo dell'utente. Lo username non distingue maiuscole e minuscole; la password sì. Risponde `400` se mancano i campi e `401` con un messaggio generico se le credenziali non sono corrette. |

Esempio di login:

```text
POST http://localhost:5000/api/identity/login
Content-Type: application/json

{ "username": "student1", "password": "<password di sviluppo>" }
```

### Rooms

Base pubblica: `/api/rooms`

| Metodo | Percorso | Accesso | Parametri | Descrizione |
|---|---|---|---|---|
| `GET` | `/api/rooms/` | anonimo | Query opzionali: `type`, `minCapacity`, `buildingId`, `equipmentIds` (ripetibile) | Cerca gli spazi attivi; i filtri possono essere combinati. Con più `equipmentIds` lo spazio deve avere tutte le attrezzature richieste. Ogni risultato include `equipment` con ID e nome di ciascuna attrezzatura assegnata. |
| `GET` | `/api/rooms/{id}` | anonimo | `id`: GUID dello spazio | Restituisce il dettaglio di uno spazio attivo, incluse attrezzature e orari; risponde `404` se non esiste o è dismesso. |
| `POST` | `/api/rooms/` | Staff | Body: `RoomWriteRequest` | Crea uno spazio attivo e pubblica `RoomCreated` tramite outbox; risponde `409` se il nome è già usato nell'edificio. |
| `PUT` | `/api/rooms/{id}` | Staff | `id`: GUID; body: `RoomWriteRequest` | Aggiorna i dati e gli orari, quindi pubblica `RoomUpdated` tramite outbox; risponde `404` se lo spazio non esiste o è dismesso e `409` se il nome è già usato nell'edificio. |
| `POST` | `/api/rooms/{id}/deactivate` | Staff | `id`: GUID dello spazio | Imposta `IsActive = false` senza cancellare lo spazio, lo esclude dalla ricerca e pubblica `RoomDeactivated` tramite outbox; Bookings aggiorna la copia locale e rifiuta nuove prenotazioni. |

Esempio di ricerca filtrata:

```text
GET http://localhost:5000/api/rooms/?type=Classroom&minCapacity=30&buildingId=11111111-1111-1111-1111-111111111111&equipmentIds=e1000000-0000-0000-0000-000000000001
```

### Health check

Questi endpoint sono esposti direttamente dai servizi per controlli di disponibilità e non sono instradati dal Gateway:

| Metodo | Percorso | Descrizione |
|---|---|---|
| `GET` | `/health/live` | Indica se il processo del servizio è attivo. |
| `GET` | `/health/ready` | Indica se il servizio è pronto; per Rooms verifica SQL Server e MongoDB, per Identity il database SQL. |

## Endpoint pianificati

Le API non ancora implementate non sono replicate qui: il riferimento completo per le proposte è la sezione **6, Dettaglio per servizio**, del [documento handoff](handoff-architettura.md#6-dettaglio-per-servizio-riferimento-per-scrivere-storie-e-task), in particolare le sezioni 6.1–6.3 per Rooms, Bookings e Identity. Include anche endpoint di disponibilità, cancellazione, check-in, no-show, report e gestione delle anagrafiche. Quando una route viene implementata e testata, aggiungila alla tabella del servizio in **Endpoint implementati** qui sopra e aggiornane lo stato nell'handoff, se necessario. In questo modo il catalogo resta focalizzato sulle route reali senza duplicare e far divergere l'intero backlog API.

## Aggiungere un endpoint

1. Inserisci la route nella tabella **Endpoint implementati** del servizio corretto, mantenendo l'ordine per metodo e percorso.
2. Indica il percorso pubblico completo, inclusa la base del servizio (`/api/rooms`, `/api/bookings` o `/api/identity`), i parametri e una descrizione breve del risultato.
3. Se la route è ancora una proposta, aggiornala nel [documento handoff](handoff-architettura.md#6-dettaglio-per-servizio-riferimento-per-scrivere-storie-e-task); non duplicarla qui.
4. Indica nella colonna **Accesso** chi può chiamarla, ed esplicita la stessa regola nel campo `AuthorizationPolicy` della route in `appsettings.json` del Gateway (vedi [gateway.md](gateway.md)): una route senza policy fa fallire i test. Aggiungi codici di risposta non ovvi e link alla documentazione del contratto o ai test, quando applicabili.
5. Aggiorna il catalogo nella stessa modifica in cui aggiungi o cambi la route.

Riga da copiare e compilare:

```markdown
| `GET` | `/api/<service>/<path>` | anonimo / autenticato / Staff | Query: `name`; path: `{id}` | Descrizione del risultato. |
```
