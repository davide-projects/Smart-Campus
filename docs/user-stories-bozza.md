# User Stories: bozza

> **Stato:** bozza da revisionare con Davide, poi da validare con il PO. Dopo la validazione ogni storia diventa una issue con il template *User Story*.
>
> Le storie sono ordinate per **incremento proposto**: l'incremento 1 è il minimo che dimostra il valore principale (prenotare senza conflitti).
> Le stime in story point sono relative tra loro (riferimento: US-07 = 2).

## Decisioni del team

| # | Decisione | Storie interessate |
|---|---|---|
| D1 | Si può prenotare anche **come gruppo**: chi prenota (organizzatore) sceglie i membri tra gli studenti registrati | US-05 |
| D2 | I partecipanti sono calcolati: organizzatore + membri, entro la capienza dello spazio | US-04, US-05 |
| D3 | **Nessun utente** può avere due prenotazioni confermate sovrapposte, da solo o in gruppo, anche in spazi diversi | US-04, US-05, US-06 |
| D4 | I membri vedono la prenotazione e possono fare il check-in; non possono cancellarla né modificare i partecipanti | US-05, US-07, US-08, US-14 |

**Domanda aperta:** l'organizzatore può aggiungere o togliere membri dopo la conferma? Se sì, serve una storia dedicata.

## Assunzioni usate nella bozza (da confermare)

| # | Assunzione | Storie interessate |
|---|---|---|
| A1 | Le prenotazioni si fanno a blocchi di **30 minuti** | US-03, US-04, US-05, US-13 |
| A2 | Una prenotazione dura al massimo **4 ore** e si fa al massimo **14 giorni** prima | US-04, US-05 |
| A3 | Prenotazioni consecutive (10:00–11:00 e 11:00–12:00) **non** sono in conflitto | US-04, US-05, US-06 |
| A4 | Uno studente vede che una fascia è occupata, ma **non chi** l'ha prenotata | US-03, US-06 |
| A5 | Si può cancellare una prenotazione fino all'orario di inizio | US-08 |
| A6 | Il check-in è possibile da 15 minuti prima a 15 minuti dopo l'inizio | US-14, US-15 |
| A7 | Gli utenti sono già registrati (dati sintetici): non c'è la registrazione autonoma | US-09 |
| A8 | Studenti e personale possono prenotare tutti gli spazi prenotabili, laboratori compresi | US-04 |

---

## Incremento 1: prenotare con fiducia

### [US-01] - Cercare gli spazi del campus

**User Story**
Come studente
voglio cercare gli spazi del campus filtrando per tipo, capienza ed edificio
in modo da trovare rapidamente quelli adatti alla mia attività.

**Descrizione**
Lo studente apre l'elenco degli spazi (aule, laboratori, sale studio) e lo restringe con i filtri disponibili. Per ogni spazio vede almeno nome, tipo, edificio e capienza. Gli spazi dismessi non compaiono.

**Criteri di accettazione**
La storia è considerata completata quando:
- [ ] Senza filtri, vengono mostrati tutti gli spazi attivi con nome, tipo, edificio e capienza.
- [ ] Filtrando per tipo "Sala studio" compaiono solo le sale studio.
- [ ] Filtrando per capienza minima 30 non compaiono spazi con capienza inferiore a 30.
- [ ] Combinando più filtri vengono mostrati solo gli spazi che li soddisfano tutti.
- [ ] Se nessuno spazio soddisfa i filtri compare un messaggio esplicito, non una pagina vuota.

**Story Point**
Stima: 3

---

### [US-02] - Vedere il dettaglio di uno spazio

**User Story**
Come studente
voglio vedere le informazioni complete di uno spazio
in modo da capire se è adatto prima di prenotarlo.

**Descrizione**
Dall'elenco lo studente apre uno spazio e ne vede le caratteristiche: tipo, edificio, piano, capienza, attrezzature disponibili e orari di apertura. Da questa pagina può passare alla disponibilità e alla prenotazione.

**Criteri di accettazione**
La storia è considerata completata quando:
- [ ] Il dettaglio mostra tipo, edificio, piano, capienza, attrezzature e orari di apertura per ogni giorno della settimana.
- [ ] Uno spazio non prenotabile è indicato chiaramente come tale.
- [ ] Aprendo uno spazio inesistente compare un messaggio "spazio non trovato".

**Story Point**
Stima: 2

---

### [US-03] - Vedere quando uno spazio è libero

**User Story**
Come studente
voglio vedere le fasce libere e occupate di uno spazio in un giorno
in modo da scegliere quando andarci senza sovrappormi ad altri.

