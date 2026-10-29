# Spec Delta: bookings

## ADDED Requirements

### Requirement: Prenotazione con i membri del gruppo
`POST /api/bookings` SHALL accettare `memberIds`, un elenco facoltativo di GUID degli altri studenti. Se la richiesta rispetta tutte le regole, Bookings MUST creare nella stessa transazione la prenotazione `Confirmed` e un membro per ogni id, e rispondere `201` con `participants` = 1 + numero di membri (INV-5). Un rifiuto MUST NOT lasciare né la prenotazione né i membri.

#### Scenario: Prenotazione con due membri
- **WHEN** Mario Rossi invia `POST /api/bookings` per l'«Aula A1» (capienza 30), libera dopodomani dalle 10:00 alle 11:00, con `memberIds` = id di Anna Bianchi e di Luca Verdi, entrambi studenti liberi
- **THEN** la risposta è `201` con `participants` = 3 e `status` = «Confirmed»

#### Scenario: Membri salvati con la prenotazione
- **WHEN** la prenotazione di gruppo di Mario Rossi con Anna Bianchi e Luca Verdi è stata creata e un utente `Staff` chiama `GET /api/bookings` per l'«Aula A1» e quel giorno
- **THEN** la voce ha `participants` = 3 e `memberIds` con gli id di Anna Bianchi e Luca Verdi

### Requirement: Codice di motivo nei rifiuti di gruppo
Ogni rifiuto introdotto da questa change MUST essere un `ProblemDetails` con la proprietà `code` indicata nel suo requisito e, dove indicato, `limits`, come i rifiuti esistenti (`docs/handoff-architettura.md`, 6.2).

#### Scenario: Codice nel rifiuto per membro impegnato
- **WHEN** un rifiuto di `POST /api/bookings` è dovuto a un membro già impegnato
- **THEN** il corpo della risposta contiene `status`, `title`, `detail` e `code` = `members-occupied`

### Requirement: Prenotazioni di gruppo solo per gli studenti
Un utente con ruolo `Staff` (claim `role`) MUST NOT poter creare prenotazioni di gruppo: se `memberIds` contiene almeno un id, Bookings MUST rispondere `403` con titolo «Prenotazione di gruppo non consentita», messaggio «Il personale non può creare prenotazioni di gruppo.» e `code` = `group-booking-forbidden`, prima di ogni altro controllo sulla richiesta e senza chiamare Identity né creare nulla. Con `memberIds` assente o vuoto la prenotazione singola di uno `Staff` MUST restare invariata.

#### Scenario: Staff con membri
- **WHEN** Elena Conti (`Staff`) invia `POST /api/bookings` con `memberIds` = id di Anna Bianchi
- **THEN** la risposta è `403` con titolo «Prenotazione di gruppo non consentita», messaggio «Il personale non può creare prenotazioni di gruppo.» e `code` = `group-booking-forbidden`, Identity non riceve alcuna richiesta e nessuna prenotazione viene creata

#### Scenario: Staff senza membri
- **WHEN** Elena Conti (`Staff`) invia `POST /api/bookings` con `memberIds` = `[]` per una fascia libera
- **THEN** la risposta è `201` con `participants` = 1

### Requirement: Numero massimo di membri
Prima di ogni altro controllo sui membri, Bookings MUST rifiutare con `422`, titolo «Membri non validi», messaggio «Puoi aggiungere al massimo 10 membri.», `code` = `too-many-members` e `limits` = `{ "maxMembers": 10 }`, senza creare nulla e senza chiamare Identity, una richiesta con più di 10 id in `memberIds`, contati prima di togliere i duplicati. Con 10 membri la richiesta MUST proseguire.

#### Scenario: Più di 10 membri
- **WHEN** Mario Rossi invia `POST /api/bookings` con 11 id distinti in `memberIds`
- **THEN** la risposta è `422` con titolo «Membri non validi», messaggio «Puoi aggiungere al massimo 10 membri.», `code` = `too-many-members` e `limits.maxMembers` = 10, Identity non riceve alcuna richiesta e nessuna prenotazione viene creata

#### Scenario: Esattamente 10 membri
- **WHEN** Mario Rossi chiede l'«Aula A1» (capienza 30), libera, con 10 membri studenti liberi
- **THEN** la risposta è `201` con `participants` = 11

### Requirement: Membri del gruppo validi
Dopo il controllo sul numero, Bookings MUST rifiutare con `422` e titolo «Membri non validi», senza creare nulla e senza chiamare Identity, una richiesta che contiene l'organizzatore tra i membri («Non puoi aggiungerti come membro: sei già l'organizzatore.», `code` = `organizer-as-member`) o, dopo, lo stesso membro più di una volta («Ogni studente può essere aggiunto una sola volta.», `code` = `duplicate-members`).

