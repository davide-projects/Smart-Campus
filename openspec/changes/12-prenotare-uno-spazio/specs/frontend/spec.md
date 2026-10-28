# Spec Delta: frontend

## Purpose

Descrive il comportamento visibile dell'applicazione Angular di SmartCampus: le pagine che studenti e personale usano per cercare gli spazi e gestire le proprie prenotazioni.

## ADDED Requirements

### Requirement: Pulsante «Prenota spazio» nel dettaglio dello spazio
Nel dettaglio di uno spazio prenotabile il pulsante «Prenota spazio» SHALL essere attivo e aprire `/rooms/:id/book` per quello spazio. Per uno spazio non prenotabile il dettaglio SHALL continuare a mostrare il pulsante disabilitato «Non prenotabile».

#### Scenario: Spazio prenotabile
- **WHEN** lo studente apre il dettaglio dell'«Aula A1», prenotabile, e attiva «Prenota spazio»
- **THEN** si apre `/rooms/<id dell'Aula A1>/book`

#### Scenario: Spazio non prenotabile
- **WHEN** lo studente apre il dettaglio di uno spazio non prenotabile
- **THEN** il pulsante «Non prenotabile» è disabilitato e non c'è un pulsante «Prenota spazio» attivo

### Requirement: Accesso alla pagina di prenotazione
La pagina `/rooms/:id/book` SHALL essere riservata agli utenti autenticati. Un visitatore senza sessione MUST essere portato al login e, dopo l'accesso, tornare alla stessa pagina di prenotazione. La pagina SHALL mostrare il nome dello spazio da prenotare; se lo spazio non esiste o è dismesso SHALL mostrare «Spazio non trovato» senza il modulo.

#### Scenario: Visitatore non autenticato
- **WHEN** un visitatore senza sessione attiva «Prenota spazio» oppure apre direttamente `/rooms/<id>/book`
- **THEN** viene portato al login e, dopo l'accesso, torna a `/rooms/<id>/book`

#### Scenario: Nome dello spazio
- **WHEN** uno studente autenticato apre la pagina di prenotazione dell'«Aula A1»
- **THEN** la pagina mostra «Aula A1» e il modulo di prenotazione

#### Scenario: Spazio inesistente
- **WHEN** uno studente autenticato apre `/rooms/<id>/book` con l'id di uno spazio che Rooms non trova
- **THEN** la pagina mostra «Spazio non trovato» e non mostra il modulo

