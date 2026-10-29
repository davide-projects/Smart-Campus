Storia #16 · [US-05] Prenotare uno spazio per un gruppo: **Come** studente **voglio** prenotare uno spazio indicando gli altri studenti che parteciperanno **in modo da** dare a tutto il gruppo la certezza dello spazio e far ritrovare a ognuno la prenotazione.

## Why

Oggi `POST /api/bookings` crea solo prenotazioni per una persona (`participants` = 1). Il modello di Bookings prevede già i membri (`ReservationMember`), «Le mie prenotazioni» li mostra e la cancellazione li esclude, ma nessuna API li crea: uno studente non può prenotare un'aula per il proprio gruppo con la garanzia che nessun membro sia impegnato altrove e che lo spazio basti per tutti. Per verificare che i membri siano studenti (INV-9) Bookings deve sapere chi è registrato e con quale ruolo, un dato che oggi ha solo Identity.

### Criteri di accettazione della storia

- [ ] Lo studente può aggiungere come membri solo utenti registrati con ruolo studente.
- [ ] Il numero di partecipanti mostrato e salvato è uguale a organizzatore più membri.
- [ ] Se i partecipanti superano la capienza dello spazio, la prenotazione viene rifiutata con un messaggio che indica la capienza massima.
- [ ] Se anche un solo membro ha già una prenotazione confermata sovrapposta, la prenotazione viene rifiutata e non crea alcuna prenotazione.
- [ ] Dopo la conferma, la prenotazione compare tra le prenotazioni di ogni membro.
- [ ] I membri non possono cancellare la prenotazione né aggiungere o togliere partecipanti.

## What Changes