#### Scenario: Organizzatore tra i membri
- **WHEN** Mario Rossi invia `POST /api/bookings` con il proprio id in `memberIds`
- **THEN** la risposta è `422` con titolo «Membri non validi» e messaggio «Non puoi aggiungerti come membro: sei già l'organizzatore.», `code` = `organizer-as-member` e nessuna prenotazione viene creata

#### Scenario: Membro ripetuto
- **WHEN** Mario Rossi invia `POST /api/bookings` con `memberIds` che contiene due volte l'id di Anna Bianchi
- **THEN** la risposta è `422` con titolo «Membri non validi» e messaggio «Ogni studente può essere aggiunto una sola volta.», `code` = `duplicate-members` e nessuna prenotazione viene creata

### Requirement: Membri solo studenti registrati
Bookings MUST accettare come membri solo utenti registrati con ruolo `Student` (INV-9), secondo Identity al momento della richiesta. Altrimenti MUST rispondere `422`, titolo «Membri non validi», messaggio «Puoi aggiungere come membri solo studenti registrati.», `code` = `members-not-students`, senza creare nulla. Il messaggio MUST NOT indicare quale id è stato rifiutato né altri dati degli utenti.

#### Scenario: Membro con ruolo Staff
- **WHEN** Mario Rossi invia `POST /api/bookings` con `memberIds` = id di Anna Bianchi e di Elena Conti (`Staff`)
- **THEN** la risposta è `422` con titolo «Membri non validi», messaggio «Puoi aggiungere come membri solo studenti registrati.» e `code` = `members-not-students`, e non esiste alcuna prenotazione né alcun membro nuovo

#### Scenario: Membro inesistente
- **WHEN** Mario Rossi invia `POST /api/bookings` con un id che non corrisponde a nessun utente registrato
- **THEN** la risposta è `422` con messaggio «Puoi aggiungere come membri solo studenti registrati.», `code` = `members-not-students` e nessuna prenotazione viene creata

### Requirement: Capienza dello spazio
Bookings MUST rifiutare con `422`, titolo «Capienza superata» e messaggio «Lo spazio ospita al massimo N persone.» (N = capienza della copia locale dello spazio), `code` = `capacity-exceeded` e `limits` = `{ "maxParticipants": N }` una richiesta il cui numero di partecipanti supera la capienza (INV-5), senza creare nulla. Un numero di partecipanti uguale alla capienza MUST essere accettato.

#### Scenario: Capienza superata
- **WHEN** Mario Rossi chiede la «Sala studio B2», capienza 4, libera, con 4 membri studenti (5 partecipanti)
- **THEN** la risposta è `422` con titolo «Capienza superata» e messaggio «Lo spazio ospita al massimo 4 persone.», `code` = `capacity-exceeded` e `limits.maxParticipants` = 4, e nessuna prenotazione viene creata

#### Scenario: Partecipanti uguali alla capienza
- **WHEN** Mario Rossi chiede la «Sala studio B2», capienza 4, libera, con 3 membri studenti liberi
- **THEN** la risposta è `201` con `participants` = 4

### Requirement: Nessuna sovrapposizione per i membri
Bookings MUST rifiutare con `409`, titolo «Membro già impegnato» e messaggio «Uno o più membri hanno già una prenotazione in questa fascia.» e `code` = `members-occupied`, senza creare nulla, una richiesta in cui anche un solo membro è già organizzatore o membro di una prenotazione `Confirmed` sovrapposta, in qualunque spazio (INV-8). Prenotazioni cancellate e adiacenti MUST NOT contare.

#### Scenario: Membro che ha già prenotato altrove
- **WHEN** Anna Bianchi ha una prenotazione confermata della «Sala studio B2» dalle 10:00 alle 11:00 e Mario Rossi chiede l'«Aula A1» dalle 10:30 alle 11:30 dello stesso giorno con Anna Bianchi e Luca Verdi come membri
- **THEN** la risposta è `409` con titolo «Membro già impegnato» e messaggio «Uno o più membri hanno già una prenotazione in questa fascia.», `code` = `members-occupied` e l'«Aula A1» non ha prenotazioni in quella fascia

#### Scenario: Membro già membro di un altro gruppo
- **WHEN** Luca Verdi è membro di una prenotazione confermata organizzata da Giulia Neri dalle 10:00 alle 11:00 e Mario Rossi chiede un altro spazio dalle 10:00 alle 10:30 con Luca Verdi come membro
- **THEN** la risposta è `409` con messaggio «Uno o più membri hanno già una prenotazione in questa fascia.» e nessuna prenotazione né alcun membro viene creato

