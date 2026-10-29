# Spec Delta: bookings

## ADDED Requirements

### Requirement: Check-in di una prenotazione
Bookings SHALL esporre `POST /api/bookings/{id}/check-in` (`id` = GUID della prenotazione, nessun corpo). Se il check-in è consentito, Bookings MUST registrare un `AttendanceRecord` `CheckIn` con `RecordedAt` = istante della richiesta e `RecordedBy` = claim `sub`, e rispondere `200` con il corpo `{ "checkedInAt": <RecordedAt, ISO 8601 con fuso> }`. Da quel momento la prenotazione risulta usata. `Status` MUST restare invariato.

#### Scenario: L'organizzatore fa il check-in all'inizio
- **WHEN** Studente1 ha una prenotazione confermata dell'«Aula A1» oggi dalle 10:00 alle 11:00 e alle 10:05 invia `POST /api/bookings/{id}/check-in`
- **THEN** la risposta è `200` con `checkedInAt` uguale all'istante delle 10:05, esiste un solo `AttendanceRecord` `CheckIn` per la prenotazione con `RecordedBy` = Studente1, e la prenotazione resta `Confirmed`

### Requirement: Finestra del check-in
Per l'organizzatore e i membri il check-in MUST essere accettato solo se `Start − 15 min ≤ adesso ≤ Start + 15 min` (assunzione A6), estremi inclusi. Fuori dalla finestra Bookings MUST rispondere `422` con titolo «Check-in non consentito» e messaggio «Il check-in è possibile da 15 minuti prima a 15 minuti dopo l'inizio.», senza registrare nulla. Un utente con ruolo `Staff` MUST NOT essere soggetto alla finestra.

#### Scenario: Esattamente 15 minuti prima dell'inizio
- **WHEN** l'organizzatore di una prenotazione che inizia alle 10:00 invia il check-in alle 09:45:00
- **THEN** la risposta è `200` e il check-in è registrato

#### Scenario: Esattamente 15 minuti dopo l'inizio
- **WHEN** un membro di una prenotazione che inizia alle 10:00 invia il check-in alle 10:15:00
- **THEN** la risposta è `200` e il check-in è registrato

#### Scenario: Un istante prima della finestra
- **WHEN** l'organizzatore di una prenotazione che inizia alle 10:00 invia il check-in un istante prima delle 09:45:00
- **THEN** la risposta è `422` con titolo «Check-in non consentito» e messaggio «Il check-in è possibile da 15 minuti prima a 15 minuti dopo l'inizio.» e nessun `AttendanceRecord` è registrato

#### Scenario: Un istante dopo la finestra
- **WHEN** l'organizzatore di una prenotazione che inizia alle 10:00 invia il check-in un istante dopo le 10:15:00
- **THEN** la risposta è `422` con messaggio «Il check-in è possibile da 15 minuti prima a 15 minuti dopo l'inizio.» e nessun `AttendanceRecord` è registrato

#### Scenario: Prenotazione del giorno dopo
- **WHEN** Studente1 invia il check-in per una sua prenotazione confermata di domani
- **THEN** la risposta è `422` con messaggio «Il check-in è possibile da 15 minuti prima a 15 minuti dopo l'inizio.» e la prenotazione non risulta usata

#### Scenario: Personale fuori dalla finestra
- **WHEN** un utente `Staff`, che non è organizzatore né membro, invia il check-in alle 10:40 per una prenotazione confermata iniziata alle 10:00
- **THEN** la risposta è `200` e il check-in è registrato con `RecordedBy` = l'utente `Staff`

### Requirement: Chi può fare il check-in
Solo l'organizzatore (`OrganizerId` uguale al claim `sub`), un membro (`ReservationMember` con `UserId` uguale al claim `sub`) e un utente con ruolo `Staff` (claim `role`) MUST poter fare il check-in. Ogni altro utente autenticato MUST ricevere `403` con titolo «Check-in non consentito» e messaggio «Non puoi fare il check-in di questa prenotazione.», qualunque sia lo stato della prenotazione, e nessun `AttendanceRecord` MUST essere registrato.

