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

**Maiuscole e minuscole.** Lo username **non distingue** maiuscole e minuscole: `student1`, `Student1` e `STUDENT1` accedono con lo stesso utente, perché il confronto è fatto dal database (SQL Server, collation predefinita insensibile alle maiuscole). La **password** invece le distingue. Il token e il profilo riportano sempre lo username come è salvato (`student1`), non come è stato digitato. Per lo stesso motivo non possono esistere due utenti con username che differiscono solo per le maiuscole.

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

Gateway, Rooms e Bookings usano `AddSmartCampusJwtBearer` da `ServiceDefaults` per verificare issuer, audience, scadenza e firma con gli stessi valori di Identity. Il Gateway applica le policy YARP; Rooms rivalida il token e richiede `Staff` sulle route di scrittura; Bookings rivalida il token e legge chi prenota dal claim `sub`. `MapInboundClaims = false` mantiene i nomi originali e consente a `[Authorize(Roles = "Staff")]` di leggere il ruolo senza rimappature.

## Configurazione

| Chiave | Dove | Note |
|---|---|---|
| `ConnectionStrings:Database` | user-secrets / variabile d'ambiente | Stringa di connessione a `SmartCampus_Identity` |
| `Jwt:SigningKey` | user-secrets / variabile d'ambiente `Jwt__SigningKey` | **Segreto condiviso** da Identity, Gateway e Rooms, almeno 32 byte. Se manca o è corta, il servizio non parte e dice perché |
| `Jwt:Issuer`, `Jwt:Audience`, `Jwt:ExpirationMinutes` | `appsettings.json` | Non sono segreti |
| `Seed:DefaultPassword` | user-secrets / variabile d'ambiente `Seed__DefaultPassword` | Password di sviluppo degli utenti sintetici. Se vuota, gli utenti non vengono caricati |

**Sviluppo con Identity in locale.** Lo `UserSecretsId` è già nel progetto, quindi non serve `dotnet user-secrets init`:

```bash
dotnet user-secrets set "ConnectionStrings:Database" "Server=localhost,1433;User Id=sa;Password=<la tua>;TrustServerCertificate=True;Database=SmartCampus_Identity" --project src/Services/Identity/SmartCampus.Identity.Api
dotnet user-secrets set "Jwt:SigningKey" "<una stringa casuale di almeno 32 caratteri>" --project src/Services/Identity/SmartCampus.Identity.Api
dotnet user-secrets set "Jwt:SigningKey" "<la stessa chiave usata da Identity>" --project src/Services/Rooms/SmartCampus.Rooms.Api
dotnet user-secrets set "Seed:DefaultPassword" "<password di sviluppo>" --project src/Services/Identity/SmartCampus.Identity.Api
```

**Docker.** `JWT_SIGNING_KEY` e `SEED_DEFAULT_PASSWORD` si impostano nel `.env` (il modello è `.env.example`); `docker-compose.yml` passa la stessa chiave di firma a Identity e Rooms. Se mancano, `docker compose` si ferma con un messaggio che lo spiega.

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

La migrazione `InitialCreate` è inclusa e viene applicata all'avvio del servizio con `Migrate()`. Per aggiungerne una nuova, con il tool locale del repository:

```bash
dotnet tool restore
dotnet ef migrations add <Nome> --project src/Services/Identity/SmartCampus.Identity.Api
```

Un database di sviluppo creato con una versione precedente, senza migrazioni, va ricreato: altrimenti `Migrate()` si ferma con "There is already an object named 'Users'". Basta eliminare il solo database `SmartCampus_Identity` (in Docker, in alternativa, `docker compose down -v` cancella tutti i volumi).

## Limiti noti

- **Uscita (logout):** il token è senza stato e non c'è revoca lato server. "Uscire" significa che il frontend scarta il token; resta valido fino alla scadenza se qualcuno lo ha copiato. Per un progetto dimostrativo è un limite accettato; la durata breve lo contiene.
- **Nessun refresh token e nessuna registrazione autonoma** (fuori perimetro, assunzione A7).
- **Nessun limite ai tentativi di login** (rate limiting o blocco temporaneo): da valutare se il PO lo richiede.
- **Username senza distinzione tra maiuscole e minuscole:** dipende dalla collation del database e non da codice dell'applicazione. Se si cambiasse database o collation (per esempio una collation sensibile alle maiuscole), il comportamento cambierebbe: in quel caso va aggiornato questo documento e va aggiunto un test.
- Se un utente cambia ruolo, il token già emesso mantiene il ruolo vecchio fino alla scadenza.