**Descrizione**
Nel dettaglio dello spazio lo studente sceglie un giorno e vede la giornata divisa nelle fasce dell'orario di apertura, ciascuna marcata come libera o occupata. Non vede chi ha prenotato le fasce occupate.

**Criteri di accettazione**
La storia è considerata completata quando:
- [ ] Per il giorno scelto vengono mostrate solo le fasce comprese nell'orario di apertura.
- [ ] Una fascia con una prenotazione confermata appare occupata; le altre appaiono libere.
- [ ] Una fascia di una prenotazione cancellata appare di nuovo libera.
- [ ] Le fasce occupate non mostrano il nome di chi ha prenotato.
- [ ] In un giorno di chiusura viene mostrato "spazio chiuso" invece delle fasce.

**Story Point**
Stima: 5

---

### [US-04] - Prenotare uno spazio

**User Story**
Come studente
voglio prenotare uno spazio per una fascia oraria
in modo da avere la certezza di poterlo usare senza che altri contino sullo stesso spazio.

**Descrizione**
Lo studente prenota per sé stesso: sceglie spazio, giorno, ora di inizio e di fine, e conferma. Il sistema accetta la prenotazione solo se lo spazio è libero per tutta la fascia, lo studente non ha già un'altra prenotazione nello stesso orario e la richiesta rispetta le regole (orari, durata, anticipo). È il requisito centrale del prodotto: **due prenotazioni confermate dello stesso spazio non possono mai sovrapporsi**, nemmeno se vengono richieste nello stesso istante. La prenotazione per più persone è descritta in US-05.

**Criteri di accettazione**
La storia è considerata completata quando:
- [ ] Prenotando una fascia libera, lo studente riceve una conferma con spazio, giorno e orario, e la fascia risulta occupata per tutti.
- [ ] Una richiesta che si sovrappone anche in parte a una prenotazione confermata dello stesso spazio viene rifiutata e non crea alcuna prenotazione.
- [ ] Una prenotazione che inizia esattamente quando finisce un'altra viene accettata.
- [ ] Con 20 richieste contemporanee per la stessa fascia libera, ne viene confermata esattamente una (verificato su database reale).
- [ ] Uno studente che ha già una prenotazione confermata (singola o di gruppo) sovrapposta alla fascia richiesta, anche in un altro spazio, non può prenotare.
- [ ] Vengono rifiutate, con un messaggio che spiega il motivo, le richieste: fuori dall'orario di apertura, nel passato, più lunghe di 4 ore o oltre 14 giorni in anticipo.

**Story Point**
Stima: 8

---

### [US-05] - Prenotare uno spazio per un gruppo

**User Story**
Come studente
voglio prenotare uno spazio indicando gli altri studenti che parteciperanno
in modo che tutto il gruppo abbia la certezza dello spazio e ritrovi la prenotazione.

**Descrizione**
Durante la prenotazione lo studente (organizzatore) aggiunge come membri altri studenti registrati. Il numero di partecipanti è calcolato automaticamente: organizzatore più membri, e non può superare la capienza dello spazio. Valgono tutte le regole della prenotazione singola (US-04); in più, nessun membro può avere un'altra prenotazione confermata nello stesso orario. I membri vedono la prenotazione tra le proprie e possono fare il check-in, ma non possono cancellarla né modificare i membri.

**Criteri di accettazione**
La storia è considerata completata quando:
- [ ] Lo studente può aggiungere come membri solo utenti registrati con ruolo studente.
- [ ] Il numero di partecipanti mostrato e salvato è uguale a organizzatore più membri.
- [ ] Se i partecipanti superano la capienza dello spazio, la prenotazione viene rifiutata con un messaggio che indica la capienza massima.
- [ ] Se anche un solo membro ha già una prenotazione confermata sovrapposta, la prenotazione viene rifiutata e non crea alcuna prenotazione.
- [ ] Dopo la conferma, la prenotazione compare tra le prenotazioni di ogni membro.
- [ ] I membri non possono cancellare la prenotazione né aggiungere o togliere partecipanti.

**Story Point**
Stima: 5

---

### [US-06] - Capire perché una prenotazione non è possibile

**User Story**
Come studente
voglio sapere chiaramente perché la mia prenotazione è stata rifiutata e quali alternative ho
in modo da riorganizzarmi subito invece di cercare nuove conferme altrove.

**Descrizione**
Quando una prenotazione non va a buon fine, lo studente vede un messaggio comprensibile con il motivo. Se lo spazio è occupato, gli vengono proposte le fasce libere più vicine dello stesso spazio nello stesso giorno.

