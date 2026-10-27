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
| Un gruppo trova occupata un'aula che sembrava libera | Sapere con fiducia se uno spazio è disponibile | Rooms + Bookings |
| Le conferme passano da messaggi sparsi | Avere una conferma unica e ritrovabile | Bookings |
| Una rinuncia non libera lo spazio per altri | La cancellazione rende subito lo spazio disponibile | Bookings |
| Due gruppi si aspettano di usare lo stesso spazio | **Nessun impegno incompatibile** | Bookings (invariante) |
| Il personale ha percezioni contrastanti sull'uso | Distinguere intenzione (prenotazione), uso effettivo e assenza di informazione | Bookings |

### Criterio di completamento di questo handoff
Il gruppo è in grado di produrre una prima versione di Product Backlog ordinabile dal PO, con almeno le storie del **Product Goal** e del **primo incremento** dotate di criteri di accettazione.

---

## 2. Input e stato attuale

### Stato del repository
| Elemento | Stato |
|---|---|
| Solution .NET 10, 4 servizi + gateway YARP | ✅ servizi in sviluppo; Rooms e Identity espongono API applicative |
| Health check `/health/live`, `/health/ready` | ✅ |
| Contratti eventi (`SmartCampus.Contracts`) | ✅ Rooms pubblica gli eventi di ciclo di vita con gli orari; Bookings aggiorna la copia locale |
| Frontend Angular 22 con proxy verso il gateway | ✅ shell con ricerca degli spazi |
| docker-compose (SQL Server, RabbitMQ, servizi, web) | ✅ configurazione valida; build immagini da verificare con Docker attivo |
| CI GitHub Actions | ✅ eseguita e verde sul commit precedente alla rebase; da rieseguire sul branch aggiornato |
| Database, EF Core, migrazioni, seed | ✅ Identity, Rooms e Bookings hanno persistenza e migrazioni |
| Autenticazione JWT | ✅ Identity emette i token; Rooms applica l'autorizzazione Staff |
| Messaggistica (MassTransit + outbox) | ✅ versione 8.5.12 condivisa; Rooms usa RabbitMQ e outbox EF Core; Bookings consuma gli eventi |
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
| **Studente** | `Student` | Trovare uno spazio adatto, prenotarlo con certezza, gestire le proprie prenotazioni, segnalare la presenza |
| **Personale del campus** | `Staff` | Gestire il catalogo degli spazi, avere visibilità sulle prenotazioni, interpretare l'utilizzo con consapevolezza dei limiti dei dati |
| **Visitatore non autenticato** | — | *(proposta)* consultare il catalogo senza prenotare. Da validare con il PO |
| **Sistema** | — | Propagare eventi tra servizi, mantenere la coerenza delle copie locali |

