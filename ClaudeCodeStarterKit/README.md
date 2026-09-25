# ClaudeCodeStarterKit

Template pentru un proiect C#/.NET dezvoltat si intretinut aproape exclusiv cu Claude Code.
Solutie .NET 9 care compileaza si trece testele din prima, plus toate fisierele de proces:
gate de verificare, cerinte cu ID, ADR-uri, cifre de validare citite de teste, hooks, skills, CI.

Ghidul de referinta cu motivatia fiecarei piese e `GHID.md`. E sursa principala si se editeaza
direct; documentul de pe claude.ai din care a pornit e doar istoric.
Impartirea e simpla: `CLAUDE.md` tine regulile, `GHID.md` tine explicatiile. Ghidul exista o
singura data, aici in kit, nu se copiaza in proiecte; fiecare proiect trimite la el din
`CLAUDE.md` si din README. Daca un proiect paraseste vreodata repo-ul `photo`, ia atunci o copie
a ghidului cu el.

## Pornirea unui proiect nou

1. Copiaza tot folderul sub numele noului proiect si intra in el.
2. Redenumeste placeholder-ul `Starter` (nume PascalCase):

   ```bash
   powershell -NoProfile -ExecutionPolicy Bypass -File scripts/rename.ps1 -Name MyTool
   ```

3. Rescrie primul paragraf din `CLAUDE.md`, sterge `GHID.md` si `monorepo/` din copie (raman in
   kit, iar `CLAUDE.md` deja trimite la ele acolo) si sterge codul de exemplu (`Stats`, `NumberFile`,
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
   `/docs-health` lunar si la fiecare model nou, `/release` cand vrei o versiune cu tag.

## Ce e unde

| Cale | Rol |
| --- | --- |
| `CLAUDE.md` | comenzi, reguli, pointeri; sub 200 de linii, in engleza |
| `docs/requirements.md` | cerinte cu ID si status; sursa adevarului pentru comportament |
| `docs/decisions/` | un ADR per decizie |
| `docs/validation.md` + `.json` | cifre de acceptanta; JSON-ul e citit de `ValidationTests` |
| `docs/plans/`, `docs/design/` | planuri aprobate; note de design citite la nevoie |
| `tests/testdata/` | fixtures in repo + manifest cu hash; fisierele mari in `large/` (ignorat de git) |
| `scripts/verify.ps1` | gate-ul unic: manifest, restore, format, build strict, teste |
| `scripts/check-staged.ps1` | refuza commit-ul care atinge `src/` fara linie in CHANGELOG |
| `scripts/hooks/` | git pre-commit si commit-msg; hook-urile Claude Code: gate la commit, urmarire editari, gate la Stop |
| `.claude/settings.json` | permisiuni si hooks, versionate; `settings.local.json` ramane local |
| `.claude/skills/` | `/new-feature`, `/wrap-up`, `/validate`, `/docs-health`, `/mutate`, `/release` |
| `scripts/release.ps1` | partea mecanica a unui release: verify, versiune, CHANGELOG, publish; commit-ul si tag-ul le propune skill-ul si le decizi tu |
| `.claude/agents/` | subagenti definiti in repo: `docs-auditor`, auditul documentatiei cu context curat, fara `src/`; un agent nou apare dupa repornirea sesiunii |
| `scripts/mutate.ps1`, `.config/dotnet-tools.json` | mutation testing pe Core cu Stryker.NET, la cerere; minute, nu in gate |
| `.github/workflows/ci.yml`, `bitbucket-pipelines.yml` | CI, acelasi script |

| `monorepo/` | hook si CI pentru cazul in care kit-ul e un folder intr-un repo mai mare |

## Ce e impus mecanic, nu doar scris in CLAUDE.md

- Commit fara `verify.ps1` verde: refuzat de git pre-commit si de hook-ul Claude Code; `--no-verify` blocat.
- Commit care atinge `src/` fara linie in `CHANGELOG.md`: refuzat de `check-staged.ps1`.
- Cerinta `implemented` fara test `R###_...`, sau test cu ID inexistent: `RepoInvariantTests` pica.
- Tura Claude Code care a editat `src/` sau `tests/` fara verify verde dupa ultima editare si
  fara CHANGELOG modificat: hook-ul Stop o refuza si trimite la `/wrap-up`.
- Mesaj de commit pe mai multe linii, cu trailere sau peste 72 de caractere: refuzat de hook-ul
  `commit-msg`, pentru oricine.
- Orice `git commit`, `merge`, `push` sau `rebase` al modelului: cere acordul omului in momentul
  rularii, prin lista `ask` din `.claude/settings.json`.

Ce ramane pe judecata: daca o schimbare merita ADR si daca o cerinta e bine formulata.

## Standalone sau folder intr-un monorepo

Ambele merg. Claude Code ia ca radacina folderul in care e pornit, deci CLAUDE.md, settings,
hooks si skills sunt per folder. Intr-un monorepo, hook-ul de pre-commit ruleaza `verify.ps1`
doar in proiectele atinse de fisierele din staging, iar un proiect fara `scripts/verify.ps1`
nu e verificat, deci proiectele existente pot adopta gate-ul pe rand. Detalii in
`monorepo/README.md`.

## Adoptare intr-un proiect existent

In trei straturi, fiecare util singur: gate-ul (o ora, fara cod), documentatia (cateva sesiuni,
fara cod), legatura cerinte-teste (gradual). Detalii si exemplul FlatCheck in `GHID.md`,
sectiunea "Migrarea unui proiect existent".

## Cerinte de masina

.NET SDK 9.0.3xx (fixat in `global.json`), Windows PowerShell 5.1 sau `pwsh`, git. Prima rulare
a `scripts/mutate.ps1` descarca Stryker.NET prin `dotnet tool restore`; nu e nevoie de instalare globala.
