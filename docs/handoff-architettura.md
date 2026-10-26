# Handoff architettura → user stories

> Documento di passaggio tra il lavoro architetturale e la scrittura del Product Backlog.
> Segue la struttura di handoff vista nel corso: **obiettivo e criterio · input e stato · output atteso · evidenze · incertezze e limiti**.
> Tutto ciò che è indicato come *proposta* va **discusso e validato con il PO** prima di entrare nel backlog: l'architettura non decide il perimetro.

---

## 1. Obiettivo e criterio

### Obiettivo del passaggio
Permettere al gruppo di scrivere **user stories orientate al valore** che:
- descrivono un bisogno di un attore, non una soluzione tecnica;
- possono essere realizzate come **fette verticali** (frontend → gateway → servizio → database) e quindi dimostrate;
- rispettano i confini dei servizi definiti in [architettura.md](architettura.md);
- hanno criteri di accettazione osservabili e verificabili con dati sintetici.

### Fonte del bisogno (traccia SmartCampus)
| Problema osservato | Bisogno | Dove si risolve |
|---|---|---|
| Un gruppo trova occupata un'aula che sembrava libera | Sapere con fiducia se uno spazio è disponibile | Spazi + Prenotazioni |
| Le conferme passano da messaggi sparsi | Avere una conferma unica e ritrovabile | Prenotazioni |
| Una rinuncia non libera lo spazio per altri | La cancellazione rende subito lo spazio disponibile | Prenotazioni |
| Due gruppi si aspettano di usare lo stesso spazio | **Nessun impegno incompatibile** | Prenotazioni (invariante) |
| Il personale ha percezioni contrastanti sull'uso | Distinguere intenzione (prenotazione), uso effettivo e assenza di informazione | Presenze |

### Criterio di completamento di questo handoff
Il gruppo è in grado di produrre una prima versione di Product Backlog ordinabile dal PO, con almeno le storie del **Product Goal** e del **primo incremento** dotate di criteri di accettazione.

---

## 2. Input e stato attuale

### Stato del repository
| Elemento | Stato |
|---|---|
| Solution .NET 10, 4 servizi + gateway YARP | ✅ scheletro, compila, solo endpoint informativo `GET /api/{servizio}/` |
| Health check `/health/live`, `/health/ready` | ✅ |
| Contratti eventi (`SmartCampus.Contracts`) | ✅ definiti, non ancora pubblicati/consumati |
| Frontend Angular 22 con proxy verso il gateway | ✅ shell vuota |
| docker-compose (SQL Server, RabbitMQ, servizi, web) | ✅ configurazione valida; build immagini da verificare con Docker attivo |
| CI GitHub Actions | ✅ definita, mai eseguita |
| Database, EF Core, migrazioni, seed | ❌ da fare |
| Autenticazione JWT | ❌ da fare |
| Messaggistica (MassTransit + outbox) | ❌ da fare |
| Test di concorrenza prenotazioni | ❌ da fare (prova chiave del progetto) |

### Vincoli che le storie devono rispettare
- **Stack:** backend C# / ASP.NET Core, frontend Angular, database SQL Server.
- **Dati:** solo sintetici, prodotti dal gruppo. Nessun dato personale reale.
- **Segreti:** mai in repository, log, screenshot, prompt.
- **Cloud:** progettazione cloud-ready; il deploy è facoltativo e senza spese. La demo è locale.
- **Fuori perimetro:** sensori reali, infrastrutture di produzione, scenario Big Data.
- **Prove:** concorrenza e persistenza vanno dimostrate su **SQL Server reale**, non con mock o provider in-memory.

---

## 3. Attori

