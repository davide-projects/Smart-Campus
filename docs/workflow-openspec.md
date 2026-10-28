# Workflow con OpenSpec e agenti

Le storie si pianificano con [OpenSpec](https://openspec.dev/docs) prima di scrivere codice. La pianificazione resta nel repository insieme al codice, quindi chi fa la review legge prima *cosa* deve succedere e poi *come* è stato fatto. Questo documento integra [CONTRIBUTING.md](../CONTRIBUTING.md), che resta valido per issue, branch, commit e PR.

## Struttura

```
openspec/
├── config.yaml              contesto del progetto e regole per gli artefatti
├── specs/<servizio>/spec.md  comportamento attuale: identity, rooms, bookings, gateway, frontend
└── changes/
    ├── <storia>-<descrizione>/   change in corso
    │   ├── proposal.md          perché e cosa cambia
    │   ├── specs/<servizio>/    delta: requisiti ADDED / MODIFIED / REMOVED
    │   ├── design.md            come, con le scelte tecniche
    │   └── tasks.md             un gruppo per ogni task GitHub
    └── archive/                 change concluse
.claude/
├── commands/opsx/, skills/  generati da `openspec init` (non modificarli a mano: `openspec update`)
└── agents/                  agenti del team
```

Le spec in `openspec/specs/` partono vuote e crescono con le change archiviate: non si scrivono spec per codice che non si sta cambiando.

## Convenzioni

| Elemento | Regola |
|---|---|
| Change | Una per **user story**, nome `<numero-storia>-<descrizione>`, es. `73-raffina-ricerca-spazi` |
| Delta spec | Per servizio: `specs/identity`, `specs/rooms`, `specs/bookings`, `specs/gateway`, `specs/frontend` |
| `tasks.md` | Un gruppo per task GitHub: `## 1. Slider per la capienza minima (#74)` |
| Lingua | Italiano; restano in inglese le intestazioni di OpenSpec, `SHALL`/`MUST`, `WHEN`/`THEN` e gli identificatori |
| Task del piano | Ogni storia ha due sub-issue in più: «Pianificazione OpenSpec» e «Archivio OpenSpec». Le PR della change chiudono queste, non la storia |
| Archivio | Dopo il merge dell'ultima PR della storia, con la PR del task «Archivio OpenSpec» |

## Ciclo di una storia

```
Storia #73 (label story)
 ├── #90 Pianificazione OpenSpec   (task)
 ├── #74, #75, #76, #77            (task di sviluppo)
 └── #91 Archivio OpenSpec         (task)

 1. docs/90-pianifica-raffina-ricerca-spazi     /opsx:propose  (agente spec-writer)
 │    PR "Closes #90" con la sola change → review del piano → merge su main
 │
 2. feat/74-slider-capienza                     /opsx:apply sul gruppo (#74)
 │    agente frontend-developer o backend-developer
 │    PR "Closes #74": codice + tasks.md spuntato → agente reviewer → review del team → merge
 │    (uguale per #75, #76...)
 │
 3. docs/91-archivia-raffina-ricerca-spazi      /opsx:archive
      PR "Closes #91" che sposta la change in archive/ e aggiorna openspec/specs/
```

I numeri sono un esempio. Il nome della change usa sempre il numero della **storia** (`73-...`), i branch quello del **task** (`90`, `74`, `91`...).

1. **Pianificazione.** Si creano i sub-issue «Pianificazione OpenSpec» e «Archivio OpenSpec» della storia. Dal branch del primo si crea la change con `/opsx:propose` (o con l'agente `spec-writer`). La PR contiene solo `openspec/changes/<nome>/`: il team discute ambito e requisiti qui, non nel codice. Per esplorare prima un'area poco chiara c'è `/opsx:explore`.
2. **Implementazione.** Ogni task ha il suo branch e la sua PR, come da CONTRIBUTING.md. L'agente legge la change su `main`, implementa solo il gruppo del task e spunta i punti fatti in `tasks.md`. Se durante il lavoro un requisito si rivela sbagliato, si aggiorna la change nella stessa PR (`/opsx:update`) e lo si scrive nel corpo della PR.
3. **Archivio.** Quando tutti i task di sviluppo sono chiusi, dal branch del task «Archivio OpenSpec» `/opsx:archive` controlla che `tasks.md` sia completo, fonde le delta in `openspec/specs/` e sposta la change in `archive/`.

## Agenti

Sono in `.claude/agents/` e si richiamano per nome in Claude Code.

| Agente | Cosa fa | Cosa non fa |
|---|---|---|
| `spec-writer` | Crea la change di una storia e la valida | Codice, issue, branch, PR |
| `frontend-developer` | Implementa un task frontend da una change approvata | Scelte non scritte nella change, push |
| `backend-developer` | Implementa un task dei servizi .NET da una change approvata | Scelte non scritte nella change, push |
| `reviewer` | Review di una PR rispetto a change, CONTRIBUTING e convenzioni | Modifiche e pubblicazione della review |

Regole comuni: nessuna scelta implicita (si chiede), dati solo sintetici, commit senza trailer di attribuzione, push e PR solo dopo l'approvazione di chi lavora sul task.

## Installazione

Serve Node.js 20.19 o superiore.

```bash
npm install -g @fission-ai/openspec@1.15.0
```

Dopo un aggiornamento della CLI, rigenerare comandi e skill:

```bash
openspec update
```

Comandi utili: `openspec list` (change in corso), `openspec show <change>`, `openspec validate <change>`.
