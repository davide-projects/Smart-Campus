# identity Specification

## Purpose

Descrive il comportamento osservabile del servizio Identity: chi è registrato e con quale ruolo, esposto agli altri servizi e al frontend senza rivelare più dati del necessario.

## Requirements

### Requirement: Verifica interna degli studenti
Identity SHALL esporre `POST /internal/users/students/check` con corpo `{ "userIds": [GUID, ...] }` e rispondere `200` con `{ "students": [{ "id", "displayName" }], "notStudents": [GUID, ...] }`. Ogni id richiesto MUST comparire una sola volta, in una sola delle due liste: in `students` se è un utente registrato con ruolo `Student`, altrimenti in `notStudents` (utente `Staff` o id inesistente).

#### Scenario: Tutti studenti
- **WHEN** Bookings invia `POST /internal/users/students/check` con gli id di Anna Bianchi e Luca Verdi, entrambi `Student`
- **THEN** la risposta è `200` con `students` che contiene i due id con `displayName` «Anna Bianchi» e «Luca Verdi», e `notStudents` vuota

#### Scenario: Utente Staff
- **WHEN** la richiesta contiene l'id di Anna Bianchi (`Student`) e quello di Elena Conti (`Staff`)
- **THEN** `students` contiene solo Anna Bianchi e `notStudents` contiene l'id di Elena Conti

#### Scenario: Id inesistente
- **WHEN** la richiesta contiene un GUID che non corrisponde a nessun utente
- **THEN** quel GUID compare in `notStudents` e la risposta è comunque `200`

#### Scenario: Id ripetuto nella richiesta
- **WHEN** la richiesta contiene due volte l'id di Anna Bianchi
- **THEN** Anna Bianchi compare una sola volta in `students`

### Requirement: Accesso alla verifica interna
`POST /internal/users/students/check` MUST richiedere un JWT valido emesso da Identity (firma, emittente, destinatario, scadenza), con qualunque ruolo. Senza token o con un token non valido Identity MUST rispondere `401` senza dire nulla sugli id richiesti.

#### Scenario: Senza token
- **WHEN** una richiesta arriva a Identity senza intestazione `Authorization`
- **THEN** la risposta è `401` senza corpo con le liste

#### Scenario: Token non valido
- **WHEN** una richiesta arriva con un token scaduto o firmato con un'altra chiave
- **THEN** la risposta è `401`

#### Scenario: Token di uno studente
- **WHEN** la richiesta porta il token valido di Mario Rossi (`Student`), inoltrato da Bookings
- **THEN** la risposta è `200` con le due liste

### Requirement: Richiesta di verifica non valida
Identity MUST rispondere `400` (ValidationProblem) a `POST /internal/users/students/check` se il corpo manca, non è JSON valido, `userIds` manca o è vuoto, oppure contiene valori che non sono GUID.

#### Scenario: Elenco vuoto
- **WHEN** la richiesta ha corpo `{ "userIds": [] }`
- **THEN** la risposta è `400`

#### Scenario: Valore non GUID
- **WHEN** la richiesta ha corpo `{ "userIds": ["abc"] }`
- **THEN** la risposta è `400`

### Requirement: Verifica interna non raggiungibile dal Gateway
La verifica interna MUST NOT essere instradata dal Gateway: una richiesta a `/internal/**` sul Gateway MUST ricevere `404` e MUST NOT arrivare a Identity, con o senza token.

#### Scenario: Chiamata dal browser attraverso il Gateway
- **WHEN** un client chiama `POST /internal/users/students/check` sul Gateway con un token valido
- **THEN** il Gateway risponde `404` e Identity non riceve la richiesta

#### Scenario: Tentativo sotto /api/identity
- **WHEN** un client chiama `POST /api/identity/internal/users/students/check` sul Gateway con un token valido
- **THEN** la risposta è `404`, perché Identity non espone la verifica sotto `/api/identity`

### Requirement: Ricerca degli studenti
Identity SHALL esporre `GET /api/identity/students?search=<testo>`, che risponde `200` con un array di studenti (`id`, `username`, `displayName`) il cui nome visualizzato o username contiene il testo cercato, senza distinguere maiuscole e minuscole. Gli spazi iniziali e finali del testo MUST essere ignorati. La risposta MUST contenere solo utenti con ruolo `Student` e solo quei tre campi, ordinati per `displayName` e poi per `username`.

