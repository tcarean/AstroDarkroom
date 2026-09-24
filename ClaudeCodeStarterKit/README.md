# ClaudeCodeStarterKit

Template pentru un proiect C#/.NET dezvoltat si intretinut aproape exclusiv cu Claude Code.
Solutie .NET 9 care compileaza si trece testele din prima, plus toate fisierele de proces:
gate de verificare, cerinte cu ID, ADR-uri, cifre de validare citite de teste, hooks, skills, CI.

Ghidul de referinta cu motivatia fiecarei piese e `GHID.md` (copia din repo a documentului
"Claude Code pentru proiecte .NET", editabil pe claude.ai; la modificari se re-exporta).
Textul scurt pentru model e `CLAUDE.md`; textul pentru om e `docs/ai-workflow.md`.
`GHID.md` tine de kit, nu de proiectele facute din el; se poate sterge din copii.

## Pornirea unui proiect nou

1. Copiaza tot folderul sub numele noului proiect si intra in el.
2. Redenumeste placeholder-ul `Starter` (nume PascalCase):

   ```bash
   powershell -NoProfile -ExecutionPolicy Bypass -File scripts/rename.ps1 -Name MyTool
   ```

3. Rescrie primul paragraf din `CLAUDE.md` si sterge codul de exemplu (`Stats`, `NumberFile`,
   fixtures, R-001..R-004, V-001) cand apare primul feature real. Pana atunci, ele arata cum se
   leaga o cerinta de un test si de o cifra de validare.
4. Instaleaza hook-ul de pre-commit (dupa `git init` daca e repo nou; intr-un monorepo
   detecteaza singur si pune o copie in `<radacina>/.githooks/`):

   ```bash
   powershell -NoProfile -ExecutionPolicy Bypass -File scripts/install-hooks.ps1
   ```

5. Ruleaza gate-ul o data, ca sa vezi ca e verde:

   ```bash
   powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1
   ```

6. Primul commit. De aici, in Claude Code: `/new-feature` pentru orice comportament nou,
   `/wrap-up` la sfarsitul fiecarei sesiuni, `/validate` dupa schimbari in cod numeric,
   `/docs-health` lunar si la fiecare model nou.

## Ce e unde

| Cale | Rol |
| --- | --- |
| `CLAUDE.md` | comenzi, reguli, pointeri; sub 200 de linii, in engleza |
| `docs/requirements.md` | cerinte cu ID si status; sursa adevarului pentru comportament |
| `docs/decisions/` | un ADR per decizie |
| `docs/validation.md` + `.json` | cifre de acceptanta; JSON-ul e citit de `ValidationTests` |
| `docs/plans/`, `docs/design/` | planuri aprobate; note de design citite la nevoie |
| `docs/ai-workflow.md` | cum se dezvolta proiectul, pentru cine vine dupa |
| `tests/testdata/` | fixtures in repo + manifest cu hash; fisierele mari in `large/` (ignorat de git) |
| `scripts/verify.ps1` | gate-ul unic: manifest, restore, format, build strict, teste |
| `scripts/hooks/` | git pre-commit si hook-ul Claude Code care ruleaza verify inainte de commit |
| `.claude/settings.json` | permisiuni si hooks, versionate; `settings.local.json` ramane local |
| `.claude/skills/` | `/new-feature`, `/wrap-up`, `/validate`, `/docs-health` |
| `.github/workflows/ci.yml`, `bitbucket-pipelines.yml` | CI, acelasi script |

| `monorepo/` | hook si CI pentru cazul in care kit-ul e un folder intr-un repo mai mare |

## Standalone sau folder intr-un monorepo

Ambele merg. Claude Code ia ca radacina folderul in care e pornit, deci CLAUDE.md, settings,
hooks si skills sunt per folder. Intr-un monorepo, hook-ul de pre-commit ruleaza `verify.ps1`
doar in proiectele atinse de fisierele din staging, iar un proiect fara `scripts/verify.ps1`
nu e verificat, deci proiectele existente pot adopta gate-ul pe rand. Detalii in
`monorepo/README.md`.

## Adoptare intr-un proiect existent

In ordinea asta, fiecare pas util de unul singur:

1. `scripts/verify.ps1` si `scripts/check-testdata.ps1` (cu numele solutiei), apoi `install-hooks.ps1`.
2. `.claude/settings.json` cu hook-ul de commit si `.claude/skills/`.
3. `docs/requirements.md` si `docs/decisions/`, umplute din CLAUDE.md-ul existent, care se
   scurteaza pe masura ce se muta continutul. Tinta: sub 200 de linii.
4. `docs/validation.json` citit de un `ValidationTests`, cu cifrele care azi stau in note.
5. `tests/testdata/manifest.json` pentru datele de test, mai ales cele mari din afara repo-ului.

## Cerinte de masina

.NET SDK 9.0.3xx (fixat in `global.json`), Windows PowerShell 5.1 sau `pwsh`, git.
