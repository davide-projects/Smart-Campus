# Tasks

Ordine: gruppo 1 (#46) → gruppo 2 (#47, dopo il merge di #46). Il gruppo 1 parte da `main` dopo il merge della PR #119, oppure rigenera la migrazione in rebase (`design.md`, decisione 10).

## 1. Bookings: check-in (#46)

Area: `svc:bookings`.

- [x] 1.1 Aggiungere il modello del check-in:
  - `AttendanceType` (`CheckIn`, `NoShow`) in `Data/Entities/Enums/`;
  - l'entità `AttendanceRecord` (`Id`, `ReservationId`, `Reservation`, `Type`, `RecordedAt`, `RecordedBy`) e la navigazione `Reservation.AttendanceRecords`;
  - `AttendanceRecordConfiguration`: tabella `AttendanceRecords`, `Type` come stringa di al massimo 20 caratteri, FK con cascade, indice **unico** su `ReservationId`;
  - il `DbSet` in `BookingDbContext`.

  Generare la migrazione `AddAttendanceRecords` dopo `AddRoomType` della PR #119 e verificare che lo snapshot contenga entrambe le modifiche.

  > Nota: migrazione rigenerata dopo AddRoomType (`20261029082648_AddAttendanceRecords`).
- [x] 1.2 Aggiungere a `ReservationRules` le costanti della finestra (15 minuti prima, 15 dopo) e `ValidateCheckIn` (decisione 3), con i rifiuti e i messaggi esatti della spec. Unit test in `ReservationRulesTests`, con «adesso» passato come parametro:
  - bordi della finestra per organizzatore e membro: `Start − 15 min`, `Start` e `Start + 15 min` consentiti; un tick prima e un tick dopo rifiutati con «Il check-in è possibile da 15 minuti prima a 15 minuti dopo l'inizio.»; prenotazione del giorno dopo rifiutata (criterio del task);
  - `Staff` estraneo fuori finestra, prima e dopo, consentito;
  - estraneo → `403` «Check-in non consentito» / «Non puoi fare il check-in di questa prenotazione.», anche su prenotazione cancellata, no-show o già usata;
  - cancellata, no-show, già registrato → `422` con titolo «Check-in non consentito» e i rispettivi messaggi, anche per `Staff` e anche dentro la finestra;
  - ordine con più regole violate: cancellata fuori finestra → «cancellata»; già registrato fuori finestra → «già registrato».
- [x] 1.3 Esporre `POST /api/bookings/{id:guid}/check-in` in `ReservationEndpoints` (`WithName("CheckInReservation")`, `Produces<CheckInResponse>(200)`, `ProducesProblem` per `401`, `403`, `404`, `422`):
  - `401` «Accesso richiesto» / «Effettua l'accesso per fare il check-in.» con token valido senza `sub` GUID;
  - transazione esplicita dentro la execution strategy (`ChangeTracker.Clear()` a ogni tentativo) con lettura della prenotazione `WITH (UPDLOCK, ROWLOCK)`, poi appartenenza come membro e tipo dell'eventuale record nella stessa transazione (decisione 4);
  - `404` «Prenotazione non trovata» / «La prenotazione richiesta non esiste.»;
  - `IsInRole(StaffRole)` e regola 1.2;
  - inserimento con `RecordedAt = now` e `RecordedBy = sub`, commit, poi `200` con `{ checkedInAt }`;
  - con violazione dell'indice unico (`SqlException` `2601`/`2627`): rollback, `ChangeTracker.Clear()`, rilettura e `422` del record trovato.
- [x] 1.4 Cancellazione di una prenotazione usata (decisione 7):
  - aggiungere `hasCheckIn` a `ValidateCancellation`, con il nuovo rifiuto `422` «Cancellazione non consentita» / «Non è possibile cancellare una prenotazione già usata.» dopo `403` e «già cancellata» e prima del limite di tempo;
  - leggere il check-in nella `DELETE`;
  - aggiungere `!r.AttendanceRecords.Any(a => a.Type == AttendanceType.CheckIn)` alla `WHERE` dell'`ExecuteUpdateAsync`.

  Unit test in `ReservationRulesTests`: organizzatore e `Staff` su una futura con check-in → `422` «già usata»; iniziata con check-in → «già usata» (non «già iniziata»); estraneo su usata → `403`; già cancellata con check-in per l'organizzatore → nessuna modifica; i test esistenti con `hasCheckIn = false` restano verdi.
- [x] 1.5 Aggiungere `CheckedInAt` (`DateTimeOffset?`) a `MyReservationItem` e alla proiezione di `GetMineAsync` (decisione 6), senza cambiare liste e ordinamenti. In `MyReservationsTests`: `checkedInAt` `null` senza record; valorizzato con un `CheckIn` in `upcoming` e in `past`; `null` con il solo `NoShow`.
- [x] 1.6 Test di integrazione (Testcontainers) in una nuova classe `CheckInTests` della collezione `Bookings API`, con `TestClock` fissato e `Reset` nel `Dispose`. Membri e record `NoShow` si inseriscono direttamente nel database.
  - Organizzatore nella finestra → `200` con `checkedInAt` uguale al clock, un solo record `CheckIn` con `RecordedBy` = organizzatore, prenotazione ancora `Confirmed`.
  - Estremi della finestra (`Start − 15 min`, `Start + 15 min`) accettati; un secondo prima e un secondo dopo → `422` con titolo e messaggio, senza record.
  - Secondo check-in della stessa persona e di un altro membro → `422` «Il check-in di questa prenotazione è già stato registrato.», record invariato (criterio del task).
  - Estraneo → `403` con titolo e messaggio, nessun record (criterio del task).
  - Gruppo: check-in di un membro, poi `GET /api/bookings/mine` di organizzatore e altro membro con lo stesso `checkedInAt`; check-in dell'organizzatore visto dal membro.
  - `Staff` estraneo fuori finestra → `200` con `RecordedBy` = `Staff`; `Staff` dopo il check-in di uno studente → `422`.
  - Prenotazione cancellata e prenotazione con `NoShow` → `422` con i messaggi, anche per `Staff`.
  - Id sconosciuto → `404` con titolo e messaggio. Senza token, con firma errata, con token scaduto e con token senza firma (`alg: none`) → `401` senza record. Token valido senza `sub` → `401` «Accesso richiesto» / «Effettua l'accesso per fare il check-in.».
  - Concorrenza: organizzatore e membro in parallelo, e cinque richieste parallele dell'organizzatore (client distinti, partenza comune) → una sola `200`, le altre `422` «già registrato», nessun `500`, un solo record. Ripetere 5 volte su prenotazioni nuove.
  - Indice unico: inserire un `NoShow` direttamente e verificare che un secondo record per la stessa prenotazione sia rifiutato dal database.
- [x] 1.7 Test di integrazione in `CancelReservationTests` per la prenotazione usata:
  - organizzatore e `Staff` su prenotazione futura con check-in → `422` «Non è possibile cancellare una prenotazione già usata.», prenotazione `Confirmed` e ancora in `upcoming`;
  - già iniziata con check-in → stesso messaggio;
  - estraneo → `403`;
  - concorrenza check-in contro cancellazione (decisione 4): con il clock 5 minuti prima dell'inizio, l'organizzatore invia `DELETE` e un membro `POST .../check-in` in parallelo (client distinti, partenza comune), ripetuto 20 volte su prenotazioni nuove. A ogni ripetizione:
    - esattamente una delle due riesce: `204` con prenotazione `Cancelled` e nessun record, oppure `200` con prenotazione `Confirmed` e un record `CheckIn`;
    - l'altra riceve il `422` corretto: «Non è possibile cancellare una prenotazione già usata.» oppure «Non è possibile fare il check-in di una prenotazione cancellata.»;
    - nessun `500` e mai una prenotazione `Cancelled` con un record `CheckIn`.

    Se il test fallisce, applicare il ripiego della decisione 4 sulla `DELETE` e scriverlo nella PR.

  I test esistenti della cancellazione restano verdi. `dotnet test SmartCampus.slnx` verde.
- [x] 1.8 Aggiornare la documentazione (decisione 9):
  - `docs/endpoints.md`: riga del check-in, `checkedInAt` di `/mine`, nuovo `422` della `DELETE`;
  - `docs/handoff-architettura.md`: 6.2 (colonna di accesso del check-in «organizzatore, membro o Staff»), flusso F4 e I-9;
  - `docs/architettura.md` e `docs/servizi.md`: check-in consentito anche allo Staff;
  - `docs/gateway.md`: passo di prova manuale. Senza token → `401` dal Gateway. Con il token di `student1` nella finestra della propria prenotazione → `200`: serve una prenotazione che inizi entro 15 minuti, quindi va creata nei 15 minuti prima di una fascia, perché si prenota solo alla mezz'ora e non nel passato; secondo invio → `422`; con il token di `student2` estraneo → `403`; `DELETE` della prenotazione usata → `422`.

  Eseguire la prova e riportarne l'esito nella PR.

  > Nota: prova manuale dal Gateway eseguita il 2026-10-29, esito nella PR.

## 2. Frontend: pulsante di check-in (#47)

Area: `frontend`. Parte dopo il merge di #46.

- [x] 2.1 Aggiungere `checkedInAt: string | null` a `MyReservationItem`, `CheckInResponse` e la funzione pura `isInCheckInWindow(item, now)` in `models/booking.model.ts`. Aggiornare con `checkedInAt: null` i dati di prova esistenti (`my-bookings.spec.ts`, `cancel-booking-dialog.spec.ts`, `bookings.service.spec.ts`). Unit test della funzione: `start − 15 min` e `start + 15 min` dentro, un millisecondo prima e dopo fuori, indipendente dal fuso del dispositivo.
- [x] 2.2 Aggiungere `BookingsService.checkIn(id): Observable<CheckInResponse>` (`POST /api/bookings/{id}/check-in`); test con `HttpTestingController` su metodo, URL, corpo vuoto e risposta.
- [x] 2.3 In `my-bookings`:
  - mostrare «Usata» (`tabindex="-1"`) sulle voci con `checkedInAt` nelle schede «Future» e «Passate»;
  - mostrare «Check-in» con `disabledInteractive` sulle voci di «Future» senza check-in, abilitato solo nella finestra, con nome accessibile «Check-in per <spazio>, <giorno e orario>»;
  - fuori finestra, sotto il pulsante il testo «Il check-in è possibile da 15 minuti prima a 15 minuti dopo l'inizio.» collegato con `aria-describedby`;
  - nascondere «Cancella» sulle voci con `checkedInAt`;
  - aggiornare il signal `now` ogni 30 secondi con `takeUntilDestroyed`.

  Test con data corrente controllata e timer finti:
  - «Usata» in «Future» e in «Passate», assente senza check-in;
  - pulsante abilitato per organizzatore e membro nella finestra e agli estremi, con il nome accessibile atteso;
  - disabilitato ma focalizzabile, con il testo e `aria-describedby`, per una prenotazione di domani, un istante prima della finestra e in corso oltre la finestra;
  - nessuna richiesta con clic, Invio o Spazio sul pulsante disabilitato;
  - assente con check-in e nelle schede «Passate» e «Cancellate»;
  - «Cancella» assente sulla voce futura con `checkedInAt` (MODIFIED «Azione «Cancella» nelle prenotazioni future») e presente con «Check-in» sulla voce futura dell'organizzatore senza check-in;
  - dopo 30 secondi il pulsante si abilita all'apertura della finestra e si disabilita alla chiusura, senza nuove `GET /api/bookings/mine`;
  - nessun timer attivo dopo la distruzione del componente.
- [x] 2.4 Collegare il pulsante a `checkIn()`: invio senza conferma, pulsante disabilitato durante la richiesta. Dopo il `200`:
  - aggiornare la voce in locale con il `checkedInAt` della risposta;
  - aprire la `MatSnackBar` «Check-in registrato.» senza azione e con `duration` 5000;
  - spostare il focus sull'etichetta «Usata» della voce.

  Per gli errori, avviso nella card con `role="alert"`: `detail` per `403`/`404`/`422`, «Non è stato possibile registrare il check-in. Riprova più tardi.» per gli altri; avviso svuotato al tentativo successivo; con `401` nessun avviso e sessione chiusa.

  Test (criterio del task: tutti i casi della storia visibili):
  - una sola richiesta con doppio clic e nessun dialog;
  - dopo il `200` la voce mostra «Usata» senza «Check-in» né «Cancella», e `getMine()` è stato chiamato una sola volta;
  - snackbar con il testo e senza pulsanti (`MatSnackBarHarness`), assente dopo 5 secondi con timer finti;
  - `document.activeElement` è l'etichetta «Usata»;
  - i `detail` di `403`, `404` e dei `422` (fuori finestra, già registrato, cancellata) nella card con `role="alert"`;
  - messaggio generico con `500` ed errore di rete;
  - avviso rimosso al nuovo tentativo;
  - `401` che porta al login con `returnUrl` `/my-bookings` senza avviso.
- [x] 2.5 Verificare a mano e riportarlo nella PR; `npx ng test --watch=false` verde.
  - Tastiera: Tab fino a «Check-in», anche disabilitato, con la descrizione letta dallo screen reader; Invio e Spazio; snackbar e avvisi annunciati; focus su «Usata» dopo il check-in; focus visibile.
  - Larghezza 375px: voce con «Cancella», «Check-in», testo di spiegazione, avviso e «Usata» senza scorrimento orizzontale.

  > Nota: prove manuali eseguite il 2026-10-29, esito nella PR.
- [x] 2.6 Verificare con `docker compose up --build` e riportare l'esito nella PR:
  - creare dal Gateway una prenotazione di `student1` sulla prima mezz'ora che inizia fra più di 15 minuti;
  - aprire la pagina fuori finestra: pulsante disabilitato e testo;
  - lasciare la pagina aperta fino all'apertura della finestra: pulsante abilitato entro 30 secondi;
  - fare il check-in e vedere la snackbar, «Usata» e «Cancella» sparito;
  - con il token di `staff1` verificare via API il check-in fuori finestra su un'altra prenotazione.

  Nella PR dichiarare che il caso del gruppo è coperto dai test 1.6 se la US-05 (PR #126) non è ancora unita.

  > Nota: prove con `docker compose` eseguite il 2026-10-29, esito nella PR.
