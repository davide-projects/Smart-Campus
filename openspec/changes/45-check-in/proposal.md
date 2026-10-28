Storia #45 · [US-14] Segnalare che sto usando lo spazio (check-in): **Come** studente **voglio** confermare di essere arrivato nello spazio prenotato **in modo che** risulti che lo spazio è stato effettivamente usato.

## Why

Oggi Bookings sa solo che uno spazio è stato *prenotato*, non che è stato *usato*: senza un check-in il report di utilizzo (US-17) non può distinguere le prenotazioni usate da quelle senza informazione (`docs/handoff-architettura.md`, glossario e sezione 6.2). L'entità `AttendanceRecord` è prevista dall'architettura ma non esiste ancora, e la pagina «Le mie prenotazioni» (US-07, US-08) non offre nessuna azione allo studente all'arrivo. L'assunzione A6 (`docs/user-stories-bozza.md`) fissa la finestra: da 15 minuti prima a 15 minuti dopo l'inizio.

### Criteri di accettazione della storia

- [ ] Da 15 minuti prima a 15 minuti dopo l'inizio, lo studente può fare il check-in e la prenotazione risulta "usata".
- [ ] Fuori da questa finestra il check-in non è possibile e viene spiegato perché.
- [ ] In una prenotazione di gruppo, il check-in di un solo membro o dell'organizzatore segna la prenotazione come "usata" per tutto il gruppo.
- [ ] Non è possibile fare due volte il check-in sulla stessa prenotazione.
- [ ] Non è possibile fare il check-in su una prenotazione cancellata o di cui non si è organizzatore né membro.

## What Changes

