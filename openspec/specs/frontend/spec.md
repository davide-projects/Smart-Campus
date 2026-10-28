# frontend Specification

## Purpose

Descrive il comportamento visibile dell'applicazione Angular di SmartCampus: le pagine che studenti e personale usano per cercare gli spazi e gestire le proprie prenotazioni.

## Requirements

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
Al primo caricamento della pagina SHALL comparire un indicatore di avanzamento; se quella chiamata fallisce per un motivo diverso da `401` SHALL mostrare «Non è stato possibile caricare le prenotazioni. Riprova più tardi.» con `role="alert"`, senza schede e senza messaggi di scheda vuota. Il ricaricamento dopo una cancellazione riuscita segue invece il requisito «Esito della cancellazione»: schede visibili e avviso sopra le schede se fallisce.

#### Scenario: Errore del servizio
- **WHEN** al primo caricamento della pagina `GET /api/bookings/mine` risponde `500` o non risponde
- **THEN** la pagina mostra il messaggio di errore e non mostra «Non hai prenotazioni future.»

#### Scenario: Indicatore al primo caricamento
- **WHEN** lo studente apre la pagina e la risposta di `GET /api/bookings/mine` non è ancora arrivata
- **THEN** la pagina mostra l'indicatore di avanzamento e non mostra le schede

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

### Requirement: Azione «Cancella» nelle prenotazioni future
Nella scheda «Future» di «Le mie prenotazioni», le voci con `isOrganizer` = `true` e inizio successivo all'ora del dispositivo, valutata al caricamento dell'elenco, SHALL mostrare il pulsante «Cancella». Le voci di cui l'utente è solo membro, quelle già iniziate e tutte le voci di «Passate» e «Cancellate» MUST NOT mostrarlo. Il nome accessibile del pulsante SHALL indicare spazio, giorno e orario della prenotazione.

#### Scenario: Prenotazione futura creata dallo studente
- **WHEN** Studente1 apre la pagina e nella scheda «Future» c'è la sua prenotazione dell'«Aula A1» di giovedì 5 novembre 2026 dalle 10:00 alle 11:00, non ancora iniziata
- **THEN** la voce mostra il pulsante «Cancella»

#### Scenario: Prenotazione di gruppo di cui lo studente è membro
- **WHEN** Studente2 apre la pagina e nella scheda «Future» c'è una prenotazione di gruppo futura organizzata da Studente1
- **THEN** la voce mostra «Prenotazione di gruppo» e «Organizzatore: <nome>» e non mostra il pulsante «Cancella»

#### Scenario: Prenotazione in corso
- **WHEN** Studente1 apre la pagina alle 10:30 e nella scheda «Future» c'è la sua prenotazione di oggi dalle 10:00 alle 11:00
- **THEN** la voce non mostra il pulsante «Cancella»

#### Scenario: Passate e cancellate
- **WHEN** lo studente seleziona la scheda «Passate» o la scheda «Cancellate»
- **THEN** nessuna voce mostra il pulsante «Cancella», nemmeno quelle che ha creato lui

### Requirement: Conferma della cancellazione
«Cancella» SHALL aprire un dialog «Cancellare la prenotazione?» con spazio, edificio («—» se non noto), giorno e orario e i pulsanti «Annulla» e «Cancella prenotazione». Solo «Cancella prenotazione» MUST inviare `DELETE /api/bookings/{id}`; «Annulla», Esc e il clic fuori MUST chiudere senza richieste. Durante la `DELETE` i due pulsanti MUST essere disabilitati ed Esc e il clic fuori MUST NOT chiudere il dialog.

#### Scenario: Dialog bloccato durante la richiesta
- **WHEN** lo studente ha attivato «Cancella prenotazione» e la risposta non è ancora arrivata
- **THEN** «Annulla» e «Cancella prenotazione» sono disabilitati, Esc e il clic fuori lasciano il dialog aperto, e all'arrivo di una risposta di errore i due pulsanti tornano attivi

#### Scenario: Riepilogo nel dialog
- **WHEN** lo studente attiva «Cancella» sulla prenotazione dell'«Aula A1» (Edificio A) di giovedì 5 novembre 2026 dalle 10:00 alle 11:00
- **THEN** si apre il dialog «Cancellare la prenotazione?» con «Aula A1», «Edificio A», «giovedì 5 novembre 2026, 10:00–11:00» e i pulsanti «Annulla» e «Cancella prenotazione»

