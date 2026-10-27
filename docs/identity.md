# Identity: login, profilo e token

Servizio `src/Services/Identity/` (porta locale **5101**, database `SmartCampus_Identity`). Copre la storia **US-09** (task #29): utenti sintetici, login e profilo. La validazione del token negli altri servizi e nel Gateway è il task #30.

## API

Tutte le route passano dal Gateway (`/api/identity/**`).

| Metodo | Route | Accesso | Risposta |
|---|---|---|---|
| POST | `/api/identity/login` | anonimo | `200` con token e profilo · `400` campi mancanti · `401` credenziali errate |
| GET | `/api/identity/me` | token valido | `200` con il profilo · `401` token assente, scaduto o non valido |

**Login**

```json
POST /api/identity/login
{ "username": "student1", "password": "<password di sviluppo>" }
```

```json
200 OK
{
  "accessToken": "<jwt>",
  "tokenType": "Bearer",
  "expiresAt": "2026-12-01T10:00:00+00:00",
  "user": { "id": "a0000000-0000-0000-0000-000000000001", "username": "student1", "displayName": "Mario Rossi", "role": "Student" }
}
```

Con credenziali errate la risposta è sempre `401` con il messaggio "Nome utente o password non corretti.", sia per un utente inesistente sia per una password sbagliata: non si rivela quale dei due dati è sbagliato.

**Profilo**: `GET /api/identity/me` con l'intestazione `Authorization: Bearer <jwt>`. Il profilo è letto dal database, non dal token.

## Contratto del token (per il task #30)

JWT firmato HMAC-SHA256. I nomi dei claim sono in `JwtClaimNames` e **non vengono rimappati** (`MapInboundClaims = false`).

| Claim | Contenuto |
|---|---|
| `sub` | Id dell'utente (GUID) |
| `preferred_username` | Nome utente |
| `name` | Nome visualizzato |
| `role` | `Student` oppure `Staff` |
| `iss` / `aud` | `SmartCampus.Identity` / `SmartCampus` (configurabili in `Jwt:Issuer` e `Jwt:Audience`) |
| `iat` / `nbf` / `exp` | Durata di default 60 minuti (`Jwt:ExpirationMinutes`) |

Il Gateway e gli altri servizi devono usare **stessi issuer, audience e chiave di firma**, con `NameClaimType = "name"` e `RoleClaimType = "role"`, così `[Authorize(Roles = "Staff")]` funziona senza altre mappature.

## Configurazione

| Chiave | Dove | Note |
|---|---|---|
| `ConnectionStrings:Database` | user-secrets / variabile d'ambiente | Stringa di connessione a `SmartCampus_Identity` |
| `Jwt:SigningKey` | user-secrets / variabile d'ambiente `Jwt__SigningKey` | **Segreto**, almeno 32 byte. Se manca o è corta, il servizio non parte e dice perché |
| `Jwt:Issuer`, `Jwt:Audience`, `Jwt:ExpirationMinutes` | `appsettings.json` | Non sono segreti |
| `Seed:DefaultPassword` | `appsettings.Development.json` (solo sviluppo) o variabile `Seed__DefaultPassword` | Se vuota, gli utenti sintetici non vengono caricati |

```bash
dotnet user-secrets init --project src/Services/Identity/SmartCampus.Identity.Api
dotnet user-secrets set "ConnectionStrings:Database" "Server=localhost,1433;User Id=sa;Password=<la tua>;TrustServerCertificate=True;Database=SmartCampus_Identity" --project src/Services/Identity/SmartCampus.Identity.Api
dotnet user-secrets set "Jwt:SigningKey" "<una stringa casuale di almeno 32 caratteri>" --project src/Services/Identity/SmartCampus.Identity.Api
```

Per Docker, aggiungere `Jwt__SigningKey` (e `Seed__DefaultPassword`) al `.env` e al servizio `identity` in `docker-compose.yml`; la chiave va aggiunta anche a `.env.example` come segnaposto, mai con il valore reale.

## Utenti sintetici

Caricati all'avvio se la tabella `Users` è vuota. Hanno tutti la stessa password di sviluppo (`Seed:DefaultPassword`) e **identificativi fissi**, così i seed degli altri servizi possono farvi riferimento.

| Username | Nome | Ruolo | Id |
|---|---|---|---|
| `student1` | Mario Rossi | Student | `a0000000-0000-0000-0000-000000000001` |
| `student2` | Anna Bianchi | Student | `a0000000-0000-0000-0000-000000000002` |
| `student3` | Luca Verdi | Student | `a0000000-0000-0000-0000-000000000003` |
| `student4` | Giulia Neri | Student | `a0000000-0000-0000-0000-000000000004` |
| `student5` | Paolo Gialli | Student | `a0000000-0000-0000-0000-000000000005` |
| `staff1` | Elena Conti | Staff | `b0000000-0000-0000-0000-000000000001` |
| `staff2` | Marco Russo | Staff | `b0000000-0000-0000-0000-000000000002` |

Le password sono salvate solo come hash (PBKDF2 con sale casuale, hasher standard di ASP.NET Core).

## Migrazioni

Finché non esiste una migrazione, all'avvio il database viene creato direttamente dal modello (con un avviso nel log). Per generare la migrazione iniziale:

```bash
dotnet tool install --global dotnet-ef   # una tantum
dotnet ef migrations add InitialCreate --project src/Services/Identity/SmartCampus.Identity.Api
```

Dopo la generazione, all'avvio viene applicata con `Migrate()`. Se il database di sviluppo era stato creato senza migrazioni, va ricreato (in Docker: `docker compose down -v`).

## Limiti noti

- **Uscita (logout):** il token è senza stato e non c'è revoca lato server. "Uscire" significa che il frontend scarta il token; resta valido fino alla scadenza se qualcuno lo ha copiato. Per un progetto dimostrativo è un limite accettato; la durata breve lo contiene.
- **Nessun refresh token e nessuna registrazione autonoma** (fuori perimetro, assunzione A7).
- **Nessun limite ai tentativi di login** (rate limiting o blocco temporaneo): da valutare se il PO lo richiede.
- Se un utente cambia ruolo, il token già emesso mantiene il ruolo vecchio fino alla scadenza.
