Storia #12 · [US-04] Prenotare uno spazio: **Come** studente **voglio** prenotare uno spazio per una fascia oraria **in modo da** avere la certezza di poterlo usare senza che altri contino sullo stesso spazio.

## Why

È il requisito centrale del prodotto: due prenotazioni confermate dello stesso spazio non devono mai sovrapporsi, nemmeno se richieste nello stesso istante, e la stessa persona non deve essere in due prenotazioni nello stesso orario. Bookings crea già le prenotazioni singole (`POST /api/bookings`, task #13, PR #86), ma manca la prova su SQL Server reale che la garanzia regga con richieste contemporanee e manca la pagina con cui lo studente prenota: nel dettaglio dello spazio il pulsante «Prenota spazio» è ancora disabilitato.

### Criteri di accettazione della storia

- [ ] Prenotando una fascia libera, lo studente riceve una conferma con spazio, giorno e orario, e la fascia risulta occupata per tutti.
- [ ] Una richiesta che si sovrappone anche in parte a una prenotazione confermata dello stesso spazio viene rifiutata e non crea alcuna prenotazione.
- [ ] Una prenotazione che inizia esattamente quando finisce un'altra viene accettata.
- [ ] Con 20 richieste contemporanee per la stessa fascia libera, ne viene confermata esattamente una (verificato su database reale).
- [ ] Uno studente che ha già una prenotazione confermata (singola o di gruppo) sovrapposta alla fascia richiesta, anche in un altro spazio, non può prenotare.
- [ ] Vengono rifiutate, con un messaggio che spiega il motivo, le richieste: fuori dall'orario di apertura, nel passato, più lunghe di 4 ore o oltre 14 giorni in anticipo.

## What Changes

- **Bookings (#13, già unito con la PR #86):** `POST /api/bookings` crea una prenotazione singola confermata a nome dell'utente del token. Le regole INV-1…INV-4, INV-6, INV-8 e le assunzioni A1 (mezz'ora) e A2 (4 ore, 14 giorni) restituiscono `401`, `404`, `409` o `422` con ProblemDetails in italiano. INV-1 e INV-8 sono garantiti da due lock nella stessa transazione: `UPDLOCK, ROWLOCK` sulla riga dello spazio e `sp_getapplock` sulla persona (`user:{id}`, attesa massima 10 secondi). La change descrive questo comportamento nelle spec, così l'archivio lo porta in `openspec/specs/bookings`.
- **Bookings (#14):** test di integrazione su SQL Server reale (Testcontainers) con richieste parallele: 20 richieste sulla stessa fascia libera dello stesso spazio confermano esattamente una prenotazione; due richieste parallele della stessa persona su due spazi diversi ne confermano una sola. I test girano in CI.
- **Frontend (#15):** nuova pagina `/rooms/:id/book`, riservata agli utenti autenticati e aperta dal pulsante «Prenota spazio» del dettaglio di uno spazio prenotabile, con giorno (da oggi a oggi + 14 giorni), ora di inizio e ora di fine (mezz'ore dentro l'orario di apertura) e motivo facoltativo. Dopo il `201` mostra nella stessa pagina la conferma con spazio, giorno e orario nell'ora del campus, con «Torna allo spazio» e «Prenota un'altra fascia»; i rifiuti mostrano il messaggio del servizio, e il conflitto sullo spazio affianca un collegamento alla disponibilità dello spazio.

### Fuori ambito

- Prenotazioni di gruppo con membri e controllo della capienza (US-05): la `POST` resta per una sola persona (`participants` = 1).
- Restituire le fasce libere nel `409` di conflitto sullo spazio, come suggerito in `docs/handoff-architettura.md` (6.2): il messaggio resta «Lo spazio è già prenotato in questa fascia.» e il frontend offre il collegamento alla disponibilità.
- Validazione della firma del JWT in Bookings: oggi la verifica solo il Gateway; è tracciata in #91.
- Salvataggio del nome dell'organizzatore nella `POST` (claim `name`): è nella change `22-mie-prenotazioni`, task #23.
- Vista della disponibilità giornaliera nel frontend: è il task #11 (US-03); questa storia la usa per verificare che la fascia prenotata risulti occupata.
- Pagina «Le mie prenotazioni» (US-07) e cancellazione (US-08).

## Capabilities

### New Capabilities

- `bookings`: comportamento osservabile del servizio Bookings; in questa change, la creazione di una prenotazione singola senza conflitti (`POST /api/bookings`) e la sua tenuta con richieste contemporanee.
- `frontend`: comportamento visibile dell'applicazione Angular; in questa change, la pagina di prenotazione di uno spazio e la conferma.

Anche la change `22-mie-prenotazioni` introduce `bookings` e `frontend`: i nomi dei requisiti sono diversi, quindi le due change si archiviano in qualunque ordine senza collisioni (vedi `design.md`).

### Modified Capabilities

Nessuna: `openspec/specs/` è ancora vuota.

Il Gateway non cambia: la route generica `bookings` (`/api/bookings/**`, policy `Default`) copre già `POST /api/bookings` per ogni utente autenticato.

## Impact

- **Servizi toccati:** Bookings (solo test nel task #14; il codice è già su `main` con la PR #86) e Frontend. Gateway, Rooms e Identity invariati.
- **Contratti ed eventi tra servizi:** nessuno nuovo o modificato. Bookings continua a non pubblicare eventi.
- **Database:** nessuna migrazione. Il lock sulla persona usa `sp_getapplock`, che non richiede tabelle.
- **API:** `POST /api/bookings` invariata rispetto alla PR #86.
- **Dipendenze tra task:** #13 (fatto) → #14 → #15. Il #15 parte dopo il merge di #11 (US-03, vista della disponibilità giornaliera), perché ne usa la vista per mostrare e verificare la fascia occupata.
- **Documentazione:** `docs/handoff-architettura.md`, `docs/architettura.md` e `docs/servizi.md` vengono allineati sul meccanismo di INV-8 nella stessa PR di pianificazione (#96). Il task #14 aggiorna in `docs/handoff-architettura.md` la riga di stato «Test di concorrenza prenotazioni». `docs/endpoints.md` descrive già la `POST` (PR #86).