**Criteri di accettazione**
La storia è considerata completata quando:
- [ ] Se la fascia è occupata, il messaggio lo dice e mostra le fasce libere dello stesso spazio in quel giorno.
- [ ] Se non ci sono fasce libere quel giorno, il messaggio lo indica esplicitamente.
- [ ] Per ogni altro motivo di rifiuto (orario, capienza, durata, anticipo) il messaggio indica il limite da rispettare (es. "aperto dalle 08:00 alle 20:00").
- [ ] Se lo studente ha già una prenotazione nella stessa fascia, il messaggio lo indica.
- [ ] In una prenotazione di gruppo, se uno o più membri hanno già una prenotazione nella stessa fascia, il messaggio lo indica.
- [ ] I dati inseriti nel modulo restano compilati dopo il rifiuto.

**Story Point**
Stima: 3

---

### [US-07] - Vedere le mie prenotazioni

**User Story**
Come studente
voglio vedere in un unico elenco le mie prenotazioni
in modo da ritrovare le conferme senza cercarle tra messaggi diversi.

**Descrizione**
Lo studente apre la sezione "Le mie prenotazioni" e vede le prenotazioni future in ordine di data, con spazio, edificio, giorno e orario: sia quelle che ha creato sia quelle di gruppo di cui è membro. Può consultare anche quelle passate e quelle cancellate.

**Criteri di accettazione**
La storia è considerata completata quando:
- [ ] Vengono mostrate le prenotazioni future dello studente in ordine cronologico, con spazio, edificio, giorno e orario.
- [ ] Le prenotazioni di gruppo di cui lo studente è membro compaiono nell'elenco, indicate come prenotazioni di gruppo con l'organizzatore.
- [ ] Le prenotazioni passate e quelle cancellate sono consultabili separatamente.
- [ ] Lo studente non vede le prenotazioni di cui non è organizzatore né membro.
- [ ] Se non ci sono prenotazioni compare un messaggio con il collegamento alla ricerca degli spazi.

**Story Point**
Stima: 2

---

### [US-08] - Cancellare una mia prenotazione

**User Story**
Come studente
voglio cancellare una prenotazione che non mi serve più
in modo da liberare lo spazio per chi ne ha bisogno.

**Descrizione**
Dall'elenco delle sue prenotazioni lo studente cancella una prenotazione futura che ha creato lui, dopo una richiesta di conferma. Lo spazio torna subito disponibile per tutti. Per una prenotazione di gruppo, la cancellazione vale per tutti i membri.

**Criteri di accettazione**
La storia è considerata completata quando:
- [ ] Dopo la conferma, la prenotazione risulta cancellata e non è più tra le future, né per l'organizzatore né per i membri.
- [ ] Subito dopo la cancellazione, la fascia appare libera nella disponibilità dello spazio e un altro studente può prenotarla.
- [ ] Non è possibile cancellare una prenotazione già iniziata o passata.
- [ ] Uno studente non può cancellare la prenotazione di un altro utente.
- [ ] Un membro di un gruppo che non è l'organizzatore non può cancellare la prenotazione, che resta valida.

**Story Point**
Stima: 3

---

## Incremento 2: accesso e gestione degli spazi

### [US-09] - Accedere con le proprie credenziali

**User Story**
Come utente del campus (studente o personale)
voglio accedere con le mie credenziali
in modo da vedere e fare solo ciò che è previsto per il mio ruolo.

**Descrizione**
L'utente inserisce nome utente e password e accede alle funzioni del proprio ruolo: lo studente ricerca, prenota e gestisce le proprie prenotazioni; il personale gestisce anche gli spazi e consulta i report. Gli utenti sono dati sintetici già presenti nel sistema.

**Criteri di accettazione**
La storia è considerata completata quando:
- [ ] Con credenziali corrette l'utente accede e vede il proprio nome e ruolo.
- [ ] Con credenziali errate compare un messaggio generico, senza indicare quale dato è sbagliato.
- [ ] Senza accesso non è possibile prenotare né vedere prenotazioni.
- [ ] Uno studente non vede e non può usare le funzioni riservate al personale, nemmeno chiamandole direttamente.
- [ ] L'utente può uscire e, dopo l'uscita, le funzioni protette non sono più accessibili.

**Story Point**
Stima: 5

---

### [US-10] - Gestire gli spazi del campus

**User Story**
Come personale del campus
voglio inserire e modificare gli spazi con caratteristiche e orari di apertura
in modo che le informazioni consultate dagli studenti siano sempre aggiornate.