- **Bookings (#46):** nuova `POST /api/bookings/{id}/check-in`, che risponde `200` con `{ checkedInAt }`. Possono farla l'organizzatore, i membri (`ReservationMember`) e chi ha ruolo `Staff` (claim `role`). Gli altri utenti ricevono `403` «Check-in non consentito» / «Non puoi fare il check-in di questa prenotazione.», una prenotazione inesistente `404` «Prenotazione non trovata» / «La prenotazione richiesta non esiste.». Per organizzatore e membri vale la finestra A6, estremi inclusi: `Start − 15 min ≤ adesso ≤ Start + 15 min`; lo `Staff` non ha limiti di finestra. Le violazioni di regola rispondono `422` con titolo «Check-in non consentito», nell'ordine: cancellata, no-show, già registrato, fuori finestra, ognuna con il suo messaggio. Il check-in registra un `AttendanceRecord` di tipo `CheckIn` (`RecordedAt` = istante della richiesta, `RecordedBy` = claim `sub`), valido per tutto il gruppo.
- **Bookings (#46), modello:** nuova entità `AttendanceRecord` (`Id`, `ReservationId`, `Type`, `RecordedAt`, `RecordedBy`) con enum `Type` già completo (`CheckIn`, `NoShow`), indice unico su `ReservationId` (un solo record di presenza per prenotazione) e migrazione EF Core. Nessuna API crea ancora record `NoShow` (lo farà la US-15), ma il check-in li considera già. Con richieste contemporanee la violazione dell'indice diventa il `422` corretto, mai `500`.
- **Bookings (#46), `GET /api/bookings/mine`:** ogni voce riceve il campo nuovo `checkedInAt` (istante del check-in, `null` se non c'è).
- **Bookings (#46), cancellazione:** una prenotazione con check-in non si può più cancellare: `DELETE /api/bookings/{id}` risponde `422` «Cancellazione non consentita» / «Non è possibile cancellare una prenotazione già usata.». Il controllo sta dopo il `403` e la cancellazione ripetuta (`204`) e prima del limite di tempo.
- **Frontend (#47):** in «Le mie prenotazioni» le voci con `checkedInAt` mostrano l'etichetta «Usata», nelle schede «Future» e «Passate», e non mostrano «Cancella». Le voci della scheda «Future» senza check-in mostrano il pulsante «Check-in». È abilitato solo nella finestra, ricalcolata ogni 30 secondi. Fuori finestra resta visibile e raggiungibile da tastiera ma disabilitato, con sotto il testo «Il check-in è possibile da 15 minuti prima a 15 minuti dopo l'inizio.». Il clic invia subito, senza conferma. Dopo il `200`:
  - compare la snackbar «Check-in registrato.»;
  - la voce diventa «Usata» con il valore della risposta, senza ricaricare l'elenco;
  - il focus va sull'etichetta «Usata».

  Gli errori compaiono in un avviso nella card della prenotazione.

### Fuori ambito

- Segnare il no-show (US-15) e il report di utilizzo (US-17): qui si prepara solo il tipo `NoShow` dell'enum.
- Annullare o correggere un check-in; check-in per singolo membro (uno per prenotazione, per tutto il gruppo).
- Pagina o azioni del personale: lo `Staff` fa il check-in solo tramite API.
- Etichetta «Usata» nella scheda «Cancellate»: una prenotazione con check-in non si può più cancellare.
- Notifiche ai membri ed eventi di integrazione: Bookings continua a non pubblicare eventi.
- Restrizioni per ruolo sul Gateway: la route generica `bookings` (`/api/bookings/**`, policy `Default`) copre già la nuova route.
- Storico dei check-in su MongoDB (opzione con riserva dell'handoff, non implementata).

## Capabilities

### New Capabilities

Nessuna.

### Modified Capabilities

- `bookings`:
  - nuovi requisiti per `POST /api/bookings/{id}/check-in`: chi può farlo, finestra A6, un solo check-in, prenotazioni cancellate o no-show, `404`, `401`, richieste contemporanee;
  - nuovo requisito «Nessuna cancellazione di una prenotazione usata»;
  - modificati «Contenuto di ogni voce» (`checkedInAt`), «Cancellazione di una prenotazione», «Cancellazione solo prima dell'inizio» e «Cancellazioni contemporanee», per il controllo sulla prenotazione usata.
- `frontend`:
  - nuovi requisiti per l'etichetta «Usata» e per il pulsante «Check-in»: finestra, aggiornamento ogni 30 secondi, esito, errori, tastiera e 375px;
  - modificato «Azione «Cancella» nelle prenotazioni future»: «Cancella» non compare sulle voci con check-in.

Rooms, Identity e Gateway non cambiano.

## Impact

- **Servizi toccati:** Bookings e Frontend.
- **Contratti ed eventi tra servizi:** nessuno nuovo o modificato (`SmartCampus.Contracts` invariato).
- **API:**
  - nuova `POST /api/bookings/{id}/check-in`;
  - `GET /api/bookings/mine` con il campo in più `checkedInAt`, compatibile: il frontend attuale lo ignora finché non arriva #47;
  - `DELETE /api/bookings/{id}` con un nuovo `422` per le prenotazioni usate.
- **Database:** nuova tabella `AttendanceRecords` con FK verso `Reservations`, indice unico su `ReservationId` e migrazione EF Core in Bookings. La PR aperta #119 (#43) aggiunge la migrazione `20261028150650_AddRoomType` e modifica `BookingDbContextModelSnapshot.cs`. La migrazione di #46 va quindi generata dopo il merge della #119 (o rigenerata in rebase): vedi `design.md`, decisione 10.
- **Configurazione:** nessuna modifica a user-secrets, `docker-compose.yml` o rotte del Gateway.
- **Dipendenze tra task:** #46 → #47.
- **Change in revisione `16-prenotare-per-un-gruppo` (PR #126, US-05):** nessun nome di requisito in comune; vedi `design.md`, decisione 10, per i file condivisi (`ReservationRules`, `booking.model.ts`).
- **Documentazione:**
  - `docs/endpoints.md`: nuova riga del check-in, campo `checkedInAt` di `/mine`, nuovo `422` della `DELETE`;
  - `docs/handoff-architettura.md`: finestra A6, stato della route, colonna di accesso del check-in «organizzatore, membro o Staff» nella tabella 6.2, flusso F4, messaggi nella tabella degli errori, I-9 risolta per la parte della finestra;
  - `docs/architettura.md` e `docs/servizi.md`: check-in consentito anche allo Staff;
  - `docs/gateway.md`: prova manuale del check-in.
