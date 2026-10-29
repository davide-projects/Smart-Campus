# Spec Delta: frontend

## ADDED Requirements

### Requirement: Membri del gruppo nel modulo di prenotazione
Il modulo di `/rooms/:id/book` SHALL avere il campo facoltativo «Membri del gruppo». Da 2 caratteri scritti il campo SHALL proporre gli studenti di `GET /api/identity/students?search=<testo>`, con nome visualizzato e username; scegliendone uno diventa un membro, mostrato come chip con il nome. Gli studenti già scelti MUST NOT essere riproposti.

#### Scenario: Ricerca di uno studente
- **WHEN** Mario Rossi scrive «bia» nel campo «Membri del gruppo»
- **THEN** viene chiamato `GET /api/identity/students?search=bia` e tra le proposte compare «Anna Bianchi» con «student2»

#### Scenario: Meno di 2 caratteri
- **WHEN** lo studente scrive un solo carattere nel campo «Membri del gruppo»
- **THEN** non viene inviata alcuna ricerca e non compaiono proposte

#### Scenario: Scelta di un membro
- **WHEN** lo studente sceglie «Anna Bianchi» tra le proposte
- **THEN** sotto il campo compare il chip «Anna Bianchi», il testo del campo si svuota e le proposte si chiudono

#### Scenario: Membro già scelto non riproposto
- **WHEN** «Anna Bianchi» è già un membro e lo studente cerca di nuovo «bia»
- **THEN** «Anna Bianchi» non compare tra le proposte

#### Scenario: Membri inviati con la prenotazione
- **WHEN** lo studente ha scelto Anna Bianchi e Luca Verdi e conferma la prenotazione
- **THEN** la richiesta a `POST /api/bookings` contiene `memberIds` con gli id di Anna Bianchi e Luca Verdi

#### Scenario: Prenotazione senza membri
- **WHEN** lo studente conferma senza aver scelto membri
- **THEN** la richiesta a `POST /api/bookings` contiene `memberIds` = `[]`

### Requirement: Solo studenti proposti come membri
Un membro SHALL potersi aggiungere solo scegliendolo tra le proposte della ricerca, che contiene solo studenti. Il testo scritto senza scegliere una proposta MUST NOT aggiungere un membro.

#### Scenario: Testo scritto senza scegliere
- **WHEN** lo studente scrive «Elena Conti» nel campo «Membri del gruppo» e preme Invio senza che una proposta sia evidenziata
- **THEN** nessun chip viene aggiunto e la prenotazione non viene inviata

#### Scenario: Utente Staff non proposto
- **WHEN** lo studente cerca «Conti» e il servizio risponde con un array vuoto
- **THEN** il campo mostra «Nessuno studente trovato.» e nessun membro può essere aggiunto

### Requirement: Numero massimo di membri nel modulo
Con 10 membri scelti il campo «Membri del gruppo» SHALL disabilitare la ricerca e mostrare «Puoi aggiungere al massimo 10 membri.». Rimuovendo un membro la ricerca SHALL tornare disponibile e il messaggio sparire.

#### Scenario: Decimo membro aggiunto
- **WHEN** lo studente aggiunge il decimo membro
- **THEN** il campo di ricerca è disabilitato, compare «Puoi aggiungere al massimo 10 membri.» e non si possono aggiungere altri membri

#### Scenario: Membro rimosso dopo il tetto
- **WHEN** i membri sono 10 e lo studente ne rimuove uno
- **THEN** il campo di ricerca torna utilizzabile e il messaggio sparisce

### Requirement: Membri del gruppo nascosti allo Staff
Per un utente con ruolo `Staff` il modulo di prenotazione MUST NOT mostrare il campo «Membri del gruppo»; SHALL mostrare «Partecipanti: 1» e inviare `memberIds` = `[]`.

#### Scenario: Staff apre il modulo
- **WHEN** Elena Conti (`Staff`) apre `/rooms/<id>/book`
- **THEN** il campo «Membri del gruppo» non c'è, il modulo mostra «Partecipanti: 1» e la conferma invia `memberIds` = `[]`

### Requirement: Rimozione di un membro
Ogni chip SHALL avere un pulsante di rimozione con nome accessibile «Rimuovi <nome>». Rimuovendo il chip lo studente esce dai membri e il numero di partecipanti si aggiorna.

#### Scenario: Rimozione con il pulsante
- **WHEN** i membri sono Anna Bianchi e Luca Verdi e lo studente attiva «Rimuovi Anna Bianchi»
- **THEN** resta solo il chip «Luca Verdi» e i partecipanti diventano 2

### Requirement: Ricerca degli studenti non riuscita
Se `GET /api/identity/students` non risponde o risponde con un errore diverso da `401`, il campo SHALL mostrare «Non è stato possibile cercare gli studenti. Riprova più tardi.» senza perdere i membri già scelti né i valori del modulo. Con `401` la sessione MUST essere chiusa come nelle altre pagine.

