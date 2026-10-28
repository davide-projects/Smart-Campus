# rooms Specification

## Purpose

Descrive il comportamento osservabile del servizio Rooms: l'anagrafica degli spazi del campus e gli eventi con cui Rooms informa gli altri servizi dei cambiamenti degli spazi.

## Requirements

### Requirement: Nome dell'edificio negli eventi di creazione e modifica di uno spazio
Gli eventi `RoomCreated` e `RoomUpdated` pubblicati da Rooms MUST contenere, nel campo `BuildingName`, il nome dell'edificio a cui lo spazio appartiene nel momento in cui l'evento viene accodato. Il campo SHALL essere valorizzato in ogni punto in cui Rooms accoda questi eventi tramite outbox: creazione, modifica e pubblicazione iniziale degli spazi seed.

#### Scenario: Creazione di uno spazio
- **WHEN** un utente `Staff` crea uno spazio nell'edificio «Edificio A» con `POST /api/rooms`
- **THEN** l'evento `RoomCreated` pubblicato per quello spazio contiene `BuildingName` = «Edificio A»

#### Scenario: Modifica di uno spazio con cambio di edificio
- **WHEN** un utente `Staff` sposta con `PUT /api/rooms/{id}` uno spazio dall'«Edificio A» alla «Biblioteca»
- **THEN** l'evento `RoomUpdated` pubblicato contiene `BuildingName` = «Biblioteca»

#### Scenario: Modifica di uno spazio senza cambio di edificio
- **WHEN** un utente `Staff` modifica la capienza di uno spazio dell'«Edificio B» senza cambiarne l'edificio
- **THEN** l'evento `RoomUpdated` pubblicato contiene `BuildingName` = «Edificio B»

#### Scenario: Pubblicazione iniziale degli spazi seed
- **WHEN** Rooms pubblica all'avvio, una sola volta, gli eventi `RoomCreated` degli spazi seed attivi
- **THEN** ogni evento contiene il nome dell'edificio del relativo spazio

### Requirement: Compatibilità dei contratti degli spazi
L'aggiunta di `BuildingName` a `RoomCreated` e `RoomUpdated` MUST essere additiva e facoltativa: un evento senza il campo SHALL restare valido e il campo vale `null`. Gli altri campi (`RoomId`, `Name`, `Capacity`, `IsBookable`, `IsActive`, `OpeningHours`) e l'evento `RoomDeactivated` SHALL restare invariati.

#### Scenario: Campi esistenti invariati
- **WHEN** Rooms pubblica `RoomUpdated` per uno spazio
- **THEN** l'evento contiene gli stessi campi e valori di prima di questa change, più `BuildingName`

#### Scenario: Evento senza il nome dell'edificio
- **WHEN** un consumer riceve un `RoomCreated` o `RoomUpdated` che non contiene `BuildingName`, per esempio accodato prima di questa modifica
- **THEN** l'evento viene letto senza errori e `BuildingName` vale `null`
