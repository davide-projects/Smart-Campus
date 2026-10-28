# Spec Delta: bookings

## Purpose

Descrive il comportamento osservabile del servizio Bookings: prenotazioni degli spazi senza conflitti, consultazione delle proprie prenotazioni e copia locale degli spazi prenotabili ricevuta da Rooms.

## ADDED Requirements

### Requirement: Endpoint delle mie prenotazioni
Bookings SHALL esporre `GET /api/bookings/mine`, che risponde `200` con le prenotazioni dell'utente identificato dal claim `sub` del token inoltrato dal Gateway, nell'oggetto `{ "upcoming": [], "past": [], "cancelled": [] }`. Le tre liste MUST essere complete, senza paginazione né limite al numero di voci.

#### Scenario: Utente autenticato
- **WHEN** uno studente autenticato chiama `GET /api/bookings/mine`
- **THEN** la risposta è `200` con un oggetto JSON che contiene le liste `upcoming`, `past` e `cancelled`

#### Scenario: Nessun limite al numero di voci
- **WHEN** l'utente ha 60 prenotazioni passate e chiama `GET /api/bookings/mine`
- **THEN** la lista `past` contiene tutte e 60 le prenotazioni, senza parametri di pagina

#### Scenario: Token assente o senza identificativo utente
- **WHEN** la richiesta arriva a Bookings senza intestazione `Authorization: Bearer` oppure con un token senza un claim `sub` valido
- **THEN** la risposta è `401` con titolo «Accesso richiesto» e messaggio «Effettua l'accesso per vedere le tue prenotazioni.»

#### Scenario: Chiamata attraverso il Gateway senza token
- **WHEN** un client chiama `GET /api/bookings/mine` sul Gateway senza token o con un token non valido
- **THEN** il Gateway risponde `401` e la richiesta non arriva a Bookings

### Requirement: Solo le prenotazioni di cui l'utente è organizzatore o membro
L'elenco MUST contenere soltanto le prenotazioni in cui l'utente è organizzatore (`OrganizerId`) o membro (`ReservationMember`), in qualunque stato. Ogni prenotazione SHALL comparire una sola volta, in una sola lista.

#### Scenario: Prenotazione creata dall'utente
- **WHEN** Studente1 ha creato una prenotazione e chiama `GET /api/bookings/mine`
- **THEN** la prenotazione compare nell'elenco

#### Scenario: Prenotazione di gruppo di cui l'utente è membro
- **WHEN** Studente2 è membro di una prenotazione organizzata da Studente1 e chiama `GET /api/bookings/mine`
- **THEN** la prenotazione compare nell'elenco di Studente2

#### Scenario: Prenotazione altrui
- **WHEN** Studente3 non è né organizzatore né membro di una prenotazione di Studente1 e chiama `GET /api/bookings/mine`
- **THEN** quella prenotazione non compare in nessuna lista dell'elenco di Studente3

#### Scenario: Prenotazione di gruppo vista dall'organizzatore
- **WHEN** Studente1 organizza una prenotazione con due membri e chiama `GET /api/bookings/mine`
- **THEN** la prenotazione compare una sola volta, in una sola lista

### Requirement: Sezione delle prenotazioni future
La lista `upcoming` SHALL contenere le prenotazioni `Confirmed` la cui fine non è ancora passata nel momento della richiesta, comprese quelle in corso, in ordine crescente di inizio.

#### Scenario: Ordine cronologico
- **WHEN** l'utente ha due prenotazioni confermate, una dopodomani alle 10:00 e una domani alle 14:00
- **THEN** `upcoming` elenca prima quella di domani alle 14:00 e poi quella di dopodomani alle 10:00

#### Scenario: Prenotazione in corso
- **WHEN** l'utente ha una prenotazione confermata iniziata alle 10:00 che termina alle 11:00 e chiama l'endpoint alle 10:30
- **THEN** la prenotazione compare in `upcoming`

