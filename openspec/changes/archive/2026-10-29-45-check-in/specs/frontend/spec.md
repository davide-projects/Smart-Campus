# Spec Delta: frontend

## ADDED Requirements

### Requirement: Prenotazioni usate
Nelle schede «Future» e «Passate» di «Le mie prenotazioni», le voci con `checkedInAt` diverso da `null` SHALL mostrare l'etichetta «Usata»; le voci con `checkedInAt` = `null` MUST NOT mostrarla. L'etichetta vale per tutto il gruppo: compare anche a chi non ha fatto il check-in.

#### Scenario: Prenotazione in corso con check-in
- **WHEN** nella scheda «Future» c'è la prenotazione dell'«Aula A1» di oggi dalle 10:00 alle 11:00 con `checkedInAt` valorizzato
- **THEN** la voce mostra «Usata» e non mostra né «Check-in» né «Cancella»

#### Scenario: Prenotazione passata con check-in
- **WHEN** lo studente seleziona la scheda «Passate» e una voce ha `checkedInAt` valorizzato
- **THEN** la voce mostra «Usata»

#### Scenario: Prenotazione senza check-in
- **WHEN** una voce delle schede «Future» o «Passate» ha `checkedInAt` = `null`
- **THEN** la voce non mostra «Usata»

#### Scenario: Check-in fatto da un altro membro
- **WHEN** Anna Bianchi è membro di una prenotazione di gruppo su cui Mario Rossi ha fatto il check-in, e apre la pagina
- **THEN** la voce di Anna Bianchi mostra «Usata» e non mostra il pulsante «Check-in»

### Requirement: Pulsante «Check-in» nelle prenotazioni future
Nella scheda «Future», ogni voce con `checkedInAt` = `null` SHALL mostrare il pulsante con testo visibile «Check-in», per l'organizzatore e per i membri. Il pulsante SHALL essere abilitato solo se l'ora del dispositivo è nella finestra `inizio − 15 min ≤ adesso ≤ inizio + 15 min`, estremi inclusi. Le schede «Passate» e «Cancellate» MUST NOT mostrarlo. Il nome accessibile SHALL essere «Check-in per <spazio>, <giorno e orario>».

#### Scenario: Nella finestra
- **WHEN** alle 09:50 Studente1 apre la pagina e nella scheda «Future» c'è la sua prenotazione dell'«Aula A1» di giovedì 5 novembre 2026 dalle 10:00 alle 11:00, senza check-in
- **THEN** la voce mostra il pulsante «Check-in» abilitato, con nome accessibile «Check-in per Aula A1, giovedì 5 novembre 2026, 10:00–11:00»

#### Scenario: Membro di una prenotazione di gruppo
- **WHEN** alle 10:10 Studente2, membro di una prenotazione di gruppo iniziata alle 10:00 e senza check-in, apre la pagina
- **THEN** la voce mostra il pulsante «Check-in» abilitato

#### Scenario: Esattamente agli estremi della finestra
- **WHEN** la prenotazione inizia alle 10:00 e l'ora valutata è le 09:45:00 oppure le 10:15:00
- **THEN** il pulsante «Check-in» è abilitato

#### Scenario: Passate e cancellate
- **WHEN** lo studente seleziona la scheda «Passate» o la scheda «Cancellate»
- **THEN** nessuna voce mostra il pulsante «Check-in»

### Requirement: Check-in fuori dalla finestra
Fuori dalla finestra il pulsante «Check-in» SHALL restare visibile e raggiungibile con Tab, ma disabilitato. Sotto il pulsante SHALL comparire il testo visibile «Il check-in è possibile da 15 minuti prima a 15 minuti dopo l'inizio.», collegato al pulsante come descrizione accessibile. Dentro la finestra il testo MUST NOT comparire. Attivare il pulsante disabilitato MUST NOT inviare richieste.

#### Scenario: Prenotazione di domani
- **WHEN** nella scheda «Future» c'è una prenotazione di domani senza check-in
- **THEN** la voce mostra il pulsante «Check-in» disabilitato e sotto il testo «Il check-in è possibile da 15 minuti prima a 15 minuti dopo l'inizio.»

#### Scenario: Prenotazione in corso oltre la finestra
- **WHEN** alle 10:20 la scheda «Future» mostra la prenotazione di oggi dalle 10:00 alle 11:00, senza check-in
- **THEN** il pulsante «Check-in» è disabilitato e la voce mostra il testo di spiegazione