#### Scenario: Membro di una prenotazione di gruppo
- **WHEN** Studente2 è membro della prenotazione di gruppo organizzata da Studente1, dalle 10:00 alle 11:00, e alle 09:50 invia il check-in
- **THEN** la risposta è `200` e il check-in è registrato con `RecordedBy` = Studente2

#### Scenario: Studente estraneo
- **WHEN** Studente3, che non è né organizzatore né membro, invia il check-in alle 10:05 per la prenotazione di Studente1 iniziata alle 10:00
- **THEN** la risposta è `403` con titolo «Check-in non consentito» e messaggio «Non puoi fare il check-in di questa prenotazione.» e la prenotazione non risulta usata

#### Scenario: Estraneo su una prenotazione cancellata
- **WHEN** Studente3 invia il check-in per una prenotazione cancellata di Studente1
- **THEN** la risposta è `403`, perché il controllo di chi può fare il check-in viene prima delle regole sullo stato

### Requirement: Ordine delle regole del check-in
Quando più regole sono violate, Bookings MUST rispondere con la prima nell'ordine: chi può fare il check-in (`403`), prenotazione cancellata, prenotazione segnata no-show, check-in già registrato, finestra (`422`).

#### Scenario: Prenotazione cancellata fuori finestra
- **WHEN** l'organizzatore invia il check-in per una sua prenotazione cancellata che inizia domani
- **THEN** la risposta è `422` con messaggio «Non è possibile fare il check-in di una prenotazione cancellata.»

#### Scenario: Check-in già registrato fuori finestra
- **WHEN** l'organizzatore ha fatto il check-in alle 10:00 e alle 10:30 lo invia di nuovo
- **THEN** la risposta è `422` con messaggio «Il check-in di questa prenotazione è già stato registrato.»

### Requirement: Check-in valido per tutto il gruppo
Un solo check-in, fatto dall'organizzatore, da un membro o dallo `Staff`, MUST segnare come usata la prenotazione per tutti: in `GET /api/bookings/mine` dell'organizzatore e di ogni membro la voce MUST avere lo stesso `checkedInAt`.

#### Scenario: Check-in di un membro visto dal gruppo
- **WHEN** nella prenotazione di gruppo di Studente1 con i membri Studente2 e Studente4, Studente2 fa il check-in alle 09:55 e poi Studente1 e Studente4 chiamano `GET /api/bookings/mine`
- **THEN** per entrambi la voce ha `checkedInAt` uguale all'istante delle 09:55

#### Scenario: Check-in dell'organizzatore visto dai membri
- **WHEN** Studente1 organizza una prenotazione con il membro Studente2, fa il check-in alle 10:00 e Studente2 chiama `GET /api/bookings/mine`
- **THEN** la voce di Studente2 ha `checkedInAt` uguale all'istante delle 10:00

### Requirement: Un solo check-in per prenotazione
Una prenotazione MUST avere al massimo un `AttendanceRecord`, di tipo `CheckIn` o `NoShow`. Se il check-in è già registrato, una nuova richiesta di chiunque possa fare il check-in MUST ricevere `422` con titolo «Check-in non consentito» e messaggio «Il check-in di questa prenotazione è già stato registrato.», e il record esistente MUST restare invariato (`RecordedAt`, `RecordedBy`).

#### Scenario: Secondo check-in della stessa persona
- **WHEN** Studente1 ha fatto il check-in alle 10:00 e alle 10:02 invia di nuovo `POST /api/bookings/{id}/check-in`
- **THEN** la risposta è `422` con messaggio «Il check-in di questa prenotazione è già stato registrato.» e il check-in resta quello delle 10:00 di Studente1

#### Scenario: Secondo check-in di un altro membro del gruppo
- **WHEN** Studente2 ha fatto il check-in sulla prenotazione di gruppo e Studente1, organizzatore, invia il check-in sulla stessa prenotazione
- **THEN** la risposta è `422` con messaggio «Il check-in di questa prenotazione è già stato registrato.» e `RecordedBy` resta Studente2

#### Scenario: Personale dopo il check-in di uno studente
- **WHEN** Studente1 ha fatto il check-in e un utente `Staff` invia il check-in sulla stessa prenotazione
- **THEN** la risposta è `422` con messaggio «Il check-in di questa prenotazione è già stato registrato.»