**Descrizione**
Il personale crea un nuovo spazio o ne modifica uno esistente: nome, tipo, edificio, piano, capienza, attrezzature, orari di apertura e se è prenotabile. Le modifiche diventano visibili nella ricerca e nella disponibilità.

**Criteri di accettazione**
La storia è considerata completata quando:
- [ ] Il personale può creare uno spazio che poi compare nella ricerca degli studenti.
- [ ] Il personale può modificare capienza, attrezzature e orari, e gli studenti vedono i dati aggiornati.
- [ ] Vengono rifiutati, con un messaggio chiaro, capienza minore o uguale a zero e orario di chiusura precedente all'apertura.
- [ ] Uno spazio segnato come non prenotabile resta visibile ma non può essere prenotato.

**Story Point**
Stima: 5

---

### [US-11] - Dismettere uno spazio

**User Story**
Come personale del campus
voglio dismettere uno spazio che non è più disponibile
in modo che nessuno possa più prenotarlo.

**Descrizione**
Il personale dismette uno spazio (ad esempio per lavori o chiusura definitiva). Lo spazio non compare più nella ricerca e non è prenotabile. Il personale viene avvisato se esistono prenotazioni future su quello spazio.

**Criteri di accettazione**
La storia è considerata completata quando:
- [ ] Uno spazio dismesso non compare nella ricerca degli studenti e non può essere prenotato.
- [ ] Prima di confermare, il personale vede quante prenotazioni future esistono su quello spazio.
- [ ] Le prenotazioni passate dello spazio restano consultabili nei report.

**Story Point**
Stima: 3

---

### [US-12] - Vedere le prenotazioni di uno spazio

**User Story**
Come personale del campus
voglio vedere chi ha prenotato uno spazio in un giorno
in modo da rispondere alle richieste degli studenti e risolvere dubbi sull'uso delle aule.

**Descrizione**
Il personale sceglie uno spazio e un giorno e vede l'elenco delle prenotazioni con orario, utente e numero di partecipanti.

**Criteri di accettazione**
La storia è considerata completata quando:
- [ ] Per lo spazio e il giorno scelti vengono mostrate le prenotazioni confermate in ordine di orario, con utente e partecipanti.
- [ ] Le prenotazioni cancellate sono distinguibili da quelle confermate.
- [ ] La funzione non è accessibile agli studenti.

**Story Point**
Stima: 2

---

### [US-13] - Trovare spazi liberi in un orario

**User Story**
Come studente
voglio cercare gli spazi liberi in un giorno e in una fascia oraria precisi
in modo da non dover controllare la disponibilità di ogni spazio uno alla volta.

**Descrizione**
Lo studente indica giorno, ora di inizio, ora di fine e, se vuole, tipo e capienza minima. Il sistema mostra solo gli spazi aperti e liberi per tutta la fascia richiesta, da cui si può prenotare direttamente.

**Criteri di accettazione**
La storia è considerata completata quando:
- [ ] Vengono mostrati solo gli spazi aperti e senza prenotazioni confermate che si sovrappongono alla fascia richiesta.
- [ ] I filtri per tipo e capienza minima restringono il risultato.
- [ ] Da un risultato si può prenotare con giorno e orario già compilati.
- [ ] Se nessuno spazio è libero compare un messaggio esplicito.

**Story Point**
Stima: 5

---

## Incremento 3: utilizzo effettivo degli spazi

### [US-14] - Segnalare che sto usando lo spazio (check-in)

**User Story**
Come studente
voglio confermare di essere arrivato nello spazio prenotato
in modo che risulti che lo spazio è stato effettivamente usato.

**Descrizione**
All'inizio della prenotazione lo studente fa il check-in dalla sezione "Le mie prenotazioni". Il check-in è possibile solo in una finestra intorno all'orario di inizio. In una prenotazione di gruppo può farlo l'organizzatore o qualsiasi membro, e basta un check-in per segnare la prenotazione come usata.

**Criteri di accettazione**
La storia è considerata completata quando:
- [ ] Da 15 minuti prima a 15 minuti dopo l'inizio, lo studente può fare il check-in e la prenotazione risulta "usata".
- [ ] Fuori da questa finestra il check-in non è possibile e viene spiegato perché.
- [ ] In una prenotazione di gruppo, il check-in di un solo membro o dell'organizzatore segna la prenotazione come "usata" per tutto il gruppo.
- [ ] Non è possibile fare due volte il check-in sulla stessa prenotazione.
- [ ] Non è possibile fare il check-in su una prenotazione cancellata o di cui non si è organizzatore né membro.

**Story Point**
Stima: 3

---

### [US-15] - Registrare una mancata presentazione