### Requirement: Giorni e orari proposti nel modulo
Il giorno SHALL essere scelto solo tra oggi e oggi + 14 giorni (date dell'ora del campus); i giorni in cui lo spazio è chiuso MUST NOT essere selezionabili. Ora di inizio e di fine SHALL essere scelte solo all'ora esatta o alla mezz'ora e solo dentro l'orario di apertura dello spazio nel giorno scelto, letto da `GET /api/rooms/{id}`. L'ora di fine SHALL proporre solo orari successivi all'ora di inizio scelta; se l'ora di inizio cambia e la fine scelta non è più successiva, l'ora di fine MUST essere azzerata. Il giorno SHALL potersi scegliere solo dal calendario: il campo «Giorno» MUST NOT accettare testo scritto a mano. Le regole restano verificate dal servizio: un rifiuto `422` SHALL comunque essere mostrato.

#### Scenario: Orari selezionabili
- **WHEN** lo studente apre la scelta dell'ora di inizio o di fine
- **THEN** ogni orario proposto termina in «:00» o «:30»

#### Scenario: Orari dentro l'apertura
- **WHEN** l'«Aula A1» è aperta il giovedì dalle 08:00 alle 20:00 e lo studente sceglie giovedì 5 novembre 2026, prima di scegliere l'ora di inizio
- **THEN** l'ora di inizio va da 08:00 a 19:30, l'ora di fine da 08:30 a 20:00, e non sono proposti orari fuori da questo intervallo

#### Scenario: Fine dopo l'inizio
- **WHEN** l'«Aula A1» è aperta giovedì 5 novembre 2026 dalle 08:00 alle 20:00 e lo studente sceglie l'ora di inizio 10:00
- **THEN** l'ora di fine va da 10:30 a 20:00

#### Scenario: Inizio spostato oltre la fine
- **WHEN** lo studente ha scelto 10:00–11:00 e poi cambia l'ora di inizio in 11:00
- **THEN** l'ora di fine torna vuota ed è segnalata come obbligatoria

#### Scenario: Giorno solo dal calendario
- **WHEN** lo studente prova a scrivere una data nel campo «Giorno»
- **THEN** il campo non accetta il testo e il giorno si sceglie dal calendario, anche da tastiera

#### Scenario: Giorni selezionabili
- **WHEN** oggi è il 5 novembre 2026 e lo studente apre il calendario del campo «Giorno»
- **THEN** sono selezionabili solo i giorni dal 5 al 19 novembre 2026 in cui lo spazio è aperto; i giorni precedenti, successivi o di chiusura sono disabilitati

### Requirement: Modulo di prenotazione
Il modulo SHALL avere i campi obbligatori «Giorno», «Ora di inizio» e «Ora di fine» e il campo facoltativo «Motivo», che MUST NOT accettare più di 200 caratteri. Il modulo SHALL inviare giorno e orari nell'ora del campus (`Europe/Rome`), qualunque sia il fuso del dispositivo, e non SHALL inviare una seconda richiesta finché la prima è in corso.

#### Scenario: Campi obbligatori mancanti
- **WHEN** lo studente prova a confermare senza giorno, ora di inizio o ora di fine
- **THEN** il campo mancante è segnalato come obbligatorio e nessuna richiesta viene inviata

#### Scenario: Motivo troppo lungo
- **WHEN** lo studente scrive nel campo «Motivo»
- **THEN** il campo non accetta più di 200 caratteri

#### Scenario: Orario inviato nell'ora del campus
- **WHEN** il browser è impostato sul fuso UTC e lo studente prenota il 5 novembre 2026 dalle 10:00 alle 11:00
- **THEN** la richiesta a `POST /api/bookings` contiene `start` = `2026-11-05T10:00:00+01:00` ed `end` = `2026-11-05T11:00:00+01:00`

#### Scenario: Ora legale
- **WHEN** lo studente prenota il 5 giugno 2026 dalle 10:00 alle 11:00
- **THEN** la richiesta contiene `start` con fuso `+02:00`

#### Scenario: Doppio invio
- **WHEN** lo studente attiva due volte di seguito la conferma mentre la richiesta è in corso
- **THEN** viene inviata una sola richiesta a `POST /api/bookings`

### Requirement: Conferma della prenotazione
Dopo una risposta `201` la pagina SHALL mostrare, nella stessa pagina e al posto del modulo, la conferma con il nome dello spazio, il giorno e l'orario della prenotazione, in italiano e nell'ora del campus, nel formato «giovedì 5 novembre 2026, 10:00–11:00». Sotto la conferma SHALL esserci il collegamento «Torna allo spazio», che apre `/rooms/:id`, e il pulsante «Prenota un'altra fascia», che ripropone il modulo vuoto per lo stesso spazio.

#### Scenario: Prenotazione confermata
- **WHEN** lo studente prenota l'«Aula A1» il 5 novembre 2026 dalle 10:00 alle 11:00 e il servizio risponde `201`
- **THEN** la pagina mostra la conferma con «Aula A1» e «giovedì 5 novembre 2026, 10:00–11:00» e non mostra più il modulo

#### Scenario: Torna allo spazio
- **WHEN** dopo la conferma lo studente attiva «Torna allo spazio»
- **THEN** si apre il dettaglio dell'«Aula A1» (`/rooms/<id>`)

#### Scenario: Prenota un'altra fascia
- **WHEN** dopo la conferma lo studente attiva «Prenota un'altra fascia»
- **THEN** la conferma lascia il posto al modulo vuoto per l'«Aula A1», senza messaggi di rifiuto

#### Scenario: Dispositivo in un altro fuso
- **WHEN** il browser è impostato sul fuso UTC e la risposta `201` riguarda una fascia dalle 10:00 alle 11:00 ora di Roma
- **THEN** la conferma mostra comunque «10:00–11:00»

### Requirement: Fascia prenotata occupata nella disponibilità
Dopo la conferma, la fascia prenotata SHALL risultare occupata nella vista della disponibilità giornaliera dello spazio, per lo studente che ha prenotato e per ogni altro utente.

#### Scenario: Disponibilità dopo la prenotazione
- **WHEN** lo studente ha prenotato l'«Aula A1» il 5 novembre 2026 dalle 10:00 alle 11:00 e un altro utente apre la disponibilità dell'«Aula A1» per quel giorno
- **THEN** le fasce dalle 10:00 alle 11:00 risultano occupate

### Requirement: Messaggi di rifiuto della prenotazione
Se `POST /api/bookings` risponde `409` o `422`, la pagina SHALL mostrare il messaggio `detail` della risposta, con `role="alert"`, mantenendo i valori inseriti nel modulo. Se il servizio non risponde o risponde con un altro errore SHALL mostrare «Non è stato possibile completare la prenotazione. Riprova più tardi.».

#### Scenario: Fuori dall'orario di apertura
- **WHEN** lo studente chiede l'«Aula A1» dalle 19:00 alle 21:00 e il servizio risponde `422` con `detail` «Lo spazio è aperto dalle 08:00 alle 20:00.»
- **THEN** la pagina mostra «Lo spazio è aperto dalle 08:00 alle 20:00.» e il modulo conserva giorno e orari scelti

#### Scenario: Durata, anticipo e passato
- **WHEN** il servizio risponde `422` con `detail` «Una prenotazione può durare al massimo 4 ore.», «Si può prenotare al massimo 14 giorni prima.» oppure «Non è possibile prenotare nel passato.»
- **THEN** la pagina mostra quel messaggio

#### Scenario: Stessa persona già impegnata
- **WHEN** il servizio risponde `409` con `detail` «Hai già una prenotazione in questa fascia oraria.»
- **THEN** la pagina mostra «Hai già una prenotazione in questa fascia oraria.»

#### Scenario: Altra prenotazione in corso
- **WHEN** il servizio risponde `409` con `detail` «Hai un'altra prenotazione in corso, riprova tra qualche secondo.»
- **THEN** la pagina mostra quel messaggio e lo studente può riprovare con lo stesso modulo

#### Scenario: Errore del servizio
- **WHEN** `POST /api/bookings` risponde `500` o non risponde
- **THEN** la pagina mostra «Non è stato possibile completare la prenotazione. Riprova più tardi.»

### Requirement: Collegamento alla disponibilità dopo un conflitto sullo spazio
Quando il rifiuto è il conflitto sullo spazio («Lo spazio è già prenotato in questa fascia.»), la pagina SHALL affiancare al messaggio un collegamento al dettaglio dello spazio (`/rooms/:id`, senza il giorno nell'URL), dove si consulta la disponibilità, così lo studente può scegliere un'altra fascia.

#### Scenario: Fascia già occupata
- **WHEN** il servizio risponde `409` con `detail` «Lo spazio è già prenotato in questa fascia.»
- **THEN** la pagina mostra il messaggio e un collegamento che apre `/rooms/<id>` dello stesso spazio

#### Scenario: Altri rifiuti senza collegamento
- **WHEN** il servizio risponde `409` con `detail` «Hai già una prenotazione in questa fascia oraria.»
- **THEN** la pagina non mostra il collegamento alla disponibilità

### Requirement: Sessione non valida durante la prenotazione
Se `POST /api/bookings` risponde `401`, la pagina SHALL mostrare il `detail` della risposta quando presente («Effettua l'accesso per prenotare.») e la sessione MUST essere chiusa come nelle altre pagine, riportando lo studente al login con ritorno alla pagina di prenotazione. Nessuna prenotazione risulta creata.

#### Scenario: Token rifiutato
- **WHEN** lo studente conferma e il servizio risponde `401`
- **THEN** la sessione viene chiusa e lo studente arriva al login con l'avviso di sessione scaduta e il ritorno a `/rooms/<id>/book`

### Requirement: Prenotazione da tastiera
Tutti i campi del modulo, la conferma e i collegamenti della pagina SHALL essere raggiungibili con Tab nell'ordine di lettura e utilizzabili senza mouse; la conferma SHALL potersi inviare con Invio. I messaggi di rifiuto e la conferma SHALL essere annunciati alle tecnologie assistive e il focus MUST essere visibile.

#### Scenario: Prenotazione solo da tastiera
- **WHEN** lo studente usa solo la tastiera sulla pagina di prenotazione
- **THEN** sceglie giorno, ora di inizio e di fine, scrive il motivo, invia la richiesta e legge la conferma o il messaggio di rifiuto senza usare il mouse

### Requirement: Prenotazione su schermo da 375px
A 375px di larghezza la pagina di prenotazione SHALL mostrare campi, pulsanti, messaggi e conferma per intero, senza scorrimento orizzontale.

#### Scenario: Schermo da 375px
- **WHEN** lo studente apre la pagina di prenotazione su uno schermo largo 375px
- **THEN** il modulo, i messaggi di rifiuto e la conferma con spazio, giorno e orario sono leggibili e utilizzabili senza scorrere in orizzontale