#### Scenario: Lo studente annulla
- **WHEN** nel dialog lo studente attiva «Annulla» oppure preme Esc
- **THEN** il dialog si chiude, nessuna richiesta viene inviata e la prenotazione resta nella scheda «Future»

#### Scenario: Doppio invio
- **WHEN** lo studente attiva due volte di seguito «Cancella prenotazione» mentre la richiesta è in corso
- **THEN** viene inviata una sola richiesta `DELETE /api/bookings/{id}`

### Requirement: Esito della cancellazione
Dopo una risposta `204` il dialog SHALL chiudersi, il focus MUST andare sull'etichetta della scheda «Future» e la pagina SHALL mostrare una snackbar «Prenotazione cancellata.», senza pulsanti, che si chiude da sola dopo 5 secondi. L'elenco SHALL essere ricaricato da `GET /api/bookings/mine` con le schede sempre visibili e «Future» selezionata, senza indicatore di caricamento al loro posto; la prenotazione cancellata compare in «Cancellate».

#### Scenario: Cancellazione confermata
- **WHEN** lo studente conferma la cancellazione della prenotazione dell'«Aula A1» e il servizio risponde `204`
- **THEN** il dialog si chiude, compare «Prenotazione cancellata.», l'elenco viene richiesto di nuovo e la scheda «Future» resta selezionata senza l'«Aula A1»

#### Scenario: Schede visibili durante il ricaricamento
- **WHEN** dopo il `204` la risposta di `GET /api/bookings/mine` non è ancora arrivata
- **THEN** le schede restano visibili con «Future» selezionata e le voci già mostrate, e non compare la barra di caricamento al loro posto

#### Scenario: Snackbar di esito
- **WHEN** compare la snackbar «Prenotazione cancellata.»
- **THEN** non ha pulsanti e si chiude da sola dopo 5 secondi

#### Scenario: Focus dopo la cancellazione
- **WHEN** lo studente conferma da tastiera la cancellazione e il servizio risponde `204`
- **THEN** il focus è sull'etichetta della scheda «Future»

#### Scenario: Ricaricamento fallito
- **WHEN** dopo il `204` la richiesta `GET /api/bookings/mine` risponde `500` o non risponde
- **THEN** restano le schede con le voci già mostrate e sopra le schede compare «Non è stato possibile caricare le prenotazioni. Riprova più tardi.» con `role="alert"`

#### Scenario: La prenotazione compare tra le cancellate
- **WHEN** dopo la cancellazione confermata lo studente seleziona la scheda «Cancellate»
- **THEN** la scheda mostra l'«Aula A1» con spazio, edificio, giorno e orario

#### Scenario: Ultima prenotazione futura cancellata
- **WHEN** lo studente aveva una sola prenotazione futura e ne conferma la cancellazione con risposta `204`
- **THEN** la scheda «Future» mostra «Non hai prenotazioni future.» con il collegamento «Cerca uno spazio»

### Requirement: Cancellazione rifiutata
Con `403`, `404` o `422` il dialog SHALL restare aperto e mostrare il `detail` della risposta in un elemento con `role="alert"`; con altri errori (escluso `401`) o senza risposta SHALL mostrare «Non è stato possibile cancellare la prenotazione. Riprova più tardi.». L'elenco non cambia e lo studente chiude con «Annulla». Con `401` il dialog MUST chiudersi senza messaggi e la sessione MUST essere chiusa come nelle altre pagine, con ritorno a `/my-bookings` dopo il login.

#### Scenario: Prenotazione iniziata nel frattempo
- **WHEN** lo studente conferma la cancellazione e il servizio risponde `422` con `detail` «Non è possibile cancellare una prenotazione già iniziata o passata.»
- **THEN** il dialog resta aperto e mostra quel messaggio con `role="alert"`, e dopo «Annulla» la prenotazione è ancora nella scheda «Future»

#### Scenario: Cancellazione non consentita
- **WHEN** il servizio risponde `403` con `detail` «Non puoi cancellare questa prenotazione.»
- **THEN** il dialog resta aperto e mostra «Non puoi cancellare questa prenotazione.»

#### Scenario: Prenotazione non trovata
- **WHEN** il servizio risponde `404` con `detail` «La prenotazione richiesta non esiste.»
- **THEN** il dialog resta aperto e mostra «La prenotazione richiesta non esiste.»