**User Story**
Come personale del campus
voglio segnare che nessuno si è presentato per una prenotazione
in modo da distinguere gli spazi prenotati ma non usati.

**Descrizione**
Dopo la finestra di check-in, il personale può segnare una prenotazione senza check-in come "non presentato".

**Criteri di accettazione**
La storia è considerata completata quando:
- [ ] Il personale può segnare "non presentato" solo dopo la fine della finestra di check-in e solo se non c'è stato check-in.
- [ ] Una prenotazione segnata "non presentato" non può ricevere un check-in successivo.
- [ ] La funzione non è accessibile agli studenti.

**Story Point**
Stima: 2

---

### [US-16] - Capire come vengono usati gli spazi

**User Story**
Come personale del campus
voglio un riepilogo per spazio e periodo che distingua prenotazioni usate, non usate e senza informazioni
in modo da interpretare l'utilizzo reale senza scambiare ipotesi per certezze.

**Descrizione**
Il personale sceglie un periodo e vede, per ogni spazio, quante prenotazioni ci sono state e come sono andate: usate (con check-in), non usate (non presentato), senza informazione (né check-in né segnalazione) e cancellate. Il riepilogo dichiara esplicitamente i limiti dei dati: una prenotazione senza informazione non significa che lo spazio sia rimasto vuoto.

**Criteri di accettazione**
La storia è considerata completata quando:
- [ ] Per ogni spazio e periodo scelto vengono mostrati separatamente: prenotazioni usate, non usate, senza informazione e cancellate, con le ore corrispondenti.
- [ ] Con dati di prova noti (es. 2 usate, 1 non presentato, 1 senza informazione) il riepilogo mostra esattamente questi valori.
- [ ] Le prenotazioni senza informazione non sono mai conteggiate né come usate né come non usate.
- [ ] Il riepilogo riporta una nota che spiega il significato di "senza informazione".
- [ ] La funzione non è accessibile agli studenti.

**Story Point**
Stima: 8

---

### [US-17] - Confrontare l'utilizzo tra aree del campus

**User Story**
Come personale del campus
voglio filtrare il riepilogo di utilizzo per edificio e tipo di spazio
in modo da confrontare aree diverse del campus.

**Descrizione**
Nel riepilogo di utilizzo il personale applica filtri per edificio e tipo di spazio e vede i totali del sottoinsieme scelto.

**Criteri di accettazione**
La storia è considerata completata quando:
- [ ] Filtrando per edificio vengono mostrati solo gli spazi di quell'edificio, con i relativi totali.
- [ ] Filtrando per tipo vengono mostrati solo gli spazi di quel tipo, con i relativi totali.
- [ ] I totali del sottoinsieme sono coerenti con la somma dei singoli spazi mostrati.

**Story Point**
Stima: 2

---

## Riepilogo

| ID | Titolo | Incremento | SP |
|---|---|---|---|
| US-01 | Cercare gli spazi del campus | 1 | 3 |
| US-02 | Vedere il dettaglio di uno spazio | 1 | 2 |
| US-03 | Vedere quando uno spazio è libero | 1 | 5 |
| US-04 | Prenotare uno spazio | 1 | 8 |
| US-05 | Prenotare uno spazio per un gruppo | 1 | 5 |
| US-06 | Capire perché una prenotazione non è possibile | 1 | 3 |
| US-07 | Vedere le mie prenotazioni | 1 | 2 |
| US-08 | Cancellare una mia prenotazione | 1 | 3 |
| US-09 | Accedere con le proprie credenziali | 2 | 5 |
| US-10 | Gestire gli spazi del campus | 2 | 5 |
| US-11 | Dismettere uno spazio | 2 | 3 |
| US-12 | Vedere le prenotazioni di uno spazio | 2 | 2 |
| US-13 | Trovare spazi liberi in un orario | 2 | 5 |
| US-14 | Segnalare che sto usando lo spazio (check-in) | 3 | 3 |
| US-15 | Registrare una mancata presentazione | 3 | 2 |
| US-16 | Capire come vengono usati gli spazi | 3 | 8 |
| US-17 | Confrontare l'utilizzo tra aree del campus | 3 | 2 |
| | **Totale** | | **66** |

Incremento 1: 31 SP · Incremento 2: 20 SP · Incremento 3: 15 SP.

**Nota per la revisione:** nell'incremento 1 gli studenti usano un utente di prova fisso, perché l'accesso vero arriva con US-09; i membri di un gruppo (US-05) si scelgono tra gli utenti sintetici già presenti. Se Davide o il PO preferiscono avere l'accesso già nel primo incremento, basta spostare US-09 e togliere US-13 per mantenere un carico simile.