### Requirement: Nessun check-in su una prenotazione cancellata
Bookings MUST rifiutare con `422`, titolo «Check-in non consentito» e messaggio «Non è possibile fare il check-in di una prenotazione cancellata.», il check-in di una prenotazione `Cancelled`, anche per lo `Staff` e anche dentro la finestra, senza registrare nulla.

#### Scenario: Organizzatore su una prenotazione cancellata
- **WHEN** Studente1 ha cancellato la prenotazione delle 10:00 e alle 09:50 invia il check-in
- **THEN** la risposta è `422` con messaggio «Non è possibile fare il check-in di una prenotazione cancellata.» e nessun `AttendanceRecord` è registrato

#### Scenario: Personale su una prenotazione cancellata
- **WHEN** un utente `Staff` invia il check-in per una prenotazione cancellata
- **THEN** la risposta è `422` con messaggio «Non è possibile fare il check-in di una prenotazione cancellata.»

### Requirement: Nessun check-in su una prenotazione segnata no-show
Se la prenotazione ha un `AttendanceRecord` di tipo `NoShow`, Bookings MUST rifiutare il check-in con `422`, titolo «Check-in non consentito» e messaggio «La prenotazione è già stata segnata come non usata.», anche per lo `Staff`, senza registrare nulla.

#### Scenario: Prenotazione già segnata come non presentata
- **WHEN** la prenotazione di Studente1 dalle 10:00 alle 11:00 ha un record `NoShow` e Studente1 alle 10:10 invia il check-in
- **THEN** la risposta è `422` con messaggio «La prenotazione è già stata segnata come non usata.» e la prenotazione non ha alcun record `CheckIn`

#### Scenario: Personale su una prenotazione no-show
- **WHEN** un utente `Staff` invia il check-in per una prenotazione con un record `NoShow`
- **THEN** la risposta è `422` con messaggio «La prenotazione è già stata segnata come non usata.»

### Requirement: Prenotazione per il check-in inesistente
Se `id` non corrisponde a nessuna prenotazione salvata, `POST /api/bookings/{id}/check-in` MUST rispondere `404` con titolo «Prenotazione non trovata» e messaggio «La prenotazione richiesta non esiste.», senza registrare nulla.

#### Scenario: Id sconosciuto
- **WHEN** uno studente autenticato invia `POST /api/bookings/{id}/check-in` con un GUID che non corrisponde a nessuna prenotazione
- **THEN** la risposta è `404` con titolo «Prenotazione non trovata» e messaggio «La prenotazione richiesta non esiste.» e nessun `AttendanceRecord` è registrato

### Requirement: Accesso richiesto per il check-in
`POST /api/bookings/{id}/check-in` MUST essere riservato agli utenti con un token valido. Senza token o con un token non valido, l'autenticazione di Bookings MUST rispondere `401` prima dell'endpoint. Con un token valido senza un claim `sub` GUID, l'endpoint MUST rispondere `401` con titolo «Accesso richiesto» e messaggio «Effettua l'accesso per fare il check-in.». In entrambi i casi nessun `AttendanceRecord` MUST essere registrato.

#### Scenario: Richiesta senza token
- **WHEN** `POST /api/bookings/{id}/check-in` arriva a Bookings senza intestazione `Authorization`
- **THEN** la risposta è `401` e la prenotazione non risulta usata

#### Scenario: Token valido senza identificativo utente
- **WHEN** la richiesta arriva con un token firmato correttamente ma senza claim `sub`
- **THEN** la risposta è `401` con titolo «Accesso richiesto» e messaggio «Effettua l'accesso per fare il check-in.» e la prenotazione non risulta usata

#### Scenario: Token non valido
- **WHEN** la richiesta arriva a Bookings con un token firmato con un'altra chiave, scaduto o senza firma
- **THEN** la risposta è `401`, restituita dall'autenticazione di Bookings prima dell'endpoint, che non viene eseguito, e nessun `AttendanceRecord` è registrato

#### Scenario: Chiamata attraverso il Gateway senza token
- **WHEN** un client chiama `POST /api/bookings/{id}/check-in` sul Gateway senza token
- **THEN** il Gateway risponde `401` e la richiesta non arriva a Bookings