#### Scenario: Ricerca per nome
- **WHEN** Mario Rossi cerca `bia`
- **THEN** la risposta è `200` con Anna Bianchi (`username` `student2`) e nessun altro campo oltre `id`, `username` e `displayName`

#### Scenario: Ricerca per username
- **WHEN** Mario Rossi cerca `STUDENT3`
- **THEN** la risposta contiene Luca Verdi

#### Scenario: Utenti Staff esclusi
- **WHEN** Mario Rossi cerca `Conti`, il cognome di Elena Conti (`Staff`)
- **THEN** la risposta è `200` con un array vuoto

#### Scenario: Nessuna corrispondenza
- **WHEN** uno studente cerca un testo che nessuno studente contiene
- **THEN** la risposta è `200` con un array vuoto

### Requirement: Chi cerca escluso dai risultati
La ricerca MUST escludere l'utente che la esegue (claim `sub` del token), anche se il suo nome contiene il testo cercato.

#### Scenario: Studente che cerca il proprio nome
- **WHEN** Mario Rossi cerca `Rossi`
- **THEN** Mario Rossi non compare nei risultati

### Requirement: Limiti della ricerca degli studenti
Il testo cercato MUST avere almeno 2 caratteri dopo aver tolto gli spazi iniziali e finali. Altrimenti Identity MUST rispondere `400` (ValidationProblem sul campo `search`) con il messaggio «Scrivi almeno 2 caratteri per cercare.». La risposta MUST contenere al massimo 10 studenti, i primi nell'ordine della ricerca.

#### Scenario: Un solo carattere
- **WHEN** uno studente chiama `GET /api/identity/students?search=a`
- **THEN** la risposta è `400` con il messaggio «Scrivi almeno 2 caratteri per cercare.»

#### Scenario: Parametro mancante
- **WHEN** uno studente chiama `GET /api/identity/students` senza `search`
- **THEN** la risposta è `400` con il messaggio «Scrivi almeno 2 caratteri per cercare.»

#### Scenario: Più di 10 studenti corrispondenti
- **WHEN** 12 studenti diversi da chi cerca contengono `stud` nello username e uno studente cerca `stud`
- **THEN** la risposta contiene esattamente 10 studenti, i primi 10 per `displayName` e poi per `username`

### Requirement: Accesso alla ricerca degli studenti
`GET /api/identity/students` MUST essere riservata agli utenti autenticati. Senza token o con un token non valido, Identity MUST rispondere `401`. Attraverso il Gateway la richiesta senza token MUST ricevere `401` dal Gateway e non arrivare a Identity.

#### Scenario: Senza token direttamente a Identity
- **WHEN** una richiesta `GET /api/identity/students?search=bia` arriva a Identity senza intestazione `Authorization`
- **THEN** la risposta è `401`

#### Scenario: Senza token attraverso il Gateway
- **WHEN** un client chiama `GET /api/identity/students?search=bia` sul Gateway senza token
- **THEN** il Gateway risponde `401` e la richiesta non arriva a Identity

### Requirement: Ricerca degli studenti riservata agli studenti
Solo un utente con ruolo `Student` (claim `role`) SHALL poter cercare gli studenti, perché solo gli studenti creano prenotazioni di gruppo. A un utente `Staff` Identity MUST rispondere `403` con titolo «Ricerca non consentita» e messaggio «Solo gli studenti possono cercare altri studenti.», senza risultati.

#### Scenario: Utente Staff
- **WHEN** Elena Conti (`Staff`) chiama `GET /api/identity/students?search=bia`
- **THEN** la risposta è `403` con titolo «Ricerca non consentita» e messaggio «Solo gli studenti possono cercare altri studenti.» e nessuno studente nel corpo

#### Scenario: Utente Staff attraverso il Gateway
- **WHEN** Elena Conti (`Staff`) chiama `GET /api/identity/students?search=bia` sul Gateway
- **THEN** il Gateway inoltra la richiesta e la risposta è il `403` di Identity
