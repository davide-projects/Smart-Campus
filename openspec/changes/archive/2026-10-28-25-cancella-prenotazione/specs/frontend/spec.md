# Spec Delta: frontend

## ADDED Requirements

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

## MODIFIED Requirements

### Requirement: Caricamento ed errore
Al primo caricamento della pagina SHALL comparire un indicatore di avanzamento; se quella chiamata fallisce per un motivo diverso da `401` SHALL mostrare «Non è stato possibile caricare le prenotazioni. Riprova più tardi.» con `role="alert"`, senza schede e senza messaggi di scheda vuota. Il ricaricamento dopo una cancellazione riuscita segue invece il requisito «Esito della cancellazione»: schede visibili e avviso sopra le schede se fallisce.

#### Scenario: Errore del servizio
- **WHEN** al primo caricamento della pagina `GET /api/bookings/mine` risponde `500` o non risponde
- **THEN** la pagina mostra il messaggio di errore e non mostra «Non hai prenotazioni future.»

#### Scenario: Indicatore al primo caricamento
- **WHEN** lo studente apre la pagina e la risposta di `GET /api/bookings/mine` non è ancora arrivata
- **THEN** la pagina mostra l'indicatore di avanzamento e non mostra le schede