### Requirement: Check-in contemporanei
Con check-in contemporanei sulla stessa prenotazione Bookings MUST registrare al massimo un `AttendanceRecord`; le altre richieste MUST ricevere il `422` del record presente dopo il tentativo («già registrato» per `CheckIn`, «non usata» per `NoShow`), mai `500`. La garanzia SHALL essere verificata su SQL Server reale.

#### Scenario: Organizzatore e membro nello stesso istante
- **WHEN** alle 10:00 l'organizzatore e un membro inviano in parallelo il check-in sulla stessa prenotazione iniziata alle 10:00
- **THEN** una risposta è `200`, l'altra è `422` con messaggio «Il check-in di questa prenotazione è già stato registrato.», nessuna è `500` e la prenotazione ha un solo record `CheckIn`

#### Scenario: Stessa richiesta ripetuta in parallelo
- **WHEN** l'organizzatore invia in parallelo cinque check-in sulla stessa prenotazione nella finestra
- **THEN** esattamente una risposta è `200`, le altre quattro sono `422`, nessuna è `500` e la prenotazione ha un solo record `CheckIn`

### Requirement: Nessuna cancellazione di una prenotazione usata
`DELETE /api/bookings/{id}` dell'organizzatore o dello `Staff` su una prenotazione `Confirmed` con un record `CheckIn` MUST ricevere `422` «Cancellazione non consentita» / «Non è possibile cancellare una prenotazione già usata.», anche prima dell'inizio, e la prenotazione MUST restare invariata. Il controllo MUST venire dopo `403` e dopo la cancellazione ripetuta (`204`), prima del limite di tempo.

#### Scenario: Check-in prima dell'inizio e poi cancellazione
- **WHEN** Studente1 ha fatto il check-in alle 09:50 sulla prenotazione delle 10:00 e alle 09:55 invia `DELETE /api/bookings/{id}`
- **THEN** la risposta è `422` con titolo «Cancellazione non consentita» e messaggio «Non è possibile cancellare una prenotazione già usata.» e la prenotazione resta `Confirmed` con `checkedInAt` invariato

#### Scenario: Personale su una prenotazione usata
- **WHEN** un utente `Staff` ha registrato il check-in su una prenotazione di dopodomani e poi invia `DELETE /api/bookings/{id}`
- **THEN** la risposta è `422` con messaggio «Non è possibile cancellare una prenotazione già usata.»

#### Scenario: Prenotazione usata e già iniziata
- **WHEN** l'organizzatore invia `DELETE /api/bookings/{id}` alle 10:30 per una prenotazione delle 10:00 con check-in
- **THEN** la risposta è `422` con messaggio «Non è possibile cancellare una prenotazione già usata.», perché il controllo sull'uso viene prima del limite di tempo

#### Scenario: Estraneo su una prenotazione usata
- **WHEN** Studente3, né organizzatore né membro, invia `DELETE /api/bookings/{id}` per una prenotazione usata di Studente1
- **THEN** la risposta è `403` con messaggio «Non puoi cancellare questa prenotazione.»

### Requirement: Check-in e cancellazione contemporanei
Con un check-in e una `DELETE /api/bookings/{id}` contemporanei sulla stessa prenotazione, Bookings MUST far riuscire al massimo una delle due. L'altra MUST ricevere il `422` corrispondente allo stato dopo la prima, mai `500`. Una prenotazione `Cancelled` MUST NOT avere un record `CheckIn`.

#### Scenario: Check-in e cancellazione nello stesso istante
- **WHEN** alle 09:55 l'organizzatore invia `DELETE /api/bookings/{id}` e un membro invia `POST /api/bookings/{id}/check-in` in parallelo sulla stessa prenotazione delle 10:00
- **THEN** una sola delle due riesce. Se vince la cancellazione, la risposta è `204`, il check-in riceve `422` «Non è possibile fare il check-in di una prenotazione cancellata.» e la prenotazione è `Cancelled` senza record. Se vince il check-in, la risposta è `200`, la cancellazione riceve `422` «Non è possibile cancellare una prenotazione già usata.» e la prenotazione resta `Confirmed` con un record `CheckIn`. Nessuna risposta è `500`

