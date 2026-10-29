# Design

## Context

Motivazione e ambito: vedi `proposal.md`. Requisiti: `specs/identity`, `specs/bookings`, `specs/frontend`.

Stato attuale rilevante:

- **Bookings, modello.** `Reservation` ha già `Participants` e la collezione `Members`. `ReservationMember` (`ReservationId`, `UserId`) è nella tabella `ReservationMembers` da `InitialCreate` (`20261027095732`): chiave composta `ReservationId` + `UserId`, FK con cascade e indice `IX_ReservationMembers_UserId`. Nessuna migrazione serve.
- **Bookings, `POST /api/bookings` (`ReservationEndpoints.CreateReservationAsync`).** L'ordine attuale è:
  1. `sub` e `name` → `401`;
  2. `ReservationRules.ValidateRequest` (regole pure);
  3. transazione dentro l'execution strategy di `EnableRetryOnFailure`;
  4. `UPDLOCK, ROWLOCK` su `BookableRooms` → `404`;
  5. `sp_getapplock` su `user:{organizerId}` (10 s → `409` «Prenotazione in corso»);
  6. `ValidateAgainstRoom` (`422`);
  7. conflitto sullo spazio (`409`);
  8. conflitto dell'organizzatore come organizzatore o membro (`409`);
  9. insert con `Participants = 1`.

  La query di INV-8 guarda già i membri (`reservation.Members.Any(...)`), e lo stesso fanno `GET /api/bookings/mine` (`isGroup`, `isOrganizer`) e la lista Staff (`memberIds`).
- **Bookings, cancellazione.** `ReservationRules.ValidateCancellation` consente la cancellazione solo se `reservation.OrganizerId == userId` oppure `isStaff`. Ogni altro utente, membri compresi, riceve `403` «Non puoi cancellare questa prenotazione.». La spec lo dice già (requisito «Chi può cancellare una prenotazione», scenario «Membro non organizzatore»): questa change non tocca la cancellazione, ne verifica solo il comportamento con un gruppo creato dalla `POST`.
- **Bookings, route.** `/api/bookings/{id:guid}` è mappata solo per `DELETE`. Un `PUT` o `PATCH` sullo stesso percorso riceve `405` dal routing di ASP.NET Core; il Gateway inoltra tutti i metodi (route `bookings`, policy `Default`).
- **Identity.** `Program.cs` mappa tutto sotto `/api/identity`. `AddSmartCampusJwt` registra già `AddSmartCampusJwtBearer`, ma oggi solo `/me` usa `RequireAuthorization()`. `User` ha `Id`, `Username` (max 64, unico), `DisplayName` (max 128) e `Role` salvato come testo. La collation predefinita di SQL Server non distingue maiuscole e minuscole.
- **Gateway.** Le route sono `identity-login` e `identity` (`/api/identity/{**catch-all}`, policy `Default`), più quelle di rooms e bookings. Non c'è un fallback: un percorso senza route riceve `404` senza essere inoltrato.
- **Frontend.** `components/room-booking/` ha un modulo reattivo (`day`, `start`, `end`, `purpose`) e conosce `room().capacity` da `RoomsService.getRoomById`. `rejectionMessage()` mostra il `detail` solo per `409` e `422`. `BookingsService.create()` invia `CreateReservationRequest` senza membri. `authInterceptor` chiude la sessione su ogni `401` di `/api/`.
- **Comunicazione tra servizi.** Finora solo eventi MassTransit (Rooms → Bookings). Nessun servizio chiama un altro servizio in HTTP.

## Goals / Non-Goals

**Goals:**

- Creare prenotazioni di gruppo che rispettano INV-5, INV-8 e INV-9 in modo atomico: tutto o niente.
- Introdurre la prima chiamata sincrona tra servizi in modo che sia contenuta: breve, senza lock aperti, con un fallimento esplicito (`503`) e senza segreti nuovi.
- Offrire allo studente la scelta dei membri senza esporre dati degli utenti oltre `id`, `username` e `displayName` dei soli studenti.

**Non-Goals:**

