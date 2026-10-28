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

| Metodo | Percorso | Accesso | Parametri | Descrizione |
|---|---|---|---|---|
| `GET` | `/api/bookings/` | autenticato | — | Endpoint informativo; conferma che il servizio Bookings risponde. |
| `GET` | `/api/bookings/rooms/{roomId}/future-reservations-count` | Staff | `roomId`: GUID dello spazio | Conta le prenotazioni confermate non ancora terminate di uno spazio (comprese quelle in corso), da mostrare al personale prima della dismissione; risponde `404` se lo spazio non esiste. Con un ruolo diverso da `Staff` risponde `403`. |
| `POST` | `/api/bookings/` | autenticato | Body JSON: `roomId` (GUID), `start`, `end` (date-ora ISO 8601 con fuso, es. `2026-11-04T10:00:00+01:00`), `purpose` (facoltativo, max 200 caratteri) | Crea una prenotazione confermata a nome dell'utente del token (claim `sub`). Risponde `201` con `id`, `roomId`, `roomName`, `start`, `end`, `participants`, `status`; `401` senza token; `404` se lo spazio non esiste; `409` se lo spazio è già prenotato in quella fascia, se l'utente ha già un'altra prenotazione sovrapposta o se un'altra sua prenotazione è ancora in elaborazione; `422` se la richiesta viola una regola (fine non dopo l'inizio, inizio nel passato, orari non sulla mezz'ora, durata oltre 4 ore, anticipo oltre 14 giorni, spazio non attivo o non prenotabile, giorni diversi, fuori dall'orario di apertura). |

### Identity

Base pubblica: `/api/identity`

Il contratto del token (claim, durata, configurazione) e gli utenti sintetici di sviluppo sono descritti in [identity.md](identity.md).

| Metodo | Percorso | Accesso | Parametri | Descrizione |
|---|---|---|---|---|
| `GET` | `/api/identity/` | autenticato | — | Endpoint informativo; conferma che il servizio Identity risponde. |
| `GET` | `/api/identity/me` | autenticato | Intestazione `Authorization: Bearer <jwt>` | Restituisce il profilo dell'utente autenticato (`id`, `username`, `displayName`, `role`), letto dal database. Risponde `401` se il token è assente, scaduto o non valido. |
| `POST` | `/api/identity/login` | anonimo | Body JSON: `username`, `password` | Verifica le credenziali e restituisce il token JWT (`accessToken`, `tokenType`, `expiresAt`) insieme al profilo dell'utente. Risponde `400` se mancano i campi e `401` con un messaggio generico se le credenziali non sono corrette. |

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
| `GET` | `/api/rooms/` | anonimo | Query opzionali: `type`, `minCapacity`, `buildingId` | Cerca gli spazi attivi; i filtri possono essere combinati. |
| `GET` | `/api/rooms/{id}` | anonimo | `id`: GUID dello spazio | Restituisce il dettaglio di uno spazio attivo, incluse attrezzature e orari; risponde `404` se non esiste o è dismesso. |
| `POST` | `/api/rooms/` | Staff | Body: `RoomWriteRequest` | Crea uno spazio attivo e pubblica `RoomCreated` tramite outbox; risponde `409` se il nome è già usato nell'edificio. |
| `PUT` | `/api/rooms/{id}` | Staff | `id`: GUID; body: `RoomWriteRequest` | Aggiorna i dati e gli orari, quindi pubblica `RoomUpdated` tramite outbox; risponde `404` se lo spazio non esiste o è dismesso e `409` se il nome è già usato nell'edificio. |
| `POST` | `/api/rooms/{id}/deactivate` | Staff | `id`: GUID dello spazio | Dismette lo spazio e pubblica `RoomDeactivated` tramite outbox. |

Esempio di ricerca filtrata:

```text
GET http://localhost:5000/api/rooms/?type=Classroom&minCapacity=30&buildingId=11111111-1111-1111-1111-111111111111
```

### Health check

Questi endpoint sono esposti direttamente dai servizi per controlli di disponibilità e non sono instradati dal Gateway:

| Metodo | Percorso | Descrizione |
|---|---|---|
| `GET` | `/health/live` | Indica se il processo del servizio è attivo. |
| `GET` | `/health/ready` | Indica se il servizio è pronto; per Rooms e Identity verifica anche il database. |

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
