# bookings Specification

## Purpose

Descrive il comportamento osservabile del servizio Bookings: prenotazioni degli spazi senza conflitti, consultazione delle proprie prenotazioni e copia locale degli spazi prenotabili ricevuta da Rooms.

## Requirements

### Requirement: Endpoint delle mie prenotazioni
Bookings SHALL esporre `GET /api/bookings/mine`, che risponde `200` con le prenotazioni dell'utente identificato dal claim `sub` del token, nell'oggetto `{ "upcoming": [], "past": [], "cancelled": [] }`. Il token MUST essere validato da Bookings stesso (firma, emittente, destinatario e scadenza), anche quando la richiesta arriva dal Gateway. Le tre liste MUST essere complete, senza paginazione né limite al numero di voci.

#### Scenario: Utente autenticato
- **WHEN** uno studente autenticato chiama `GET /api/bookings/mine`
- **THEN** la risposta è `200` con un oggetto JSON che contiene le liste `upcoming`, `past` e `cancelled`

#### Scenario: Nessun limite al numero di voci
- **WHEN** l'utente ha 60 prenotazioni passate e chiama `GET /api/bookings/mine`
- **THEN** la lista `past` contiene tutte e 60 le prenotazioni, senza parametri di pagina

#### Scenario: Token assente o senza identificativo utente
- **WHEN** la richiesta arriva a Bookings senza intestazione `Authorization: Bearer`, oppure con un token firmato correttamente ma senza un claim `sub` che sia un GUID
- **THEN** la risposta è `401`; senza token la restituisce l'autenticazione di Bookings prima dell'endpoint, mentre con il token senza `sub` la restituisce l'endpoint con titolo «Accesso richiesto» e messaggio «Effettua l'accesso per vedere le tue prenotazioni.»

#### Scenario: Token non valido
- **WHEN** la richiesta arriva a Bookings con un token firmato con un'altra chiave, scaduto o senza firma
- **THEN** la risposta è `401`, restituita dall'autenticazione di Bookings prima dell'endpoint, che non viene eseguito

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
`POST /api/bookings` MUST salvare con la prenotazione il nome dell'organizzatore letto dal claim `name` del token; il nome resta quello del momento della creazione. Se il claim manca, è vuoto o supera i 128 caratteri, la richiesta MUST essere rifiutata con `401` come quando manca `sub`, senza creare la prenotazione. Richiesta e risposta `201` non cambiano.

#### Scenario: Nome presente nel token
- **WHEN** l'utente con claim `name` = «Mario Rossi» crea una prenotazione con `POST /api/bookings`
- **THEN** la risposta è `201` come prima e la prenotazione compare in `GET /api/bookings/mine` con `organizerName` = «Mario Rossi»

#### Scenario: Claim name assente o vuoto
- **WHEN** l'utente invia `POST /api/bookings` con un token che ha un `sub` valido ma non ha il claim `name`, oppure lo ha vuoto
- **THEN** la risposta è `401` con titolo «Accesso richiesto» e messaggio «Effettua l'accesso per prenotare.» e nessuna prenotazione viene creata

#### Scenario: Claim name troppo lungo
- **WHEN** l'utente invia `POST /api/bookings` con un token che ha un `sub` valido e un claim `name` di 129 caratteri
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

### Requirement: Cancellazione di una prenotazione
Bookings SHALL esporre `DELETE /api/bookings/{id}` (`id` = GUID della prenotazione). Se l'utente può cancellarla e l'inizio non è ancora arrivato, Bookings MUST portare la prenotazione a `Status` = `Cancelled`, salvare in `CancelledAt` l'istante della cancellazione e rispondere `204` senza corpo. La riga MUST restare nel database e non tornare più `Confirmed` (INV-7); la cancellazione vale per tutti i membri.

#### Scenario: L'organizzatore cancella una prenotazione futura
- **WHEN** Studente1 ha prenotato l'«Aula A1» dopodomani dalle 10:00 alle 11:00 e invia `DELETE /api/bookings/{id}` per quella prenotazione
- **THEN** la risposta è `204`, la prenotazione ha `Status` = `Cancelled` e `CancelledAt` uguale all'istante della richiesta

#### Scenario: Non più tra le future dell'organizzatore
- **WHEN** Studente1 ha cancellato la propria prenotazione dell'«Aula A1» di dopodomani e chiama `GET /api/bookings/mine`
- **THEN** la prenotazione non compare in `upcoming` e compare in `cancelled`

#### Scenario: Non più tra le future dei membri
- **WHEN** Studente1 cancella una prenotazione di gruppo di cui Studente2 è membro e Studente2 chiama `GET /api/bookings/mine`
- **THEN** la prenotazione non compare in `upcoming` di Studente2 e compare nella sua lista `cancelled`