- Modifica dei membri dopo la creazione, check-in, notifiche, eventi.
- Resilienza avanzata (retry, circuit breaker) o una cache dei ruoli.
- Spec di login e profilo di Identity: esistono ma restano fuori dalla capability `identity` finché una storia non li cambia.

## Decisions

### 1. INV-9 con una chiamata HTTP sincrona Bookings → Identity (decisione dell'utente)

Bookings chiede a Identity, al momento della prenotazione, quali `memberIds` sono studenti registrati. Identity è l'unico proprietario di utenti e ruoli (regola d'oro di `docs/servizi.md`: si chiedono i dati via API o con un messaggio, non si legge il database di altri). Questa è **la prima chiamata HTTP tra servizi del progetto**: va descritta in `docs/servizi.md` («Come si parlano i servizi») e in `docs/architettura.md`.

- *Scartata:* evento `UserRegistered` (e simili) con una copia locale degli utenti in Bookings, come `BookableRoom` per Rooms. Sarebbe più robusta (la prenotazione non dipende da Identity acceso) ma richiede eventi nuovi in `SmartCampus.Contracts`, outbox in Identity, consumer, tabella e migrazione in Bookings, e una pubblicazione iniziale per gli utenti già presenti. Porta inoltre consistenza eventuale su un controllo di sicurezza: un utente appena passato a `Staff` resterebbe «studente» per Bookings finché l'evento non arriva. Troppo lavoro per una storia da 5 punti; resta un'evoluzione possibile.
- *Scartata:* ruolo dei membri letto dal token. Impossibile: il token è solo dell'organizzatore e non contiene nulla sui membri.
- *Scartata:* endpoint sotto `/api/identity/...` bloccato o filtrato nel Gateway (route con policy dedicata o esclusa). Mette nella configurazione del Gateway una regola di sicurezza facile da rompere: basta una route `catch-all` più larga. Un percorso fuori da `/api` non ha route per costruzione.
- *Scartata:* retry e resilienza con un pacchetto (Polly, `Microsoft.Extensions.Http.Resilience`). Una dipendenza nuova per un solo client; un retry allungherebbe l'attesa dell'utente, che può ritentare da sé (il messaggio lo invita a farlo).

### 2. Contratto dell'endpoint interno di Identity (decisione dell'utente)

`POST /internal/users/students/check`, mappato in un gruppo `/internal` separato da `/api/identity` con `RequireAuthorization()` (qualunque utente autenticato).

- **Richiesta:** `{ "userIds": [guid, ...] }`. `400` (ValidationProblem sul campo `userIds`, messaggio «Indica almeno un utente da verificare.») se il corpo manca, il JSON non è valido, `userIds` manca o è vuoto, o un valore non è un GUID (il binding dei minimal API dà già `400` per il JSON non convertibile).
- **Risposta `200`:** `{ "students": [{ "id", "displayName" }], "notStudents": [guid, ...] }`. Ogni id distinto compare una sola volta: in `students` se esiste con `Role = Student`, altrimenti in `notStudents` (`Staff` o inesistente). Identity non distingue «inesistente» da «Staff», così Bookings non può usarlo per scoprire chi è registrato. Una sola query `WHERE Id IN (...)`.
- **Autenticazione:** Bookings inoltra l'intestazione `Authorization` della richiesta dell'utente. Nessun segreto nuovo né credenziali di servizio: Identity valida il token con la stessa chiave che usa per emetterlo. Il token è già stato validato da Bookings, quindi un `401` di Identity è un caso anomalo (vedi decisione 4).
- **Non esposto dal Gateway:** nessuna route corrisponde a `/internal/**`, quindi il Gateway risponde `404`. Il Gateway non cambia configurazione. Un test di integrazione del Gateway, con il servizio a valle finto già usato dai test, verifica il `404` e che la richiesta non arrivi a valle. In Docker Compose Identity non pubblica porte verso l'host: solo i container della rete interna (Bookings) lo raggiungono.
- `displayName` in `students` non serve oggi a Bookings, ma fa parte del contratto deciso. Bookings non lo salva.

