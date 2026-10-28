Storia #22 · [US-07] Vedere le mie prenotazioni: **Come** studente **voglio** vedere in un unico elenco le mie prenotazioni **in modo da** ritrovare le conferme senza cercarle tra messaggi diversi.

## Why

Oggi le conferme di prenotazione non hanno un posto dove essere ritrovate: Bookings crea le prenotazioni (`POST /api/bookings`, PR #86) ma non permette di elencarle. Lo studente deve poter vedere in un'unica pagina le prenotazioni che ha creato e quelle di gruppo di cui è membro, comprese passate e cancellate.

Per mostrare edificio e organizzatore senza chiamare altri servizi a ogni richiesta, Bookings deve conoscere il nome dell'edificio dello spazio (oggi assente da `BookableRoom` e dagli eventi di Rooms) e il nome dell'organizzatore (oggi ha solo `OrganizerId`).

### Criteri di accettazione della storia

- [ ] Vengono mostrate le prenotazioni future dello studente in ordine cronologico, con spazio, edificio, giorno e orario.
- [ ] Le prenotazioni di gruppo di cui lo studente è membro compaiono nell'elenco, indicate come prenotazioni di gruppo con l'organizzatore.
- [ ] Le prenotazioni passate e quelle cancellate sono consultabili separatamente.
- [ ] Lo studente non vede le prenotazioni di cui non è organizzatore né membro.
- [ ] Se non ci sono prenotazioni compare un messaggio con il collegamento alla ricerca degli spazi.

## What Changes

- **Rooms (#90):** gli eventi `RoomCreated` e `RoomUpdated` trasportano il nome dell'edificio dello spazio, valorizzato quando Rooms li accoda nell'outbox (creazione, modifica e pubblicazione iniziale degli spazi seed).
- **Contratti:** `RoomCreated` e `RoomUpdated` in `SmartCampus.Contracts` ricevono il campo `BuildingName`. Il cambiamento è additivo: i consumer che non lo leggono continuano a funzionare.
- **Bookings (#23):**
  - la copia locale `BookableRoom` salva il nome dell'edificio ricevuto dagli eventi (migrazione EF);
  - `Reservation` salva il nome dell'organizzatore, letto dal claim `name` del JWT nella `POST /api/bookings` (migrazione EF);
  - nuovo endpoint `GET /api/bookings/mine` con le prenotazioni in cui l'utente è organizzatore o membro, divise in future, passate e cancellate.
- **Frontend (#24):** nuova pagina «Le mie prenotazioni» con le tre sezioni, l'indicazione delle prenotazioni di gruppo e dell'organizzatore e il messaggio con il collegamento alla ricerca quando non ci sono prenotazioni.

### Fuori ambito

- Cancellare una prenotazione (US-08, storia separata): la pagina non offre azioni sulle voci.
- Creare prenotazioni di gruppo con membri (US-05): questa storia mostra i membri già presenti in `ReservationMember`, non li crea.
- Check-in dalla pagina (US-14).
- Vista del personale sulle prenotazioni di uno spazio (US-12).
- Propagare a Bookings la rinomina di un edificio fatta senza modificare gli spazi: oggi Rooms non ha un evento per gli edifici (limite noto, vedi design).
- Seconda validazione del JWT in Bookings: resta affidata al Gateway (`docs/gateway.md`).

## Capabilities

### New Capabilities

- `rooms`: comportamento osservabile del servizio Rooms; in questa change, il contenuto degli eventi di ciclo di vita degli spazi verso gli altri servizi (nome dell'edificio).
- `bookings`: comportamento osservabile del servizio Bookings; in questa change, l'elenco «le mie prenotazioni», la copia locale dell'edificio e il nome dell'organizzatore salvato alla creazione.
- `frontend`: comportamento visibile dell'applicazione Angular; in questa change, la pagina «Le mie prenotazioni».

### Modified Capabilities

Nessuna: `openspec/specs/` è ancora vuota.

Il Gateway non cambia: la route generica `bookings` (`/api/bookings/**`, policy `Default`) copre già `GET /api/bookings/mine` per ogni utente autenticato.

## Impact

- **Servizi toccati:** Rooms, Bookings, Frontend. Il Gateway non cambia configurazione.
- **Contratti tra servizi:** `RoomCreated` e `RoomUpdated` (campo nuovo `BuildingName`). `RoomDeactivated` invariato.
- **Database Bookings:** una sola migrazione EF Core con la colonna del nome dell'edificio su `BookableRooms` e la colonna del nome dell'organizzatore su `Reservations`. Database Rooms invariato.
- **API:** nuovo `GET /api/bookings/mine`; `POST /api/bookings` salva anche il nome dell'organizzatore, senza cambiare richiesta e risposta.
- **Dipendenze tra task:** #90 → #23 → #24. La #23 parte dopo il merge della #90 e riusa `POST /api/bookings` e `CurrentUser` della PR #86, già su `main`.
- **Documentazione:** `docs/endpoints.md`, `docs/architettura.md`, `docs/handoff-architettura.md` (sezioni 6.1 e 6.2), `docs/servizi.md` se descrive i dati di `BookableRoom`, `docs/gateway.md` (prova manuale di `GET /api/bookings/mine`).