#### Scenario: Prenotazione del membro adiacente
- **WHEN** Anna Bianchi ha una prenotazione confermata dalle 10:00 alle 11:00 e Mario Rossi chiede un altro spazio dalle 11:00 alle 12:00 con Anna Bianchi come membro
- **THEN** la risposta è `201`

#### Scenario: Prenotazione del membro cancellata
- **WHEN** Anna Bianchi ha cancellato la propria prenotazione dalle 10:00 alle 11:00 e Mario Rossi chiede un altro spazio nella stessa fascia con Anna Bianchi come membro
- **THEN** la risposta è `201`

#### Scenario: Prima il conflitto dell'organizzatore
- **WHEN** sia Mario Rossi sia il membro Anna Bianchi hanno una prenotazione confermata sovrapposta alla fascia richiesta
- **THEN** la risposta è `409` con messaggio «Hai già una prenotazione in questa fascia oraria.»

### Requirement: Membro con un'altra prenotazione in elaborazione
Le richieste che coinvolgono la stessa persona, come organizzatore o come membro, MUST essere elaborate una alla volta. Se una richiesta attende per più di 10 secondi una richiesta che coinvolge uno dei suoi membri, Bookings MUST rispondere `409`, titolo «Prenotazione in corso», messaggio «Un membro del gruppo ha un'altra prenotazione in corso, riprova tra qualche secondo.» e `code` = `member-booking-in-progress`, senza salvare nulla.

#### Scenario: Attesa sul lock di un membro oltre i 10 secondi
- **WHEN** una richiesta che coinvolge Anna Bianchi è ancora in elaborazione dopo 10 secondi e arriva una richiesta di Mario Rossi con Anna Bianchi come membro
- **THEN** la seconda richiesta riceve `409` con titolo «Prenotazione in corso» e messaggio «Un membro del gruppo ha un'altra prenotazione in corso, riprova tra qualche secondo.», `code` = `member-booking-in-progress` e non crea alcuna prenotazione

### Requirement: Verifica dei membri non disponibile
Se Identity non risponde entro 3 secondi, è irraggiungibile, risponde con un errore o con una risposta non leggibile, Bookings MUST rispondere `503`, titolo «Servizio non disponibile», messaggio «Non è possibile verificare i membri in questo momento. Riprova tra poco.» e `code` = `members-check-unavailable`, senza creare nulla. Una richiesta senza membri MUST NOT dipendere da Identity.

#### Scenario: Identity non risponde in tempo
- **WHEN** Mario Rossi invia `POST /api/bookings` con un membro e Identity non risponde entro 3 secondi
- **THEN** la risposta è `503` con titolo «Servizio non disponibile» e messaggio «Non è possibile verificare i membri in questo momento. Riprova tra poco.», `code` = `members-check-unavailable` e nessuna prenotazione né alcun membro viene creato

#### Scenario: Identity risponde con un errore
- **WHEN** Identity risponde `500` alla verifica dei membri
- **THEN** la risposta di `POST /api/bookings` è `503` con lo stesso messaggio e nessuna prenotazione viene creata

#### Scenario: Prenotazione singola con Identity spento
- **WHEN** Identity non è raggiungibile e Mario Rossi invia `POST /api/bookings` senza `memberIds` per una fascia libera
- **THEN** la risposta è `201` con `participants` = 1

### Requirement: Verifica dei membri con il token di chi prenota
Per verificare i membri Bookings MUST chiamare la verifica interna di Identity con la stessa intestazione `Authorization: Bearer` ricevuta da chi prenota, senza credenziali proprie. La chiamata MUST avvenire una sola volta per richiesta e solo se `memberIds` contiene almeno un id.

#### Scenario: Token inoltrato
- **WHEN** Mario Rossi invia `POST /api/bookings` con due membri
- **THEN** Identity riceve una sola richiesta `POST /internal/users/students/check` con gli id dei due membri e con l'intestazione `Authorization` di Mario Rossi

#### Scenario: Nessuna chiamata senza membri
- **WHEN** Mario Rossi invia `POST /api/bookings` con `memberIds` vuoto
- **THEN** Identity non riceve alcuna richiesta

### Requirement: Prenotazione di gruppo tra le prenotazioni dei membri
Dopo il `201` di una prenotazione con membri, la prenotazione MUST comparire in `GET /api/bookings/mine` di ogni membro, nella lista corrispondente al suo stato, con `isGroup` = `true`, `isOrganizer` = `false` e `organizerName` dell'organizzatore.

#### Scenario: Ogni membro ritrova la prenotazione
- **WHEN** Mario Rossi ha prenotato l'«Aula A1» dopodomani dalle 10:00 alle 11:00 con Anna Bianchi e Luca Verdi, e ognuno dei due chiama `GET /api/bookings/mine`
- **THEN** entrambi trovano la prenotazione in `upcoming` con `isGroup` = `true`, `isOrganizer` = `false` e `organizerName` = «Mario Rossi»

