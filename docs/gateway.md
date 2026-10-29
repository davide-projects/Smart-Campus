# Gateway: validazione del token e regole di accesso

Servizio `src/Gateway/SmartCampus.Gateway/` (porta locale **5000**). Copre il task **#30** della storia **US-09**: il Gateway è l'unico punto che controlla il JWT emesso da Identity e decide chi può chiamare cosa. Il contratto del token è in [identity.md](identity.md).

## Come funziona

1. Il frontend fa il login (`POST /api/identity/login`) e riceve il token.
2. Per le chiamate successive invia `Authorization: Bearer <jwt>` al Gateway.
3. Il Gateway verifica firma, issuer, audience e scadenza. Poi applica la regola della route (tabella sotto) e, se la richiesta è ammessa, la inoltra al servizio **con la stessa intestazione `Authorization`**.

Risposte del Gateway: **401** se il token manca, è scaduto o non è valido (con `WWW-Authenticate: Bearer`); **403** se il token è valido ma il ruolo non basta.

## Regole di accesso

Le regole sono nel campo `AuthorizationPolicy` di ogni route in `appsettings.json` (sezione `ReverseProxy:Routes`).

| Route | Metodi | Accesso | Policy |
|---|---|---|---|
| `identity-login` → `/api/identity/login` | `POST` | anonimo | `Anonymous` |
| `identity` → `/api/identity/**` (tutto il resto) | tutti | utente autenticato | `Default` |
| `rooms-read` → `/api/rooms/**` | `GET` | anonimo (catalogo pubblico) | `Anonymous` |
| `rooms-manage` → `/api/rooms/**` | `POST`, `PUT`, `PATCH`, `DELETE` | solo `Staff` | `Staff` |
| `bookings-future-count` → `/api/bookings/rooms/{roomId}/future-reservations-count` | `GET` | solo `Staff` | `Staff` |
| `bookings-list` → `/api/bookings` | `GET` | solo `Staff` | `Staff` |
| `bookings` → `/api/bookings/**` (tutto il resto) | tutti | utente autenticato (`Student` o `Staff`) | `Default` |

Note:

- `Default` e `Anonymous` sono nomi riservati da YARP; `Staff` è definita in `Auth/AuthenticationExtensions.cs` (utente autenticato con claim `role` = `Staff`).
- `rooms-manage` è già pronta per la gestione degli spazi (US-10): oggi Rooms espone solo `GET`, quindi non cambia nulla per le route esistenti.
- `bookings-future-count` ha `Order: -1`, quindi viene valutata prima della route generica `bookings` (che ha l'ordine predefinito, `0`): il conteggio delle prenotazioni future, pensato per l'avviso prima di dismettere uno spazio (US-11), è riservato al personale, mentre il resto di Bookings resta aperto a ogni utente autenticato. Le altre route di Bookings riservate a `Staff` si aggiungono allo stesso modo.
- `bookings-list` ha anch'essa `Order: -1` e riserva a `Staff` la `GET /api/bookings` (con o senza barra finale), cioè l'elenco delle prenotazioni di uno spazio in un giorno (US-12). Le altre richieste su `/api/bookings` (per esempio la creazione con `POST`) restano alla route generica `bookings` e sono ammesse a `Student` e `Staff`. Bookings applica `Staff` anche da solo (`RequireRole("Staff")`, come Rooms): chi lo chiama direttamente con un token `Student` riceve comunque `403`.
- Le richieste **preflight CORS** (`OPTIONS` con `Origin`) sono gestite dal middleware CORS prima dell'autenticazione: non servono token e non ricevono 401.
- Su una route anonima (catalogo) un token scaduto o non valido **non** blocca la richiesta: viene ignorato.
- **Ogni nuova route deve dichiarare la propria `AuthorizationPolicy`.** Un test (`EveryRoute_DeclaresItsAccessPolicy`) fallisce se manca, così nessuna route resta aperta per dimenticanza.
- Gli endpoint `/health/live` e `/health/ready` del Gateway sono anonimi.
- `GET /api/identity/students` (ricerca degli studenti, US-05) è coperta dalla route `identity`: il Gateway inoltra i token `Student` e `Staff`, e il `403` per lo `Staff` lo restituisce Identity.
- **`/internal/**` non ha route.** Gli endpoint interni dei servizi (per esempio `POST /internal/users/students/check` di Identity, usato da Bookings) stanno fuori da `/api`: il Gateway risponde `404`, con o senza token, e la richiesta non arriva al servizio. Non c'è una regola che li blocca: semplicemente nessuna route li raggiunge, quindi non va aggiunta una route con un percorso che li includa.

## Configurazione

| Chiave | Dove | Note |
|---|---|---|
| `Jwt:SigningKey` | user-secrets / variabile d'ambiente `Jwt__SigningKey` | **Segreto**, almeno 32 byte, **identica a quella di Identity** (e a quella di Rooms e Bookings). Se manca o è corta, il Gateway non parte e dice perché |
| `Jwt:Issuer`, `Jwt:Audience` | `appsettings.json` | Devono coincidere con Identity (`SmartCampus.Identity` / `SmartCampus`) |

**Sviluppo con il Gateway in locale** (lo `UserSecretsId` è già nel progetto):

```bash
dotnet user-secrets set "Jwt:SigningKey" "<la stessa chiave di Identity>" --project src/Gateway/SmartCampus.Gateway
```

**Docker.** `docker-compose.yml` passa `JWT_SIGNING_KEY` (dal `.env`) a `identity`, `gateway`, `rooms` e `bookings`, quindi le chiavi coincidono per costruzione.

## Prova manuale (Postman)

Con Identity, Rooms, Bookings e Gateway avviati:

1. `POST http://localhost:5000/api/identity/login` con `{ "username": "student1", "password": "<password di sviluppo>" }` → copia `accessToken`.
2. `GET http://localhost:5000/api/bookings/availability?roomId=<id dello spazio>&date=2026-11-03` **senza** token → `401`. Con `Authorization: Bearer <token>` → `200` con le fasce (o `404` se lo spazio non esiste).
3. `GET http://localhost:5000/api/rooms/` senza token → `200` (catalogo pubblico).
4. `GET http://localhost:5000/api/identity/me` con il token → `200` con il profilo.
5. Ripeti il login con `staff1`: i token `Student` e `Staff` sono ammessi sulle route generali di Bookings (per esempio `/api/bookings/availability`); solo `Staff` potrà usare le route di gestione degli spazi di Rooms quando esisteranno.
6. `GET http://localhost:5000/api/bookings/rooms/<id dello spazio>/future-reservations-count`: senza token → `401`, con il token di `student1` → `403`, con quello di `staff1` → `200` con il conteggio (o `404` se lo spazio non esiste).
7. `GET http://localhost:5000/api/bookings?roomId=<id dello spazio>&date=2026-11-03`: senza token → `401`, con il token di `student1` → `403`, con quello di `staff1` → `200` con l'elenco delle prenotazioni di quel giorno (o `404` se lo spazio non esiste).
8. `DELETE http://localhost:5000/api/bookings/<id della prenotazione>` su una prenotazione futura di `student1` (creata con `POST /api/bookings` e il suo token): senza token → `401` dal Gateway; con il token di un altro studente (per esempio `student2`) → `403` «Non puoi cancellare questa prenotazione.»; con il token di `student1` → `204`, e la fascia torna libera in `GET /api/bookings/availability`.
9. `POST http://localhost:5000/api/bookings/<id della prenotazione>/check-in`, senza corpo, su una prenotazione di `student1` che inizia entro 15 minuti. Si prenota solo alla mezz'ora e non nel passato, quindi va creata con `POST /api/bookings` nei 15 minuti prima di una fascia (per esempio alle 10:50 per le 11:00). Senza token → `401` dal Gateway; con il token di `student2` (estraneo) → `403` «Non puoi fare il check-in di questa prenotazione.»; con il token di `student1` → `200` con `checkedInAt`; secondo invio → `422` «Il check-in di questa prenotazione è già stato registrato.»; poi `DELETE` della stessa prenotazione con il token di `student1` → `422` «Non è possibile cancellare una prenotazione già usata.».
10. `GET http://localhost:5000/api/identity/students?search=bia`: senza token → `401`; con il token di `student1` → `200` con Anna Bianchi; con quello di `staff1` → `403` «Ricerca non consentita» da Identity.
11. `POST http://localhost:5000/internal/users/students/check` con il token di `student1` e corpo `{ "userIds": ["a0000000-0000-0000-0000-000000000002"] }` → `404`: l'endpoint interno non è raggiungibile dal Gateway.

### Avvio senza chiave

Il Gateway non deve partire senza una chiave JWT valida. Il rifiuto in fase di avvio non è coperto da un test automatico, quindi si controlla a mano:

1. Rimuovi la chiave: `dotnet user-secrets remove "Jwt:SigningKey" --project src/Gateway/SmartCampus.Gateway`.
2. Avvia il Gateway: `dotnet run --project src/Gateway/SmartCampus.Gateway`.
3. Deve fermarsi subito con un `OptionsValidationException` il cui messaggio nomina `Jwt:SigningKey`.
4. Rimetti la chiave con `dotnet user-secrets set "Jwt:SigningKey" "<la stessa di Identity>" --project src/Gateway/SmartCampus.Gateway`.

## Test

`tests/Gateway/SmartCampus.Gateway.IntegrationTests` avvia il Gateway reale davanti a un servizio a valle finto (nessun database, nessun Docker): verifica anonimo / `Student` / `Staff` su ogni gruppo di route, token scaduto, firma errata, issuer o audience sbagliati, inoltro dell'intestazione `Authorization`, preflight CORS, health anonimi, rifiuto di una chiave JWT assente o troppo corta (verificato sulle opzioni, senza avviare il server).

```bash
dotnet test tests/Gateway/SmartCampus.Gateway.IntegrationTests
```

## Limiti noti

- **Rooms e Bookings rivalidano il token** con `AddSmartCampusJwtBearer` (stessa configurazione `Jwt` del Gateway), anche quando la richiesta passa dal Gateway. Rooms applica inoltre il ruolo `Staff` alle route di scrittura; Bookings richiede un utente autenticato su tutte le route di `/api/bookings` e ricava chi prenota dal claim `sub` del token verificato. Chiamare direttamente la porta di Bookings in locale, senza token valido, dà `401`; per Rooms vale per le route `Staff` (il catalogo resta pubblico). Identity valida il token per `GET /api/identity/me`, `GET /api/identity/students` (dove richiede anche il ruolo `Student`) e per l'endpoint interno `POST /internal/users/students/check`, che non passa dal Gateway. In Docker Compose solo il Gateway è esposto all'host.
- **Il ruolo `Staff` su `future-reservations-count` è controllato solo dal Gateway:** Bookings, chiamato direttamente, lo accetta da qualsiasi utente autenticato.
- Il ruolo letto dal Gateway è quello scritto nel token al momento del login: se cambia, vale il vecchio fino alla scadenza.
- Nessuna revoca dei token: vale quanto scritto in [identity.md](identity.md#limiti-noti).
- Tolleranza sull'orologio di 30 secondi nella verifica della scadenza.