### Requirement: Fascia libera subito dopo la cancellazione
Dal momento in cui `DELETE /api/bookings/{id}` risponde `204`, la fascia della prenotazione cancellata MUST risultare libera in `GET /api/bookings/availability` per ogni utente e MUST NOT impedire una nuova prenotazione dello stesso spazio, né una nuova prenotazione dei suoi ex partecipanti nella stessa fascia (INV-1 e INV-8 contano solo le prenotazioni `Confirmed`).

#### Scenario: Fascia di nuovo libera nella disponibilità
- **WHEN** Studente1 cancella la prenotazione dell'«Aula A1» di dopodomani dalle 10:00 alle 11:00 e subito dopo Studente2 chiama `GET /api/bookings/availability` per l'«Aula A1» e quel giorno
- **THEN** le fasce delle 10:00 e delle 10:30 risultano libere

#### Scenario: Fascia prenotabile da un altro studente
- **WHEN** Studente1 ha cancellato la prenotazione dell'«Aula A1» di dopodomani dalle 10:00 alle 11:00 e Studente2 invia `POST /api/bookings` per l'«Aula A1» nella stessa fascia
- **THEN** la risposta è `201` e l'«Aula A1» ha una sola prenotazione `Confirmed` in quella fascia, quella di Studente2

### Requirement: Chi può cancellare una prenotazione
Solo l'organizzatore (`OrganizerId` uguale al claim `sub`) e un utente con ruolo `Staff` (claim `role`) MUST poter cancellare una prenotazione. Ogni altro utente autenticato, compresi i membri non organizzatori, MUST ricevere `403` con titolo «Cancellazione non consentita» e messaggio «Non puoi cancellare questa prenotazione.», anche se la prenotazione è già cancellata, e la prenotazione MUST restare invariata (`Status`, `CancelledAt`).

#### Scenario: Studente estraneo
- **WHEN** Studente3, che non è né organizzatore né membro, invia `DELETE /api/bookings/{id}` per una prenotazione futura di Studente1
- **THEN** la risposta è `403` con titolo «Cancellazione non consentita» e messaggio «Non puoi cancellare questa prenotazione.» e la prenotazione resta `Confirmed`

#### Scenario: Membro non organizzatore
- **WHEN** Studente2 è membro della prenotazione di gruppo futura organizzata da Studente1 e invia `DELETE /api/bookings/{id}` per quella prenotazione
- **THEN** la risposta è `403` con messaggio «Non puoi cancellare questa prenotazione.», la prenotazione resta `Confirmed` senza `CancelledAt` e compare ancora in `upcoming` per Studente1 e per Studente2

#### Scenario: Personale del campus
- **WHEN** un utente con ruolo `Staff`, che non è organizzatore né membro, invia `DELETE /api/bookings/{id}` per una prenotazione futura di Studente1
- **THEN** la risposta è `204` e la prenotazione è `Cancelled`

#### Scenario: Prenotazione già cancellata chiesta da un estraneo
- **WHEN** Studente3 invia `DELETE /api/bookings/{id}` per una prenotazione di Studente1 già cancellata
- **THEN** la risposta è `403` con messaggio «Non puoi cancellare questa prenotazione.» e `CancelledAt` resta invariato

#### Scenario: Prenotazione passata chiesta da un estraneo
- **WHEN** Studente3 invia `DELETE /api/bookings/{id}` per una prenotazione confermata di Studente1 terminata ieri
- **THEN** la risposta è `403`, perché il controllo di chi può cancellare viene prima del limite di tempo

### Requirement: Cancellazione solo prima dell'inizio
Bookings MUST rifiutare con `422`, titolo «Cancellazione non consentita» e messaggio «Non è possibile cancellare una prenotazione già iniziata o passata.», la cancellazione di una prenotazione `Confirmed` il cui inizio è uguale o precedente all'istante della richiesta (assunzione A5), per l'organizzatore e per lo Staff. La prenotazione MUST restare invariata.

#### Scenario: Prenotazione già iniziata
- **WHEN** Studente1 ha una prenotazione confermata dalle 10:00 alle 11:00 e alle 10:15 invia `DELETE /api/bookings/{id}`
- **THEN** la risposta è `422` con titolo «Cancellazione non consentita» e messaggio «Non è possibile cancellare una prenotazione già iniziata o passata.» e la prenotazione resta `Confirmed`

#### Scenario: Prenotazione passata
- **WHEN** Studente1 invia `DELETE /api/bookings/{id}` per una sua prenotazione confermata terminata ieri
- **THEN** la risposta è `422` con messaggio «Non è possibile cancellare una prenotazione già iniziata o passata.» e la prenotazione resta `Confirmed` nella lista `past`

#### Scenario: Inizio esattamente uguale all'istante della richiesta
- **WHEN** la richiesta di cancellazione dell'organizzatore arriva esattamente all'orario di inizio della prenotazione
- **THEN** la risposta è `422` e la prenotazione resta `Confirmed`

#### Scenario: Un istante prima dell'inizio
- **WHEN** la richiesta di cancellazione dell'organizzatore arriva un istante prima dell'orario di inizio
- **THEN** la risposta è `204` e la prenotazione è `Cancelled`