### Requirement: Sezione delle prenotazioni passate
La lista `past` SHALL contenere le prenotazioni `Confirmed` già terminate nel momento della richiesta, in ordine decrescente di inizio (dalla più recente).

#### Scenario: Prenotazione terminata
- **WHEN** l'utente ha una prenotazione confermata terminata ieri alle 11:00
- **THEN** la prenotazione compare in `past` e non in `upcoming`

#### Scenario: Ordine dalla più recente
- **WHEN** l'utente ha due prenotazioni confermate terminate, una ieri e una la settimana scorsa
- **THEN** `past` elenca prima quella di ieri

#### Scenario: Fine esattamente uguale all'istante della richiesta
- **WHEN** la richiesta arriva esattamente nell'istante in cui una prenotazione confermata termina
- **THEN** la prenotazione compare in `past`

### Requirement: Sezione delle prenotazioni cancellate
La lista `cancelled` SHALL contenere tutte le prenotazioni `Cancelled` dell'utente, future o passate che siano, in ordine decrescente di inizio.

#### Scenario: Cancellata futura
- **WHEN** l'utente ha una prenotazione `Cancelled` il cui orario è domani
- **THEN** la prenotazione compare solo in `cancelled`

#### Scenario: Cancellata passata
- **WHEN** l'utente ha una prenotazione `Cancelled` il cui orario è la settimana scorsa
- **THEN** la prenotazione compare solo in `cancelled`

#### Scenario: Ordine delle cancellate
- **WHEN** l'utente ha due prenotazioni cancellate, una con inizio domani e una con inizio la settimana scorsa
- **THEN** `cancelled` elenca prima quella con inizio domani

### Requirement: Contenuto di ogni voce
Ogni voce MUST contenere `id`, `roomId`, `roomName`, `buildingName`, `start`, `end` (ISO 8601 con fuso), `isGroup`, `organizerName` e `isOrganizer` (vero se l'utente della richiesta è l'organizzatore). `buildingName` e `organizerName` SHALL valere `null` quando Bookings non li conosce.

#### Scenario: Voce completa
- **WHEN** Mario Rossi chiama l'endpoint e ha creato una prenotazione dell'«Aula A1» nell'«Edificio A», domani dalle 10:00 alle 11:00, senza membri
- **THEN** la voce contiene `roomName` = «Aula A1», `buildingName` = «Edificio A», `start` e `end` con il fuso, `isGroup` = `false`, `organizerName` = «Mario Rossi» e `isOrganizer` = `true`

#### Scenario: Voce vista da un membro
- **WHEN** Anna Bianchi chiama l'endpoint ed è membro di una prenotazione creata da Mario Rossi
- **THEN** la voce contiene `isGroup` = `true`, `organizerName` = «Mario Rossi» e `isOrganizer` = `false`

#### Scenario: Edificio non noto
- **WHEN** la prenotazione riguarda uno spazio la cui copia locale non ha ancora il nome dell'edificio
- **THEN** la voce contiene `buildingName` = `null` e gli altri campi valorizzati

#### Scenario: Organizzatore non noto
- **WHEN** la prenotazione è stata creata prima che Bookings salvasse il nome dell'organizzatore
- **THEN** la voce contiene `organizerName` = `null` e gli altri campi valorizzati

### Requirement: Indicazione delle prenotazioni di gruppo
`isGroup` SHALL essere `true` se la prenotazione ha almeno un membro (`ReservationMember`), in coerenza con INV-5 (partecipanti = organizzatore + membri), e `false` altrimenti. Il valore MUST essere lo stesso per l'organizzatore e per i membri.

#### Scenario: Prenotazione con membri vista dall'organizzatore
- **WHEN** l'organizzatore di una prenotazione con almeno un membro chiama l'endpoint
- **THEN** la voce contiene `isGroup` = `true`

