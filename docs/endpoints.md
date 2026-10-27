# Catalogo degli endpoint

Questo documento è l'elenco condiviso degli endpoint HTTP di SmartCampus. Aggiornalo quando aggiungi o modifichi una route: registra solo endpoint implementati nella sezione del relativo servizio e usa il metodo HTTP e il percorso esatti. Le proposte non ancora implementate vanno nella sezione **Pianificati**, non nell'elenco degli endpoint attivi.

## Come leggere il catalogo

- **Percorso pubblico**: URL da chiamare passando dal Gateway, senza host e porta.
- I servizi sono elencati in ordine alfabetico; dentro ogni servizio le route sono ordinate per metodo e percorso.
- `{id}` indica un parametro nel percorso. I parametri dopo `?` sono query string.
- `—` significa che non sono previsti parametri o che il dettaglio non è ancora definito.
- Le route applicative passano dal Gateway. Gli endpoint di salute sono esposti dal singolo servizio e non fanno parte del routing `/api` del Gateway.

## Endpoint implementati

### Bookings

Base pubblica: `/api/bookings`

| Metodo | Percorso | Parametri | Descrizione |
|---|---|---|---|
| `GET` | `/api/bookings/` | — | Endpoint informativo; conferma che il servizio Bookings risponde. |

### Identity

Base pubblica: `/api/identity`

| Metodo | Percorso | Parametri | Descrizione |
|---|---|---|---|
| `GET` | `/api/identity/` | — | Endpoint informativo; conferma che il servizio Identity risponde. |

### Rooms

Base pubblica: `/api/rooms`

| Metodo | Percorso | Parametri | Descrizione |
|---|---|---|---|
| `GET` | `/api/rooms/` | Query opzionali: `type`, `minCapacity`, `buildingId` | Cerca gli spazi attivi; i filtri possono essere combinati. |
| `GET` | `/api/rooms/{id}` | `id`: GUID dello spazio | Restituisce il dettaglio di uno spazio attivo, incluse attrezzature e orari; risponde `404` se non esiste o è dismesso. |

Esempio di ricerca filtrata:

```text
GET http://localhost:5000/api/rooms/?type=Classroom&minCapacity=30&buildingId=11111111-1111-1111-1111-111111111111
```

### Health check

Questi endpoint sono esposti direttamente dai servizi per controlli di disponibilità e non sono instradati dal Gateway:

| Metodo | Percorso | Descrizione |
|---|---|---|
| `GET` | `/health/live` | Indica se il processo del servizio è attivo. |
| `GET` | `/health/ready` | Indica se il servizio è pronto; per Rooms verifica anche il database. |

## Endpoint pianificati

Le route seguenti compaiono nel documento di handoff ma non sono ancora implementate. Spostale nel catalogo degli endpoint implementati solo dopo aver aggiunto le route e i relativi test.

| Servizio | Metodo | Percorso proposto | Scopo |
|---|---|---|---|
| Bookings | `GET` | `/api/bookings/availability?roomId={roomId}&date={date}` | Cercare le fasce libere di uno spazio. |
| Bookings | `POST` | `/api/bookings` | Creare una prenotazione singola o di gruppo. |
| Bookings | `GET` | `/api/bookings/mine` | Elencare le prenotazioni dell'utente autenticato. |
| Identity | `POST` | `/api/identity/login` | Autenticare l'utente e restituire un JWT. |
| Identity | `GET` | `/api/identity/me` | Restituire profilo e ruolo dell'utente autenticato. |
| Rooms | `POST` | `/api/rooms` | Creare uno spazio. |
| Rooms | `PUT` | `/api/rooms/{id}` | Modificare uno spazio. |
| Rooms | `POST` | `/api/rooms/{id}/deactivate` | Dismettere uno spazio. |

## Aggiungere un endpoint

1. Inserisci la route nella tabella **Endpoint implementati** del servizio corretto, mantenendo l'ordine per metodo e percorso.
2. Indica il percorso pubblico completo, inclusa la base del servizio (`/api/rooms`, `/api/bookings` o `/api/identity`), i parametri e una descrizione breve del risultato.
3. Se la route non è ancora implementata, aggiungila invece a **Endpoint pianificati** e marcala come proposta.
4. Specifica qui eventuali requisiti di autenticazione/autorizzazione, codici di risposta non ovvi e link alla documentazione del contratto o ai test, quando applicabili.
5. Aggiorna il catalogo nella stessa modifica in cui aggiungi o cambi la route.

Riga da copiare e compilare:

```markdown
| `GET` | `/api/<service>/<path>` | Query: `name`; path: `{id}` | Descrizione del risultato; autenticazione/ruolo richiesto, se previsto. |
```