### 3. Client in Bookings: `IdentityClient` tipizzato (decisione dell'utente)

- `IdentityClient` in `Features/Reservations/` (o in una cartella `Clients/`), registrato con `builder.Services.AddHttpClient<IdentityClient>(...)`.
- `BaseAddress` letto da `Services:Identity:BaseAddress`:
  - `appsettings.json`: `http://localhost:5101`, la porta di `launchSettings` di Identity;
  - `docker-compose.yml`: `Services__Identity__BaseAddress: http://identity:8080`;
  - all'avvio un valore mancante, non assoluto o con uno schema diverso da `http` e `https` ferma il servizio con un messaggio che nomina la chiave, come per `Jwt:SigningKey` (decisione dell'utente). Lo schema va controllato perché su macOS e Linux un percorso come `/internal` è letto come URI assoluto `file://`.
- `Timeout` = 3 secondi. Nessun retry, nessun pacchetto nuovo: `IHttpClientFactory` è già nel framework.
- Metodo del tipo `CheckStudentsAsync(IReadOnlyCollection<Guid> userIds, string authorizationHeader, CancellationToken)`. Restituisce un risultato con tre esiti: tutti studenti, almeno un non studente, Identity non disponibile. Il client gestisce `HttpRequestException`, il timeout (`TaskCanceledException` non causata dal `CancellationToken` della richiesta), le risposte non `200` e il JSON non leggibile, e li riduce tutti a «non disponibile». Una risposta `200` senza `students` o senza `notStudents` conta come JSON non leggibile. Il chiamante non vede eccezioni.
- **Log:** un `LogWarning` con il solo motivo tecnico (timeout, codice di stato, tipo di eccezione). Niente id utente, nomi o token: l'intestazione `Authorization` non va mai nei log. I log HTTP predefiniti di `IHttpClientFactory` riportano URL e stato, non le intestazioni, quindi vanno bene.

### 4. Momento della chiamata e ordine dei controlli (decisione dell'utente)

Nuovo ordine di `CreateReservationAsync`:

1. `sub` e `name` → `401` (invariato).
2. Utente `Staff` (`user.IsInRole(StaffRole)`, come nella cancellazione) con almeno un id in `memberIds` → `403` «Prenotazione di gruppo non consentita» / «Il personale non può creare prenotazioni di gruppo.» (decisione dell'utente). Viene prima delle regole sulla richiesta: è un permesso, e uno `Staff` riceve sempre lo stesso esito qualunque siano i dati.
3. `ValidateRequest` (invariato) e poi le regole pure sui membri, in quest'ordine: più di 10 id → `422` «Puoi aggiungere al massimo 10 membri.» (decisione dell'utente), organizzatore tra i membri, membri ripetuti → `422` «Membri non validi». Il tetto si conta sugli id ricevuti, prima di togliere i duplicati, e limita sia la chiamata a Identity sia i lock `sp_getapplock` per richiesta. Una nuova funzione pura in `ReservationRules` (es. `ValidateMembers(memberIds, organizerId)`, con la costante `MaxMembers = 10`), con unit test.
4. Solo se ci sono membri: chiamata a Identity, **prima** di aprire la transazione e prima di qualunque lock, così una risposta lenta (fino a 3 s) non tiene bloccati lo spazio o le persone.
   - Identity non disponibile → `503` «Servizio non disponibile» / «Non è possibile verificare i membri in questo momento. Riprova tra poco.». Qui rientrano timeout, errori di rete, `5xx`, JSON non leggibile e anche le altre risposte non `200` (`401`, `400`): in tutti i casi Bookings non sa se i membri sono studenti, e il problema non dipende dall'utente.
   - Almeno un id in `notStudents` → `422` «Membri non validi» / «Puoi aggiungere come membri solo studenti registrati.». Lo stesso vale per un id che Identity non riporta né in `students` né in `notStudents` (decisione dell'utente): un membro è accettato solo se Identity lo conferma come studente. Con il contratto della decisione 2 il caso non capita.
5. Transazione: lock sullo spazio → `404`.
6. Lock `user:{id}` su organizzatore **e** membri, presi uno alla volta in ordine crescente di id (`Guid.CompareTo` su tutto l'insieme, organizzatore compreso). Timeout sul lock:
   - dell'organizzatore → `409` «Hai un'altra prenotazione in corso, riprova tra qualche secondo.» (invariato);
   - di un membro → `409` «Prenotazione in corso» / «Un membro del gruppo ha un'altra prenotazione in corso, riprova tra qualche secondo.».
7. `ValidateAgainstRoom` (`422`, invariato), poi capienza: `1 + membri > room.Capacity` → `422` «Capienza superata» / «Lo spazio ospita al massimo {Capacity} persone.». La capienza va controllata dopo il lock sulla riga dello spazio, come le altre regole sullo spazio. Il testo è quello proposto in `docs/handoff-architettura.md` (6.2), con il punto finale.
8. Conflitto sullo spazio (`409`, invariato).
9. Conflitto dell'organizzatore (`409` «Hai già una prenotazione in questa fascia oraria.», invariato), poi conflitto dei membri: una query `Confirmed`, sovrapposta, con `OrganizerId IN (membri)` oppure `Members.Any(UserId IN membri)` → `409` «Membro già impegnato» / «Uno o più membri hanno già una prenotazione in questa fascia.» (testo dell'handoff 6.2).
10. Insert di `Reservation` con `Participants = 1 + membri` e di un `ReservationMember` per ogni membro con un solo `SaveChangesAsync`, poi commit.

**Perché i lock in ordine di id.** Due richieste che coinvolgono le stesse persone (per esempio Mario con Anna come membro e, in parallelo, Anna con Mario) prendono i lock nello stesso ordine: la seconda aspetta la prima invece di bloccarsi a vicenda. Il lock sullo spazio viene sempre prima di quelli sulle persone, quindi l'ordine globale resta «spazio, poi persone per id crescente» e non introduce cicli. I lock sono legati alla transazione (`@LockOwner = 'Transaction'`) e si liberano al commit o al rollback.

**Perché Identity prima della transazione.** Con la chiamata dentro la transazione ogni prenotazione di gruppo terrebbe il lock sulla riga dello spazio fino a 3 secondi, bloccando chiunque prenoti lo stesso spazio. Il prezzo è che il ruolo è letto un istante prima del commit: se un utente cambiasse ruolo in quell'istante, la prenotazione userebbe il ruolo precedente. Il rischio è accettato: oggi i ruoli non cambiano a runtime.

**Retry di EF Core.** La chiamata a Identity è fuori da `strategy.ExecuteAsync`: un nuovo tentativo della transazione per un errore transitorio di SQL non richiama Identity.

**Codici di motivo (decisione dell'utente).** Con #20 ogni rifiuto di `POST /api/bookings` porta `code` (costanti in `RejectionCodes`, tabella in `docs/handoff-architettura.md` 6.2). I nuovi rifiuti seguono la stessa regola, un codice per caso così la UI non deve leggere il testo:

| Rifiuto | HTTP | `code` | `limits` |
|---|---|---|---|
| `Staff` con membri | 403 | `group-booking-forbidden` | — |
| Più di 10 membri | 422 | `too-many-members` | `maxMembers` (10) |
| Organizzatore tra i membri | 422 | `organizer-as-member` | — |
| Membro ripetuto | 422 | `duplicate-members` | — |
| Membri non studenti o inesistenti | 422 | `members-not-students` | — |
| Identity non disponibile | 503 | `members-check-unavailable` | — |
| Lock di un membro oltre 10 s | 409 | `member-booking-in-progress` | — |
| Capienza superata | 422 | `capacity-exceeded` | `maxParticipants` (capienza dello spazio) |
| Membro già impegnato | 409 | `members-occupied` | — |

`members-occupied` e `capacity-exceeded` sono i nomi già riservati dall'handoff; la chiave `maxParticipants` segue `maxLength`, `maxDurationMinutes` e `maxAdvanceDays`. Il timeout del lock di un membro ha un codice diverso da `booking-in-progress` perché il messaggio è diverso.

**Conseguenza dell'ordine.** Una richiesta con un membro non studente e uno spazio inesistente riceve `422` (membri) e non `404`, perché i membri si controllano prima di aprire la transazione. È voluto: i controlli che non richiedono lock vengono prima.

### 5. Contratto della `POST` (decisione dell'utente)

- `CreateReservationRequest` acquisisce `IReadOnlyList<Guid>? MemberIds`. `null` o vuoto → prenotazione singola, con lo stesso comportamento di oggi e nessuna chiamata a Identity. La richiesta senza il campo resta compatibile.
- `ReservationResponse` non cambia: `participants` riflette già il numero di partecipanti. Non si aggiunge `memberIds` alla risposta, che il frontend conosce già.
- Al massimo 10 `memberIds` (11 partecipanti): oltre, `422` prima della chiamata a Identity (decisione dell'utente). Senza un tetto una richiesta con migliaia di id arriverebbe a Identity e prenderebbe altrettanti lock prima del `422` di capienza, che richiede la riga dello spazio.
  - *Scartato:* nessun tetto oltre alla capienza, per il motivo appena detto.
  - L'endpoint interno di Identity non ha un tetto proprio: è raggiungibile solo dalla rete interna e Bookings gli invia al massimo 10 id.
- Solo gli studenti creano prenotazioni di gruppo: uno `Staff` con membri riceve `403` (decisione dell'utente). La prenotazione singola di uno `Staff` resta com'è oggi.

### 6. Ricerca degli studenti in Identity (decisione dell'utente, con valori fissati qui)

`GET /api/identity/students?search=<testo>` nel gruppo `/api/identity`, con `RequireAuthorization()`. Nel Gateway è già coperta dalla route `identity` (policy `Default`).

- **Solo studenti (decisione dell'utente).** Il controllo del ruolo è nell'endpoint (claim `role` = `Student`); uno `Staff` riceve `403` «Ricerca non consentita» / «Solo gli studenti possono cercare altri studenti.». Lo `Staff` non può creare prenotazioni di gruppo (decisione 5), quindi non ha motivo di cercare studenti. Il Gateway resta invariato: la route `identity` lascia passare lo `Staff` e il `403` arriva da Identity.
  - *Scartata:* route dedicata nel Gateway con policy `Student`. Toccherebbe la configurazione del Gateway per una sola route, e Identity deve comunque proteggersi da chi lo chiama direttamente.

- **Testo minimo: 2 caratteri** dopo `Trim()`; altrimenti `400` ValidationProblem sul campo `search`, messaggio «Scrivi almeno 2 caratteri per cercare.».
- **Massimo 10 risultati.** Abbastanza per scegliere in un autocomplete senza permettere di scaricare l'elenco degli studenti in poche richieste.
- **Corrispondenza:** `DisplayName.Contains(text) || Username.Contains(text)`. EF Core lo traduce in `LIKE` ed esegue l'escape di `%`, `_` e `[`. Maiuscole e minuscole non contano per la collation del database, come per il login.
- **Filtri:** solo `Role = Student`; esclude l'utente del claim `sub`. Con un `sub` non GUID risponde `401`, come `/me`.
- **Ordine:** `DisplayName`, poi `Username`; `Take(10)`.
- **Campi:** `id`, `username`, `displayName`. Lo username serve a distinguere due studenti con lo stesso nome nell'autocomplete; il ruolo non serve, perché sono tutti `Student`.
- *Scartata:* ricerca anche degli utenti `Staff` con il ruolo nella risposta, lasciando al frontend il filtro. Esporrebbe il personale senza motivo e sposterebbe INV-9 nel browser.
- *Scartata:* risposta paginata. Inutile per un autocomplete.

### 7. Frontend: membri nel modulo esistente (decisione dell'utente)

- **Servizio e modello.**
  - Nuovo `services/students.service.ts` con `search(text): Observable<StudentSummary[]>` su `GET /api/identity/students` (`HttpParams`).
  - `StudentSummary` (`id`, `username`, `displayName`) in `models/user.model.ts`.
  - `CreateReservationRequest` acquisisce `memberIds: string[]`, inviato sempre: `[]` senza membri.
- **Campo «Membri del gruppo»** con `MatAutocomplete` su un input e `MatChipGrid` per i chip (`mat-chip-row` con `matChipRemove`). Tutto è già in `@angular/material`: nessun pacchetto nuovo.
  - Ricerca con `debounceTime(300)`, `distinctUntilChanged` e `switchMap`, solo da 2 caratteri dopo `trim`. Le proposte escludono gli id già scelti.
  - Ogni opzione mostra `displayName` e, sotto, `username`; il chip mostra `displayName`.
  - Scegliendo un'opzione si aggiunge il membro, l'input si svuota e le proposte si chiudono.
  - Invio nell'input senza un'opzione evidenziata non aggiunge nulla e non invia il modulo (`preventDefault` sul `keydown.enter` dell'input quando il pannello non ha un'opzione attiva). Non si usa `matChipInputTokenEnd` per creare chip dal testo libero.
- **Staff e tetto dei membri.**
  - Con `auth.isStaff()` il campo «Membri del gruppo» non compare e «Partecipanti: 1» resta fisso: il servizio rifiuterebbe comunque con `403`.
  - Con 10 membri l'input del campo si disabilita e sotto compare «Puoi aggiungere al massimo 10 membri.» (stesso testo del servizio); togliendo un chip torna attivo.
- **Stato dei membri.** Un signal `members` (`StudentSummary[]`) fuori dal `FormGroup`, perché i chip non sono un valore di input digitato. `participants = computed(() => 1 + members().length)`. `overCapacity = computed(() => participants() > room().capacity)`.
- **Partecipanti e avviso.**
  - «Partecipanti: N» in un elemento con `aria-live="polite"`.
  - Con `overCapacity()` compare «Lo spazio ospita al massimo N persone.» con `role="alert"` (stesso testo del servizio), e `submit()` non invia la richiesta.
  - La capienza viene da Rooms e può differire per un attimo dalla copia di Bookings (consistenza eventuale, vedi `docs/servizi.md`): il `422` del servizio resta la difesa e viene mostrato.
- **Errori della ricerca.**
  - Errore diverso da `401` → «Non è stato possibile cercare gli studenti. Riprova più tardi.» sotto il campo, con `role="alert"`; la sequenza di ricerca non si interrompe (`catchError` dentro lo `switchMap`).
  - Nessun risultato → opzione disabilitata «Nessuno studente trovato.».
  - `401` → lo gestisce `authInterceptor`.
- **Messaggi della `POST` (decisione dell'utente).** Per `409`, `422` e `503` `rejectionMessage()` sceglie il messaggio dal `code` per i rifiuti introdotti da questa change (tabella «Codici di motivo» della decisione 4, tranne `group-booking-forbidden`), con i testi di `docs/handoff-architettura.md` 6.2; per `capacity-exceeded` il numero viene da `limits.maxParticipants`. Per un codice non previsto, per i rifiuti di #20 o se mancano i `limits` necessari mostra il `detail`; senza `detail` (per esempio il `503` del Gateway quando Bookings è spento) resta il messaggio generico. `group-booking-forbidden` (`403`) riceve il messaggio generico: lo `Staff` non vede il campo dei membri.
- *Scartata:* scegliere dal `code` anche i rifiuti di #20 (compreso `room-occupied` al posto di `ROOM_CONFLICT_DETAIL`). È fuori dall'ambito di questa storia.
- **Conferma.** «Partecipanti: N» solo se `participants > 1`. «Prenota un'altra fascia» svuota anche `members`.
- *Scartata:* membri come `FormArray` nel `FormGroup`. Complica il reset e la validazione senza vantaggi: i membri non si scrivono, si scelgono.
- *Scartata:* invio consentito con la capienza superata, lasciando al servizio il `422`. Una richiesta che sicuramente fallisce non aggiunge informazioni; l'avviso spiega già il motivo.

## Risks / Trade-offs

- [Identity spento o lento blocca le prenotazioni di gruppo (fino a 3 s di attesa, poi `503`)] → limite accettato con la decisione 1. Le prenotazioni singole non chiamano Identity e restano disponibili. L'evoluzione naturale è la copia locale tramite eventi.
- [Ruolo letto fuori dalla transazione] → un cambio di ruolo nell'istante tra verifica e commit non viene visto. Oggi i ruoli non cambiano a runtime; rischio accettato.
- [Token dell'utente inoltrato a un altro servizio] → resta nella rete interna, verso il servizio che l'ha emesso, e non viene mai registrato nei log. Nessun segreto nuovo.
- [Molti lock per richiesta: 1 + numero di membri `sp_getapplock`] → al massimo 11 per il tetto di 10 membri; i lock in ordine di id evitano i deadlock. Un test con «organizzatori incrociati» lo verifica.
- [La ricerca permette di scoprire nomi e username degli studenti] → solo per studenti autenticati, al massimo 10 risultati per richiesta, da 2 caratteri. I dati sono sintetici. Nessun limite di frequenza (come per il login, vedi `docs/identity.md`, «Limiti noti»).
- [Messaggi generici: lo studente non sa quale membro è impegnato o non è studente] → scelta voluta per non rivelare dati di altri. Lo studente toglie i membri uno alla volta, oppure lo chiede al gruppo.
- [Frontend e Bookings possono vedere capienze diverse per qualche istante] → il `422` del servizio resta mostrato.
- [Confronto sul testo del `detail` nel frontend (`ROOM_CONFLICT_DETAIL`)] → invariato; i nuovi messaggi si scelgono dal `code` e non aggiungono confronti sul testo.
- [Testi dei rifiuti di gruppo duplicati nel frontend e in `ReservationRules`] → coincidono con la tabella 6.2 dell'handoff; un cambio di testo va fatto nei due punti. Il `detail` resta il ripiego per i codici non previsti.

## Migration Plan

- **Database:** nessuna migrazione, né in Bookings né in Identity.
- **Configurazione:**
  - `Services:Identity:BaseAddress` in `src/Services/Bookings/SmartCampus.Bookings.Api/appsettings.json` (`http://localhost:5101`);
  - in `docker-compose.yml` `Services__Identity__BaseAddress: http://identity:8080` e `identity` tra le dipendenze di `bookings`;
  - nessun segreto nuovo né user-secrets nuovi;
  - Gateway invariato.
- **Ordine di rilascio:** #123 (Identity) prima del #17. Se Bookings arrivasse prima, le prenotazioni di gruppo darebbero `503` e quelle singole funzionerebbero. Il #18 dopo #123 e #17.
- **Rollback:** togliere `memberIds` dalla `POST` riporta il comportamento attuale. L'endpoint interno può restare senza effetti.
- **Documentazione:**
  - #123: `docs/endpoints.md` (`GET /api/identity/students` nella tabella di Identity e una sezione per gli endpoint interni, non instradati dal Gateway), `docs/identity.md` (API, endpoint interno, limiti della ricerca), `docs/gateway.md` (`/internal/**` non instradato e passo di prova manuale; «Limiti noti»: Identity valida il token anche per le nuove route), `docs/handoff-architettura.md` 6.3 (nuove route);
  - #17: `docs/endpoints.md` (riga della `POST` con `memberIds`, `422`, `409` e `503`), `docs/servizi.md` (nuova riga in «Come si parlano i servizi» per la chiamata HTTP Bookings → Identity; lock sui membri in «Come Bookings evita le sovrapposizioni»), `docs/architettura.md` (Bookings: chiamata a Identity e configurazione), `docs/handoff-architettura.md` (flusso F1 con la verifica dei membri, tabella degli errori 6.2 con i nuovi messaggi).
- **Archivio:** dopo il merge di #123, #17 e #18, con `/opsx:archive` dal branch del task «Archivio OpenSpec» (#122). L'archivio crea `openspec/specs/identity/spec.md` con il `Purpose` della delta.

## Open Questions

Nessuna. Il tetto di 10 membri e il divieto per lo `Staff` sono stati decisi dall'utente (decisioni 4 e 5).