| Attore | Ruolo tecnico | Obiettivi principali |
|---|---|---|
| **Studente** | `Studente` | Trovare uno spazio adatto, prenotarlo con certezza, gestire le proprie prenotazioni, segnalare la presenza |
| **Personale del campus** | `Personale` | Gestire il catalogo degli spazi, avere visibilità sulle prenotazioni, interpretare l'utilizzo con consapevolezza dei limiti dei dati |
| **Visitatore non autenticato** | — | *(proposta)* consultare il catalogo senza prenotare. Da validare con il PO |
| **Sistema** | — | Propagare eventi tra servizi, mantenere la coerenza delle copie locali |

> Domanda per il PO: esistono altri ruoli (docente, rappresentante di un'associazione studentesca) con regole diverse? Se sì, sono nel perimetro?

---

## 4. Glossario condiviso

Usare **questi termini** in storie, codice, API e UI. Se un termine cambia, si aggiorna qui.

| Termine | Significato | Servizio proprietario |
|---|---|---|
| **Spazio** | Luogo prenotabile o no: aula, laboratorio, sala studio | Spazi |
| **Edificio** | Raggruppamento fisico di spazi | Spazi |
| **Attrezzatura** | Dotazione di uno spazio (proiettore, PC, lavagna…) | Spazi |
| **Orario di apertura** | Fasce in cui uno spazio può essere prenotato | Spazi |
| **Spazio prenotabile** | Copia locale in Prenotazioni dei soli dati che servono a prenotare | Prenotazioni |
| **Prenotazione** | Impegno di un utente su uno spazio per un intervallo `[Inizio, Fine)` | Prenotazioni |
| **Conflitto** | Due prenotazioni *confermate* dello stesso spazio con intervalli sovrapposti | Prenotazioni |
| **Disponibilità** | Intervalli liberi di uno spazio in un giorno, calcolati da orari − prenotazioni | Prenotazioni |
| **Check-in** | Registrazione che lo spazio è stato effettivamente usato | Presenze |
| **No-show** | Prenotazione confermata senza check-in nella finestra prevista | Presenze |
| **Nessuna informazione** | Né check-in né no-show registrato: **non** significa "non usato" | Presenze |

---

## 5. Mappa servizi → capacità → epiche

Le **epiche** sono aree di valore; le storie dentro un'epica possono toccare più servizi (fetta verticale).

| Epica | Valore | Servizi coinvolti | Incremento proposto |
|---|---|---|---|
| **E1. Trovare uno spazio** | So quali spazi esistono e quali fanno al caso mio | Spazi, Frontend | 1 |
| **E2. Prenotare senza conflitti** | Ho una conferma affidabile, nessuno ha lo stesso spazio nello stesso momento | Prenotazioni, Spazi (eventi), Frontend | 1 |
| **E3. Gestire le mie prenotazioni** | Ritrovo le conferme, rinuncio e libero lo spazio | Prenotazioni, Frontend | 1 |
| **E4. Accesso e ruoli** | Ognuno vede e fa solo ciò che gli compete | Autenticazione, Gateway, tutti | 2 |
| **E5. Gestire il catalogo** | Il personale mantiene le informazioni aggiornate | Spazi, Prenotazioni (eventi), Frontend | 1–2 |
| **E6. Registrare l'utilizzo effettivo** | Distinguere intenzione e uso reale | Presenze, Prenotazioni (eventi), Frontend | 3 |
| **E7. Comprendere l'utilizzo** | Il personale interpreta i dati senza trasformare ipotesi in certezze | Presenze, Frontend | 3 |
| **E8. Qualità, avvio e prove** *(abilitante)* | Il prodotto è riproducibile e verificabile | Tutti | trasversale |

> E8 non è valore per l'utente finale: va gestita tramite **Definition of Done** e task tecnici dentro le storie, non come storie "Come sviluppatore voglio…", salvo accordo con il PO.

---

## 6. Dettaglio per servizio (riferimento per scrivere storie e task)

### 6.1 Spazi

**Responsabilità:** cosa esiste nel campus e cosa è prenotabile.

| Entità | Campi proposti | Note |
|---|---|---|
| `Edificio` | Id, Nome, Indirizzo/Codice | |
| `Spazio` | Id, EdificioId, Nome, Tipo (`Aula`/`Laboratorio`/`SalaStudio`), Capienza, Piano, Prenotabile, Attivo | `Attivo=false` = dismesso, non cancellato fisicamente |
| `Attrezzatura` | Id, Nome | Relazione N:M con Spazio |
| `OrarioApertura` | SpazioId, GiornoSettimana, OraApertura, OraChiusura | Più fasce per giorno ammesse? *(domanda PO)* |

**API previste**
| Metodo | Route | Ruolo | Scopo |
|---|---|---|---|
| GET | `/api/spazi?tipo=&capienzaMin=&edificioId=&attrezzatura=` | tutti | Ricerca |
| GET | `/api/spazi/{id}` | tutti | Dettaglio |
| POST | `/api/spazi` | Personale | Crea |
| PUT | `/api/spazi/{id}` | Personale | Modifica |
| POST | `/api/spazi/{id}/disattiva` | Personale | Disattiva |
| GET/POST/PUT | `/api/spazi/edifici`, `/api/spazi/attrezzature` | Personale (scrittura) | Anagrafiche |

**Eventi pubblicati:** `SpazioCreato`, `SpazioAggiornato`, `SpazioDisattivato` (+ orari di apertura, vedi incertezza I-3).

**Regole**
- Capienza > 0.
- Orario di chiusura > orario di apertura.
- Un nome di spazio è univoco nell'edificio *(proposta)*.

### 6.2 Prenotazioni ⭐

**Responsabilità:** prenotare senza conflitti, cancellare, calcolare la disponibilità.

| Entità | Campi proposti |
|---|---|
| `Prenotazione` | Id, SpazioId, UtenteId, Inizio, Fine, NumeroPartecipanti, Motivo *(opz.)*, Stato (`Confermata`/`Cancellata`), CreataIl, CancellataIl, RowVersion |
| `SpazioPrenotabile` | SpazioId, Nome, Capienza, Prenotabile, Attivo, Orari — copia aggiornata dagli eventi del servizio Spazi |

**API previste**
| Metodo | Route | Ruolo | Scopo |
|---|---|---|---|
| GET | `/api/prenotazioni/disponibilita?spazioId=&giorno=` | autenticato | Fasce libere |
| GET | `/api/prenotazioni/disponibilita/ricerca?giorno=&inizio=&fine=&capienzaMin=` | autenticato | Spazi liberi in un intervallo *(proposta)* |
| POST | `/api/prenotazioni` | Studente, Personale | Crea → `201` oppure `409 Conflict` |
| GET | `/api/prenotazioni/mie` | autenticato | Le mie prenotazioni |
| DELETE | `/api/prenotazioni/{id}` | proprietario o Personale | Cancella |
| GET | `/api/prenotazioni?spazioId=&giorno=` | Personale | Vista per spazio |

**Invarianti (da trasformare in criteri di accettazione)**
1. **INV-1** Nessuna sovrapposizione tra prenotazioni `Confermata` dello stesso spazio. Intervalli semiaperti: una prenotazione 10:00–11:00 e una 11:00–12:00 **non** sono in conflitto.
2. **INV-2** `Fine > Inizio`.
3. **INV-3** L'intervallo è interamente dentro l'orario di apertura.
4. **INV-4** Lo spazio è `Attivo` e `Prenotabile`.
5. **INV-5** `NumeroPartecipanti ≤ Capienza`.
6. **INV-6** Non si prenota nel passato.
7. **INV-7** Una prenotazione cancellata non torna confermata *(proposta: si crea una nuova prenotazione)*.

**Meccanismo anti-conflitto (SQL Server):** lock `UPDLOCK, ROWLOCK` sulla riga `SpazioPrenotabile` + verifica sovrapposizione + insert nella stessa transazione. Vedi [architettura.md](architettura.md).

**Eventi:** consuma `Spazio*`; pubblica `PrenotazioneConfermata`, `PrenotazioneCancellata` (tramite outbox).

**Codici di errore da rendere comprensibili in UI**
| Caso | HTTP | Messaggio utente proposto |
|---|---|---|
| Conflitto | 409 | "Lo spazio è già prenotato in questa fascia. Ecco le fasce libere…" |
| Fuori orario | 422 | "Lo spazio è aperto dalle X alle Y" |
| Capienza superata | 422 | "Lo spazio ospita al massimo N persone" |
| Spazio non prenotabile/disattivato | 422 | "Questo spazio non è prenotabile" |
| Cancellazione di prenotazione altrui | 403 | "Non puoi cancellare questa prenotazione" |

### 6.3 Presenze

**Responsabilità:** cosa è successo davvero e report per il personale.

| Entità | Campi proposti |
|---|---|
| `PrenotazioneProiezione` | PrenotazioneId, SpazioId, UtenteId, Inizio, Fine, Stato — da eventi |
| `RegistrazioneUtilizzo` | Id, PrenotazioneId, Tipo (`CheckIn`/`NoShow`), RegistrataIl, RegistrataDa |

**API previste**
| Metodo | Route | Ruolo | Scopo |
|---|---|---|---|
| POST | `/api/presenze/checkin` `{prenotazioneId}` | proprietario | Check-in |
| POST | `/api/presenze/{prenotazioneId}/no-show` | Personale | Segna no-show |
| GET | `/api/presenze/report/spazi?da=&a=&edificioId=` | Personale | Report aggregato |

**Regole**
- Check-in ammesso solo nella finestra `[Inizio − X min, Inizio + Y min]` *(X, Y da concordare)*.
- Una prenotazione ha al massimo un check-in.
- Il report mostra per ogni spazio/periodo: ore prenotate, ore con check-in, no-show, **prenotazioni senza informazione**, e cancellazioni.
- Il report **non** afferma che uno spazio sia "inutilizzato" se mancano informazioni; lo dichiara esplicitamente.

### 6.4 Autenticazione

| Entità | Campi |
|---|---|
| `Utente` | Id, Username, NomeVisualizzato, PasswordHash, Ruolo |

| Metodo | Route | Scopo |
|---|---|---|
| POST | `/api/autenticazione/login` | Restituisce JWT |
| GET | `/api/autenticazione/me` | Profilo e ruolo |

- Utenti **solo sintetici**, caricati da seed.
- Registrazione self-service **fuori perimetro** *(proposta)*.
- La chiave di firma JWT è un segreto: user-secrets / variabile d'ambiente.

### 6.5 Gateway e Frontend

- Il frontend chiama solo `/api/...` sul gateway.
- Il gateway valida il JWT; i servizi verificano il ruolo sulle singole operazioni.
- Schermate proposte:
  - **Studente:** ricerca spazi · dettaglio spazio con disponibilità · prenota · le mie prenotazioni · check-in.
  - **Personale:** gestione spazi/edifici/attrezzature · prenotazioni per spazio · report utilizzo.
- Requisito trasversale: utilizzabile da desktop e mobile (responsive), dimostrato in demo.

---

## 7. Flussi tra servizi (da conoscere per stimare le storie)

### F1. Prenotazione
```
Angular → Gateway → Prenotazioni
  Prenotazioni: lock SpazioPrenotabile → verifica INV-1..6 → INSERT + outbox (1 transazione)
  → 201 Created
  outbox → broker → Presenze: crea PrenotazioneProiezione
```
### F2. Cancellazione
```
Angular → Gateway → Prenotazioni: Stato = Cancellata + outbox → 204
  → lo slot è subito disponibile in GET disponibilità
  → broker → Presenze aggiorna la proiezione
```
### F3. Modifica catalogo
```
Personale → Gateway → Spazi: aggiorna Spazio + outbox
  → broker → Prenotazioni aggiorna SpazioPrenotabile
```
> **Consistenza eventuale:** dopo una modifica del servizio Spazi, Prenotazioni si aggiorna dopo pochi istanti. Va dichiarato come limite noto e considerato nei criteri di accettazione (vedi I-4).

### F4. Check-in
```
Studente → Gateway → Presenze: verifica proprietario + finestra temporale → registra
```

**Implicazione per le storie:** una storia di E2 senza Spazi e senza copia locale non è dimostrabile. Il primo incremento deve includere almeno Spazi (lettura + seed) e Prenotazioni.

---

## 8. Proposta di backlog iniziale (da validare con il PO)

> La versione aggiornata e completa, nel formato obbligatorio con descrizione, criteri e story point, è in [user-stories-bozza.md](user-stories-bozza.md). La tabella seguente resta come mappa storie → epiche.

Formato: *Come [attore] voglio [azione] così da [valore]*. Priorità MoSCoW **proposta**.

### Incremento 1: prenotare con fiducia

| ID | Storia | Epica | Priorità |
|---|---|---|---|
| US-01 | Come studente voglio cercare gli spazi filtrando per tipo, capienza ed edificio, così da trovare quelli adatti alla mia attività | E1 | Must |
| US-02 | Come studente voglio vedere il dettaglio di uno spazio (attrezzature, capienza, orari), così da capire se fa al caso mio | E1 | Must |
| US-03 | Come studente voglio vedere le fasce libere di uno spazio in un giorno, così da scegliere quando andarci | E2 | Must |
| US-04 | Come studente voglio prenotare uno spazio per una fascia oraria, così da avere la certezza di poterlo usare | E2 | Must |
| US-05 | Come studente voglio essere avvisato chiaramente se la fascia è già occupata e vedere le alternative, così da riorganizzarmi subito | E2 | Must |
| US-06 | Come studente voglio vedere l'elenco delle mie prenotazioni future, così da ritrovare le conferme in un unico posto | E3 | Must |
| US-07 | Come studente voglio cancellare una mia prenotazione, così da liberare lo spazio per altri | E3 | Must |

### Incremento 2: ruoli e gestione

| ID | Storia | Epica | Priorità |
|---|---|---|---|
| US-08 | Come utente voglio accedere con le mie credenziali, così da vedere e gestire solo ciò che mi compete | E4 | Must |
| US-09 | Come personale voglio creare e modificare spazi, orari e attrezzature, così che le informazioni siano aggiornate | E5 | Must |
| US-10 | Come personale voglio disattivare uno spazio, così che non venga più prenotato | E5 | Should |
| US-11 | Come personale voglio vedere le prenotazioni di uno spazio in un giorno, così da rispondere alle richieste degli studenti | E5 | Should |
| US-12 | Come studente voglio cercare gli spazi liberi in un intervallo orario, così da non controllarli uno a uno | E1/E2 | Should |

### Incremento 3: utilizzo effettivo

| ID | Storia | Epica | Priorità |
|---|---|---|---|
| US-13 | Come studente voglio fare check-in quando arrivo nello spazio, così da confermare l'uso effettivo | E6 | Must |
| US-14 | Come personale voglio segnare una prenotazione come no-show, così da registrare che lo spazio non è stato usato | E6 | Should |
| US-15 | Come personale voglio un report per spazio e periodo che distingua prenotato/usato/non usato/senza informazione, così da interpretare l'uso senza trarre conclusioni non supportate | E7 | Must |
| US-16 | Come personale voglio filtrare il report per edificio e tipo di spazio, così da confrontare aree del campus | E7 | Could |

### Candidati fuori dal minimo (da proporre, non da assumere)
| ID | Storia | Note |
|---|---|---|
| US-17 | Notifica a chi cercava uno spazio quando si libera (lista d'attesa) | Richiede un nuovo servizio o modulo Notifiche |
| US-18 | Prenotazioni ricorrenti (es. ogni martedì) | Complica INV-1 |
| US-19 | Cancellazione automatica per no-show | Regola di business delicata |
| US-20 | Esportazione CSV del report | |

---

## 9. Esempi di criteri di accettazione

Formato *Dato / Quando / Allora*, con dati sintetici di riferimento.

### US-04 / US-05: prenotare senza conflitti
- **CA-1** *Dato* l'Aula A1 (capienza 30) aperta 08:00–20:00 e libera il 15/12 dalle 10 alle 12, *quando* Studente1 prenota 10:00–11:00 per 20 persone, *allora* riceve conferma e la fascia risulta occupata nella disponibilità.
- **CA-2** *Dato* A1 prenotata 10:00–11:00, *quando* Studente2 prova 10:30–11:30, *allora* riceve il messaggio di conflitto con le fasce libere e non viene creata alcuna prenotazione.
- **CA-3** *Dato* A1 prenotata 10:00–11:00, *quando* Studente2 prenota 11:00–12:00, *allora* la prenotazione è confermata (confini adiacenti non sono conflitto).
- **CA-4** *Dato* A1 libera 10:00–11:00, *quando* 20 richieste concorrenti prenotano la stessa fascia, *allora* **esattamente una** è confermata e le altre ricevono conflitto. *(Verificato con test di integrazione su SQL Server reale.)*
- **CA-5** *Quando* uno studente prenota 19:00–21:00, *allora* riceve l'errore "fuori orario".
- **CA-6** *Quando* uno studente indica 40 partecipanti per A1, *allora* riceve l'errore di capienza.

### US-07: cancellare
- **CA-1** *Dato* una prenotazione mia 10–11, *quando* la cancello, *allora* scompare dalle mie prenotazioni future e la fascia torna libera nella disponibilità.
- **CA-2** *Quando* provo a cancellare la prenotazione di un altro studente, *allora* ricevo un errore e la prenotazione resta valida.

### US-15: report
- **CA-1** *Dato* 4 prenotazioni di A1 nella settimana (2 con check-in, 1 no-show, 1 senza registrazione), *quando* il personale apre il report, *allora* vede 2 usate, 1 non usata, 1 **senza informazione**, indicate separatamente.
- **CA-2** Il report riporta una nota esplicita: le prenotazioni senza informazione non indicano che lo spazio sia stato libero o inutilizzato.

---

## 10. Definition of Done proposta

Un elemento è *Done* quando:
1. Il codice è su `main` tramite pull request revisionata da almeno un altro membro.
2. La CI è verde (build + test backend e frontend).
3. I criteri di accettazione sono verificati, con test automatici dove possibile e prova manuale documentata altrimenti.
4. Le regole di dominio hanno unit test; gli endpoint toccati hanno test di integrazione su SQL Server reale.
5. Funziona avviando il sistema da zero con `docker compose up --build` e i dati di seed.
6. La UI funziona su desktop e su viewport mobile.
7. Nessun segreto o dato personale reale introdotto.
8. Documentazione aggiornata se cambia API, avvio o regole (README, `docs/`).
9. Limiti noti dichiarati nella PR.

---

## 11. Dati sintetici necessari

Le storie devono indicare quali dati di seed richiedono. Base proposta:
- **Edifici:** 3 (es. Edificio A, B, Biblioteca).
- **Spazi:** ~15, con tutti i tipi, capienze diverse, almeno 1 non prenotabile e 1 disattivato.
- **Orari:** standard 08:00–20:00 lun–ven, almeno uno spazio con orario diverso e uno chiuso il sabato.
- **Utenti:** 5 studenti, 2 personale (credenziali solo di sviluppo, nel seed, mai riutilizzate altrove).
- **Prenotazioni:** su due settimane, comprese adiacenti, cancellate e passate.
- **Presenze:** mix di check-in, no-show e prenotazioni senza informazione.

---

## 12. Incertezze e limiti (domande aperte per il PO)

| ID | Domanda | Impatto |
|---|---|---|
| I-1 | Granularità delle prenotazioni: libera, a slot di 30 min o di 1 ora? | US-03, US-04, UI |
| I-2 | Durata massima di una prenotazione e anticipo massimo (es. 14 giorni)? | Nuove invarianti |
| I-3 | Più fasce di apertura al giorno? Chiusure straordinarie (festività)? | Spazi, INV-3 |
| I-4 | È accettabile che una modifica del catalogo si rifletta sulle prenotazioni dopo qualche secondo? | Consistenza eventuale |
| I-5 | Cosa succede alle prenotazioni future se uno spazio viene disattivato o cambia orario? | US-10: cancellazione automatica, avviso, blocco della modifica? |
| I-6 | Limite di prenotazioni attive per studente? | Equità d'uso |
| I-7 | Gli studenti possono prenotare i laboratori o solo il personale? | Regole per ruolo |
| I-8 | Entro quando si può cancellare? | US-07 |
| I-9 | Finestra di check-in (X, Y minuti) e chi può segnare il no-show? | US-13, US-14 |
| I-10 | Uno studente può vedere *chi* ha prenotato uno spazio? | Privacy: proposta di mostrare solo "occupato" |
| I-11 | Fuso orario: tutto in ora locale del campus? | Persistenza (`DateTimeOffset`) |
| I-12 | Il visitatore non autenticato può consultare il catalogo? | Gateway, E1 |

**Limiti noti dell'architettura**
- La consistenza tra servizi è **eventuale**: le copie locali (spazi in Prenotazioni, prenotazioni in Presenze) possono essere in ritardo di qualche istante.
- I dati sintetici verificano il software, **non** dimostrano benefici o risparmi reali del campus.
- Il deploy in cloud è **progettato, non eseguito**.

---

## 13. Indicazioni per scrivere le storie

1. **Una storia è un bisogno, non un servizio.** Evitare "Creare l'API del servizio Spazi": è un task dentro US-01.
2. **Fette verticali:** ogni storia deve essere dimostrabile dalla UI (o, se concordato, dall'API).
3. **INVEST:** indipendente, negoziabile, di valore, stimabile, piccola, testabile. Se una storia attraversa 3 servizi ed è grande, dividerla per regola (es. US-04 senza capienza, poi capienza) piuttosto che per livello tecnico.
4. **Collegare ogni storia a:** epica, servizi coinvolti, invarianti (INV-x), domande aperte (I-x), dati di seed necessari.
5. **I task tecnici** (EF Core, migrazioni, MassTransit, outbox, JWT, Testcontainers) vanno sotto le storie che li richiedono la prima volta.
6. **Le domande aperte** non si risolvono con un'assunzione silenziosa: si scrive l'assunzione nella storia e la si porta al PO.

### Template di storia (formato obbligatorio del corso)
```
[ID] - Titolo della User Story

User Story
Come [tipo di utente / ruolo]
voglio [funzionalità / azione]
in modo da [beneficio / valore ottenuto].

Descrizione
Cosa deve essere realizzato e in quale contesto, senza dettagli tecnici di implementazione.

Criteri di accettazione
La storia è considerata completata quando:
[ ] [Criterio di accettazione 1]
[ ] [Criterio di accettazione 2]
[ ] [Criterio di accettazione 3]

Story Point
Stima: [1 / 2 / 3 / 5 / 8 / 13]
```
Le informazioni di questo handoff che il formato non prevede (servizi, invarianti, dati di seed, domande aperte) **non vanno aggiunte alla storia**: si usano per scrivere i criteri e la descrizione, e i dettagli tecnici finiscono nei task (sub-issue).

La bozza completa è in [user-stories-bozza.md](user-stories-bozza.md).
