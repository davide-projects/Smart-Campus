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
| `bookings` → `/api/bookings/**` | tutti | utente autenticato (`Student` o `Staff`) | `Default` |

Note:

- `Default` e `Anonymous` sono nomi riservati da YARP; `Staff` è definita in `Auth/AuthenticationExtensions.cs` (utente autenticato con claim `role` = `Staff`).
- `rooms-manage` è già pronta per la gestione degli spazi (US-10): oggi Rooms espone solo `GET`, quindi non cambia nulla per le route esistenti.
- Le richieste **preflight CORS** (`OPTIONS` con `Origin`) sono gestite dal middleware CORS prima dell'autenticazione: non servono token e non ricevono 401.
- Su una route anonima (catalogo) un token scaduto o non valido **non** blocca la richiesta: viene ignorato.
- **Ogni nuova route deve dichiarare la propria `AuthorizationPolicy`.** Un test (`EveryRoute_DeclaresItsAccessPolicy`) fallisce se manca, così nessuna route resta aperta per dimenticanza.
- Gli endpoint `/health/live` e `/health/ready` del Gateway sono anonimi.

## Configurazione

| Chiave | Dove | Note |
|---|---|---|
| `Jwt:SigningKey` | user-secrets / variabile d'ambiente `Jwt__SigningKey` | **Segreto**, almeno 32 byte, **identica a quella di Identity**. Se manca o è corta, il Gateway non parte e dice perché |
| `Jwt:Issuer`, `Jwt:Audience` | `appsettings.json` | Devono coincidere con Identity (`SmartCampus.Identity` / `SmartCampus`) |

**Sviluppo con il Gateway in locale** (lo `UserSecretsId` è già nel progetto):

```bash
dotnet user-secrets set "Jwt:SigningKey" "<la stessa chiave di Identity>" --project src/Gateway/SmartCampus.Gateway
```

**Docker.** `docker-compose.yml` passa `JWT_SIGNING_KEY` (dal `.env`) sia a `identity` sia a `gateway`, quindi le due chiavi coincidono per costruzione.

## Prova manuale (Postman)

Con Identity, Rooms, Bookings e Gateway avviati:

1. `POST http://localhost:5000/api/identity/login` con `{ "username": "student1", "password": "<password di sviluppo>" }` → copia `accessToken`.
2. `GET http://localhost:5000/api/bookings/` **senza** token → `401`. Con `Authorization: Bearer <token>` → `200`.
3. `GET http://localhost:5000/api/rooms/` senza token → `200` (catalogo pubblico).
4. `GET http://localhost:5000/api/identity/me` con il token → `200` con il profilo.
5. Ripeti il login con `staff1`: i token `Student` e `Staff` sono ammessi su Bookings; solo `Staff` potrà usare le route di gestione degli spazi di Rooms quando esisteranno.

## Test

`tests/Gateway/SmartCampus.Gateway.IntegrationTests` avvia il Gateway reale davanti a un servizio a valle finto (nessun database, nessun Docker): verifica anonimo / `Student` / `Staff` su ogni gruppo di route, token scaduto, firma errata, issuer o audience sbagliati, inoltro dell'intestazione `Authorization`, preflight CORS, health anonimi e avvio che fallisce senza chiave valida.

```bash
dotnet test tests/Gateway/SmartCampus.Gateway.IntegrationTests
```

## Limiti noti

- **I servizi a valle non rivalidano il token** (scelta per semplicità): le regole sono applicate solo dal Gateway. In locale le porte dei servizi (5101–5103) restano raggiungibili direttamente e **senza controlli**; con Docker Compose solo il Gateway (5000) è esposto all'host. Se in futuro un servizio dovesse essere raggiungibile da altre vie, va aggiunta la validazione anche lì (stessa configurazione `Jwt`).
- Il ruolo letto dal Gateway è quello scritto nel token al momento del login: se cambia, vale il vecchio fino alla scadenza.
- Nessuna revoca dei token: vale quanto scritto in [identity.md](identity.md#limiti-noti).
- Tolleranza sull'orologio di 30 secondi nella verifica della scadenza.