#### Scenario: Personale su una prenotazione iniziata
- **WHEN** un utente `Staff` invia `DELETE /api/bookings/{id}` per una prenotazione confermata già iniziata
- **THEN** la risposta è `422` e la prenotazione resta `Confirmed`

### Requirement: Cancellazione ripetuta
Se la prenotazione è già `Cancelled`, `DELETE /api/bookings/{id}` richiesto dall'organizzatore o da un utente `Staff` MUST rispondere `204` senza modificarla: `CancelledAt` MUST restare quello della prima cancellazione, anche se nel frattempo l'orario di inizio è passato.

#### Scenario: Seconda richiesta dell'organizzatore
- **WHEN** Studente1 ha cancellato la propria prenotazione e invia di nuovo `DELETE /api/bookings/{id}`
- **THEN** la risposta è `204` e `CancelledAt` è quello della prima cancellazione

#### Scenario: Prenotazione cancellata e poi iniziata
- **WHEN** Studente1 ha cancellato ieri una prenotazione che è iniziata oggi alle 10:00 e alle 10:30 invia di nuovo `DELETE /api/bookings/{id}`
- **THEN** la risposta è `204` e la prenotazione resta `Cancelled` con il `CancelledAt` di ieri

### Requirement: Prenotazione da cancellare inesistente
Se `id` non corrisponde a nessuna prenotazione salvata, `DELETE /api/bookings/{id}` MUST rispondere `404` con titolo «Prenotazione non trovata» e messaggio «La prenotazione richiesta non esiste.», senza modificare nulla. Una prenotazione la cui creazione non ha ancora completato il salvataggio non esiste ancora per la cancellazione.

#### Scenario: Id sconosciuto
- **WHEN** uno studente autenticato invia `DELETE /api/bookings/{id}` con un GUID che non corrisponde a nessuna prenotazione
- **THEN** la risposta è `404` con titolo «Prenotazione non trovata» e messaggio «La prenotazione richiesta non esiste.» e nessuna prenotazione cambia

#### Scenario: Prenotazione in corso di creazione
- **WHEN** arriva `DELETE /api/bookings/{id}` per l'id di una prenotazione la cui `POST /api/bookings` non ha ancora salvato la riga
- **THEN** la risposta è `404` con messaggio «La prenotazione richiesta non esiste.»; dopo il salvataggio la prenotazione è `Confirmed` e si può cancellare con una nuova richiesta

### Requirement: Accesso richiesto per cancellare
`DELETE /api/bookings/{id}` MUST essere riservato agli utenti con un token valido. Senza token o con un token non valido, l'autenticazione di Bookings MUST rispondere `401` prima dell'endpoint. Con un token valido senza un claim `sub` GUID, l'endpoint MUST rispondere `401` con titolo «Accesso richiesto» e messaggio «Effettua l'accesso per cancellare la prenotazione.». In entrambi i casi la prenotazione non cambia.

#### Scenario: Richiesta senza token
- **WHEN** una richiesta `DELETE /api/bookings/{id}` arriva a Bookings senza intestazione `Authorization`
- **THEN** la risposta è `401` e la prenotazione resta `Confirmed`

#### Scenario: Token valido senza identificativo utente
- **WHEN** una richiesta `DELETE /api/bookings/{id}` arriva con un token firmato correttamente ma senza claim `sub`
- **THEN** la risposta è `401` con titolo «Accesso richiesto» e messaggio «Effettua l'accesso per cancellare la prenotazione.» e la prenotazione resta `Confirmed`

#### Scenario: Chiamata attraverso il Gateway senza token
- **WHEN** un client chiama `DELETE /api/bookings/{id}` sul Gateway senza token
- **THEN** il Gateway risponde `401` e la richiesta non arriva a Bookings

### Requirement: Cancellazioni contemporanee
Con richieste contemporanee sulla stessa prenotazione, il passaggio a `Cancelled` MUST avvenire in un'unica operazione atomica che riesce solo se la prenotazione è ancora `Confirmed` e non è iniziata. Le altre richieste MUST ricevere la risposta che corrisponde allo stato riletto dopo il tentativo (`204`, `403`, `404` o `422`) e mai `500`.

#### Scenario: Due cancellazioni nello stesso istante
- **WHEN** l'organizzatore e un utente `Staff` inviano in parallelo `DELETE /api/bookings/{id}` per la stessa prenotazione futura
- **THEN** entrambe le risposte sono `204`, nessuna è `500` e la prenotazione è `Cancelled` con `CancelledAt` uguale all'istante delle richieste

#### Scenario: Stessa richiesta ripetuta in parallelo
- **WHEN** l'organizzatore invia in parallelo cinque `DELETE /api/bookings/{id}` per la stessa prenotazione futura
- **THEN** tutte le risposte sono `204`, nessuna è `500` e la prenotazione è `Cancelled` con `CancelledAt` uguale all'istante delle richieste
