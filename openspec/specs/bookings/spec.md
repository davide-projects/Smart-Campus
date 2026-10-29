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
- **THEN** la risposta è `422` con messaggio «Non è possibile cancellare una prenotazione già usata.» (`code` = `cancellation-already-used`) e la prenotazione resta in `upcoming`

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
Bookings MUST rifiutare con `422`, titolo «Cancellazione non consentita» e messaggio «Non è possibile cancellare una prenotazione già iniziata o passata.» la cancellazione di una prenotazione `Confirmed` senza check-in con inizio uguale o precedente all'istante della richiesta (assunzione A5), anche per lo Staff, lasciandola invariata. Con check-in vale prima «Nessuna cancellazione di una prenotazione usata».

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
Con richieste contemporanee sulla stessa prenotazione, il passaggio a `Cancelled` MUST avvenire in un'unica operazione atomica che riesce solo se la prenotazione è ancora `Confirmed`, non è iniziata e non ha un check-in. Le altre richieste MUST ricevere la risposta che corrisponde allo stato riletto dopo il tentativo (`204`, `403`, `404` o `422`) e mai `500`.

#### Scenario: Due cancellazioni nello stesso istante
- **WHEN** l'organizzatore e un utente `Staff` inviano in parallelo `DELETE /api/bookings/{id}` per la stessa prenotazione futura
- **THEN** entrambe le risposte sono `204`, nessuna è `500` e la prenotazione è `Cancelled` con `CancelledAt` uguale all'istante delle richieste

#### Scenario: Stessa richiesta ripetuta in parallelo
- **WHEN** l'organizzatore invia in parallelo cinque `DELETE /api/bookings/{id}` per la stessa prenotazione futura
- **THEN** tutte le risposte sono `204`, nessuna è `500` e la prenotazione è `Cancelled` con `CancelledAt` uguale all'istante delle richieste

#### Scenario: Check-in registrato tra la lettura e la scrittura
- **WHEN** la prenotazione riceve il check-in dopo che la `DELETE` l'ha letta e prima della scrittura
- **THEN** la `DELETE` non la cancella e risponde `422` con messaggio «Non è possibile cancellare una prenotazione già usata.» (`code` = `cancellation-already-used`), senza `500`

### Requirement: Creazione di una prenotazione singola
Bookings SHALL esporre `POST /api/bookings` con corpo `{ roomId, start, end, purpose }` (`start` ed `end` in ISO 8601 con fuso, `purpose` facoltativo). Se la richiesta rispetta tutte le regole, MUST creare una prenotazione `Confirmed` a nome dell'utente del claim `sub` e rispondere `201` con `id`, `roomId`, `roomName`, `start`, `end`, `participants` = 1 e `status` = `Confirmed`. Da quel momento la fascia è occupata per tutti.

#### Scenario: Prenotazione di una fascia libera
- **WHEN** Studente1 autenticato invia `POST /api/bookings` per l'«Aula A1», aperta 08:00–20:00 e libera, dopodomani dalle 10:00 alle 11:00 ora di Roma
- **THEN** la risposta è `201` con intestazione `Location` `/api/bookings/{id}` e corpo con `id`, `roomId` dell'«Aula A1», `roomName` = «Aula A1», `start` e `end` della fascia con il fuso, `participants` = 1 e `status` = «Confirmed»

#### Scenario: La fascia prenotata risulta occupata per tutti
- **WHEN** Studente1 ha prenotato l'«Aula A1» dopodomani dalle 10:00 alle 11:00 e un altro utente chiama `GET /api/bookings/availability` per l'«Aula A1» e quel giorno
- **THEN** le fasce delle 10:00 e delle 10:30 risultano occupate

#### Scenario: Motivo facoltativo
- **WHEN** lo studente invia la richiesta senza `purpose` oppure con un motivo di al massimo 200 caratteri
- **THEN** la prenotazione viene creata con `201`