#### Scenario: Nessuna prenotazione cancellata e usata
- **WHEN** check-in e cancellazione della stessa prenotazione vengono ripetuti in parallelo su molte prenotazioni
- **THEN** nessuna prenotazione risulta `Cancelled` con un record `CheckIn`

## MODIFIED Requirements

### Requirement: Contenuto di ogni voce
Ogni voce MUST contenere `id`, `roomId`, `roomName`, `buildingName`, `start`, `end` (ISO 8601 con fuso), `isGroup`, `organizerName`, `isOrganizer` (vero se l'utente della richiesta è l'organizzatore) e `checkedInAt` (ISO 8601 con fuso: istante del check-in della prenotazione, fatto da chiunque). `buildingName` e `organizerName` SHALL valere `null` quando Bookings non li conosce; `checkedInAt` SHALL valere `null` quando la prenotazione non ha un check-in.

#### Scenario: Voce completa
- **WHEN** Mario Rossi chiama l'endpoint e ha creato una prenotazione dell'«Aula A1» nell'«Edificio A», domani dalle 10:00 alle 11:00, senza membri
- **THEN** la voce contiene `roomName` = «Aula A1», `buildingName` = «Edificio A», `start` e `end` con il fuso, `isGroup` = `false`, `organizerName` = «Mario Rossi», `isOrganizer` = `true` e `checkedInAt` = `null`

#### Scenario: Voce vista da un membro
- **WHEN** Anna Bianchi chiama l'endpoint ed è membro di una prenotazione creata da Mario Rossi
- **THEN** la voce contiene `isGroup` = `true`, `organizerName` = «Mario Rossi» e `isOrganizer` = `false`

#### Scenario: Edificio non noto
- **WHEN** la prenotazione riguarda uno spazio la cui copia locale non ha ancora il nome dell'edificio
- **THEN** la voce contiene `buildingName` = `null` e gli altri campi valorizzati

#### Scenario: Organizzatore non noto
- **WHEN** la prenotazione è stata creata prima che Bookings salvasse il nome dell'organizzatore
- **THEN** la voce contiene `organizerName` = `null` e gli altri campi valorizzati

#### Scenario: Prenotazione con check-in
- **WHEN** Mario Rossi ha fatto il check-in alle 10:05 sulla prenotazione dell'«Aula A1» di oggi dalle 10:00 alle 11:00 e chiama l'endpoint
- **THEN** la voce contiene `checkedInAt` uguale all'istante delle 10:05, con il fuso

#### Scenario: Prenotazione passata con check-in
- **WHEN** la prenotazione di ieri dalle 10:00 alle 11:00 ha avuto il check-in alle 09:50 e l'utente chiama l'endpoint
- **THEN** la voce compare in `past` con `checkedInAt` uguale all'istante delle 09:50 di ieri

#### Scenario: Record no-show senza check-in
- **WHEN** la prenotazione ha soltanto un record `NoShow`
- **THEN** la voce contiene `checkedInAt` = `null`

### Requirement: Cancellazione di una prenotazione
Bookings SHALL esporre `DELETE /api/bookings/{id}` (`id` = GUID della prenotazione). Se l'utente può cancellarla, la prenotazione non ha un check-in e l'inizio non è ancora arrivato, Bookings MUST portare la prenotazione a `Status` = `Cancelled`, salvare in `CancelledAt` l'istante della cancellazione e rispondere `204` senza corpo. La riga MUST restare nel database e non tornare più `Confirmed` (INV-7); la cancellazione vale per tutti i membri.

#### Scenario: L'organizzatore cancella una prenotazione futura
- **WHEN** Studente1 ha prenotato l'«Aula A1» dopodomani dalle 10:00 alle 11:00 e invia `DELETE /api/bookings/{id}` per quella prenotazione
- **THEN** la risposta è `204`, la prenotazione ha `Status` = `Cancelled` e `CancelledAt` uguale all'istante della richiesta

#### Scenario: Non più tra le future dell'organizzatore
- **WHEN** Studente1 ha cancellato la propria prenotazione dell'«Aula A1» di dopodomani e chiama `GET /api/bookings/mine`
- **THEN** la prenotazione non compare in `upcoming` e compare in `cancelled`

