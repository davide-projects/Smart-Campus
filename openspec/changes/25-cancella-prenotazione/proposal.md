Storia #25 · [US-08] Cancellare una mia prenotazione: **Come** studente **voglio** cancellare una prenotazione che non mi serve più **in modo da** liberare lo spazio per chi ne ha bisogno.

## Why

Oggi una prenotazione confermata non si può annullare: se lo studente rinuncia, lo spazio resta occupato e nessun altro può usarlo (`docs/handoff-architettura.md`, sezione 1: «La cancellazione rende subito lo spazio disponibile»). Bookings ha già tutto il necessario nel modello (`Status` `Confirmed`/`Cancelled`, `CancelledAt`, `RowVersion`) e la pagina «Le mie prenotazioni» (US-07) ha già la scheda «Cancellate», ma manca l'operazione: non esiste `DELETE /api/bookings/{id}` e la pagina non offre alcuna azione sulle voci.

### Criteri di accettazione della storia

- [ ] Dopo la conferma, la prenotazione risulta cancellata e non è più tra le future, né per l'organizzatore né per i membri.
- [ ] Subito dopo la cancellazione, la fascia appare libera nella disponibilità dello spazio e un altro studente può prenotarla.
- [ ] Non è possibile cancellare una prenotazione già iniziata o passata.
- [ ] Uno studente non può cancellare la prenotazione di un altro utente.
- [ ] Un membro di un gruppo che non è l'organizzatore non può cancellare la prenotazione, che resta valida.

## What Changes

- **Bookings (#26):** nuovo `DELETE /api/bookings/{id}`. Porta la prenotazione a `Status = Cancelled` e salva `CancelledAt`, poi risponde `204`. Possono cancellare l'organizzatore (`OrganizerId` = claim `sub`) e chi ha ruolo `Staff` (claim `role`); i membri non organizzatori e gli altri utenti ricevono `403` «Cancellazione non consentita» / «Non puoi cancellare questa prenotazione.», anche su una prenotazione già cancellata. Si cancella solo prima dell'orario di inizio, per tutti, Staff compreso (assunzione A5): una prenotazione già iniziata o passata riceve `422` «Cancellazione non consentita» / «Non è possibile cancellare una prenotazione già iniziata o passata.». Una prenotazione già cancellata, chiesta dall'organizzatore o dallo Staff, riceve `204` senza modifiche (idempotente, `CancelledAt` invariato). Da quel momento la fascia è libera in `GET /api/bookings/availability`, prenotabile con `POST /api/bookings` e la prenotazione passa da `upcoming` a `cancelled` in `GET /api/bookings/mine` per organizzatore e membri: le query esistenti filtrano già su `Status`.
- **Bookings (#26), allineamento della spec:** il requisito «Endpoint delle mie prenotazioni» descrive ancora un utente identificato «dal claim `sub` del token inoltrato dal Gateway» e un `401` «Accesso richiesto» anche senza token. Dopo la PR #101 (#91) Bookings valida da sé il JWT e la richiesta senza token o con token non valido viene respinta dall'autenticazione prima dell'endpoint. La change corregge il requisito su quello che il codice fa oggi.
- **Bookings (#26), concorrenza:** il passaggio a `Cancelled` è un solo `UPDATE` condizionato a `Status = Confirmed` e inizio non ancora arrivato; con richieste parallele al massimo una cancella, le altre ricevono la risposta dello stato riletto, mai `500`. Una prenotazione la cui `POST` non ha ancora fatto commit non esiste: `404`.
- **Frontend (#27):** nella pagina «Le mie prenotazioni», azione «Cancella» sulle voci della scheda «Future» di cui l'utente è organizzatore e non ancora iniziate. L'azione apre un dialog (`MatDialog`) «Cancellare la prenotazione?» con spazio, edificio, giorno e orario e i pulsanti «Annulla» e «Cancella prenotazione». Su `204` il dialog si chiude, una snackbar mostra «Prenotazione cancellata.», l'elenco si ricarica con la scheda «Future» ancora selezionata e la prenotazione compare in «Cancellate». Gli errori restano nel dialog, con `role="alert"`.

### Fuori ambito

- Pagina o azioni del personale (`Staff`): lo Staff può cancellare solo tramite API; nessuna vista «prenotazioni per spazio».
- Cancellazione dopo l'inizio, anche per lo Staff, e cancellazione parziale (rimozione di un membro: US-05).
- Ripristino di una prenotazione cancellata (INV-7: una cancellata non torna confermata).
- Notifiche ai membri (e-mail o altro) ed eventi di integrazione: Bookings continua a non pubblicare eventi.
- Restrizione per ruolo sul Gateway: la route generica `bookings` (`/api/bookings/**`, policy `Default`) copre già `DELETE /api/bookings/{id}` per ogni utente autenticato; il controllo organizzatore/Staff è di Bookings.
- Vista della disponibilità nel frontend (task #11, US-03, ancora aperto): il criterio 2 si verifica sull'API di disponibilità.

## Capabilities

### New Capabilities

Nessuna.

### Modified Capabilities

- `bookings`: nuovo requisito per `DELETE /api/bookings/{id}` (autorizzazione, limite di tempo, idempotenza, effetti su disponibilità ed elenchi); requisito «Endpoint delle mie prenotazioni» corretto sul `401` dopo la PR #101.
- `frontend`: azione «Cancella» con dialog di conferma nella pagina «Le mie prenotazioni», esito, uso da tastiera e a 375px; requisito «Caricamento ed errore» limitato al primo caricamento della pagina, perché il ricaricamento dopo una cancellazione tiene le schede visibili.

Rooms, Identity e Gateway non cambiano.

## Impact

- **Servizi toccati:** Bookings e Frontend.
- **Contratti ed eventi tra servizi:** nessuno nuovo o modificato.
- **Database:** nessuna migrazione: `Status`, `CancelledAt` e `RowVersion` esistono già in `Reservations` (`InitialCreate`).
- **API:** nuova `DELETE /api/bookings/{id}`; le altre route sono invariate.
- **Configurazione:** nessuna modifica a user-secrets, `docker-compose.yml` o rotte del Gateway.
- **Dipendenze tra task:** #26 → #27.
- **Change attiva `12-prenotare-uno-spazio` (US-04):** nessun requisito in comune (nomi diversi, vedi `design.md`). Il requisito «Accesso richiesto per prenotare» di quella change descrive lo stesso `401` superato dalla PR #101: va allineato in quella change, non in questa.
- **Documentazione:** `docs/endpoints.md` (nuova riga), `docs/handoff-architettura.md` (glossario «Organizzatore», stato della `DELETE`, domanda I-8 risolta con A5, messaggi della tabella 6.2), `docs/gateway.md` (prova manuale della `DELETE`).
