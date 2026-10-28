# SmartCampus

Istruzioni per Claude Code in questo repository.

- Flusso di lavoro: [docs/workflow-openspec.md](docs/workflow-openspec.md). Ogni storia ha una change OpenSpec in `openspec/changes/<storia>-<descrizione>/`; si implementa solo da una change già unita su `main`.
- Convenzioni di issue, branch, commit, PR e lingua: [CONTRIBUTING.md](CONTRIBUTING.md). Contesto tecnico e regole per gli artefatti: [openspec/config.yaml](openspec/config.yaml).
- Agenti in `.claude/agents/`: `spec-writer`, `frontend-developer`, `backend-developer`, `reviewer`.
- Non fare scelte che la change o la storia non indicano: chiedi.
- Dati solo sintetici; nessun segreto nel repository, nei log o nei prompt.
- Commit e PR senza trailer `Co-Authored-By` né footer di generazione; push e PR solo con approvazione esplicita.