### Requirement: Accesso richiesto per prenotare
`POST /api/bookings` MUST rifiutare con `401`, titolo «Accesso richiesto» e messaggio «Effettua l'accesso per prenotare.» la richiesta che arriva a Bookings senza intestazione `Authorization: Bearer` o con un token senza un claim `sub` valido, senza creare alcuna prenotazione. Attraverso il Gateway, una richiesta senza token o con un token non valido riceve `401` dal Gateway e non arriva a Bookings.

#### Scenario: Richiesta senza token
- **WHEN** una richiesta `POST /api/bookings` arriva a Bookings senza intestazione `Authorization`
- **THEN** la risposta è `401` con titolo «Accesso richiesto» e messaggio «Effettua l'accesso per prenotare.» e nessuna prenotazione viene creata

#### Scenario: Chiamata attraverso il Gateway senza token
- **WHEN** un client chiama `POST /api/bookings` sul Gateway senza token
- **THEN** il Gateway risponde `401` e la richiesta non arriva a Bookings

### Requirement: Spazio da prenotare inesistente
Se `roomId` non corrisponde a nessuno spazio nella copia locale di Bookings, `POST /api/bookings` MUST rispondere `404` con titolo «Spazio non trovato» e messaggio «Lo spazio richiesto non esiste.», senza creare alcuna prenotazione.

#### Scenario: Spazio sconosciuto
- **WHEN** lo studente invia una richiesta valida con un `roomId` che Bookings non conosce
- **THEN** la risposta è `404` con titolo «Spazio non trovato» e messaggio «Lo spazio richiesto non esiste.»

### Requirement: Nessuna sovrapposizione sullo stesso spazio
Bookings MUST rifiutare con `409`, titolo «Spazio già prenotato» e messaggio «Lo spazio è già prenotato in questa fascia.» una richiesta che si sovrappone, anche solo in parte, a una prenotazione `Confirmed` dello stesso spazio (INV-1), senza creare alcuna prenotazione. Gli intervalli sono semiaperti `[start, end)`: una prenotazione che inizia quando un'altra finisce MUST essere accettata.

#### Scenario: Sovrapposizione parziale
- **WHEN** l'«Aula A1» è prenotata dalle 10:00 alle 11:00 e Studente2 chiede la stessa aula dalle 10:30 alle 11:30
- **THEN** la risposta è `409` con titolo «Spazio già prenotato» e messaggio «Lo spazio è già prenotato in questa fascia.» e l'«Aula A1» ha ancora una sola prenotazione confermata

#### Scenario: Fascia contenuta in una prenotazione esistente
- **WHEN** l'«Aula A1» è prenotata dalle 10:00 alle 12:00 e Studente2 chiede la stessa aula dalle 10:30 alle 11:00
- **THEN** la risposta è `409` con messaggio «Lo spazio è già prenotato in questa fascia.» e nessuna prenotazione viene creata

#### Scenario: Prenotazioni adiacenti
- **WHEN** l'«Aula A1» è prenotata dalle 10:00 alle 11:00 e Studente2 chiede la stessa aula dalle 11:00 alle 12:00
- **THEN** la risposta è `201` e l'«Aula A1» ha due prenotazioni confermate

### Requirement: Nessuna sovrapposizione per la stessa persona
Bookings MUST rifiutare con `409`, titolo «Hai già una prenotazione» e messaggio «Hai già una prenotazione in questa fascia oraria.» la richiesta di un utente che è già organizzatore o membro di una prenotazione `Confirmed` sovrapposta, in qualunque spazio (INV-8), senza creare alcuna prenotazione. Le prenotazioni cancellate e quelle adiacenti non contano.

#### Scenario: Stessa persona in un altro spazio
- **WHEN** Studente1 ha una prenotazione confermata dell'«Aula A1» dalle 10:00 alle 11:00 e chiede la «Sala studio B2» dalle 10:30 alle 11:30 dello stesso giorno
- **THEN** la risposta è `409` con titolo «Hai già una prenotazione» e messaggio «Hai già una prenotazione in questa fascia oraria.» e la «Sala studio B2» non ha prenotazioni