#### Scenario: Un istante prima della finestra
- **WHEN** la prenotazione inizia alle 10:00 e l'ora valutata è un istante prima delle 09:45:00
- **THEN** il pulsante «Check-in» è disabilitato e la voce mostra il testo di spiegazione

#### Scenario: Nessuna richiesta dal pulsante disabilitato
- **WHEN** lo studente fa clic sul pulsante «Check-in» disabilitato, oppure lo raggiunge con Tab e preme Invio o Spazio
- **THEN** non viene inviata nessuna richiesta `POST /api/bookings/{id}/check-in`

### Requirement: Aggiornamento della finestra a pagina aperta
Mentre la pagina è aperta, ogni 30 secondi l'applicazione SHALL ricalcolare con l'ora del dispositivo l'abilitazione dei pulsanti «Check-in» e la presenza del testo di spiegazione, senza chiamare il servizio. Il ricalcolo MUST fermarsi quando si lascia la pagina.

#### Scenario: La finestra si apre con la pagina aperta
- **WHEN** alle 09:44:40 lo studente ha la pagina aperta con la prenotazione delle 10:00, pulsante disabilitato, e passano 30 secondi
- **THEN** il pulsante «Check-in» è abilitato, il testo di spiegazione non c'è più e nessuna richiesta è stata inviata a `GET /api/bookings/mine`

#### Scenario: La finestra si chiude con la pagina aperta
- **WHEN** alle 10:14:50 il pulsante della prenotazione delle 10:00 è abilitato e passano 30 secondi
- **THEN** il pulsante è disabilitato e compare il testo di spiegazione

#### Scenario: Pagina chiusa
- **WHEN** lo studente lascia la pagina «Le mie prenotazioni»
- **THEN** il ricalcolo periodico si ferma

### Requirement: Esito del check-in
«Check-in» abilitato SHALL inviare subito `POST /api/bookings/{id}/check-in`, senza conferma, con il pulsante disabilitato e una sola richiesta. Dopo il `200` SHALL comparire la snackbar «Check-in registrato.» (senza pulsanti, 5 secondi), la voce SHALL mostrare «Usata» dal `checkedInAt` della risposta, senza «Check-in», «Cancella» né nuova `GET /api/bookings/mine`, e il focus MUST andare su «Usata».

#### Scenario: Check-in riuscito
- **WHEN** alle 09:55 Studente1 attiva «Check-in» sulla prenotazione dell'«Aula A1» delle 10:00 e il servizio risponde `200` con `checkedInAt`
- **THEN** compare «Check-in registrato.», la voce dell'«Aula A1» resta nella scheda «Future» e mostra «Usata» senza «Check-in» e senza «Cancella», e non viene inviata una nuova `GET /api/bookings/mine`

#### Scenario: Nessuna conferma
- **WHEN** lo studente attiva «Check-in» abilitato
- **THEN** la richiesta parte subito, senza dialog di conferma

#### Scenario: Doppio invio
- **WHEN** lo studente attiva due volte di seguito «Check-in» mentre la richiesta è in corso
- **THEN** viene inviata una sola richiesta `POST /api/bookings/{id}/check-in`

#### Scenario: Snackbar di esito
- **WHEN** compare la snackbar «Check-in registrato.»
- **THEN** non ha pulsanti e si chiude da sola dopo 5 secondi

#### Scenario: Focus dopo il check-in
- **WHEN** lo studente fa il check-in da tastiera e il servizio risponde `200`
- **THEN** il focus è sull'etichetta «Usata» della stessa voce

### Requirement: Check-in rifiutato
Con `403`, `404` o `422` la card della prenotazione SHALL mostrare il `detail` in un avviso con `role="alert"`; con altri errori (escluso `401`) o senza risposta «Non è stato possibile registrare il check-in. Riprova più tardi.». La voce non cambia e l'avviso sparisce al tentativo successivo. Con `401` la sessione MUST essere chiusa come nelle altre pagine, con ritorno a `/my-bookings`, senza avviso.

#### Scenario: Fuori finestra secondo il servizio
- **WHEN** il pulsante era abilitato secondo l'ora del dispositivo e il servizio risponde `422` con `detail` «Il check-in è possibile da 15 minuti prima a 15 minuti dopo l'inizio.»
- **THEN** la card mostra quel messaggio con `role="alert"` e la voce non mostra «Usata»

