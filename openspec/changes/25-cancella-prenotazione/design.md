# Design

## Context

Motivazione e ambito: vedi `proposal.md`. Requisiti: `specs/bookings`, `specs/frontend`.

Stato attuale rilevante:

- **Modello di Bookings:** `Reservation` ha già `Status` (`Confirmed`/`Cancelled`, salvato come stringa), `CancelledAt` (`DateTimeOffset?`) e `RowVersion` (`IsRowVersion()`, token di concorrenza di EF Core). `ReservationMember` lega i membri. Nessuna migrazione serve.
- **Le query esistenti filtrano già su `Status`:** la disponibilità (`AvailabilityEndpoints`, `AvailabilityCalculator`), INV-1 e INV-8 della `POST` e `upcoming`/`past` di `GET /api/bookings/mine` considerano solo le `Confirmed`; `cancelled` raccoglie le `Cancelled`. Una volta cambiato lo stato, criteri 1 e 2 sono soddisfatti senza toccare quelle query.
- **Autenticazione (PR #101, chiude #91):** `Program.cs` chiama `AddSmartCampusJwtBearer` e mappa il gruppo `/api/bookings` con `RequireAuthorization()`. Gli endpoint leggono l'utente da `ClaimsPrincipal` (`JwtClaimNames.Subject`). `MapInboundClaims = false` e `RoleClaimType = "role"`, quindi `user.IsInRole("Staff")` legge il claim `role` del token così com'è.
- **Il `401` oggi:** `UseAuthentication`/`UseAuthorization` vengono prima di `MapDefaultEndpoints()`, che registra `UseExceptionHandler` e `UseStatusCodePages`. Una richiesta senza token o con un token non valido viene quindi respinta dal middleware di autorizzazione prima dell'endpoint, senza il ProblemDetails «Accesso richiesto» dell'endpoint. I test lo confermano: `Mine_WithoutToken_Returns401` e `Mine_WithTokenSignedWithAnotherKey_Returns401` controllano solo lo stato, mentre `Mine_WithValidTokenWithoutValidSub_Returns401` (token valido senza `sub`) controlla anche titolo e messaggio. Il requisito «Endpoint delle mie prenotazioni» descriveva ancora il comportamento precedente alla #101: la delta MODIFIED lo corregge.
- **Gateway:** la route `bookings` (`/api/bookings/**`, tutti i metodi, policy `Default`) inoltra già `DELETE`. Il controllo organizzatore/Staff resta in Bookings, che ora valida il token da sé.
- **Frontend:** `components/my-bookings/` carica le tre schede da `BookingsService.getMine()` nel costruttore; `MyReservationItem` ha già `isOrganizer`. `authInterceptor` chiude la sessione su ogni `401` di `/api/`. Nel progetto non si usano ancora `MatDialog` né `MatSnackBar`.

## Goals / Non-Goals

**Goals:**

- Cancellare una prenotazione in modo sicuro (solo organizzatore o Staff, solo prima dell'inizio) e idempotente, con la fascia libera subito per tutti.
- Offrire allo studente organizzatore la cancellazione dalla pagina che già usa, con una conferma esplicita.
- Riallineare la spec di Bookings al comportamento del `401` dopo la PR #101.

**Non-Goals:**

- Pagine per lo Staff, notifiche ai membri, eventi di integrazione, rimozione di singoli membri.
- Cambiare la `POST` o `GET /api/bookings/mine` oltre alla correzione della spec sul `401`.

## Decisions

### 1. `DELETE /api/bookings/{id}` come cambio di stato (decisione dell'utente)

`MapDelete("/{id:guid}", CancelReservationAsync)` in `Features/Reservations/ReservationEndpoints.cs`, nello stesso gruppo autenticato. Il vincolo `:guid` fa sì che un `id` non GUID non trovi la route (`404` del routing, senza il messaggio dell'endpoint). La riga non si elimina: `Status = Cancelled`, `CancelledAt = TimeProvider.GetUtcNow()`, risposta `204` (flusso F2 di `docs/handoff-architettura.md`).

- *Scartata:* eliminazione fisica. Perderebbe lo storico richiesto dalla scheda «Cancellate» (US-07) e dal report delle cancellazioni (US-17).
- *Scartata:* `POST /api/bookings/{id}/cancel`. L'handoff (6.2) e `docs/endpoints.md` prevedono già `DELETE`; il verbo esprime bene «togli questa prenotazione dalle attive» e resta idempotente come richiesto.

### 2. Ordine dei controlli e messaggi (decisione dell'utente)

1. `sub` non GUID → `401` «Accesso richiesto» / «Effettua l'accesso per cancellare la prenotazione.».
2. Lettura della prenotazione (`AsNoTracking`); inesistente → `404` «Prenotazione non trovata» / «La prenotazione richiesta non esiste.».
3. Utente né organizzatore né `Staff` → `403` «Cancellazione non consentita» / «Non puoi cancellare questa prenotazione.».
4. Già `Cancelled` → `204` senza modifiche.
5. `Start <= now` → `422` «Cancellazione non consentita» / «Non è possibile cancellare una prenotazione già iniziata o passata.».
6. Aggiornamento atomico (decisione 4) → `204`.

L'autorizzazione viene prima dell'idempotenza: un estraneo riceve sempre `403`, anche su una prenotazione già cancellata o passata, e non scopre lo stato di una prenotazione altrui. L'idempotenza viene prima del limite di tempo, così una seconda richiesta dell'organizzatore dopo l'inizio riceve comunque `204`.

Le regole 3–5 si scrivono come funzione pura in `ReservationRules` (per esempio `ValidateCancellation(reservation, userId, isStaff, now)`, che restituisce un `ReservationRejection` o un esito «già cancellata»), coperta da unit test in `ReservationRulesTests` come le regole della `POST`. `403` e `422` usano `ToProblem` e quindi la stessa forma ProblemDetails delle altre violazioni; anche `404` e `401` seguono la forma già usata dagli altri endpoint.

### 3. Chi è `Staff`

`user.IsInRole("Staff")` sul `ClaimsPrincipal` già validato da Bookings (la costante `Roles.Staff` oggi esiste solo nel Gateway, `Auth/GatewayAuthorization.cs`; Bookings usa una propria costante per il valore). Non serve una policy del Gateway: lo studente deve poter chiamare la stessa route per le proprie prenotazioni, e la regola dipende dalla prenotazione (organizzatore), quindi la decide l'endpoint.

- *Scartata:* route separata per lo Staff con policy `Staff` sul Gateway. Duplicherebbe l'endpoint senza una pagina Staff che la usi (fuori ambito).

### 4. Concorrenza: un solo `UPDATE` condizionato (decisione dell'utente)

Dopo i controlli 1–5 sulla prenotazione letta, la cancellazione è un solo comando:

```
db.Reservations
  .Where(r => r.Id == id && r.Status == ReservationStatus.Confirmed && r.Start > now)
  .ExecuteUpdateAsync(s => s
      .SetProperty(r => r.Status, ReservationStatus.Cancelled)
      .SetProperty(r => r.CancelledAt, now))
```

- **1 riga aggiornata** → `204`.
- **0 righe** → un'altra richiesta ha cambiato la prenotazione tra la lettura e l'`UPDATE`: si rilegge la prenotazione e si applica di nuovo la stessa funzione del punto 2 (404, 403, 204 se ora è `Cancelled`, 422 se ora è iniziata). Nessuna eccezione di concorrenza, quindi nessun `500`.

**Perché:** la condizione `Status = Confirmed AND Start > now` dentro la stessa istruzione `UPDATE` è valutata da SQL Server in modo atomico sulla riga, quindi al massimo una richiesta la porta a `Cancelled` e `CancelledAt` viene scritto una volta sola (garanzia della `WHERE`, non verificata dai test: con il `TestClock` fisso ogni scrittura avrebbe lo stesso valore); il limite di tempo vale anche nell'istante esatto della scrittura. È un'istruzione singola, senza transazione esplicita: dentro la execution strategy di `EnableRetryOnFailure` un nuovo tentativo dopo un errore transitorio trova 0 righe e ricade nella rilettura, con esito `204`. SQL Server aggiorna `RowVersion` da sé anche con `ExecuteUpdate`, quindi eventuali scritture future basate sul token di concorrenza continuano a vedere la modifica.

- *Scartata:* lock `UPDLOCK, ROWLOCK` sulla riga della prenotazione in una transazione, come la `POST`. Serializza le richieste ma richiede transazione esplicita e una lettura in più sotto lock per ottenere lo stesso risultato di una sola istruzione condizionata; il lock serve alla `POST` perché lì il controllo riguarda altre righe (sovrapposizioni), qui riguarda solo la riga stessa.
- *Scartata:* concorrenza ottimistica con `RowVersion` (`SaveChangesAsync` su un'entità tracciata). La seconda richiesta riceve `DbUpdateConcurrencyException`, da intercettare e gestire con una rilettura comunque necessaria; il limite di tempo resterebbe verificato solo nel codice, non nella scrittura.

La cancellazione non prende il lock sulla riga dello spazio usato dalla `POST`: una `POST` concorrente sullo stesso spazio legge le `Confirmed` dopo aver preso il proprio lock e vede la prenotazione prima o dopo la cancellazione; in entrambi i casi la risposta (`409` o `201`) è corretta per quell'istante.

### 5. Prenotazione in corso di creazione (decisione dell'utente)

Nessuna gestione dedicata: la `POST` inserisce la riga nella propria transazione e la `DELETE` (lettura in `READ COMMITTED`) non la vede finché non c'è il commit, quindi risponde `404` «La prenotazione richiesta non esiste.». In pratica il client conosce l'`id` solo dalla risposta `201`, cioè dopo il commit. Non si prende il lock `sp_getapplock` sulla persona.

### 6. Frontend: dialog, esito e visibilità del pulsante (decisione dell'utente)

- **Servizio:** `BookingsService.cancel(id): Observable<void>` con `DELETE /api/bookings/{id}` in `services/bookings.service.ts`.
- **Dialog:** componente `CancelBookingDialog` in `components/my-bookings/cancel-booking-dialog/`, perché lo usa solo questa pagina (`components/shared/` è riservata ai componenti comuni). Titolo «Cancellare la prenotazione?», riepilogo con spazio, edificio («—» se `null») e l'etichetta di `reservationTimeLabel`, pulsanti «Annulla» e «Cancella prenotazione». Riceve i dati con `MAT_DIALOG_DATA`. `MatDialog` porta già trappola del focus, chiusura con Esc e ritorno del focus all'elemento che l'ha aperto. `closeOnNavigation` non basta per il `401`: reagisce solo a indietro/avanti nella cronologia, mentre `AuthService.expireSession()` porta al login con `router.navigate`; per questo sul `401` il dialog si chiude da sé con `dialogRef.close()`, senza mostrare messaggi. `@angular/material` è già una dipendenza: nessun pacchetto nuovo.
  - *Scartata:* `window.confirm`. Non mostra un riepilogo formattato, non segue il tema Material e non si testa con gli harness.
  - *Scartata:* conferma in linea nella card. Su 375px allarga la card e sposta le voci successive mentre lo studente decide.
- **Chiamata dal dialog:** poiché gli errori si mostrano nel dialog che resta aperto, è il dialog a chiamare `cancel()`. Durante la richiesta «Annulla» e «Cancella prenotazione» sono disabilitati e il dialog imposta `disableClose = true` (`MatDialogRef`), così Esc e il clic fuori non lo chiudono; all'arrivo di una risposta di errore pulsanti e chiusura tornano attivi. Una sola `DELETE` anche con doppio clic, e nessuna risposta che arrivi a dialog già chiuso (decisione dell'utente). Su `204` il dialog si chiude con `true`; su `403`/`404`/`422` mostra il `detail` della ProblemDetails in un elemento con `role="alert"`, su altri errori, escluso il `401`, «Non è stato possibile cancellare la prenotazione. Riprova più tardi.»; lo studente chiude con «Annulla». Sul `401` il dialog si chiude con `dialogRef.close()` senza messaggi e `authInterceptor` porta al login.
- **Dopo il `204` (decisioni dell'utente):**
  - **Snackbar:** `MatSnackBar.open('Prenotazione cancellata.', undefined, { duration: 5000 })`, senza azione; il testo è annunciato dal live region della snackbar.
  - **Ricaricamento:** l'elenco si ricarica con `getMine()`. Il caricamento iniziale del costruttore diventa un metodo riusabile, ma il ricaricamento non riattiva `loading()`: il `mat-tab-group` resta montato e visibile con «Future» selezionata e le voci già mostrate finché arriva la risposta; poi la voce cancellata compare in «Cancellate» nell'ordine del servizio.
  - **Ricaricamento fallito:** restano schede e voci già mostrate e sopra le schede compare «Non è stato possibile caricare le prenotazioni. Riprova più tardi.» con `role="alert"` (stesso testo dell'errore di caricamento iniziale, che invece continua a sostituire le schede). Serve uno stato separato da `loadFailed`, che oggi nasconde le schede.
  - **Focus:** dopo la chiusura con `true` il focus va sull'etichetta della scheda «Future» (per esempio con `FocusMonitor` o `focus()` sull'header della scheda), perché il pulsante «Cancella» che ha aperto il dialog sparisce con il ricaricamento. Va impostato dopo la chiusura del dialog, che altrimenti riporterebbe il focus sul pulsante.
  - *Scartata:* spostare la voce in locale da `upcoming` a `cancelled`. Eviterebbe una chiamata ma duplicherebbe nel frontend l'ordinamento del servizio.
- **Visibilità del pulsante:** solo se `isOrganizer` = `true` e `new Date(start) > adesso`, valutato quando l'elenco viene caricato o ricaricato. Se la pagina resta aperta oltre l'inizio, il pulsante resta visibile e il `422` del servizio è la difesa.

### 7. Coesistenza con la change `12-prenotare-uno-spazio`

- **Nomi dei requisiti:** questa change aggiunge in `bookings` «Cancellazione di una prenotazione», «Fascia libera subito dopo la cancellazione», «Chi può cancellare una prenotazione», «Cancellazione solo prima dell'inizio», «Cancellazione ripetuta», «Prenotazione da cancellare inesistente», «Accesso richiesto per cancellare», «Cancellazioni contemporanee», e modifica «Endpoint delle mie prenotazioni» (già in `openspec/specs/bookings`). In `frontend` aggiunge sei requisiti sulla cancellazione e modifica «Caricamento ed errore» (già in `openspec/specs/frontend`), limitandolo al primo caricamento della pagina. Nessuno coincide con i requisiti della change 12, che sono tutti ADDED su altri comportamenti: le due change si archiviano in qualunque ordine.
- **Requisito «Nessuna sovrapposizione sullo stesso spazio» (change 12):** dice già che contano solo le `Confirmed`; lo scenario «Fascia prenotabile da un altro studente» di questa change usa la `POST` così com'è su `main` (PR #86), senza dipendere dall'archivio della 12.
- **Da segnalare alla change 12:** il suo requisito «Accesso richiesto per prenotare» descrive per la `POST` senza token un `401` con titolo «Accesso richiesto» e messaggio «Effettua l'accesso per prenotare.». Dopo la PR #101 la richiesta senza token è respinta dall'autenticazione prima dell'endpoint (`Create_WithoutToken_Returns401` controlla solo lo stato); il messaggio resta solo per il token valido senza `sub` o senza `name`. Non è modificabile da qui, perché il requisito non è ancora in `openspec/specs/`: va corretto nella change 12 prima del suo archivio.
- **File del frontend:** la #15 della change 12 aggiungerà `create()` a `BookingsService` e i tipi della `POST` a `models/booking.model.ts`; questa change aggiunge `cancel()`. Chi arriva per secondo aggiunge solo il proprio metodo.

## Risks / Trade-offs

- [Orologio del browser diverso da quello del servizio, o pagina aperta oltre l'inizio: il pulsante può comparire su una prenotazione già iniziata] → il servizio risponde `422` e il dialog mostra il messaggio; il frontend non è l'arbitro della regola.
- [Se il ricaricamento dopo il `204` fallisce, la voce appena cancellata resta visibile in «Future» con il pulsante «Cancella»] → l'avviso `role="alert"` segnala che l'elenco non è aggiornato; una nuova cancellazione della stessa voce riceve `204` (idempotenza) e ricarica di nuovo l'elenco.
- [`ExecuteUpdate` salta il change tracker e il controllo su `RowVersion`] → accettato: l'unica condizione rilevante (ancora `Confirmed` e non iniziata) è nella `WHERE`; un test di integrazione con richieste parallele verifica tutte `204`, nessun `500`, `Status` = `Cancelled` e `CancelledAt` uguale all'istante del clock; la scrittura unica resta garantita dalla `WHERE`.
- [Lo Staff può cancellare qualunque prenotazione futura via API senza un'interfaccia che lo mostri] → limite accettato dall'utente; il `403` resta per tutti gli altri.
- [Criterio 2 non verificabile dal frontend finché la #11 (US-03) non porta la vista della disponibilità] → coperto dai test di integrazione di Bookings su `GET /api/bookings/availability` e `POST /api/bookings`; la verifica manuale usa le API dal Gateway.
- [Nessuna API crea ancora membri (US-05)] → gli scenari con i membri si coprono inserendo `ReservationMember` direttamente nel database nei test, come in `MyReservationsTests`.

## Migration Plan

- Nessuna migrazione del database e nessuna modifica di configurazione (user-secrets, `docker-compose.yml`, rotte del Gateway).
- Documentazione nel task #26: `docs/endpoints.md` (riga di `DELETE /api/bookings/{id}` con accesso, codici e messaggi; correzione della riga di `/mine` sul `401`), `docs/handoff-architettura.md` (glossario «Organizzatore»: unico studente che può cancellare, oltre allo Staff; 6.2 con lo stato della `DELETE` e i messaggi della tabella degli errori; I-8 risolta con A5), `docs/gateway.md` (passo di prova manuale della `DELETE`).
- Archivio: dopo il merge delle PR di #26 e #27, con `/opsx:archive` dal branch del task «Archivio OpenSpec» (#107).