#### Scenario: Membro di una prenotazione di gruppo
- **WHEN** Studente2 è membro di una prenotazione confermata di gruppo organizzata da Studente1 dalle 10:00 alle 11:00 e chiede un altro spazio dalle 10:00 alle 10:30
- **THEN** la risposta è `409` con messaggio «Hai già una prenotazione in questa fascia oraria.» e nessuna prenotazione viene creata

#### Scenario: Prenotazione della stessa persona adiacente
- **WHEN** Studente1 ha una prenotazione confermata dalle 10:00 alle 11:00 e chiede un altro spazio dalle 11:00 alle 12:00
- **THEN** la risposta è `201`

### Requirement: Altra prenotazione della stessa persona in elaborazione
Le richieste della stessa persona MUST essere elaborate una alla volta, anche su spazi diversi. Se una richiesta attende per più di 10 secondi che termini un'altra richiesta della stessa persona, Bookings MUST rispondere `409` con titolo «Prenotazione in corso» e messaggio «Hai un'altra prenotazione in corso, riprova tra qualche secondo.», senza salvare nulla.

#### Scenario: Attesa oltre i 10 secondi
- **WHEN** una richiesta di Studente1 è ancora in elaborazione dopo 10 secondi e arriva un'altra richiesta di Studente1
- **THEN** la seconda richiesta riceve `409` con titolo «Prenotazione in corso» e messaggio «Hai un'altra prenotazione in corso, riprova tra qualche secondo.» e non crea alcuna prenotazione

### Requirement: Prenotazioni contemporanee sullo stesso spazio
Con richieste contemporanee di utenti diversi per la stessa fascia libera dello stesso spazio, Bookings MUST confermarne esattamente una; tutte le altre MUST ricevere `409` con il messaggio «Lo spazio è già prenotato in questa fascia.». La garanzia SHALL essere verificata su SQL Server reale e restare valida su più esecuzioni consecutive.

#### Scenario: Venti richieste nello stesso istante
- **WHEN** 20 studenti diversi inviano in parallelo `POST /api/bookings` per l'«Aula A1» dopodomani dalle 10:00 alle 11:00, fascia libera
- **THEN** esattamente una risposta è `201`, le altre 19 sono `409` con titolo «Spazio già prenotato», e nel database l'«Aula A1» ha una sola prenotazione confermata in quella fascia

### Requirement: Prenotazioni contemporanee della stessa persona
Con richieste contemporanee della stessa persona per fasce sovrapposte su spazi diversi, Bookings MUST confermarne al massimo una; le altre MUST ricevere `409` e non creare prenotazioni.

#### Scenario: Stessa persona su due spazi nello stesso istante
- **WHEN** Studente1 invia in parallelo due `POST /api/bookings` per l'«Aula A1» e per la «Sala studio B2», entrambe libere, dopodomani dalle 10:00 alle 11:00
- **THEN** una risposta è `201`, l'altra è `409`, e nel database Studente1 ha una sola prenotazione confermata in quella fascia

### Requirement: Orario della prenotazione valido
Prima di controllare lo spazio, Bookings MUST rifiutare con `422` le richieste con orario non valido, senza creare alcuna prenotazione: fine non successiva all'inizio (titolo «Orario non valido», messaggio «L'orario di fine deve essere successivo a quello di inizio.»), inizio nel passato («Orario nel passato», «Non è possibile prenotare nel passato.»), inizio o fine non all'ora esatta o alla mezz'ora («Orario non valido», «Le prenotazioni iniziano e finiscono all'ora esatta o alla mezz'ora.»).

#### Scenario: Prenotazione nel passato
- **WHEN** lo studente chiede una fascia che inizia ieri alle 10:00
- **THEN** la risposta è `422` con titolo «Orario nel passato» e messaggio «Non è possibile prenotare nel passato.»

#### Scenario: Fine non successiva all'inizio
- **WHEN** lo studente chiede una fascia dalle 11:00 alle 10:00
- **THEN** la risposta è `422` con titolo «Orario non valido» e messaggio «L'orario di fine deve essere successivo a quello di inizio.»

