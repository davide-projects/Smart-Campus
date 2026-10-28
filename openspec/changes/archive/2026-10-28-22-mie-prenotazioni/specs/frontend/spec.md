# Spec Delta: frontend

## Purpose

Descrive il comportamento visibile dell'applicazione Angular di SmartCampus: le pagine che studenti e personale usano per cercare gli spazi e gestire le proprie prenotazioni.

## ADDED Requirements

### Requirement: Accesso alla pagina «Le mie prenotazioni»
L'applicazione SHALL offrire la pagina `/my-bookings`, con titolo «Le mie prenotazioni», riservata agli utenti autenticati. La navigazione SHALL mostrare la voce «Le mie prenotazioni» a ogni utente autenticato (`Student` o `Staff`) e nasconderla a chi non ha fatto l'accesso.

#### Scenario: Utente non autenticato
- **WHEN** un visitatore senza sessione apre `/my-bookings`
- **THEN** viene portato al login e, dopo l'accesso, torna a `/my-bookings`

#### Scenario: Voce di menu per l'utente autenticato
- **WHEN** uno studente o un membro del personale ha fatto l'accesso
- **THEN** la navigazione mostra la voce «Le mie prenotazioni» che apre `/my-bookings`, con il titolo «Le mie prenotazioni»

#### Scenario: Voce di menu nascosta senza accesso
- **WHEN** un visitatore non ha fatto l'accesso
- **THEN** la navigazione non mostra la voce «Le mie prenotazioni»

#### Scenario: Sessione scaduta durante il caricamento
- **WHEN** il caricamento dell'elenco da `GET /api/bookings/mine` riceve `401`
- **THEN** la sessione viene chiusa e l'utente torna al login, come per le altre pagine

### Requirement: Sezioni in schede
La pagina SHALL mostrare le prenotazioni in tre schede, «Future», «Passate» e «Cancellate», con le liste `upcoming`, `past` e `cancelled` dell'API nell'ordine ricevuto. All'apertura della pagina SHALL essere selezionata la scheda «Future».

#### Scenario: Scheda iniziale
- **WHEN** lo studente apre la pagina
- **THEN** è selezionata la scheda «Future» e sono visibili solo le prenotazioni future

#### Scenario: Prenotazioni future in ordine cronologico
- **WHEN** lo studente ha una prenotazione dell'«Aula A1» (Edificio A) domani dalle 10:00 alle 11:00 e una della «Sala studio B2» (Edificio B) dopodomani dalle 14:00 alle 15:30
- **THEN** la scheda «Future» mostra prima l'«Aula A1» e poi la «Sala studio B2», ognuna con spazio, edificio, giorno e orario

#### Scenario: Consultare le passate
- **WHEN** lo studente seleziona la scheda «Passate»
- **THEN** vede solo le prenotazioni di `past`, con spazio, edificio, giorno e orario

#### Scenario: Consultare le cancellate
- **WHEN** lo studente seleziona la scheda «Cancellate»
- **THEN** vede solo le prenotazioni di `cancelled`, con spazio, edificio, giorno e orario

### Requirement: Giorno e orario nell'ora del campus
Giorno e orario di ogni voce SHALL essere mostrati in italiano e nell'ora del campus (`Europe/Rome`), qualunque sia il fuso del dispositivo, nel formato «giovedì 5 novembre 2026, 10:00–11:00».

#### Scenario: Formato della data
- **WHEN** una prenotazione va dal 5 novembre 2026 alle 10:00 alle 11:00 ora di Roma
- **THEN** la voce mostra «giovedì 5 novembre 2026, 10:00–11:00»

#### Scenario: Dispositivo in un altro fuso
- **WHEN** il browser è impostato sul fuso UTC e la prenotazione va dalle 10:00 alle 11:00 ora di Roma
- **THEN** la voce mostra comunque «10:00–11:00»

### Requirement: Edificio non noto
Se `buildingName` è `null` la voce SHALL mostrare «—» al posto dell'edificio.

#### Scenario: Spazio senza edificio
- **WHEN** una voce ricevuta dall'API ha `buildingName` = `null`
- **THEN** al posto dell'edificio compare «—»

### Requirement: Indicazione delle prenotazioni di gruppo
Le voci con `isGroup` = `true` SHALL mostrare «Prenotazione di gruppo» e l'organizzatore: «Organizzatore: Tu» se `isOrganizer` è vero, altrimenti «Organizzatore: <nome>», oppure «Organizzatore non disponibile» se `organizerName` è `null`. Le voci singole MUST NOT mostrare l'organizzatore.