#### Scenario: Errore del servizio
- **WHEN** `DELETE /api/bookings/{id}` risponde `500` o non risponde
- **THEN** il dialog resta aperto e mostra «Non è stato possibile cancellare la prenotazione. Riprova più tardi.» e la prenotazione resta nella scheda «Future»

#### Scenario: Sessione scaduta
- **WHEN** lo studente conferma la cancellazione e il servizio risponde `401`
- **THEN** il dialog si chiude senza mostrare messaggi, la sessione viene chiusa e lo studente arriva al login con l'avviso di sessione scaduta e il ritorno a `/my-bookings`

### Requirement: Cancellazione da tastiera
«Cancella» SHALL essere raggiungibile con Tab e attivabile con Invio o Spazio. Il focus MUST restare dentro il dialog finché è aperto, con «Annulla» e «Cancella prenotazione» raggiungibili con Tab; Esc SHALL valere come «Annulla». Senza cancellazione il focus MUST tornare sul pulsante «Cancella» di partenza; dopo una cancellazione riuscita sull'etichetta della scheda «Future». Il focus MUST essere visibile.

#### Scenario: Cancellazione solo da tastiera
- **WHEN** lo studente usa solo la tastiera sulla pagina
- **THEN** raggiunge «Cancella» di una prenotazione, apre il dialog, sceglie «Annulla» o «Cancella prenotazione» e legge l'esito o il messaggio di errore senza usare il mouse

#### Scenario: Ritorno del focus
- **WHEN** lo studente apre il dialog da tastiera e lo chiude con Esc
- **THEN** il focus torna sul pulsante «Cancella» della stessa prenotazione

### Requirement: Cancellazione su schermo da 375px
A 375px di larghezza il pulsante «Cancella», il dialog con il riepilogo, i suoi due pulsanti, i messaggi di errore e la snackbar SHALL essere leggibili e utilizzabili per intero, senza scorrimento orizzontale.

#### Scenario: Schermo da 375px
- **WHEN** lo studente apre la pagina su uno schermo largo 375px e attiva «Cancella»
- **THEN** il dialog mostra spazio, edificio, giorno, orario, «Annulla» e «Cancella prenotazione» senza scorrere in orizzontale

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
Il giorno SHALL essere scelto solo tra oggi e oggi + 14 giorni (date dell'ora del campus); i giorni in cui lo spazio è chiuso MUST NOT essere selezionabili. Ora di inizio e di fine SHALL essere scelte solo all'ora esatta o alla mezz'ora e solo dentro l'orario di apertura dello spazio nel giorno scelto, letto da `GET /api/rooms/{id}`. Nel giorno corrente (data di `Europe/Rome`) l'ora di inizio SHALL proporre solo le mezz'ore successive all'ora attuale del campus; le mezz'ore già passate, compresa quella che coincide con l'ora attuale, MUST NOT essere proposte. L'ora di fine SHALL proporre solo orari successivi all'ora di inizio scelta; se l'ora di inizio cambia e la fine scelta non è più successiva, l'ora di fine MUST essere azzerata. Il giorno SHALL potersi scegliere solo dal calendario: il campo «Giorno» MUST NOT accettare testo scritto a mano. Le regole restano verificate dal servizio: un rifiuto `422` SHALL comunque essere mostrato.

#### Scenario: Orari selezionabili
- **WHEN** lo studente apre la scelta dell'ora di inizio o di fine
- **THEN** ogni orario proposto termina in «:00» o «:30»

#### Scenario: Orari dentro l'apertura
- **WHEN** l'«Aula A1» è aperta il giovedì dalle 08:00 alle 20:00 e lo studente sceglie giovedì 12 novembre 2026, prima di scegliere l'ora di inizio
- **THEN** l'ora di inizio va da 08:00 a 19:30, l'ora di fine da 08:30 a 20:00, e non sono proposti orari fuori da questo intervallo

#### Scenario: Mezz'ore passate nel giorno corrente
- **WHEN** oggi è giovedì 5 novembre 2026, a Roma sono le 09:10, l'«Aula A1» è aperta dalle 08:00 alle 20:00 e lo studente sceglie il 5 novembre 2026
- **THEN** l'ora di inizio va da 09:30 a 19:30 e non sono proposte le mezz'ore precedenti, qualunque sia il fuso del dispositivo

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