#### Scenario: Orario non sulla mezz'ora
- **WHEN** lo studente chiede una fascia dalle 10:15 alle 11:00
- **THEN** la risposta è `422` con titolo «Orario non valido» e messaggio «Le prenotazioni iniziano e finiscono all'ora esatta o alla mezz'ora.»

### Requirement: Durata e anticipo massimi
Bookings MUST rifiutare con `422` una prenotazione più lunga di 4 ore (titolo «Durata eccessiva», messaggio «Una prenotazione può durare al massimo 4 ore.») o che inizia oltre 14 giorni dopo il momento della richiesta (titolo «Troppo in anticipo», messaggio «Si può prenotare al massimo 14 giorni prima.»). Una durata di esattamente 4 ore e un inizio esattamente a 14 giorni MUST essere accettati.

#### Scenario: Più lunga di 4 ore
- **WHEN** lo studente chiede una fascia dalle 09:00 alle 13:30
- **THEN** la risposta è `422` con titolo «Durata eccessiva» e messaggio «Una prenotazione può durare al massimo 4 ore.»

#### Scenario: Esattamente 4 ore
- **WHEN** lo studente chiede una fascia libera dalle 09:00 alle 13:00 dentro l'orario di apertura
- **THEN** la risposta è `201`

#### Scenario: Oltre 14 giorni in anticipo
- **WHEN** lo studente chiede una fascia che inizia 15 giorni dopo il momento della richiesta
- **THEN** la risposta è `422` con titolo «Troppo in anticipo» e messaggio «Si può prenotare al massimo 14 giorni prima.»

### Requirement: Lunghezza del motivo
Bookings MUST rifiutare con `422`, titolo «Motivo troppo lungo» e messaggio «Il motivo può contenere al massimo 200 caratteri.» una richiesta con `purpose` più lungo di 200 caratteri.

#### Scenario: Motivo di 201 caratteri
- **WHEN** lo studente invia una richiesta con un motivo di 201 caratteri
- **THEN** la risposta è `422` con titolo «Motivo troppo lungo» e messaggio «Il motivo può contenere al massimo 200 caratteri.»

### Requirement: Spazio prenotabile e aperto nella fascia
Bookings MUST rifiutare con `422`, senza creare prenotazioni: uno spazio dismesso o non prenotabile («Spazio non prenotabile», «Questo spazio non è prenotabile.»); una fascia su due giorni nell'ora del campus («Orario non valido», «La prenotazione deve iniziare e finire nello stesso giorno.»); un giorno di chiusura («Fuori orario», «Lo spazio è chiuso in questo giorno.»); una fascia fuori dall'orario di apertura («Fuori orario», «Lo spazio è aperto dalle HH:mm alle HH:mm.»).

#### Scenario: Fuori dall'orario di apertura
- **WHEN** lo studente chiede l'«Aula A1», aperta 08:00–20:00, dalle 19:00 alle 21:00
- **THEN** la risposta è `422` con titolo «Fuori orario» e messaggio «Lo spazio è aperto dalle 08:00 alle 20:00.»

#### Scenario: Fine esattamente alla chiusura
- **WHEN** lo studente chiede l'«Aula A1», aperta 08:00–20:00, dalle 19:00 alle 20:00 ed è libera
- **THEN** la risposta è `201`

#### Scenario: Giorno di chiusura
- **WHEN** lo studente chiede uno spazio in un giorno senza orario di apertura
- **THEN** la risposta è `422` con titolo «Fuori orario» e messaggio «Lo spazio è chiuso in questo giorno.»

#### Scenario: Spazio non prenotabile
- **WHEN** lo studente chiede uno spazio dismesso oppure non prenotabile
- **THEN** la risposta è `422` con titolo «Spazio non prenotabile» e messaggio «Questo spazio non è prenotabile.»

