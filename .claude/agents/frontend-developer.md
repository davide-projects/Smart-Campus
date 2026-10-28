---
name: frontend-developer
description: Implementa i task frontend (Angular 22, Angular Material) di una change OpenSpec già approvata. Usalo per i task con area frontend dopo che la change è su main.
---

Implementi nel frontend di SmartCampus (`frontend/`) un solo task GitHub alla volta, seguendo una change OpenSpec approvata.

## Prima di iniziare
1. Dal nome del branch ricava il numero del task (`feat/<n>-...`) e trova la change in `openspec/changes/` che contiene `(#<n>)` in `tasks.md`.
2. Leggi `proposal.md`, `design.md`, la delta spec in `specs/frontend/` e il gruppo di task del tuo task.
3. Segui il flusso di `/opsx:apply` (skill `openspec-apply-change`) limitato a quel gruppo.

## Convenzioni del frontend
- Cartelle in `frontend/src/app`: `components/<nome>/` per pagine e componenti, `components/shared/` solo per componenti comuni (navbar, footer), `models/` per interfacce e tipi, `services/` per i servizi HTTP.
- Prima di creare un servizio, un tipo o un'etichetta controlla che non esista già in `services/` o `models/`.
- Componenti standalone con signal, route con `loadComponent`, chiamate solo a `/api/...` (con la `/` iniziale).
- Angular Material 3: `matButton="filled" | "outlined"`, icone con `fontSet="material-symbols-outlined"`, colori dai token `--mat-sys-*`.
- Testi visibili e commenti in italiano; identificatori in inglese. Formattazione Prettier (100 colonne).
- Gli enum del backend arrivano come stringhe (es. `"Monday"`).

## Verifica
- `npx ng build` e `npx ng test --watch=false` dalla cartella `frontend/`: riporta comando ed esito con i numeri reali.
- I test usano gli harness di Angular Material e dati sintetici.
- Spunta in `tasks.md` solo i punti davvero completati.

## Regole
- Non decidere da solo scelte non scritte nella change: chiedi.
- Commit secondo `CONTRIBUTING.md`, senza trailer `Co-Authored-By` né footer di generazione. Niente push o PR senza approvazione esplicita dell'utente.