#### Scenario: Studente membro di un gruppo
- **WHEN** Anna Bianchi è membro di una prenotazione organizzata da Mario Rossi
- **THEN** la voce mostra «Prenotazione di gruppo» e «Organizzatore: Mario Rossi»

#### Scenario: Studente organizzatore di un gruppo
- **WHEN** una voce ha `isGroup` = `true` e `isOrganizer` = `true`
- **THEN** la voce mostra «Prenotazione di gruppo» e «Organizzatore: Tu»

#### Scenario: Nome dell'organizzatore non noto
- **WHEN** una voce ha `isGroup` = `true`, `isOrganizer` = `false` e `organizerName` = `null`
- **THEN** la voce mostra «Prenotazione di gruppo» e «Organizzatore non disponibile»

#### Scenario: Prenotazione singola
- **WHEN** una voce ha `isGroup` = `false`
- **THEN** la voce non mostra né «Prenotazione di gruppo» né l'organizzatore

### Requirement: Messaggio per ogni scheda vuota
Ogni scheda senza prenotazioni SHALL mostrare un proprio messaggio: «Non hai prenotazioni future.» con il collegamento «Cerca uno spazio» a `/rooms` per «Future», «Non hai prenotazioni passate.» per «Passate», «Non hai prenotazioni cancellate.» per «Cancellate». Solo il messaggio delle future MUST avere il collegamento.

#### Scenario: Nessuna prenotazione futura
- **WHEN** `upcoming` è vuota
- **THEN** la scheda «Future» mostra «Non hai prenotazioni future.» e il collegamento «Cerca uno spazio»

#### Scenario: Collegamento alla ricerca
- **WHEN** lo studente attiva il collegamento «Cerca uno spazio»
- **THEN** si apre la pagina di ricerca degli spazi (`/rooms`)

#### Scenario: Nessuna prenotazione passata
- **WHEN** `past` è vuota e lo studente seleziona la scheda «Passate»
- **THEN** la scheda mostra «Non hai prenotazioni passate.» senza collegamento alla ricerca

#### Scenario: Nessuna prenotazione cancellata
- **WHEN** `cancelled` è vuota e lo studente seleziona la scheda «Cancellate»
- **THEN** la scheda mostra «Non hai prenotazioni cancellate.» senza collegamento alla ricerca

#### Scenario: Future vuote ma passate presenti
- **WHEN** `upcoming` è vuota e `past` contiene prenotazioni
- **THEN** la scheda «Future» mostra il proprio messaggio e la scheda «Passate» mostra le prenotazioni

### Requirement: Caricamento ed errore
Durante il caricamento la pagina SHALL mostrare un indicatore di avanzamento; se la chiamata fallisce per un motivo diverso da `401` SHALL mostrare «Non è stato possibile caricare le prenotazioni. Riprova più tardi.» con `role="alert"`, senza schede e senza messaggi di scheda vuota.

#### Scenario: Errore del servizio
- **WHEN** `GET /api/bookings/mine` risponde `500` o non risponde
- **THEN** la pagina mostra il messaggio di errore e non mostra «Non hai prenotazioni future.»

### Requirement: Uso da tastiera
Le schede SHALL essere raggiungibili con Tab, si cambiano con le frecce e si attivano con Invio o Spazio; il collegamento «Cerca uno spazio» SHALL essere raggiungibile con Tab e attivabile con Invio. Il focus MUST essere visibile.

#### Scenario: Navigazione da tastiera
- **WHEN** lo studente usa solo la tastiera sulla pagina
- **THEN** può passare tra le schede «Future», «Passate» e «Cancellate» e raggiungere il collegamento «Cerca uno spazio»

### Requirement: Larghezza mobile
A 375px di larghezza la pagina SHALL mostrare le tre schede e ogni voce per intero, senza scorrimento orizzontale, e la voce «Le mie prenotazioni» SHALL essere nel menu a scomparsa come le altre pagine.

#### Scenario: Schermo da 375px
- **WHEN** lo studente apre la pagina su uno schermo largo 375px
- **THEN** le etichette delle tre schede e spazio, edificio, giorno, orario e indicazione di gruppo di ogni voce sono leggibili senza scorrere in orizzontale