#### Scenario: Fascia a cavallo di due giorni
- **WHEN** lo studente chiede una fascia che inizia alle 23:30 e finisce alle 00:30 del giorno dopo, ora di Roma
- **THEN** la risposta è `422` con titolo «Orario non valido» e messaggio «La prenotazione deve iniziare e finire nello stesso giorno.»

### Requirement: Check-in di una prenotazione
Bookings SHALL esporre `POST /api/bookings/{id}/check-in` (`id` = GUID della prenotazione, nessun corpo). Se il check-in è consentito, Bookings MUST registrare un `AttendanceRecord` `CheckIn` con `RecordedAt` = istante della richiesta e `RecordedBy` = claim `sub`, e rispondere `200` con il corpo `{ "checkedInAt": <RecordedAt, ISO 8601 con fuso> }`. Da quel momento la prenotazione risulta usata. `Status` MUST restare invariato.

#### Scenario: L'organizzatore fa il check-in all'inizio
- **WHEN** Studente1 ha una prenotazione confermata dell'«Aula A1» oggi dalle 10:00 alle 11:00 e alle 10:05 invia `POST /api/bookings/{id}/check-in`
- **THEN** la risposta è `200` con `checkedInAt` uguale all'istante delle 10:05, esiste un solo `AttendanceRecord` `CheckIn` per la prenotazione con `RecordedBy` = Studente1, e la prenotazione resta `Confirmed`

### Requirement: Finestra del check-in
Per l'organizzatore e i membri il check-in MUST essere accettato solo se `Start − 15 min ≤ adesso ≤ Start + 15 min` (assunzione A6), estremi inclusi. Fuori dalla finestra Bookings MUST rispondere `422` con titolo «Check-in non consentito» e messaggio «Il check-in è possibile da 15 minuti prima a 15 minuti dopo l'inizio.» (`code` = `check-in-outside-window`), senza registrare nulla. Un utente con ruolo `Staff` MUST NOT essere soggetto alla finestra.

#### Scenario: Esattamente 15 minuti prima dell'inizio
- **WHEN** l'organizzatore di una prenotazione che inizia alle 10:00 invia il check-in alle 09:45:00
- **THEN** la risposta è `200` e il check-in è registrato

#### Scenario: Esattamente 15 minuti dopo l'inizio
- **WHEN** un membro di una prenotazione che inizia alle 10:00 invia il check-in alle 10:15:00
- **THEN** la risposta è `200` e il check-in è registrato

#### Scenario: Un istante prima della finestra
- **WHEN** l'organizzatore di una prenotazione che inizia alle 10:00 invia il check-in un istante prima delle 09:45:00
- **THEN** la risposta è `422` con titolo «Check-in non consentito» e messaggio «Il check-in è possibile da 15 minuti prima a 15 minuti dopo l'inizio.» (`code` = `check-in-outside-window`) e nessun `AttendanceRecord` è registrato

#### Scenario: Un istante dopo la finestra
- **WHEN** l'organizzatore di una prenotazione che inizia alle 10:00 invia il check-in un istante dopo le 10:15:00
- **THEN** la risposta è `422` con messaggio «Il check-in è possibile da 15 minuti prima a 15 minuti dopo l'inizio.» (`code` = `check-in-outside-window`) e nessun `AttendanceRecord` è registrato

#### Scenario: Prenotazione del giorno dopo
- **WHEN** Studente1 invia il check-in per una sua prenotazione confermata di domani
- **THEN** la risposta è `422` con messaggio «Il check-in è possibile da 15 minuti prima a 15 minuti dopo l'inizio.» (`code` = `check-in-outside-window`) e la prenotazione non risulta usata

#### Scenario: Personale fuori dalla finestra
- **WHEN** un utente `Staff`, che non è organizzatore né membro, invia il check-in alle 10:40 per una prenotazione confermata iniziata alle 10:00
- **THEN** la risposta è `200` e il check-in è registrato con `RecordedBy` = l'utente `Staff`

