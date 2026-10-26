# Come contribuire

Queste convenzioni servono a **collegare automaticamente** storie, task, branch, commit e pull request tra loro e con il GitHub Project, così lo stato del backlog si aggiorna da solo e ogni modifica è tracciabile fino al bisogno da cui nasce.

```
User Story (issue, label story)
 └── Task (sub-issue, label task)
      └── Branch  feat/42-blocca-sovrapposizioni      ← creato dalla issue
           └── Commit  feat(bookings): … (#42)
                └── Pull Request  "Closes #42"          ← merge = task chiuso = Done sul Project
```

---

## Lingua

Regola valida per **tutto il team**.

| In **inglese** | In **italiano** |
|---|---|
| Identificatori: classi, metodi, variabili, proprietà, parametri, enum, nomi dei file sorgente e dei test | Commenti nel codice |
| Nomi di servizi, progetti, cartelle, container Docker, database | Documentazione (`README`, `docs/`, questo file) |
| API: route, parametri di query, campi JSON | Messaggi di commit e parte descrittiva dei branch |
| Dati: tabelle, colonne, valori enum salvati | Titoli e descrizioni di issue e pull request |
| Nomi degli eventi tra servizi | Testi visibili all'utente: schermate Angular, messaggi di errore delle API |
| Scope dei commit e label `svc:*` | |

