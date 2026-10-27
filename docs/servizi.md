# I servizi di SmartCampus, spiegati semplici

SmartCampus non è un unico programma, ma è diviso in **pezzi indipendenti** (i *microservizi*), ognuno con **un solo compito** e **il proprio database**. Se un pezzo cambia, gli altri non se ne accorgono, purché continuino a parlarsi nello stesso modo.

Un'analogia: è come la segreteria di un campus divisa in sportelli. Ogni sportello ha il suo archivio e si occupa di una sola cosa; se ha bisogno di un'informazione la chiede a un altro sportello, ma non va a frugare nel suo archivio.

> I nomi dei servizi e di tutto ciò che compare nel codice sono in **inglese** (vedi [CONTRIBUTING.md](../CONTRIBUTING.md#lingua)). Qui accanto a ogni nome trovi il significato in italiano.

```
   Utente (browser)
        │
   ┌────▼─────┐
   │   Web    │  le schermate che l'utente vede
   └────┬─────┘
        │
   ┌────▼─────┐
   │ Gateway  │  la porta d'ingresso: smista ogni richiesta allo sportello giusto
   └────┬─────┘
        ├──────────────┬────────────────────┐
 ┌──────▼───────┐ ┌────▼────┐ ┌─────────────▼──────────┐
 │   Identity   │ │  Rooms  │ │        Bookings        │
 │  chi sei     │ │cosa c'è │ │ chi lo usa, quando,    │
 │              │ │         │ │ e se c'era davvero     │
 └──────────────┘ └─────────┘ └────────────────────────┘
```

## In una riga

| Servizio | In italiano | Cartella | Risponde alla domanda | Esempio |
|---|---|---|---|---|
| **Web** | interfaccia | `frontend/` | *Cosa vede l'utente?* | La pagina di ricerca delle aule |
| **Gateway** | porta d'ingresso | `src/Gateway/` | *A quale servizio va questa richiesta?* | Una richiesta a `/api/bookings/…` va al servizio Bookings |
| **Identity** | identità / accesso | `src/Services/Identity/` | *Chi sei e cosa puoi fare?* | Mario fa login ed è riconosciuto come *studente* (`Student`) |
| **Rooms** | spazi | `src/Services/Rooms/` | *Quali spazi esistono e come sono fatti?* | L'Aula A1 ha 30 posti, un proiettore ed è aperta dalle 8 alle 20 |
| **Bookings** | prenotazioni e presenze | `src/Services/Bookings/` | *Chi userà quale spazio e quando? Lo ha usato davvero?* | Mario ha prenotato l'Aula A1 martedì 10–11 per sé, Anna e Luca; Anna ha fatto check-in alle 10:05 |

---

## Web (frontend)
**Cosa fa:** è l'applicazione Angular che si apre nel browser, su computer o telefono. Mostra le schermate (in italiano) e invia le azioni dell'utente al Gateway.
**Cosa NON fa:** non decide nulla. Se lo spazio è libero lo stabilisce Bookings, non la pagina.

## Gateway
**Cosa fa:** è l'**unica porta d'ingresso** del sistema. Ogni richiesta del Web passa da qui e viene inoltrata al servizio giusto in base all'indirizzo (`/api/rooms/…` → Rooms, `/api/bookings/…` → Bookings, ecc.). Controlla anche che chi chiama abbia fatto l'accesso.
**Cosa NON fa:** non contiene regole del campus.
**Perché esiste:** il Web conosce un solo indirizzo; i servizi possono essere spostati o moltiplicati senza modificare il Web.

## Identity: identità e accesso
**Cosa fa:** verifica nome utente e password e rilascia un "pass" digitale (*token JWT*) che dice chi è l'utente e che ruolo ha: **studente** (`Student`) o **personale** (`Staff`). Gli altri servizi leggono il pass senza dover richiamare Identity.
**Archivio:** utenti (`User`) e ruoli; solo dati di prova inventati.
**Cosa NON fa:** non sa nulla di aule e prenotazioni.
**Storie collegate:** US-09.

## Rooms: spazi
**Cosa fa:** è l'**anagrafica del campus**: edifici (`Building`), aule, laboratori e sale studio (`Room`), attrezzature (`Equipment`) e orari di apertura (`OpeningHours`), con capienza e indicazione se sono prenotabili. Gli studenti la consultano per cercare, il personale la aggiorna.
**Cosa NON fa:** non sa se uno spazio è libero o occupato: questo è compito di Bookings.
**Quando cambia qualcosa:** avvisa Bookings (es. "l'Aula A1 ora ha 40 posti") con un **messaggio** (vedi sotto).
**Storie collegate:** US-01, US-02, US-10, US-11.

## Bookings: prenotazioni e presenze ⭐
**Cosa fa:** è il **cuore del prodotto**.
- Registra le prenotazioni (`Reservation`): chi usa quale spazio e quando, **da solo o in gruppo**. In una prenotazione di gruppo chi prenota (organizzatore, `OrganizerId`) aggiunge altri studenti come membri (`ReservationMember`).
- Calcola le fasce libere e garantisce le regole più importanti: **due prenotazioni confermate dello stesso spazio non si sovrappongono mai**, nemmeno se due persone premono "Prenota" nello stesso istante, e **nessuno può essere in due prenotazioni nello stesso orario**.
- Registra ciò che è **successo davvero**: il check-in (fatto dall'organizzatore o da un membro) e le mancate presentazioni (`AttendanceRecord`).
- Produce i riepiloghi per il personale distinguendo sempre tre casi: *usato*, *non usato*, *nessuna informazione*. Quest'ultimo caso **non** viene mai trattato come "spazio vuoto".

**Archivio:** prenotazioni, membri, check-in e mancate presentazioni, più una copia ridotta degli spazi prenotabili (`BookableRoom`: capienza, orari) ricevuta da Rooms. Così una prenotazione non dipende dal fatto che Rooms risponda in quel momento.
**Storie collegate:** US-03, US-04, US-05, US-06, US-07, US-08, US-12, US-13, US-14, US-15, US-16, US-17.

> Prima le presenze erano un servizio separato (*Attendance*). Su indicazione del docente sono state accorpate in Bookings per semplificare: check-in e prenotazioni stanno nello stesso archivio e non serve sincronizzarli.

---

## Come si parlano i servizi

Ci sono due modi:

1. **Domanda e risposta (HTTP):** "dammi gli spazi con almeno 30 posti". Chi chiede aspetta la risposta. Lo usa il Web, tramite il Gateway.
2. **Messaggi (eventi):** un servizio annuncia "è successo questo" e chi è interessato lo riceve. Chi annuncia non aspetta nessuno. I messaggi passano da **RabbitMQ**, una specie di bacheca/postino.

| Messaggio | Significato | Chi lo manda | Chi lo riceve | Perché |
|---|---|---|---|---|
| `RoomCreated`, `RoomUpdated`, `RoomDeactivated` | spazio creato / modificato / dismesso | Rooms | Bookings | Aggiornare la copia degli spazi prenotabili |

I messaggi sono definiti in `src/BuildingBlocks/SmartCampus.Contracts/Events.cs`. `RoomCreated` contiene id, nome, capienza, prenotabilità e orari di apertura; `RoomUpdated` contiene gli stessi dati più `IsActive`, così uno spazio dismesso può anche essere riattivato; `RoomDeactivated` contiene solo l'id. Rooms usa l'outbox EF Core: un messaggio accodato insieme alla modifica dei dati resta in attesa nel database se RabbitMQ non è disponibile e viene consegnato quando il broker torna raggiungibile. Bookings elabora i messaggi uno alla volta e aggiorna la propria copia degli spazi, inclusi gli orari.

**Conseguenza da conoscere:** la sincronizzazione non è immediata: quando Rooms cambia la capienza o gli orari, Bookings può continuare a usare i dati precedenti per un breve intervallo. Per esempio, una prenotazione potrebbe ancora essere validata sulla capienza precedente finché l'evento non viene elaborato. Questa consistenza eventuale è accettata; l'outbox garantisce che l'aggiornamento non venga perso quando il broker non è disponibile.

## Le altre cartelle

| Cartella | A cosa serve |
|---|---|
| `src/BuildingBlocks/SmartCampus.ServiceDefaults/` | Impostazioni comuni a tutti i servizi (es. gli indirizzi `/health/live` e `/health/ready` che dicono se un servizio funziona) |
| `src/BuildingBlocks/SmartCampus.Contracts/` | Solo i messaggi scambiati tra servizi |
| `tests/<Servizio>/` | Test di quel servizio: *UnitTests* per le regole, *IntegrationTests* con il database vero |
| `tests/SmartCampus.EndToEnd.Tests/` | Test che attraversano più servizi, come farebbe un utente |
| `docker-compose.yml` | Avvia tutto (servizi, database SQL Server, RabbitMQ) con un comando |

## Regola d'oro

> **Ogni servizio legge e scrive solo il proprio database.** Se ha bisogno di dati di un altro servizio, li chiede tramite API o li riceve con un messaggio.

Approfondimenti tecnici: [architettura.md](architettura.md).