### Requirement: Chi può fare il check-in
Solo l'organizzatore (`OrganizerId` uguale al claim `sub`), un membro (`ReservationMember` con `UserId` uguale al claim `sub`) e un utente con ruolo `Staff` (claim `role`) MUST poter fare il check-in. Ogni altro utente autenticato MUST ricevere `403` con titolo «Check-in non consentito» e messaggio «Non puoi fare il check-in di questa prenotazione.» (`code` = `check-in-forbidden`), qualunque sia lo stato della prenotazione, e nessun `AttendanceRecord` MUST essere registrato.

#### Scenario: Membro di una prenotazione di gruppo
- **WHEN** Studente2 è membro della prenotazione di gruppo organizzata da Studente1, dalle 10:00 alle 11:00, e alle 09:50 invia il check-in
- **THEN** la risposta è `200` e il check-in è registrato con `RecordedBy` = Studente2

#### Scenario: Studente estraneo
- **WHEN** Studente3, che non è né organizzatore né membro, invia il check-in alle 10:05 per la prenotazione di Studente1 iniziata alle 10:00
- **THEN** la risposta è `403` con titolo «Check-in non consentito» e messaggio «Non puoi fare il check-in di questa prenotazione.» (`code` = `check-in-forbidden`) e la prenotazione non risulta usata

#### Scenario: Estraneo su una prenotazione cancellata
- **WHEN** Studente3 invia il check-in per una prenotazione cancellata di Studente1
- **THEN** la risposta è `403` con `code` = `check-in-forbidden`, perché il controllo di chi può fare il check-in viene prima delle regole sullo stato

### Requirement: Ordine delle regole del check-in
Quando più regole sono violate, Bookings MUST rispondere con la prima nell'ordine: chi può fare il check-in (`403`), prenotazione cancellata, prenotazione segnata no-show, check-in già registrato, finestra (`422`).

#### Scenario: Prenotazione cancellata fuori finestra
- **WHEN** l'organizzatore invia il check-in per una sua prenotazione cancellata che inizia domani
- **THEN** la risposta è `422` con messaggio «Non è possibile fare il check-in di una prenotazione cancellata.» (`code` = `check-in-cancelled`)

#### Scenario: Check-in già registrato fuori finestra
- **WHEN** l'organizzatore ha fatto il check-in alle 10:00 e alle 10:30 lo invia di nuovo
- **THEN** la risposta è `422` con messaggio «Il check-in di questa prenotazione è già stato registrato.» (`code` = `check-in-already-recorded`)

### Requirement: Check-in valido per tutto il gruppo
Un solo check-in, fatto dall'organizzatore, da un membro o dallo `Staff`, MUST segnare come usata la prenotazione per tutti: in `GET /api/bookings/mine` dell'organizzatore e di ogni membro la voce MUST avere lo stesso `checkedInAt`.

#### Scenario: Check-in di un membro visto dal gruppo
- **WHEN** nella prenotazione di gruppo di Studente1 con i membri Studente2 e Studente4, Studente2 fa il check-in alle 09:55 e poi Studente1 e Studente4 chiamano `GET /api/bookings/mine`
- **THEN** per entrambi la voce ha `checkedInAt` uguale all'istante delle 09:55

#### Scenario: Check-in dell'organizzatore visto dai membri
- **WHEN** Studente1 organizza una prenotazione con il membro Studente2, fa il check-in alle 10:00 e Studente2 chiama `GET /api/bookings/mine`
- **THEN** la voce di Studente2 ha `checkedInAt` uguale all'istante delle 10:00

### Requirement: Un solo check-in per prenotazione
Una prenotazione MUST avere al massimo un `AttendanceRecord`, di tipo `CheckIn` o `NoShow`. Se il check-in è già registrato, una nuova richiesta di chiunque possa fare il check-in MUST ricevere `422` con titolo «Check-in non consentito» e messaggio «Il check-in di questa prenotazione è già stato registrato.» (`code` = `check-in-already-recorded`), e il record esistente MUST restare invariato (`RecordedAt`, `RecordedBy`).