I termini da usare sono nel glossario: [docs/handoff-architettura.md, sezione 4](docs/handoff-architettura.md#4-glossario-condiviso). Esempio: in italiano *prenotazione*, nel codice `Reservation`; in italiano *spazio*, nel codice `Room`.

```csharp
// Due prenotazioni si sovrappongono se gli intervalli [Start, End) si intersecano (INV-1).
public bool OverlapsWith(Reservation other) => Start < other.End && other.Start < End;

[Fact]
public void OverlapsWith_AdjacentReservations_ReturnsFalse() { ... }
```

```
feat(bookings): blocca prenotazioni sovrapposte (#42)       ← scope inglese, descrizione italiana
feat/42-blocca-sovrapposizioni                              ← branch: tipo e testo come da sezione 2
```

---

## 1. Issue: storie, task, bug

| Tipo | Label | Chi la crea | Template |
|---|---|---|---|
| **User Story** | `story` | Chi cura il backlog, dopo la revisione del team leader e l'accordo con il PO | *User Story* (formato obbligatorio del corso) |
| **Task** | `task` | Developers, come **sub-issue** della storia | *Task* |
| **Bug** | `bug` | Chiunque | *Bug* |

Label aggiuntive per servizio: `svc:identity`, `svc:rooms`, `svc:bookings`, `svc:attendance`, `svc:gateway`, `frontend`, `infra`.

- Le storie descrivono **un bisogno** ("Come studente voglio…"), non una soluzione tecnica, e seguono **obbligatoriamente** il formato: `[ID] - Titolo`, *User Story* (Come / voglio / in modo da), *Descrizione*, *Criteri di accettazione*, *Story Point* (1/2/3/5/8/13). La bozza è in [docs/user-stories-bozza.md](docs/user-stories-bozza.md).
- I task si creano **dentro la storia** con *Create sub-issue* (o *Add sub-issue* per una issue esistente): GitHub mostra l'avanzamento della storia (es. 3/5) e il Project vede la gerarchia.
- Si lavora sempre su una issue: **niente branch senza issue**.

## 2. Branch

**Crea il branch dalla issue**: nella colonna destra della issue → *Development* → *Create a branch*. Così il branch è collegato alla issue da subito. GitHub propone un nome come `42-titolo-della-issue`: **aggiungi il prefisso del tipo** (`feat/42-…`), altrimenti il controllo `pr-check` fallisce.

Nome: `<tipo>/<numero-issue>-<descrizione-breve>`

| Tipo | Uso |
|---|---|
| `feat/` | nuova funzionalità |
| `fix/` | correzione di bug |
| `test/` | solo test |
| `docs/` | solo documentazione |
| `chore/` | configurazione, build, CI, dipendenze |
| `refactor/` | ristrutturazione senza cambiare il comportamento |

Esempi: `feat/42-blocca-sovrapposizioni`, `fix/57-orario-chiusura-escluso`, `docs/12-readme-avvio`.

Da riga di comando (equivalente):

```bash
gh issue develop 42 --name feat/42-blocca-sovrapposizioni --checkout
```

- Un branch = **un task** (o una storia piccola senza sub-issue).
- Si parte sempre da `main` aggiornato; branch di vita breve (pochi giorni).

## 3. Commit

Formato [Conventional Commits](https://www.conventionalcommits.org/) con riferimento alla issue:

```
<tipo>(<scope>): <cosa cambia, all'imperativo> (#<issue>)
```

- **tipo:** `feat`, `fix`, `test`, `docs`, `chore`, `refactor`
- **scope:** `identity`, `rooms`, `bookings`, `attendance`, `gateway`, `frontend`, `contracts`, `infra`, `ci`, `docs`

Esempi:

```
feat(bookings): blocca prenotazioni sovrapposte con UPDLOCK (#42)
test(bookings): verifica 20 richieste concorrenti sullo stesso slot (#42)
fix(rooms): rifiuta orario di chiusura precedente all'apertura (#57)
```

Il `#42` fa comparire il commit nella cronologia della issue. **Non** usare `Closes`/`Fixes` nei commit: la chiusura avviene con la PR (vedi sotto), così una issue non si chiude prima della review.

## 4. Pull request

- **Titolo:** stesso formato dei commit, es. `feat(bookings): blocca prenotazioni sovrapposte (#42)`.
- **Descrizione:** la prima riga deve essere `Closes #42`. È questo che:
  - collega la PR alla issue (sezione *Development*);
  - **chiude automaticamente il task al merge**;
  - sposta l'elemento su **Done** nel Project.
- Più task in una PR: `Closes #42, closes #43` (meglio evitarlo).
- Per i bug: `Fixes #57`.
- Compila il template: verifiche **eseguite** (comando ed esito), Definition of Done, limiti noti.
- Almeno **1 review** di un altro membro del team prima del merge.
- **Squash and merge**: su `main` resta un solo commit per task, con il titolo della PR.

Il workflow `pr-check` verifica automaticamente nome del branch, titolo e presenza di `Closes #n`.

## 5. Chiusura delle storie

Il merge chiude i **task**, non la **storia**. La storia si chiude quando:
1. tutti i sub-issue sono chiusi;
2. i criteri di accettazione sono stati verificati;
3. il PO ha validato l'incremento.

La conformità tecnica (Definition of Done) e la validazione del PO non si sostituiscono a vicenda: per questo la storia la chiude chi cura il backlog, dopo la review con il PO.

## 6. Ciclo di lavoro in breve

```bash
# 1. dalla issue #42: Development → Create a branch (oppure)
gh issue develop 42 --name feat/42-blocca-sovrapposizioni --checkout

# 2. lavora e committa
git commit -m "feat(bookings): blocca prenotazioni sovrapposte (#42)"

# 3. allinea con main e verifica in locale
git fetch origin && git rebase origin/main
dotnet test SmartCampus.slnx

# 4. pubblica e apri la PR
git push -u origin HEAD
gh pr create --fill --body "Closes #42"
```

---

## Configurazione una tantum (proprietario del repository / Project)

### Label

```bash
gh label create story --color 1D76DB --description "User story"
gh label create task --color 0E8A16 --description "Task tecnico, sub-issue di una storia"
for s in identity rooms bookings attendance gateway; do gh label create "svc:$s" --color C5DEF5; done
gh label create frontend --color C5DEF5
gh label create infra --color C5DEF5
```

(`bug` esiste già.)

### Impostazioni del repository
- *Settings → General → Pull Requests*: abilitare solo **Allow squash merging** e **Automatically delete head branches**.
- *Settings → Branches* (o *Rules → Rulesets*) su `main`: richiedere PR, almeno 1 approvazione, check CI verdi (`backend`, `frontend`, `docker`, `pr-check`), niente push diretti.

### Workflow del GitHub Project
Nel Project → menu `…` → *Workflows*, attivare:

| Workflow | Effetto |
|---|---|
| **Auto-add to project** | filtro `is:issue,pr`: ogni nuova issue/PR del repository entra nel Project |
| **Item added to project** | Status → *Todo* |
| **Pull request linked to issue** | Status della issue → *In Progress* (scatta con `Closes #n`) |
| **Code review approved** | Status → *In Review* (o colonna equivalente) |
| **Pull request merged** | Status → *Done* |
| **Item closed** | Status → *Done* |
| **Item reopened** | Status → *In Progress* |

Nel Project conviene aggiungere i campi **Parent issue** e **Sub-issues progress** e una vista raggruppata per storia.
