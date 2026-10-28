# Spec Delta: bookings

## Purpose

Descrive il comportamento osservabile del servizio Bookings: prenotazioni degli spazi senza conflitti, consultazione delle proprie prenotazioni e copia locale degli spazi prenotabili ricevuta da Rooms.

## ADDED Requirements

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