#### Scenario: Check-in già fatto da un altro membro
- **WHEN** il servizio risponde `422` con `detail` «Il check-in di questa prenotazione è già stato registrato.»
- **THEN** la card mostra quel messaggio con `role="alert"`

#### Scenario: Prenotazione cancellata nel frattempo
- **WHEN** il servizio risponde `422` con `detail` «Non è possibile fare il check-in di una prenotazione cancellata.»
- **THEN** la card mostra quel messaggio con `role="alert"`

#### Scenario: Non consentito o non trovata
- **WHEN** il servizio risponde `403` con `detail` «Non puoi fare il check-in di questa prenotazione.» oppure `404` con `detail` «La prenotazione richiesta non esiste.»
- **THEN** la card mostra quel messaggio con `role="alert"`

#### Scenario: Errore del servizio
- **WHEN** `POST /api/bookings/{id}/check-in` risponde `500` o non risponde
- **THEN** la card mostra «Non è stato possibile registrare il check-in. Riprova più tardi.» con `role="alert"` e la voce non mostra «Usata»

#### Scenario: Sessione scaduta
- **WHEN** lo studente attiva «Check-in» e il servizio risponde `401`
- **THEN** la sessione viene chiusa e lo studente arriva al login con l'avviso di sessione scaduta e il ritorno a `/my-bookings`

### Requirement: Check-in da tastiera
Il pulsante «Check-in» SHALL essere raggiungibile con Tab anche quando è disabilitato e, se abilitato, attivabile con Invio o Spazio. Il testo di spiegazione SHALL essere letto dalle tecnologie assistive come descrizione del pulsante. Esito e avvisi di errore SHALL essere annunciati e il focus MUST essere visibile.

#### Scenario: Check-in solo da tastiera
- **WHEN** lo studente usa solo la tastiera sulla pagina nella finestra di una sua prenotazione
- **THEN** raggiunge «Check-in», lo attiva con Invio o Spazio e legge l'esito o il messaggio di errore senza usare il mouse

#### Scenario: Spiegazione da tastiera fuori finestra
- **WHEN** lo studente raggiunge con Tab il pulsante «Check-in» disabilitato
- **THEN** il pulsante riceve il focus e la sua descrizione accessibile è «Il check-in è possibile da 15 minuti prima a 15 minuti dopo l'inizio.»

### Requirement: Check-in su schermo da 375px
A 375px di larghezza il pulsante «Check-in», il testo di spiegazione, l'etichetta «Usata», l'avviso di errore e la snackbar SHALL essere leggibili e utilizzabili per intero, senza scorrimento orizzontale, anche sulle voci che mostrano insieme «Check-in» e «Cancella».

#### Scenario: Schermo da 375px
- **WHEN** lo studente apre la pagina su uno schermo largo 375px con una voce futura fuori finestra di cui è organizzatore
- **THEN** «Cancella», «Check-in» disabilitato e il testo di spiegazione sono leggibili senza scorrere in orizzontale

## MODIFIED Requirements

### Requirement: Azione «Cancella» nelle prenotazioni future
Nella scheda «Future» di «Le mie prenotazioni», le voci con `isOrganizer` = `true`, `checkedInAt` = `null` e inizio successivo all'ora del dispositivo al caricamento SHALL mostrare il pulsante «Cancella», con nome accessibile che indica spazio, giorno e orario. MUST NOT mostrarlo le voci di cui l'utente è solo membro, quelle già iniziate, quelle con check-in (anche registrato dopo il caricamento) e tutte le voci di «Passate» e «Cancellate».

#### Scenario: Prenotazione futura creata dallo studente
- **WHEN** Studente1 apre la pagina e nella scheda «Future» c'è la sua prenotazione dell'«Aula A1» di giovedì 5 novembre 2026 dalle 10:00 alle 11:00, non ancora iniziata e senza check-in
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

#### Scenario: Prenotazione futura già usata
- **WHEN** alle 09:50 Studente1 apre la pagina e la sua prenotazione delle 10:00 ha `checkedInAt` valorizzato
- **THEN** la voce mostra «Usata» e non mostra il pulsante «Cancella»

#### Scenario: Check-in fatto nella pagina
- **WHEN** alle 09:50 Studente1 vede «Cancella» e «Check-in» sulla sua prenotazione delle 10:00, attiva «Check-in» e il servizio risponde `200`
- **THEN** la voce mostra «Usata» e non mostra più né «Check-in» né «Cancella»