#### Scenario: Secondo check-in della stessa persona
- **WHEN** Studente1 ha fatto il check-in alle 10:00 e alle 10:02 invia di nuovo `POST /api/bookings/{id}/check-in`
- **THEN** la risposta è `422` con messaggio «Il check-in di questa prenotazione è già stato registrato.» (`code` = `check-in-already-recorded`) e il check-in resta quello delle 10:00 di Studente1

#### Scenario: Secondo check-in di un altro membro del gruppo
- **WHEN** Studente2 ha fatto il check-in sulla prenotazione di gruppo e Studente1, organizzatore, invia il check-in sulla stessa prenotazione
- **THEN** la risposta è `422` con messaggio «Il check-in di questa prenotazione è già stato registrato.» (`code` = `check-in-already-recorded`) e `RecordedBy` resta Studente2

#### Scenario: Personale dopo il check-in di uno studente
- **WHEN** Studente1 ha fatto il check-in e un utente `Staff` invia il check-in sulla stessa prenotazione
- **THEN** la risposta è `422` con messaggio «Il check-in di questa prenotazione è già stato registrato.» (`code` = `check-in-already-recorded`)

### Requirement: Nessun check-in su una prenotazione cancellata
Bookings MUST rifiutare con `422`, titolo «Check-in non consentito» e messaggio «Non è possibile fare il check-in di una prenotazione cancellata.» (`code` = `check-in-cancelled`), il check-in di una prenotazione `Cancelled`, anche per lo `Staff` e anche dentro la finestra, senza registrare nulla.

#### Scenario: Organizzatore su una prenotazione cancellata
- **WHEN** Studente1 ha cancellato la prenotazione delle 10:00 e alle 09:50 invia il check-in
- **THEN** la risposta è `422` con messaggio «Non è possibile fare il check-in di una prenotazione cancellata.» (`code` = `check-in-cancelled`) e nessun `AttendanceRecord` è registrato

#### Scenario: Personale su una prenotazione cancellata
- **WHEN** un utente `Staff` invia il check-in per una prenotazione cancellata
- **THEN** la risposta è `422` con messaggio «Non è possibile fare il check-in di una prenotazione cancellata.» (`code` = `check-in-cancelled`)

### Requirement: Nessun check-in su una prenotazione segnata no-show
Se la prenotazione ha un `AttendanceRecord` di tipo `NoShow`, Bookings MUST rifiutare il check-in con `422`, titolo «Check-in non consentito» e messaggio «La prenotazione è già stata segnata come non usata.» (`code` = `check-in-no-show`), anche per lo `Staff`, senza registrare nulla.

#### Scenario: Prenotazione già segnata come non presentata
- **WHEN** la prenotazione di Studente1 dalle 10:00 alle 11:00 ha un record `NoShow` e Studente1 alle 10:10 invia il check-in
- **THEN** la risposta è `422` con messaggio «La prenotazione è già stata segnata come non usata.» (`code` = `check-in-no-show`) e la prenotazione non ha alcun record `CheckIn`

#### Scenario: Personale su una prenotazione no-show
- **WHEN** un utente `Staff` invia il check-in per una prenotazione con un record `NoShow`
- **THEN** la risposta è `422` con messaggio «La prenotazione è già stata segnata come non usata.» (`code` = `check-in-no-show`)

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
Con check-in contemporanei sulla stessa prenotazione Bookings MUST registrare al massimo un `AttendanceRecord`; le altre richieste MUST ricevere il `422` del record presente dopo il tentativo («già registrato», `code` = `check-in-already-recorded`, per `CheckIn`; «non usata», `code` = `check-in-no-show`, per `NoShow`), mai `500`. La garanzia SHALL essere verificata su SQL Server reale.

#### Scenario: Organizzatore e membro nello stesso istante
- **WHEN** alle 10:00 l'organizzatore e un membro inviano in parallelo il check-in sulla stessa prenotazione iniziata alle 10:00
- **THEN** una risposta è `200`, l'altra è `422` con messaggio «Il check-in di questa prenotazione è già stato registrato.» (`code` = `check-in-already-recorded`), nessuna è `500` e la prenotazione ha un solo record `CheckIn`