#### Scenario: Non più tra le future dei membri
- **WHEN** Studente1 cancella una prenotazione di gruppo di cui Studente2 è membro e Studente2 chiama `GET /api/bookings/mine`
- **THEN** la prenotazione non compare in `upcoming` di Studente2 e compare nella sua lista `cancelled`

#### Scenario: Prenotazione con check-in non cancellata
- **WHEN** Studente1 invia `DELETE /api/bookings/{id}` per una prenotazione futura che ha già un check-in
- **THEN** la risposta è `422` con messaggio «Non è possibile cancellare una prenotazione già usata.» e la prenotazione resta in `upcoming`

### Requirement: Cancellazione solo prima dell'inizio
Bookings MUST rifiutare con `422`, titolo «Cancellazione non consentita» e messaggio «Non è possibile cancellare una prenotazione già iniziata o passata.», la cancellazione di una prenotazione `Confirmed` senza check-in il cui inizio è uguale o precedente all'istante della richiesta (assunzione A5), per l'organizzatore e per lo Staff. La prenotazione MUST restare invariata. Una prenotazione con check-in riceve invece il rifiuto di «Nessuna cancellazione di una prenotazione usata», che viene prima di questo controllo.

#### Scenario: Prenotazione già iniziata
- **WHEN** Studente1 ha una prenotazione confermata senza check-in dalle 10:00 alle 11:00 e alle 10:15 invia `DELETE /api/bookings/{id}`
- **THEN** la risposta è `422` con titolo «Cancellazione non consentita» e messaggio «Non è possibile cancellare una prenotazione già iniziata o passata.» e la prenotazione resta `Confirmed`

#### Scenario: Prenotazione passata
- **WHEN** Studente1 invia `DELETE /api/bookings/{id}` per una sua prenotazione confermata senza check-in terminata ieri
- **THEN** la risposta è `422` con messaggio «Non è possibile cancellare una prenotazione già iniziata o passata.» e la prenotazione resta `Confirmed` nella lista `past`

#### Scenario: Inizio esattamente uguale all'istante della richiesta
- **WHEN** la richiesta di cancellazione dell'organizzatore arriva esattamente all'orario di inizio di una prenotazione senza check-in
- **THEN** la risposta è `422` e la prenotazione resta `Confirmed`

#### Scenario: Un istante prima dell'inizio
- **WHEN** la richiesta di cancellazione dell'organizzatore arriva un istante prima dell'orario di inizio di una prenotazione senza check-in
- **THEN** la risposta è `204` e la prenotazione è `Cancelled`

#### Scenario: Personale su una prenotazione iniziata
- **WHEN** un utente `Staff` invia `DELETE /api/bookings/{id}` per una prenotazione confermata senza check-in già iniziata
- **THEN** la risposta è `422` e la prenotazione resta `Confirmed`

### Requirement: Cancellazioni contemporanee
Con richieste contemporanee sulla stessa prenotazione, il passaggio a `Cancelled` MUST avvenire in un'unica operazione atomica che riesce solo se la prenotazione è ancora `Confirmed`, non è iniziata e non ha un check-in. Le altre richieste MUST ricevere la risposta che corrisponde allo stato riletto dopo il tentativo (`204`, `403`, `404` o `422`) e mai `500`.

#### Scenario: Due cancellazioni nello stesso istante
- **WHEN** l'organizzatore e un utente `Staff` inviano in parallelo `DELETE /api/bookings/{id}` per la stessa prenotazione futura
- **THEN** entrambe le risposte sono `204`, nessuna è `500` e la prenotazione è `Cancelled` con `CancelledAt` uguale all'istante delle richieste

#### Scenario: Stessa richiesta ripetuta in parallelo
- **WHEN** l'organizzatore invia in parallelo cinque `DELETE /api/bookings/{id}` per la stessa prenotazione futura
- **THEN** tutte le risposte sono `204`, nessuna è `500` e la prenotazione è `Cancelled` con `CancelledAt` uguale all'istante delle richieste

#### Scenario: Check-in registrato tra la lettura e la scrittura
- **WHEN** la prenotazione riceve il check-in dopo che la `DELETE` l'ha letta e prima della scrittura
- **THEN** la `DELETE` non la cancella e risponde `422` con messaggio «Non è possibile cancellare una prenotazione già usata.», senza `500`