- **Identity (#123):**
  - nuovo endpoint interno `POST /internal/users/students/check`, fuori da `/api` e quindi non instradato dal Gateway. Riceve `{ "userIds": [...] }` e risponde `200` con `students` (`id`, `displayName`) e `notStudents` (id inesistenti o di utenti `Staff`). Richiede un JWT valido: `401` senza token, `400` con un corpo non valido o un elenco vuoto;
  - nuovo endpoint `GET /api/identity/students?search=<testo>` per il modulo di prenotazione, solo per utenti `Student` (`403` per lo `Staff`). Restituisce al massimo 10 studenti (`id`, `username`, `displayName`) il cui nome visualizzato o username contiene il testo (almeno 2 caratteri), escluso chi cerca.
- **Bookings (#17):** `POST /api/bookings` accetta `memberIds`, facoltativo: se manca o è vuoto la prenotazione resta singola, come oggi. Con i membri:
  - `participants` = 1 + numero di membri;
  - `403` se chi prenota è `Staff`: solo gli studenti creano prenotazioni di gruppo;
  - `422` per più di 10 membri, membri ripetuti, organizzatore tra i membri, membri inesistenti o non studenti (INV-9) e capienza superata, con la capienza massima nel messaggio (INV-5);
  - `409` se un membro ha una prenotazione `Confirmed` sovrapposta, come organizzatore o come membro (INV-8);
  - `503` se Identity non risponde.

  Prima della transazione Bookings chiama Identity, inoltrando il token di chi prenota. Poi prende il lock `user:{id}` su organizzatore e membri, in ordine di id, e inserisce `Reservation` e `ReservationMember` nella stessa transazione: un rifiuto non lascia righe. La prenotazione compare in `GET /api/bookings/mine` di ogni membro. Un membro che tenta `DELETE /api/bookings/{id}` riceve `403`, come oggi.
- **Frontend (#18):** nel modulo di `/rooms/:id/book`:
  - campo «Membri del gruppo» con ricerca degli studenti (autocomplete su `GET /api/identity/students`);
  - membri scelti mostrati come chip rimovibili, al massimo 10;
  - campo nascosto agli utenti `Staff`;
  - numero dei partecipanti (organizzatore + membri) calcolato e mostrato, con un avviso se supera la capienza dello spazio;
  - i messaggi `409`, `422` e `503` del servizio restano visibili, e la conferma riporta i partecipanti delle prenotazioni di gruppo;
  - il modulo si usa da tastiera e a 375px.

### Fuori ambito

- Modifica dei membri dopo la creazione (aggiunta o rimozione di partecipanti): non esiste un endpoint e non se ne aggiunge uno.
- Check-in dei membri (US-13 e seguenti).
- Notifiche ai membri (e-mail o altro) ed eventi di integrazione: Bookings continua a non pubblicare eventi.
- Copia locale degli utenti in Bookings tramite eventi (`UserRegistered`): scartata nel `design.md`.
- Indicare quale membro è impegnato o non è studente: i messaggi non rivelano dati di altri utenti.

## Capabilities

### New Capabilities

- `identity`: comportamento osservabile del servizio Identity. In questa change: la verifica interna degli studenti usata da Bookings e la ricerca degli studenti per il modulo di prenotazione. Login e profilo esistono già, ma non hanno ancora una spec e restano fuori da questa change.

### Modified Capabilities

- `bookings`: la creazione di una prenotazione accetta i membri del gruppo (al massimo 10, solo se chi prenota non è `Staff`), con capienza (INV-5), sovrapposizioni dei membri (INV-8), membri solo studenti (INV-9), indisponibilità di Identity, permessi dei membri e richieste contemporanee; requisito «Creazione di una prenotazione singola» aggiornato per `memberIds`.
- `frontend`: scelta dei membri nel modulo di prenotazione, numero di partecipanti, avviso di capienza, tastiera e 375px; requisiti «Conferma della prenotazione» e «Messaggi di rifiuto della prenotazione» aggiornati.

Il Gateway non cambia configurazione. La route `identity` (`/api/identity/**`, policy `Default`) copre già `GET /api/identity/students`. `/internal/**` non corrisponde a nessuna route, quindi il Gateway risponde `404` senza inoltrare la richiesta.

## Impact

- **Servizi toccati:** Identity, Bookings, Frontend. Gateway e Rooms senza modifiche al codice. Nel Gateway si aggiunge solo un test che verifica che `/internal/**` non arrivi a Identity.
- **Contratti tra servizi:** nuovo contratto **sincrono** Bookings → Identity, `POST /internal/users/students/check`. È la prima chiamata HTTP tra servizi del progetto: finora i servizi si parlavano solo con eventi su RabbitMQ. Nessun evento nuovo o modificato.
- **Database:** nessuna migrazione. La tabella `ReservationMembers` (chiave `ReservationId` + `UserId`, indice su `UserId`) esiste da `InitialCreate` di Bookings; Identity non cambia schema.
- **API:**
  - nuove `POST /internal/users/students/check` e `GET /api/identity/students`;
  - `POST /api/bookings` accetta `memberIds` (compatibile: senza il campo il comportamento non cambia) e può rispondere `403` (Staff con membri) e `503`.
- **Configurazione:**
  - nuova chiave `Services:Identity:BaseAddress` in Bookings: `appsettings.json` `http://localhost:5101`, `docker-compose.yml` `http://identity:8080`;
  - in `docker-compose.yml` Bookings dipende anche da `identity`;
  - nessun segreto nuovo.
- **Dipendenze tra task:** #123 → #17 → #18. Il #17 ha bisogno dell'endpoint interno, ma nei suoi test usa una Identity finta. Il #18 ha bisogno di `GET /api/identity/students` e di `memberIds` nella `POST`.
- **Documentazione:**
  - `docs/endpoints.md`, `docs/identity.md`, `docs/gateway.md` e `docs/handoff-architettura.md` (6.3) nel #123;
  - `docs/endpoints.md` (riga della `POST`), `docs/servizi.md` e `docs/architettura.md` (chiamata sincrona Bookings → Identity) e `docs/handoff-architettura.md` (F1, tabella degli errori) nel #17.
