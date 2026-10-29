# Tasks

Ordine: gruppo 1 (#123) → gruppo 2 (#17) → gruppo 3 (#18). Il gruppo 2 usa una Identity finta nei test e può partire in parallelo al gruppo 1, ma si unisce dopo; il gruppo 3 richiede entrambi su `main`.

## 1. Identity: endpoint interno e ricerca studenti (#123)

Area: `svc:identity`.

- [x] 1.1 Mappare in `Program.cs` un gruppo `/internal` con `RequireAuthorization()`, separato da `/api/identity`, e registrare `POST /internal/users/students/check` (nuovo file in `Endpoints/`, es. `InternalUserEndpoints.cs`). Richiesta `{ userIds }`, risposta `{ students: [{ id, displayName }], notStudents: [...] }` con una sola query sugli id distinti; `400` ValidationProblem per corpo mancante, `userIds` mancante o vuoto. Verificare con i test 1.3.
- [x] 1.2 Aggiungere `GET /api/identity/students?search=` in `IdentityEndpoints` con `RequireAuthorization()`. Chi cerca con claim `role` diverso da `Student` → `403` «Ricerca non consentita» / «Solo gli studenti possono cercare altri studenti.». Risultati solo `Role = Student`, escluso il `sub` (con `sub` non GUID → `401`), `Contains` su `DisplayName` o `Username`, ordine per `DisplayName` e poi `Username`, `Take(10)`, campi `id`, `username`, `displayName`. Testo con meno di 2 caratteri dopo `Trim()` → `400` sul campo `search` con «Scrivi almeno 2 caratteri per cercare.». Verificare con i test 1.4.
- [x] 1.3 Test di integrazione in `tests/Identity/SmartCampus.Identity.IntegrationTests` (nuova classe, es. `StudentsCheckTests`) con i token degli utenti di seed: tutti studenti; `Staff` e id inesistente in `notStudents`; id ripetuto una sola volta; `401` senza token e con token firmato con un'altra chiave; `200` con token `Student`; `400` con `userIds` vuoto e con un valore non GUID; `POST /api/identity/internal/users/students/check` → `404`. `dotnet test tests/Identity/SmartCampus.Identity.IntegrationTests` verde.
- [x] 1.4 Test di integrazione della ricerca (es. `StudentSearchTests`): per nome (`bia`) e per username senza distinzione di maiuscole (`STUDENT3`); `Staff` esclusi (`Conti` → `[]`); chi cerca escluso (`Rossi` cercato da Mario Rossi); solo i tre campi nel JSON; `400` con un carattere e senza `search`; al massimo 10 risultati nell'ordine previsto, inserendo nel database del test altri studenti sintetici fino a superare 10; `401` senza token; `403` con il token di `staff1` e il messaggio della spec. Test verdi.
- [x] 1.5 Test in `tests/Gateway/SmartCampus.Gateway.IntegrationTests` (in `AccessRulesTests` o in una nuova classe): `POST /internal/users/students/check` sul Gateway, con un token valido e senza token, riceve `404` e il servizio a valle finto non riceve richieste; `GET /api/identity/students?search=bia` senza token → `401`, con token `Student` o `Staff` → inoltrata (il `403` dello `Staff` lo dà Identity). Nessuna modifica a `appsettings.json` del Gateway. `dotnet test tests/Gateway/SmartCampus.Gateway.IntegrationTests` verde.
- [x] 1.6 Documentazione:
  - `docs/endpoints.md`: riga `GET /api/identity/students` nella tabella di Identity, più una sottosezione «Endpoint interni (non instradati dal Gateway)» con `POST /internal/users/students/check` (accesso, corpo, risposta, `400`/`401`);
  - `docs/identity.md`: tabella API, endpoint interno, limiti della ricerca (solo per gli studenti, `403` per lo `Staff`; 2 caratteri, 10 risultati, solo studenti nei risultati, escluso chi cerca);
  - `docs/gateway.md`: `/internal/**` non ha route e riceve `404`; in «Limiti noti», Identity valida il token anche sulle nuove route; passo di prova manuale;
  - `docs/handoff-architettura.md` 6.3: le due route.

  Verificare che route e messaggi citati coincidano con il codice.
- [x] 1.7 Eseguire `dotnet test SmartCampus.slnx` e riportare comando ed esito nella PR.

## 2. Bookings: prenotazione con membri del gruppo (#17)

Area: `svc:bookings`.

- [x] 2.1 Aggiungere `MemberIds` (`IReadOnlyList<Guid>?`) a `CreateReservationRequest` e `ReservationRules.ValidateMembers(memberIds, organizerId)` (più di `MaxMembers` = 10 id, poi organizzatore tra i membri, poi membri ripetuti → `422` «Membri non validi» con i tre messaggi della spec), chiamata dopo `ValidateRequest`. Aggiungere i rifiuti `StaffGroupForbidden()` (`403` «Prenotazione di gruppo non consentita» / «Il personale non può creare prenotazioni di gruppo.»), `CapacityExceeded(capacity)` («Capienza superata» / «Lo spazio ospita al massimo N persone.»), `MemberConflict()` («Membro già impegnato» / «Uno o più membri hanno già una prenotazione in questa fascia.»), `MemberLockTimeout()`, `NotStudents()` e `MembersCheckUnavailable()` (`503`). Ogni rifiuto ha il `code` della tabella «Codici di motivo» del design: aggiungere a `RejectionCodes` `group-booking-forbidden`, `too-many-members`, `organizer-as-member`, `duplicate-members`, `members-not-students`, `members-check-unavailable`, `member-booking-in-progress`, `capacity-exceeded`, `members-occupied` (togliendo il commento che li annuncia), con `limits` `maxMembers` per i troppi membri e `maxParticipants` per la capienza. Unit test in `ReservationRulesTests`: `null` e vuoto validi, 10 membri validi, 11 rifiutati (anche con duplicati tra gli 11), organizzatore incluso, duplicato, capienza uguale (valida) e superata di uno; per ogni rifiuto `Code` e `Limits` attesi (anche in `RejectionCodesTests`).
- [x] 2.2 Creare `IdentityClient` (HttpClient tipizzato) registrato con `AddHttpClient` in `Program.cs`:
  - `BaseAddress` da `Services:Identity:BaseAddress`, che all'avvio deve essere un URI assoluto `http` o `https`, altrimenti il servizio si ferma con un messaggio che nomina la chiave;
  - `Timeout` 3 s, nessun retry e nessun pacchetto nuovo;
  - inoltra l'intestazione `Authorization` ricevuta;
  - riduce timeout, errori di rete, risposte non `200` e JSON non leggibile all'esito «non disponibile», con un `LogWarning` senza id, nomi né token.

  Aggiungere `"Services": { "Identity": { "BaseAddress": "http://localhost:5101" } }` in `appsettings.json`. Verificare con i test 2.4.
- [x] 2.3 Aggiornare `CreateReservationAsync` nell'ordine della decisione 4 del design:
  - subito dopo il controllo del token, `403` se `user.IsInRole(StaffRole)` e `memberIds` non è vuoto;
  - verifica dei membri con Identity solo se ce ne sono, prima della transazione e fuori da `strategy.ExecuteAsync` (`503` / `422`);
  - lock `user:{id}` per organizzatore e membri in ordine crescente di id, con il messaggio di timeout dell'organizzatore o del membro;
  - capienza dopo `ValidateAgainstRoom`;
  - conflitto dei membri dopo quello dell'organizzatore;
  - insert di `Reservation` (`Participants` = 1 + membri) e `ReservationMember` con un solo `SaveChangesAsync` nella stessa transazione;
  - `.ProducesProblem(403)` e `.ProducesProblem(503)` sulla route.

  Verificare con i test 2.4 e 2.5.
- [x] 2.4 Test di integrazione (Testcontainers) in una nuova classe (es. `GroupReservationTests`). Nella fixture Identity è finta: `ConfigureTestServices` sostituisce l'handler primario di `IdentityClient` con uno stub che risponde a seconda del caso e registra le richieste ricevute. Casi:
  - `201` con 2 membri e `participants` = 3, membri salvati e visibili nella lista Staff;
  - stub chiamato una sola volta, con gli id e con la stessa intestazione `Authorization`;
  - nessuna chiamata senza membri o con `memberIds` = `[]`;
  - capienza superata → `422` con la capienza nel messaggio, e capienza esatta → `201`;
  - membro già impegnato come organizzatore e come membro → `409`;
  - membro adiacente o con prenotazione cancellata → `201`;
  - conflitto dell'organizzatore prima di quello dei membri;
  - membro non studente o inesistente → `422`;
  - organizzatore tra i membri e duplicato → `422`;
  - 11 membri → `422` «Puoi aggiungere al massimo 10 membri.» senza chiamate allo stub; 10 membri in uno spazio da 30 → `201` con `participants` = 11;
  - `Staff` con un membro → `403` senza chiamate allo stub; `Staff` con `memberIds` = `[]` → `201`;
  - Identity che non risponde entro 3 s, risponde `500`, risponde JSON non valido o è irraggiungibile → `503` con il messaggio della spec;
  - prenotazione singola con lo stub che fallirebbe → `201`.

  In ogni rifiuto, verificare `code` (e `limits` per troppi membri e capienza) nel `ProblemDetails`, e nel database che non esistano né `Reservation` né `ReservationMembers` nuovi. Togliere da `RejectionResponseTests` il commento che rimanda a US-05. `dotnet test tests/Bookings/SmartCampus.Bookings.IntegrationTests` verde.
- [x] 2.5 Test di permessi, visibilità e concorrenza:
  - prenotazione di gruppo creata con la `POST`, poi `GET /api/bookings/mine` di ogni membro (`upcoming`, `isGroup` = `true`, `isOrganizer` = `false`, `organizerName`) e dell'organizzatore (una sola volta);
  - `DELETE` del membro → `403` con la prenotazione invariata;
  - `PUT` e `PATCH` su `/api/bookings/{id}` → `405` con membri invariati;
  - in `ConcurrentReservationTests`: stesso membro in due gruppi paralleli su spazi diversi (un `201`, un `409`, nessun `500`) e organizzatori incrociati (un `201`, un `409`, nessuna attesa oltre 10 s), ripetuti 5 volte;
  - timeout del lock di un membro, tenendo `sp_getapplock` su `user:{membro}` da una connessione separata → `409` con il messaggio del membro.

  Eseguire la suite tre volte consecutive con esito verde.
- [x] 2.6 Configurazione e documentazione:
  - `docker-compose.yml`: `Services__Identity__BaseAddress: http://identity:8080` e `identity` in `depends_on` di `bookings`;
  - `docs/endpoints.md`: riga della `POST` con `memberIds` (al massimo 10), `participants`, il `403` per lo `Staff` con membri, i nuovi `422`, `409` e `503`;
  - `docs/servizi.md`: chiamata HTTP Bookings → Identity in «Come si parlano i servizi», lock sui membri in «Come Bookings evita le sovrapposizioni»;
  - `docs/architettura.md`: Bookings, verifica dei membri e chiave `Services:Identity:BaseAddress`;
  - `docs/handoff-architettura.md`: F1 con la verifica dei membri, tabella degli errori 6.2 con i messaggi definitivi e i `code` dei nuovi rifiuti (tolto «*(con US-05)*», `maxParticipants` per la capienza).

  Verificare con `docker compose up --build`: una prenotazione con `student2` come membro dà `201` e compare in «Le mie prenotazioni» di `student2`; con `identity` fermo (`docker compose stop identity`) la stessa richiesta dà `503` e una prenotazione singola dà `201`. Riportare l'esito nella PR.
- [x] 2.7 Eseguire `dotnet test SmartCampus.slnx` e riportare comando ed esito nella PR.

## 3. Frontend: scelta dei membri nel modulo di prenotazione (#18)

Area: `frontend`. Parte dopo il merge di #123 e #17.

- [x] 3.1 Aggiungere `StudentSummary` (`id`, `username`, `displayName`) in `models/user.model.ts` e `services/students.service.ts` con `search(text)` su `GET /api/identity/students?search=`. Aggiungere `memberIds: string[]` a `CreateReservationRequest`. Test Vitest con `HttpTestingController`: URL, parametro `search` codificato, metodo `GET`; corpo della `POST` con `memberIds`. Aggiornare i test esistenti di `bookings.service.spec.ts` e `room-booking.spec.ts` sul corpo della richiesta (`memberIds: []`).
- [x] 3.2 Nel modulo di `components/room-booking/` aggiungere il campo «Membri del gruppo» con `MatAutocomplete` e `MatChipGrid`:
  - ricerca con debounce di 300 ms da 2 caratteri dopo `trim`, con `switchMap`;
  - opzioni con nome visualizzato e username, senza i membri già scelti;
  - scelta → chip, input svuotato e pannello chiuso;
  - chip con «Rimuovi <nome>»;
  - Invio senza un'opzione evidenziata non aggiunge nulla e non invia il modulo;
  - «Nessuno studente trovato.» senza risultati e «Non è stato possibile cercare gli studenti. Riprova più tardi.» con `role="alert"` su errore diverso da `401`.

  Test con harness di Angular Material (`MatAutocompleteHarness`, `MatChipGridHarness`) e timer controllati: nessuna richiesta con 1 carattere, una richiesta con «bia», scelta e rimozione, membro già scelto non riproposto, Invio sul testo libero senza chip e senza `POST`, i due messaggi, `401` che chiude la sessione.
- [x] 3.3 Con `auth.isStaff()` non mostrare il campo «Membri del gruppo» (partecipanti fissi a 1, `memberIds` = `[]`). Con 10 membri disabilitare l'input e mostrare «Puoi aggiungere al massimo 10 membri.», che sparisce togliendo un chip. Test: Staff senza campo e con `memberIds` = `[]` nella `POST`; decimo membro che disabilita la ricerca e mostra il messaggio; rimozione che la riattiva.
- [x] 3.4 Mostrare «Partecipanti: N» (1 + membri) in un elemento `aria-live="polite"`. Con N oltre `room().capacity` mostrare «Lo spazio ospita al massimo N persone.» con `role="alert"`, e `submit()` non invia la richiesta. Test: 1 all'apertura, 3 con due membri, avviso e nessuna `POST` con capienza 4 e 4 membri, invio con 3 membri, avviso che sparisce rimuovendo un membro.
- [x] 3.5 Invio e risposta:
  - `memberIds` nella `POST`;
  - `rejectionMessage()` per `409`, `422` e `503` sceglie dal `code` il messaggio dei rifiuti dei gruppi (testi dell'handoff 6.2, `limits.maxParticipants` per la capienza), altrimenti mostra il `detail`, e senza `detail` il messaggio generico;
  - i chip restano dopo un rifiuto;
  - la conferma mostra «Partecipanti: N» solo con `participants > 1`;
  - «Prenota un'altra fascia» svuota anche i membri.

  Test: `409` membro impegnato, `422` capienza, troppi membri e non studente, `503` con e senza `detail`, conferma di gruppo e singola, reset dei membri.
- [ ] 3.6 Verificare a mano la tastiera (Tab sul campo, frecce e Invio sulle proposte, Esc, chip raggiungibili e rimossi con Canc o Backspace o con il pulsante, focus visibile, annuncio dei partecipanti) e la larghezza di 375px con quattro chip senza scorrimento orizzontale; riportarlo nella PR. `npx ng test --watch=false` verde.
- [ ] 3.7 Verificare con `docker compose up --build`:
  - con `student1` prenotare uno spazio con `student2` e `student3`: conferma con «Partecipanti: 3» e prenotazione visibile in «Le mie prenotazioni» di `student2` come «Prenotazione di gruppo», senza «Cancella»;
  - cercare «Conti» non propone `staff1`;
  - superare la capienza di uno spazio piccolo mostra l'avviso;
  - un membro già impegnato mostra il `409`.

  Riportare l'esito nella PR.
- [x] 3.8 Bookings, nome dei membri (decisione 8 del design):
  - `DisplayName` (`string?`) in `ReservationMember`, `HasMaxLength(128)` in `ReservationMemberConfiguration`, migrazione `AddMemberDisplayName` con `dotnet ef migrations add` (colonna nullable, `Down` che la toglie);
  - `IdentityClient.CheckStudentsAsync` restituisce, con «tutti studenti», il `displayName` di ogni id;
  - `CreateReservationAsync` lo salva in ogni `ReservationMember`; mancante, vuoto dopo `Trim` o oltre 128 caratteri → `null`, senza rifiuto.

  Test di integrazione con lo stub di Identity: nomi salvati; `displayName` assente, vuoto e di 129 caratteri → `201` e `DisplayName` `null`.
- [x] 3.9 Bookings, `members` in `GET /api/bookings/mine`: record `MyReservationMember(Id, DisplayName)` e `MyReservationItem.Members`, nella stessa query; ordine per `displayName` (`OrdinalIgnoreCase`), `null` in fondo, poi `id`; `[]` per le singole. Test di integrazione: elenco visto dall'organizzatore (in ordine, organizzatore escluso) e da un membro (uguale, con il suo nome); singola `[]`; riga con `DisplayName` `null` in fondo; gruppo cancellato con i membri in `cancelled`. Aggiornare i test esistenti di `/mine` che confrontano le voci.
- [x] 3.10 Frontend: `ReservationMemberSummary` e `members` in `MyReservationItem`; `membersLabel(item)` in `models/booking.model.ts` («Membri: A, B», «Nome non disponibile» per `null`, `null` per voci singole o senza membri); in `my-bookings` `<p class="members">` sotto l'organizzatore, con `overflow-wrap` per i 375px. Test Vitest: `membersLabel` (due nomi, nome `null`, singola); pagina con voce di gruppo, nome non noto e voce singola senza riga. Aggiornare le fixture esistenti con `members`.
- [ ] 3.11 Documentazione e verifica:
  - `docs/endpoints.md`: `members` nella risposta di `GET /api/bookings/mine`;
  - `docs/architettura.md` e `docs/handoff-architettura.md`: `DisplayName` di `ReservationMember`.

  Eseguire `dotnet test SmartCampus.slnx` e `npx ng test --watch=false` e `npx ng build`; con `docker compose up --build` una prenotazione di `student1` con `student2` e `student3` mostra in «Le mie prenotazioni» di `student2` la riga «Membri: …» con entrambi i nomi. Riportare l'esito nella PR.