#### Scenario: Prenotazione senza membri
- **WHEN** l'utente vede una prenotazione creata da lui senza membri
- **THEN** la voce contiene `isGroup` = `false`

### Requirement: Nessuna prenotazione
Se l'utente non è organizzatore né membro di alcuna prenotazione, l'endpoint SHALL rispondere `200` con tutte e tre le liste vuote.

#### Scenario: Elenco vuoto
- **WHEN** un utente senza prenotazioni chiama `GET /api/bookings/mine`
- **THEN** la risposta è `200` con `upcoming`, `past` e `cancelled` vuote

### Requirement: Nome dell'organizzatore salvato alla creazione
`POST /api/bookings` MUST salvare con la prenotazione il nome dell'organizzatore letto dal claim `name` del token; il nome resta quello del momento della creazione. Se il claim manca o è vuoto, la richiesta MUST essere rifiutata con `401` come quando manca `sub`, senza creare la prenotazione. Richiesta e risposta `201` non cambiano.

#### Scenario: Nome presente nel token
- **WHEN** l'utente con claim `name` = «Mario Rossi» crea una prenotazione con `POST /api/bookings`
- **THEN** la risposta è `201` come prima e la prenotazione compare in `GET /api/bookings/mine` con `organizerName` = «Mario Rossi»

#### Scenario: Claim name assente o vuoto
- **WHEN** l'utente invia `POST /api/bookings` con un token che ha un `sub` valido ma non ha il claim `name`, oppure lo ha vuoto
- **THEN** la risposta è `401` con titolo «Accesso richiesto» e messaggio «Effettua l'accesso per prenotare.» e nessuna prenotazione viene creata

### Requirement: Edificio nella copia locale degli spazi
Alla ricezione di `RoomCreated` o `RoomUpdated` Bookings MUST salvare nella copia locale dello spazio il `BuildingName` dell'evento, sostituendo quello precedente, con lo stesso comportamento idempotente degli altri campi. Se l'evento non contiene `BuildingName` (valore `null`), Bookings MUST lasciare invariato l'edificio già salvato. `RoomDeactivated` SHALL lasciarlo invariato.

#### Scenario: Spazio creato
- **WHEN** Bookings riceve `RoomCreated` per l'«Aula A1» con `BuildingName` = «Edificio A»
- **THEN** le prenotazioni dell'«Aula A1» in `GET /api/bookings/mine` hanno `buildingName` = «Edificio A»

#### Scenario: Spazio spostato in un altro edificio
- **WHEN** Bookings riceve `RoomUpdated` per l'«Aula A1» con `BuildingName` = «Biblioteca»
- **THEN** le prenotazioni dell'«Aula A1», comprese quelle già esistenti, hanno `buildingName` = «Biblioteca»

#### Scenario: Evento senza il nome dell'edificio
- **WHEN** la copia locale dell'«Aula A1» ha l'edificio «Edificio A» e Bookings riceve un `RoomUpdated` per l'«Aula A1» senza `BuildingName`
- **THEN** le prenotazioni dell'«Aula A1» continuano ad avere `buildingName` = «Edificio A» e gli altri campi dello spazio vengono aggiornati

#### Scenario: Spazio già presente prima della modifica
- **WHEN** uno spazio era nella copia locale prima di questa change e non ha ancora ricevuto un evento con `BuildingName`
- **THEN** le sue prenotazioni hanno `buildingName` = `null`

#### Scenario: Evento ricevuto due volte
- **WHEN** Bookings riceve due volte lo stesso `RoomCreated` con `BuildingName` = «Edificio A»
- **THEN** la copia locale contiene un solo spazio con edificio «Edificio A»

#### Scenario: Spazio dismesso
- **WHEN** Bookings riceve `RoomDeactivated` per uno spazio dell'«Edificio A»
- **THEN** le prenotazioni di quello spazio continuano ad avere `buildingName` = «Edificio A»
