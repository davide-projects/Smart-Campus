# I servizi di SmartCampus, spiegati semplici

SmartCampus non è un unico programma, ma è diviso in **pezzi indipendenti** (i *microservizi*), ognuno con **un solo compito** e **il proprio database**. Se un pezzo cambia, gli altri non se ne accorgono, purché continuino a parlarsi nello stesso modo.

Un'analogia: è come la segreteria di un campus divisa in sportelli. Ogni sportello ha il suo archivio e si occupa di una sola cosa; se ha bisogno di un'informazione la chiede a un altro sportello, ma non va a frugare nel suo archivio.

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
        ├──────────────┬───────────────┬────────────────┐
 ┌──────▼───────┐ ┌────▼────┐ ┌────────▼─────┐ ┌────────▼──┐
 │Autenticazione│ │  Spazi  │ │ Prenotazioni │ │ Presenze  │
 │  chi sei     │ │cosa c'è │ │chi lo usa    │ │chi c'era  │
 │              │ │         │ │e quando      │ │davvero    │
 └──────────────┘ └─────────┘ └──────────────┘ └───────────┘
```

## In una riga

| Servizio | Cartella | Risponde alla domanda | Esempio |
|---|---|---|---|
| **Web** | `frontend/` | *Cosa vede l'utente?* | La pagina di ricerca delle aule |
| **Gateway** | `src/Gateway/` | *A quale servizio va questa richiesta?* | Una richiesta a `/api/prenotazioni/…` va al servizio Prenotazioni |
| **Autenticazione** | `src/Services/Autenticazione/` | *Chi sei e cosa puoi fare?* | Mario fa login ed è riconosciuto come *studente* |
| **Spazi** | `src/Services/Spazi/` | *Quali spazi esistono e come sono fatti?* | L'Aula A1 ha 30 posti, un proiettore ed è aperta dalle 8 alle 20 |
| **Prenotazioni** | `src/Services/Prenotazioni/` | *Chi userà quale spazio e quando?* | L'Aula A1 è prenotata da Mario martedì dalle 10 alle 11 |
| **Presenze** | `src/Services/Presenze/` | *Lo spazio prenotato è stato usato davvero?* | Mario ha fatto check-in alle 10:05; il gruppo di Luca non si è presentato |

---

## Web (frontend)
**Cosa fa:** è l'applicazione Angular che si apre nel browser, su computer o telefono. Mostra le schermate e invia le azioni dell'utente al Gateway.
**Cosa NON fa:** non decide nulla. Se lo spazio è libero lo stabilisce Prenotazioni, non la pagina.

## Gateway
**Cosa fa:** è l'**unica porta d'ingresso** del sistema. Ogni richiesta del Web passa da qui e viene inoltrata al servizio giusto in base all'indirizzo (`/api/spazi/…` → Spazi, `/api/prenotazioni/…` → Prenotazioni, ecc.). Controlla anche che chi chiama abbia fatto l'accesso.
**Cosa NON fa:** non contiene regole del campus.
**Perché esiste:** il Web conosce un solo indirizzo; i servizi possono essere spostati o moltiplicati senza modificare il Web.

## Autenticazione
**Cosa fa:** verifica nome utente e password e rilascia un "pass" digitale (*token JWT*) che dice chi è l'utente e che ruolo ha (**studente** o **personale**). Gli altri servizi leggono il pass senza dover richiamare Autenticazione.
**Archivio:** utenti e ruoli (solo dati di prova inventati).
**Cosa NON fa:** non sa nulla di aule e prenotazioni.
**Storie collegate:** US-08.

## Spazi
**Cosa fa:** è l'**anagrafica del campus**: edifici, aule, laboratori, sale studio, con capienza, attrezzature, orari di apertura e se sono prenotabili. Gli studenti la consultano per cercare, il personale la aggiorna.
**Archivio:** edifici, spazi, attrezzature, orari.
**Cosa NON fa:** non sa se uno spazio è libero o occupato: questo è compito di Prenotazioni.
**Quando cambia qualcosa:** avvisa gli altri servizi (es. "l'Aula A1 ora ha 40 posti") tramite un **messaggio** (vedi sotto).
**Storie collegate:** US-01, US-02, US-09, US-10.

## Prenotazioni ⭐
**Cosa fa:** è il **cuore del prodotto**. Registra chi usa quale spazio e quando, calcola le fasce libere e garantisce la regola più importante: **due prenotazioni confermate dello stesso spazio non si sovrappongono mai**, nemmeno se due persone premono "Prenota" nello stesso istante.
**Archivio:** le prenotazioni, più una copia ridotta degli spazi prenotabili (capienza, orari) ricevuta da Spazi. Così una prenotazione non dipende dal fatto che Spazi risponda in quel momento.
**Cosa NON fa:** non registra se lo spazio è stato usato davvero.
**Quando cambia qualcosa:** avvisa con un messaggio ("prenotazione confermata", "prenotazione cancellata").
**Storie collegate:** US-03, US-04, US-05, US-06, US-07, US-11, US-12.

## Presenze
**Cosa fa:** registra ciò che è **successo davvero**: il check-in di chi arriva e le mancate presentazioni. Produce i riepiloghi per il personale distinguendo sempre tre casi: *usato*, *non usato*, *nessuna informazione*. Quest'ultimo caso **non** viene mai trattato come "spazio vuoto".
**Archivio:** check-in, mancate presentazioni e una copia delle prenotazioni ricevuta tramite messaggi da Prenotazioni.
**Cosa NON fa:** non crea né modifica prenotazioni.
**Storie collegate:** US-13, US-14, US-15, US-16.

---

## Come si parlano i servizi

Ci sono due modi:

1. **Domanda e risposta (HTTP):** "dammi gli spazi con almeno 30 posti". Chi chiede aspetta la risposta. Lo usa il Web, tramite il Gateway.
2. **Messaggi (eventi):** un servizio annuncia "è successo questo" e chi è interessato lo riceve. Chi annuncia non aspetta nessuno. I messaggi passano da **RabbitMQ**, una specie di bacheca/postino.

| Messaggio | Chi lo manda | Chi lo riceve | Perché |
|---|---|---|---|
| `SpazioCreato`, `SpazioAggiornato`, `SpazioDisattivato` | Spazi | Prenotazioni | Aggiornare la copia degli spazi prenotabili |
| `PrenotazioneConfermata`, `PrenotazioneCancellata` | Prenotazioni | Presenze | Sapere quali prenotazioni attendono un check-in |

I messaggi sono definiti in `src/BuildingBlocks/SmartCampus.Contracts/`.

**Conseguenza da conoscere:** i messaggi arrivano dopo un istante, non immediatamente. Se il personale cambia la capienza di un'aula, Prenotazioni la conosce dopo qualche frazione di secondo. È un limite noto e accettato (*consistenza eventuale*).

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