> Domanda per il PO: esistono altri ruoli (docente, rappresentante di un'associazione studentesca) con regole diverse? Se sì, sono nel perimetro?

---

## 4. Glossario condiviso

Usare **questi termini**: il termine italiano in storie, UI e documentazione; il nome inglese nel codice, nelle API e nel database. Se un termine cambia, si aggiorna qui.

| Termine | Nome nel codice | Significato | Servizio proprietario |
|---|---|---|---|
| **Spazio** | `Room` | Luogo prenotabile o no: aula, laboratorio, sala studio | Rooms |
| **Edificio** | `Building` | Raggruppamento fisico di spazi | Rooms |
| **Attrezzatura** | `Equipment` | Dotazione di uno spazio (proiettore, PC, lavagna…) | Rooms |
| **Orario di apertura** | `OpeningHours` | Fasce in cui uno spazio può essere prenotato | Rooms |
| **Spazio prenotabile** | `BookableRoom` | Copia locale in Bookings dei soli dati che servono a prenotare | Bookings |
| **Prenotazione** | `Reservation` | Impegno di un utente su uno spazio per un intervallo `[Start, End)` | Bookings |
| **Conflitto** | — | Due prenotazioni *confermate* dello stesso spazio con intervalli sovrapposti | Bookings |
| **Disponibilità** | `Availability` | Intervalli liberi di uno spazio in un giorno, calcolati da orari − prenotazioni | Bookings |
| **Check-in** | `CheckIn` | Registrazione che lo spazio è stato effettivamente usato | Bookings |
| **No-show** | `NoShow` | Prenotazione confermata senza check-in nella finestra prevista | Bookings |
| **Nessuna informazione** | — | Né check-in né no-show registrato: **non** significa "non usato" | Bookings |
| **Organizzatore** | `OrganizerId` | Chi crea la prenotazione; l'unico che può cancellarla | Bookings |
| **Membro** | `ReservationMember` | Altro studente incluso in una prenotazione di gruppo; la vede e può fare check-in | Bookings |
| **Studente / Personale** | `Student` / `Staff` | Ruoli degli utenti | Identity |

---

## 5. Mappa servizi → capacità → epiche

Le **epiche** sono aree di valore; le storie dentro un'epica possono toccare più servizi (fetta verticale).

| Epica | Valore | Servizi coinvolti | Incremento proposto |
|---|---|---|---|
| **E1. Trovare uno spazio** | So quali spazi esistono e quali fanno al caso mio | Rooms, Frontend | 1 |
| **E2. Prenotare senza conflitti** | Ho una conferma affidabile, nessuno ha lo stesso spazio nello stesso momento | Bookings, Rooms (eventi), Frontend | 1 |
| **E3. Gestire le mie prenotazioni** | Ritrovo le conferme, rinuncio e libero lo spazio | Bookings, Frontend | 1 |
| **E4. Accesso e ruoli** | Ognuno vede e fa solo ciò che gli compete | Identity, Gateway, tutti | 2 |
| **E5. Gestire il catalogo** | Il personale mantiene le informazioni aggiornate | Rooms, Bookings (eventi), Frontend | 1–2 |
| **E6. Registrare l'utilizzo effettivo** | Distinguere intenzione e uso reale | Bookings, Frontend | 3 |
| **E7. Comprendere l'utilizzo** | Il personale interpreta i dati senza trasformare ipotesi in certezze | Bookings, Frontend | 3 |
| **E8. Qualità, avvio e prove** *(abilitante)* | Il prodotto è riproducibile e verificabile | Tutti | trasversale |

> E8 non è valore per l'utente finale: va gestita tramite **Definition of Done** e task tecnici dentro le storie, non come storie "Come sviluppatore voglio…", salvo accordo con il PO.

---

## 6. Dettaglio per servizio (riferimento per scrivere storie e task)

### 6.1 Rooms

**Responsabilità:** cosa esiste nel campus e cosa è prenotabile.

| Entità | Campi proposti | Note |
|---|---|---|
| `Building` | Id, Name, Address/Code | |
| `Room` | Id, BuildingId, Name, Type (`Classroom`/`Lab`/`StudyRoom`), Capacity, Floor, IsBookable, IsActive | `IsActive=false` = dismesso, non cancellato fisicamente |
| `Equipment` | Id, Name | Relazione N:M con `Room` |
| `OpeningHours` | RoomId, DayOfWeek, OpensAt, ClosesAt | Decisione I-3: una sola fascia per spazio e giorno; i giorni chiusi non hanno righe. Le chiusure straordinarie non sono modellate nel primo incremento. |

**API previste**
| Metodo | Route | Ruolo | Scopo |
|---|---|---|---|
| GET | `/api/rooms?type=&minCapacity=&buildingId=&equipment=` | tutti | Ricerca |
| GET | `/api/rooms/{id}` | tutti | Dettaglio |
| POST | `/api/rooms` | Staff | Crea |
| PUT | `/api/rooms/{id}` | Staff | Modifica |
| POST | `/api/rooms/{id}/deactivate` | Staff | Disattiva |
| GET/POST/PUT | `/api/rooms/buildings`, `/api/rooms/equipment` | Staff (scrittura) | Anagrafiche |

**Eventi pubblicati:** `RoomCreated`, `RoomUpdated`, `RoomDeactivated`. `RoomCreated` e `RoomUpdated` trasportano lo snapshot degli orari come lista di `{ DayOfWeek, OpensAt, ClosesAt }`; i giorni senza fascia sono chiusi. Rooms usa l'EF Core Bus Outbox per rendere atomici gli aggiornamenti di database e l'accodamento dei messaggi; all'avvio pubblica una volta gli eventi corrispondenti agli spazi seed.

**Regole**
- Capienza > 0.
- Orario di chiusura > orario di apertura.
- Un nome di spazio è univoco nell'edificio *(proposta)*.

### 6.2 Bookings ⭐

**Responsabilità:** prenotare senza conflitti, singolarmente o in gruppo; cancellare; calcolare la disponibilità; registrare check-in e mancate presentazioni; produrre i report per il personale. Le presenze, prima nel servizio separato *Attendance*, sono state accorpate qui su indicazione del docente.

| Entità | Campi proposti |
|---|---|
| `Reservation` | Id, RoomId, OrganizerId, Start, End, Participants, Purpose *(opz.)*, Status (`Confirmed`/`Cancelled`), CreatedAt, CancelledAt, RowVersion |
| `ReservationMember` | ReservationId, UserId: gli altri studenti di una prenotazione di gruppo |
| `BookableRoom` | RoomId, Name, Capacity, IsBookable, IsActive, OpeningHours — copia aggiornata dagli eventi del servizio Rooms |
| `AttendanceRecord` | Id, ReservationId, Type (`CheckIn`/`NoShow`), RecordedAt, RecordedBy |

**API previste**
| Metodo | Route | Ruolo | Scopo |
|---|---|---|---|
| GET | `/api/bookings/availability?roomId=&date=` | autenticato | Fasce libere |
| GET | `/api/bookings/availability/search?date=&start=&end=&minCapacity=` | autenticato | Spazi liberi in un intervallo *(proposta)* |
| POST | `/api/bookings` | Student, Staff | Crea (singola o con membri) → `201` oppure `409 Conflict` |
| GET | `/api/bookings/mine` | autenticato | Le mie prenotazioni (come organizzatore o membro) |
| DELETE | `/api/bookings/{id}` | organizzatore o Staff | Cancella |
| GET | `/api/bookings?roomId=&date=` | Staff | Vista per spazio |
| POST | `/api/bookings/{id}/check-in` | organizzatore o membro | Check-in |
| POST | `/api/bookings/{id}/no-show` | Staff | Segna no-show |
| GET | `/api/bookings/reports/rooms?from=&to=&buildingId=` | Staff | Report aggregato |

**Invarianti (da trasformare in criteri di accettazione)**
1. **INV-1** Nessuna sovrapposizione tra prenotazioni `Confirmed` dello stesso spazio. Intervalli semiaperti: una prenotazione 10:00–11:00 e una 11:00–12:00 **non** sono in conflitto.
2. **INV-2** `End > Start`.
3. **INV-3** L'intervallo è interamente dentro l'orario di apertura.
4. **INV-4** Lo spazio è `IsActive` e `IsBookable`.
5. **INV-5** `Participants` = organizzatore + membri, e `Participants ≤ Capacity`.
6. **INV-6** Non si prenota nel passato.
7. **INV-7** Una prenotazione cancellata non torna confermata *(proposta: si crea una nuova prenotazione)*.
8. **INV-8** Nessun utente (organizzatore o membro) è in due prenotazioni `Confirmed` sovrapposte, anche in spazi diversi.
9. **INV-9** I membri sono utenti con ruolo `Student`.

**Meccanismo anti-conflitto (SQL Server):** per lo spazio, lock `UPDLOCK, ROWLOCK` sulla riga `BookableRoom` + verifica sovrapposizione + insert nella stessa transazione. Vedi [architettura.md](architettura.md). Per INV-8 il meccanismo è **da definire**: il lock sullo spazio non protegge la stessa persona che prenota due spazi diversi nello stesso istante.

**Eventi:** consuma `Room*`. Non pubblica eventi.

**Regole di check-in e report**
- Check-in ammesso solo nella finestra `[Start − X min, Start + Y min]` *(X, Y da concordare)*.
- Il check-in può farlo l'organizzatore o un membro; una prenotazione ha al massimo un check-in, valido per tutto il gruppo.
- Il report mostra per ogni spazio/periodo: ore prenotate, ore con check-in, no-show, **prenotazioni senza informazione**, e cancellazioni.
- Il report **non** afferma che uno spazio sia "inutilizzato" se mancano informazioni; lo dichiara esplicitamente.
- *(Opzione con riserva)* storico dei check-in su MongoDB: vedi [architettura.md](architettura.md#opzione-da-valutare-audit-dei-check-in-su-mongodb).

**Codici di errore da rendere comprensibili in UI**
| Caso | HTTP | Messaggio utente proposto |
|---|---|---|
| Conflitto sullo spazio | 409 | "Lo spazio è già prenotato in questa fascia. Ecco le fasce libere…" |
| Conflitto sulla persona | 409 | "Hai già una prenotazione in questa fascia" / "Uno o più membri hanno già una prenotazione in questa fascia" |
| Fuori orario | 422 | "Lo spazio è aperto dalle X alle Y" |
| Capienza superata | 422 | "Lo spazio ospita al massimo N persone" |
| Spazio non prenotabile/disattivato | 422 | "Questo spazio non è prenotabile" |
| Cancellazione di prenotazione altrui | 403 | "Non puoi cancellare questa prenotazione" |

### 6.3 Identity

| Entità | Campi |
|---|---|
| `User` | Id, Username, DisplayName, PasswordHash, Role (`Student`/`Staff`) |

| Metodo | Route | Scopo |
|---|---|---|
| POST | `/api/identity/login` | Restituisce JWT |
| GET | `/api/identity/me` | Profilo e ruolo |

- Utenti **solo sintetici**, caricati da seed.
- Registrazione self-service **fuori perimetro** *(proposta)*.
- La chiave di firma JWT è un segreto: user-secrets / variabile d'ambiente.

### 6.4 Gateway e Frontend

- Il frontend chiama solo `/api/...` sul gateway.
- Il gateway valida il JWT; i servizi verificano il ruolo sulle singole operazioni.
- Schermate proposte:
  - **Studente:** ricerca spazi · dettaglio spazio con disponibilità · prenota (da solo o con un gruppo) · le mie prenotazioni · check-in.
  - **Personale:** gestione spazi/edifici/attrezzature · prenotazioni per spazio · report utilizzo.
- Requisito trasversale: utilizzabile da desktop e mobile (responsive), dimostrato in demo.

---

## 7. Flussi tra servizi (da conoscere per stimare le storie)

### F1. Prenotazione (singola o di gruppo)
```
Angular → Gateway → Bookings
  Bookings: lock BookableRoom → verifica INV-1..9 → INSERT Reservation + ReservationMember (1 transazione)
  → 201 Created
```
### F2. Cancellazione
```
Angular → Gateway → Bookings: Status = Cancelled → 204
  → lo slot è subito disponibile in GET /api/bookings/availability, per tutti i membri
```
### F3. Modifica catalogo
```
Staff → Gateway → Rooms: aggiorna Room + outbox
  → broker (RoomUpdated) → Bookings aggiorna BookableRoom
```
> **Consistenza eventuale:** dopo una modifica nel servizio Rooms, Bookings si aggiorna dopo pochi istanti. Va dichiarato come limite noto e considerato nei criteri di accettazione (vedi I-4).

### F4. Check-in
```
Student → Gateway → Bookings: verifica organizzatore o membro + finestra temporale → registra AttendanceRecord
```

**Implicazione per le storie:** una storia di E2 senza Rooms e senza copia locale non è dimostrabile. Il primo incremento deve includere almeno Rooms (lettura + seed) e Bookings.

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
| US-05 | Come studente voglio prenotare uno spazio indicando gli altri studenti che parteciperanno, così che tutto il gruppo abbia la certezza dello spazio | E2 | Must |
| US-06 | Come studente voglio essere avvisato chiaramente se la fascia è già occupata e vedere le alternative, così da riorganizzarmi subito | E2 | Must |
| US-07 | Come studente voglio vedere l'elenco delle mie prenotazioni future, così da ritrovare le conferme in un unico posto | E3 | Must |
| US-08 | Come studente voglio cancellare una mia prenotazione, così da liberare lo spazio per altri | E3 | Must |

### Incremento 2: ruoli e gestione

| ID | Storia | Epica | Priorità |
|---|---|---|---|
| US-09 | Come utente voglio accedere con le mie credenziali, così da vedere e gestire solo ciò che mi compete | E4 | Must |
| US-10 | Come personale voglio creare e modificare spazi, orari e attrezzature, così che le informazioni siano aggiornate | E5 | Must |
| US-11 | Come personale voglio disattivare uno spazio, così che non venga più prenotato | E5 | Should |
| US-12 | Come personale voglio vedere le prenotazioni di uno spazio in un giorno, così da rispondere alle richieste degli studenti | E5 | Should |
| US-13 | Come studente voglio cercare gli spazi liberi in un intervallo orario, così da non controllarli uno a uno | E1/E2 | Should |

### Incremento 3: utilizzo effettivo

| ID | Storia | Epica | Priorità |
|---|---|---|---|
| US-14 | Come studente voglio fare check-in quando arrivo nello spazio, così da confermare l'uso effettivo | E6 | Must |
| US-15 | Come personale voglio segnare una prenotazione come no-show, così da registrare che lo spazio non è stato usato | E6 | Should |
| US-16 | Come personale voglio un report per spazio e periodo che distingua prenotato/usato/non usato/senza informazione, così da interpretare l'uso senza trarre conclusioni non supportate | E7 | Must |
| US-17 | Come personale voglio filtrare il report per edificio e tipo di spazio, così da confrontare aree del campus | E7 | Could |

### Candidati fuori dal minimo (da proporre, non da assumere)
| ID | Storia | Note |
|---|---|---|
| US-18 | Notifica a chi cercava uno spazio quando si libera (lista d'attesa) | Richiede un nuovo servizio o modulo Notifiche |
| US-19 | Prenotazioni ricorrenti (es. ogni martedì) | Complica INV-1 |
| US-20 | Cancellazione automatica per no-show | Regola di business delicata |
| US-21 | Esportazione CSV del report | |

---

## 9. Esempi di criteri di accettazione

Formato *Dato / Quando / Allora*, con dati sintetici di riferimento.

### US-04 / US-06: prenotare senza conflitti
- **CA-1** *Dato* l'Aula A1 (capienza 30) aperta 08:00–20:00 e libera il 15/12 dalle 10 alle 12, *quando* Studente1 prenota 10:00–11:00 per 20 persone, *allora* riceve conferma e la fascia risulta occupata nella disponibilità.
- **CA-2** *Dato* A1 prenotata 10:00–11:00, *quando* Studente2 prova 10:30–11:30, *allora* riceve il messaggio di conflitto con le fasce libere e non viene creata alcuna prenotazione.
- **CA-3** *Dato* A1 prenotata 10:00–11:00, *quando* Studente2 prenota 11:00–12:00, *allora* la prenotazione è confermata (confini adiacenti non sono conflitto).
- **CA-4** *Dato* A1 libera 10:00–11:00, *quando* 20 richieste concorrenti prenotano la stessa fascia, *allora* **esattamente una** è confermata e le altre ricevono conflitto. *(Verificato con test di integrazione su SQL Server reale.)*
- **CA-5** *Quando* uno studente prenota 19:00–21:00, *allora* riceve l'errore "fuori orario".
- **CA-6** *Quando* uno studente indica 40 partecipanti per A1, *allora* riceve l'errore di capienza.

### US-08: cancellare
- **CA-1** *Dato* una prenotazione mia 10–11, *quando* la cancello, *allora* scompare dalle mie prenotazioni future e la fascia torna libera nella disponibilità.
- **CA-2** *Quando* provo a cancellare la prenotazione di un altro studente, *allora* ricevo un errore e la prenotazione resta valida.

### US-16: report
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
| I-3 | Più fasce di apertura al giorno? Chiusure straordinarie (festività)? | **Decisione per il primo incremento:** una fascia al giorno; giorni chiusi assenti, chiusure straordinarie fuori ambito. Riaprire con il PO se cambiano i requisiti. |
| I-4 | È accettabile che una modifica del catalogo si rifletta sulle prenotazioni dopo qualche secondo? | Consistenza eventuale |
| I-5 | Cosa succede alle prenotazioni future se uno spazio viene disattivato o cambia orario? | US-11: cancellazione automatica, avviso, blocco della modifica? |
| I-6 | Limite di prenotazioni attive per studente? | Equità d'uso |
| I-7 | Gli studenti possono prenotare i laboratori o solo il personale? | Regole per ruolo |
| I-8 | Entro quando si può cancellare? | US-08 |
| I-9 | Finestra di check-in (X, Y minuti) e chi può segnare il no-show? | US-14, US-15 |
| I-10 | Uno studente può vedere *chi* ha prenotato uno spazio? | Privacy: proposta di mostrare solo "occupato" |
| I-11 | Fuso orario: tutto in ora locale del campus? | Persistenza (`DateTimeOffset`) |
| I-12 | Il visitatore non autenticato può consultare il catalogo? | Gateway, E1 |

**Limiti noti dell'architettura**
- La consistenza tra servizi è **eventuale**: le copie locali (spazi in Bookings) possono essere in ritardo di qualche istante.
- I dati sintetici verificano il software, **non** dimostrano benefici o risparmi reali del campus.
- Il deploy in cloud è **progettato, non eseguito**.

---

## 13. Indicazioni per scrivere le storie

1. **Una storia è un bisogno, non un servizio.** Evitare "Creare l'API del servizio Rooms": è un task dentro US-01.
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