#### Scenario: L'organizzatore la vede una sola volta
- **WHEN** Mario Rossi chiama `GET /api/bookings/mine` dopo aver creato la prenotazione di gruppo
- **THEN** la prenotazione compare una sola volta in `upcoming` con `isGroup` = `true` e `isOrganizer` = `true`

### Requirement: Permessi dei membri su una prenotazione di gruppo
Un membro non organizzatore MUST NOT poter cancellare la prenotazione né cambiarne i membri: `DELETE /api/bookings/{id}` MUST rispondere `403` come previsto da «Chi può cancellare una prenotazione», e Bookings MUST NOT esporre alcuna route per aggiungere o togliere membri dopo la creazione.

#### Scenario: Il membro prova a cancellare
- **WHEN** Anna Bianchi, membro della prenotazione di gruppo futura creata da Mario Rossi con `POST /api/bookings`, invia `DELETE /api/bookings/{id}`
- **THEN** la risposta è `403` con messaggio «Non puoi cancellare questa prenotazione.» e la prenotazione resta `Confirmed` con gli stessi membri

#### Scenario: Nessuna modifica dei membri
- **WHEN** Anna Bianchi o Mario Rossi inviano `PUT` o `PATCH` su `/api/bookings/{id}` della prenotazione di gruppo
- **THEN** la risposta è `405` e i membri e `participants` della prenotazione restano invariati

### Requirement: Prenotazioni di gruppo contemporanee
Con richieste contemporanee che coinvolgono la stessa persona (come organizzatore o come membro) in fasce sovrapposte su spazi diversi, Bookings MUST confermarne al massimo una; le altre MUST ricevere `409` e non creare prenotazioni né membri, mai `500`. La garanzia SHALL essere verificata su SQL Server reale.

#### Scenario: Stesso membro in due gruppi nello stesso istante
- **WHEN** Mario Rossi e Giulia Neri inviano in parallelo `POST /api/bookings` per due spazi diversi e liberi dopodomani dalle 10:00 alle 11:00, entrambi con Anna Bianchi come membro
- **THEN** una risposta è `201`, l'altra è `409`, e nel database Anna Bianchi è in una sola prenotazione confermata in quella fascia

#### Scenario: Organizzatori incrociati
- **WHEN** in parallelo Mario Rossi prenota con Anna Bianchi come membro e Anna Bianchi prenota un altro spazio con Mario Rossi come membro, nella stessa fascia
- **THEN** una risposta è `201`, l'altra è `409`, nessuna è `500` e nessuna attende oltre i 10 secondi del lock

## MODIFIED Requirements

### Requirement: Creazione di una prenotazione singola
Bookings SHALL esporre `POST /api/bookings` con corpo `{ roomId, start, end, purpose, memberIds }` (`start` ed `end` in ISO 8601 con fuso, `purpose` e `memberIds` facoltativi). Se `memberIds` manca o è vuoto e la richiesta rispetta tutte le regole, MUST creare una prenotazione `Confirmed` a nome dell'utente del claim `sub`, senza membri, e rispondere `201` con `id`, `roomId`, `roomName`, `start`, `end`, `participants` = 1 e `status` = `Confirmed`. Da quel momento la fascia è occupata per tutti.

#### Scenario: Prenotazione di una fascia libera
- **WHEN** Studente1 autenticato invia `POST /api/bookings` per l'«Aula A1», aperta 08:00–20:00 e libera, dopodomani dalle 10:00 alle 11:00 ora di Roma
- **THEN** la risposta è `201` con intestazione `Location` `/api/bookings/{id}` e corpo con `id`, `roomId` dell'«Aula A1», `roomName` = «Aula A1», `start` e `end` della fascia con il fuso, `participants` = 1 e `status` = «Confirmed»

#### Scenario: La fascia prenotata risulta occupata per tutti
- **WHEN** Studente1 ha prenotato l'«Aula A1» dopodomani dalle 10:00 alle 11:00 e un altro utente chiama `GET /api/bookings/availability` per l'«Aula A1» e quel giorno
- **THEN** le fasce delle 10:00 e delle 10:30 risultano occupate

#### Scenario: Motivo facoltativo
- **WHEN** lo studente invia la richiesta senza `purpose` oppure con un motivo di al massimo 200 caratteri
- **THEN** la prenotazione viene creata con `201`

#### Scenario: Elenco dei membri vuoto
- **WHEN** lo studente invia la richiesta con `memberIds` = `[]` per una fascia libera
- **THEN** la risposta è `201` con `participants` = 1 e la prenotazione non ha membri