#### Scenario: Errore del servizio durante la ricerca
- **WHEN** lo studente cerca «bia» e il servizio risponde `500`
- **THEN** il campo mostra «Non è stato possibile cercare gli studenti. Riprova più tardi.» e i chip già presenti restano

#### Scenario: Sessione scaduta durante la ricerca
- **WHEN** la ricerca riceve `401`
- **THEN** la sessione viene chiusa e lo studente arriva al login con il ritorno a `/rooms/<id>/book`

### Requirement: Numero di partecipanti nel modulo
Il modulo SHALL mostrare sempre «Partecipanti: N», con N = 1 (l'organizzatore) + numero di membri scelti, aggiornato a ogni aggiunta o rimozione e annunciato alle tecnologie assistive senza spostare il focus.

#### Scenario: Senza membri
- **WHEN** lo studente apre il modulo
- **THEN** il modulo mostra «Partecipanti: 1»

#### Scenario: Con due membri
- **WHEN** lo studente aggiunge Anna Bianchi e Luca Verdi
- **THEN** il modulo mostra «Partecipanti: 3»

### Requirement: Avviso di capienza superata
Se i partecipanti superano la capienza dello spazio letta da `GET /api/rooms/{id}`, il modulo SHALL mostrare «Lo spazio ospita al massimo N persone.» con `role="alert"`. Finché l'avviso è presente la conferma MUST NOT inviare la richiesta. Togliendo membri l'avviso SHALL sparire.

#### Scenario: Partecipanti oltre la capienza
- **WHEN** la «Sala studio B2» ha capienza 4 e lo studente aggiunge 4 membri
- **THEN** il modulo mostra «Partecipanti: 5» e «Lo spazio ospita al massimo 4 persone.», e la conferma non invia `POST /api/bookings`

#### Scenario: Partecipanti uguali alla capienza
- **WHEN** la «Sala studio B2» ha capienza 4 e lo studente aggiunge 3 membri
- **THEN** il modulo mostra «Partecipanti: 4» senza avviso e la conferma invia la richiesta

#### Scenario: Membro rimosso
- **WHEN** l'avviso è visibile con 5 partecipanti su capienza 4 e lo studente rimuove un membro
- **THEN** l'avviso sparisce

### Requirement: Membri del gruppo da tastiera
Il campo «Membri del gruppo», le proposte e i chip SHALL essere utilizzabili senza mouse. Il campo si raggiunge con Tab; le frecce scorrono le proposte e Invio sceglie quella evidenziata; Esc chiude le proposte. I chip si raggiungono da tastiera e si rimuovono con Canc, Backspace o con il loro pulsante. Il focus MUST essere visibile.

#### Scenario: Aggiunta e rimozione solo da tastiera
- **WHEN** lo studente usa solo la tastiera nel modulo
- **THEN** cerca «bia», sceglie «Anna Bianchi» con le frecce e Invio, raggiunge il chip e lo rimuove, e legge il numero di partecipanti aggiornato

### Requirement: Membri del gruppo su schermo da 375px
A 375px di larghezza il campo «Membri del gruppo», le proposte, i chip (che vanno a capo), il numero di partecipanti e l'avviso di capienza SHALL essere leggibili e utilizzabili per intero, senza scorrimento orizzontale.

#### Scenario: Schermo da 375px con quattro membri
- **WHEN** lo studente apre il modulo su uno schermo largo 375px e aggiunge quattro membri
- **THEN** i quattro chip vanno a capo, e nomi, pulsanti di rimozione e «Partecipanti: 5» sono leggibili senza scorrere in orizzontale

### Requirement: Membri nella voce di gruppo
In «Le mie prenotazioni» le voci con `isGroup` = `true` SHALL mostrare, sotto l'organizzatore, «Membri: » seguito dai nomi di `members` separati da virgola, nell'ordine ricevuto. Un membro con `displayName` = `null` SHALL comparire come «Nome non disponibile». Le voci singole MUST NOT mostrare la riga dei membri. A 375px la riga SHALL andare a capo senza scorrimento orizzontale.

#### Scenario: Voce di gruppo con i membri
- **WHEN** una voce ha `isGroup` = `true` e `members` con Anna Bianchi e Luca Verdi
- **THEN** la voce mostra «Prenotazione di gruppo», l'organizzatore e «Membri: Anna Bianchi, Luca Verdi»

#### Scenario: Nome di un membro non noto
- **WHEN** una voce di gruppo ha `members` con Anna Bianchi e un membro con `displayName` = `null`
- **THEN** la voce mostra «Membri: Anna Bianchi, Nome non disponibile»

#### Scenario: Prenotazione singola senza membri
- **WHEN** una voce ha `isGroup` = `false` e `members` = `[]`
- **THEN** la voce non mostra la riga dei membri

#### Scenario: Dieci membri su schermo da 375px
- **WHEN** lo studente apre la pagina su uno schermo largo 375px e una voce ha dieci membri
- **THEN** la riga dei membri va a capo e tutti i nomi sono leggibili senza scorrere in orizzontale

## MODIFIED Requirements

### Requirement: Conferma della prenotazione
Dopo una risposta `201` la pagina SHALL mostrare, nella stessa pagina e al posto del modulo, la conferma con il nome dello spazio, il giorno e l'orario della prenotazione, in italiano e nell'ora del campus, nel formato «giovedì 5 novembre 2026, 10:00–11:00». Se `participants` è maggiore di 1 la conferma SHALL mostrare anche «Partecipanti: N». Sotto la conferma SHALL esserci il collegamento «Torna allo spazio», che apre `/rooms/:id`, e il pulsante «Prenota un'altra fascia», che ripropone il modulo vuoto, senza membri, per lo stesso spazio.

#### Scenario: Prenotazione confermata
- **WHEN** lo studente prenota l'«Aula A1» il 5 novembre 2026 dalle 10:00 alle 11:00 e il servizio risponde `201`
- **THEN** la pagina mostra la conferma con «Aula A1» e «giovedì 5 novembre 2026, 10:00–11:00» e non mostra più il modulo

#### Scenario: Torna allo spazio
- **WHEN** dopo la conferma lo studente attiva «Torna allo spazio»
- **THEN** si apre il dettaglio dell'«Aula A1» (`/rooms/<id>`)

#### Scenario: Prenota un'altra fascia
- **WHEN** dopo la conferma lo studente attiva «Prenota un'altra fascia»
- **THEN** la conferma lascia il posto al modulo vuoto per l'«Aula A1», senza messaggi di rifiuto, senza membri e con «Partecipanti: 1»

#### Scenario: Dispositivo in un altro fuso
- **WHEN** il browser è impostato sul fuso UTC e la risposta `201` riguarda una fascia dalle 10:00 alle 11:00 ora di Roma
- **THEN** la conferma mostra comunque «10:00–11:00»

#### Scenario: Conferma di una prenotazione di gruppo
- **WHEN** lo studente prenota con due membri e il servizio risponde `201` con `participants` = 3
- **THEN** la conferma mostra anche «Partecipanti: 3»

#### Scenario: Conferma di una prenotazione singola
- **WHEN** il servizio risponde `201` con `participants` = 1
- **THEN** la conferma non mostra il numero di partecipanti

### Requirement: Messaggi di rifiuto della prenotazione
Se `POST /api/bookings` risponde `409`, `422` o `503`, la pagina SHALL mostrare, con `role="alert"`, il messaggio scelto dal `code` della risposta per i rifiuti dei gruppi (`too-many-members`, `organizer-as-member`, `duplicate-members`, `members-not-students`, `members-check-unavailable`, `member-booking-in-progress`, `capacity-exceeded` con il numero da `limits.maxParticipants`, `members-occupied`), con i testi di `docs/handoff-architettura.md` 6.2, e il messaggio `detail` della risposta per gli altri codici o se il `code` manca, mantenendo i valori inseriti nel modulo, compresi i membri scelti. Se il servizio non risponde o risponde con un altro errore SHALL mostrare «Non è stato possibile completare la prenotazione. Riprova più tardi.».

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

#### Scenario: Membro già impegnato
- **WHEN** lo studente prenota con Anna Bianchi e il servizio risponde `409` con `code` = `members-occupied`
- **THEN** la pagina mostra «Uno o più membri hanno già una prenotazione in questa fascia.» e il chip «Anna Bianchi» resta nel modulo

#### Scenario: Capienza superata secondo il servizio
- **WHEN** il servizio risponde `422` con `code` = `capacity-exceeded` e `limits.maxParticipants` = 4
- **THEN** la pagina mostra «Lo spazio ospita al massimo 4 persone.»

#### Scenario: Troppi membri secondo il servizio
- **WHEN** il servizio risponde `422` con `code` = `too-many-members` e `limits.maxMembers` = 10
- **THEN** la pagina mostra «Puoi aggiungere al massimo 10 membri.» e i membri scelti restano nel modulo

#### Scenario: Membro non studente
- **WHEN** il servizio risponde `422` con `code` = `members-not-students`
- **THEN** la pagina mostra «Puoi aggiungere come membri solo studenti registrati.» e i membri scelti restano nel modulo

#### Scenario: Verifica dei membri non disponibile
- **WHEN** il servizio risponde `503` con `code` = `members-check-unavailable`
- **THEN** la pagina mostra «Non è possibile verificare i membri in questo momento. Riprova tra poco.» e lo studente può riprovare con lo stesso modulo

#### Scenario: Servizio non disponibile senza messaggio
- **WHEN** il servizio risponde `503` senza `code` né `detail`
- **THEN** la pagina mostra «Non è stato possibile completare la prenotazione. Riprova più tardi.»