#### Scenario: Stessa richiesta ripetuta in parallelo
- **WHEN** l'organizzatore invia in parallelo cinque check-in sulla stessa prenotazione nella finestra
- **THEN** esattamente una risposta è `200`, le altre quattro sono `422`, nessuna è `500` e la prenotazione ha un solo record `CheckIn`

### Requirement: Nessuna cancellazione di una prenotazione usata
`DELETE /api/bookings/{id}` dell'organizzatore o dello `Staff` su una prenotazione `Confirmed` con un record `CheckIn` MUST ricevere `422` «Cancellazione non consentita» / «Non è possibile cancellare una prenotazione già usata.» (`code` = `cancellation-already-used`), anche prima dell'inizio, e la prenotazione MUST restare invariata. Il controllo MUST venire dopo `403` e dopo la cancellazione ripetuta (`204`), prima del limite di tempo.

#### Scenario: Check-in prima dell'inizio e poi cancellazione
- **WHEN** Studente1 ha fatto il check-in alle 09:50 sulla prenotazione delle 10:00 e alle 09:55 invia `DELETE /api/bookings/{id}`
- **THEN** la risposta è `422` con titolo «Cancellazione non consentita» e messaggio «Non è possibile cancellare una prenotazione già usata.» (`code` = `cancellation-already-used`) e la prenotazione resta `Confirmed` con `checkedInAt` invariato

#### Scenario: Personale su una prenotazione usata
- **WHEN** un utente `Staff` ha registrato il check-in su una prenotazione di dopodomani e poi invia `DELETE /api/bookings/{id}`
- **THEN** la risposta è `422` con messaggio «Non è possibile cancellare una prenotazione già usata.» (`code` = `cancellation-already-used`)

#### Scenario: Prenotazione usata e già iniziata
- **WHEN** l'organizzatore invia `DELETE /api/bookings/{id}` alle 10:30 per una prenotazione delle 10:00 con check-in
- **THEN** la risposta è `422` con messaggio «Non è possibile cancellare una prenotazione già usata.» (`code` = `cancellation-already-used`), perché il controllo sull'uso viene prima del limite di tempo

#### Scenario: Estraneo su una prenotazione usata
- **WHEN** Studente3, né organizzatore né membro, invia `DELETE /api/bookings/{id}` per una prenotazione usata di Studente1
- **THEN** la risposta è `403` con messaggio «Non puoi cancellare questa prenotazione.»

### Requirement: Check-in e cancellazione contemporanei
Con un check-in e una `DELETE /api/bookings/{id}` contemporanei sulla stessa prenotazione, Bookings MUST far riuscire al massimo una delle due. L'altra MUST ricevere il `422` corrispondente allo stato dopo la prima, mai `500`. Una prenotazione `Cancelled` MUST NOT avere un record `CheckIn`.

#### Scenario: Check-in e cancellazione nello stesso istante
- **WHEN** alle 09:55 l'organizzatore invia `DELETE /api/bookings/{id}` e un membro invia `POST /api/bookings/{id}/check-in` in parallelo sulla stessa prenotazione delle 10:00
- **THEN** una sola delle due riesce. Se vince la cancellazione, la risposta è `204`, il check-in riceve `422` «Non è possibile fare il check-in di una prenotazione cancellata.» (`code` = `check-in-cancelled`) e la prenotazione è `Cancelled` senza record. Se vince il check-in, la risposta è `200`, la cancellazione riceve `422` «Non è possibile cancellare una prenotazione già usata.» (`code` = `cancellation-already-used`) e la prenotazione resta `Confirmed` con un record `CheckIn`. Nessuna risposta è `500`

#### Scenario: Nessuna prenotazione cancellata e usata
- **WHEN** check-in e cancellazione della stessa prenotazione vengono ripetuti in parallelo su molte prenotazioni
- **THEN** nessuna prenotazione risulta `Cancelled` con un record `CheckIn`
