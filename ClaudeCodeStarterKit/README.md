# ClaudeCodeStarterKit

Template pentru un proiect C#/.NET dezvoltat si intretinut aproape exclusiv cu Claude Code.
Solutie .NET 9 care compileaza si trece testele din prima, plus toate fisierele de proces:
gate de verificare, cerinte cu ID, ADR-uri, cifre de validare citite de teste, hooks, skills, CI.

Ghidul de referinta cu motivatia fiecarei piese e `GHID.md`. E sursa principala si se editeaza
direct; documentul de pe claude.ai din care a pornit e doar istoric.
Impartirea e simpla: regulile stau in `.claude/rules/kit/process.md` (cele de proces, ale
kit-ului, aceleasi in fiecare proiect) si in `CLAUDE.md` (ale proiectului), `GHID.md` tine
explicatiile. Ghidul exista o
singura data, aici in kit, nu se copiaza in proiecte; fiecare proiect trimite la el din
`CLAUDE.md` si din README. Daca un proiect paraseste vreodata repo-ul `photo`, ia atunci o copie
a ghidului cu el. Licenta kit-ului, si a scripturilor pe care le copiaza in proiecte, e
GPL-3.0-or-later (`LICENSE`; fiecare script incepe cu linia SPDX).

## Pornirea unui proiect nou

1. Copiaza tot folderul sub numele noului proiect si intra in el.
2. Redenumeste placeholder-ul `Starter` (nume PascalCase):

   ```bash
   powershell -NoProfile -ExecutionPolicy Bypass -File scripts/rename.ps1 -Name MyTool
   ```

3. Rescrie primul paragraf din `CLAUDE.md`, sterge `GHID.md` si `monorepo/` din copie (raman in
   kit, iar `CLAUDE.md` deja trimite la ele acolo) si sterge codul de exemplu (`Stats`, `NumberFile`,
   fixtures, R-001..R-004, V-001) cand apare primul feature real. Pana atunci, ele arata cum se
   leaga o cerinta de un test si de o cifra de validare. In `kit.json`, pune `"kit"` pe calea
   relativa catre kit (`../ClaudeCodeStarterKit` in repo-ul `photo`; sterge cheia intr-un repo
   separat, fara kit alaturi). `rename.ps1` a schimbat deja numele solutiei acolo. Din `scripts/`
   sterge `rename.ps1`, `install-hooks.ps1`, `hooks/pre-commit`, `hooks/pre-merge-commit` si
   `hooks/commit-msg`, care raman doar in kit; restul folderului nu se editeaza niciodata in proiect (vezi "Ce e impus mecanic").
   La fel `.claude/skills/`, `.claude/agents/` si `.claude/rules/kit/`: ce are proiectul de
   adaugat la un skill se scrie in `docs/skills.md`, in sectiunea skill-ului, iar regulile lui
   proprii in `CLAUDE.md`, fiecare cu un nume.
4. Hook-urile git (`pre-commit`, `pre-merge-commit` si `commit-msg`). Intr-un repo separat, dupa `git init`,
   ruleaza scriptul de mai jos; el seteaza `core.hooksPath` la `scripts/hooks`. Intr-un folder
   din repo-ul `photo` nu rulezi nimic, nici `git init`: hook-urile sunt deja instalate la
   radacina, in `.githooks/`, si gasesc singure `scripts/verify.ps1` al proiectului. Intr-un
   monorepo scriptul se ruleaza doar din kit: o data pe o clona noua, pentru ca seteaza calea
   absoluta a `.githooks/`, prin care orice worktree ruleaza hook-urile checkout-ului principal
   (ADR 0016), si dupa ce schimbi un hook, pentru ca fiecare copie a lui scrie peste
   `.githooks/` hook-urile de langa ea.

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
| `CLAUDE.md` | comenzi, regulile proiectului, pointeri; impreuna cu fisierul de reguli sub 200 de linii, in engleza |
| `.claude/rules/kit/process.md` | regulile de proces ale kit-ului, cu nume; Claude Code il incarca odata cu `CLAUDE.md`; identic in fiecare proiect |
| `docs/requirements.md` | cerinte cu ID si status; sursa adevarului pentru comportament |
| `docs/decisions/` | un ADR per decizie |
| `docs/validation.md` + `.json` | cifre de acceptanta; JSON-ul e citit de `ValidationTests` |
| `docs/plans/`, `docs/design/` | planuri aprobate; note de design citite la nevoie |
| `tests/testdata/` | fixtures in repo + manifest cu hash; fisierele mari in `large/` (ignorat de git) |
| `kit.json` | valorile proiectului pentru scripturile, skill-urile si agentul comune: solutia, folderele de cod si de teste, datele de test, ce publica un release, ce mai poate citi auditorul, unde e kit-ul |
| `docs/skills.md` | adaugirile proiectului la skill-urile kit-ului, o sectiune per skill; fiecare skill il citeste inainte de primul pas |
| `scripts/verify.ps1` | gate-ul unic: fisiere comune si chei de proces identice cu ale kit-ului, manifest, antetele ADR-urilor (`check-decisions.ps1`), restore, format, build strict, teste |
| `scripts/sync.ps1` | copiaza `scripts/`, `.claude/skills/`, `.claude/agents/`, `.claude/rules/kit/` si cheile de proces din `.claude/settings.json` din kit in proiect (sau, din kit, in toate proiectele); `-Check` doar compara, e pasul 0 al gate-ului |
| `scripts/kit-config.ps1` | citeste `kit.json` pentru celelalte scripturi; tine singura lista a folderelor comune |
| `scripts/hooks/` | git pre-commit, pre-merge-commit si commit-msg; hook-urile Claude Code: gate la commit, urmarire editari, gate la Stop |
| `.claude/settings.json` | permisiuni, hooks si `attribution`, versionate; `hooks`, `ask`, `deny`, intrarile git din `allow` si `attribution` sunt ale kit-ului, restul lui `allow` al proiectului; `settings.local.json` ramane local |
| `.claude/skills/` | `/new-feature`, `/wrap-up`, `/validate`, `/docs-health`, `/mutate`, `/release`; ale kit-ului, identice in fiecare proiect, citesc `kit.json` si `docs/skills.md` |
| `scripts/release.ps1` | partea mecanica a unui release: verify, versiune, CHANGELOG, publish; commit-ul si tag-ul le propune skill-ul si le decizi tu |
| `.claude/agents/` | subagenti definiti in repo: `docs-auditor`, auditul documentatiei cu context curat, fara `src/`, care citeste si ADR-urile kit-ului; un agent nou apare dupa repornirea sesiunii |
| `scripts/mutate.ps1`, `.config/dotnet-tools.json` | mutation testing pe Core cu Stryker.NET, la cerere; minute, nu in gate |
| `AI Prompt to Generate Starter Project.md` | prompt neutru fata de stack si model, de rulat peste luni sau ani pe un agent nou, ca sa refaca un setup cu aceleasi principii, gate-uri si documentatie dupa bunele practici de atunci; descrie scopuri si dovezi, nu implementarea de aici |
| `.github/workflows/ci.yml`, `bitbucket-pipelines.yml` | CI, acelasi script |
| `monorepo/` | hook si CI pentru cazul in care kit-ul e un folder intr-un repo mai mare |

## Ce e impus mecanic, nu doar scris in reguli

- Commit fara `verify.ps1` verde: refuzat de git pre-commit si de hook-ul Claude Code; `--no-verify` blocat.
- Merge fara conflicte al carui rezultat nu trece `verify.ps1`: refuzat de git pre-merge-commit,
  care ruleaza acelasi gate ca pre-commit (ADR 0022).
- `scripts/`, `.claude/skills/`, `.claude/agents/` sau `.claude/rules/kit/` modificat intr-un
  proiect, nu in kit: gate-ul e rosu la pasul 0 (`sync.ps1 -Check`), deci commit-ul e refuzat,
  iar hook-ul Stop nu inchide tura. Sursa e kit-ul; `sync.ps1` copiaza. Dupa o schimbare in kit,
  primul commit din fiecare proiect cere intai `sync.ps1` (ADR 0017, ADR 0018, ADR 0028).
- O cheie de proces din `.claude/settings.json` (`hooks`, `ask`, `deny`, intrarile git din
  `allow`, `attribution`) diferita de a kit-ului: gate-ul e rosu la pasul 0 si numeste cheia;
  `sync.ps1` o rescrie, fara sa atinga restul fisierului (ADR 0028).
- Cerinta `implemented` fara test `R###_...`, sau test cu ID inexistent: `RepoInvariantTests` pica.
- Cale numita in `GHID.md` sau in acest README (script, folder, fisier) care nu exista in kit:
  `RepoInvariantTests` pica. Doar in kit; explicatiile invechite nu le prinde.
- Antet de ADR in afara formatului (titlu, data, status, `Supersedes`), numar folosit de doua
  ori, sau o inlocuire scrisa doar in unul din cele doua ADR-uri: `check-decisions.ps1`, pas
  in `verify.ps1`, e rosu (ADR 0024).
- Un ADR sau o regula citate dupa numar gol in `scripts/`, `.claude/skills/`,
  `.claude/agents/` sau `.claude/rules/kit/`, unde intr-un proiect numarul ar insemna altceva:
  `RepoInvariantTests` pica, doar in kit. Forma ceruta e "the kit's ADR NNNN", iar o regula se
  citeaza pe nume, intre ghilimele (ADR 0025, ADR 0028).
- Tura Claude Code care a editat `src/` sau `tests/` fara verify verde dupa ultima editare:
  hook-ul Stop o refuza si trimite la `/wrap-up`.
- Hook-urile Claude Code judeca proiectul in care se lucreaza, si intr-un worktree: hook-ul de
  commit ruleaza verify-ul proiectelor atinse de fisierele commit-ului, hook-ul Stop cere
  stamp-ul proiectului fiecarui fisier editat, nu ale folderului in care a
  pornit sesiunea (ADR 0020). Un commit cu o cale pe care hook-ul nu o poate citi (o variabila)
  e blocat; la fel `-n` in orice forma si `-c core.hooksPath`.
- Mesaj de commit pe mai multe linii, cu trailere sau peste 100 de caractere: refuzat de hook-ul
  `commit-msg`, pentru oricine.
- Un `git commit` al modelului pe `main`, pe alt branch decat unul `claude/` sau pe un HEAD
  detasat: refuzat de hook-ul Claude Code de commit. Pe branch-ul `claude/` al task-ului sau,
  modelul face commit fara sa intrebe; pe `main` commit-urile le face omul (ADR 0021).
- Un commit al unei sesiuni Claude Code pe un branch `claude/` in checkout-ul principal:
  refuzat de hook-ul git `pre-commit`, inainte de gate-ul lui, oricare ar fi folderul sesiunii;
  la fel un merge al sesiunii, cu commit de merge, pe un asemenea branch. Commit-urile omului,
  din shell-uri care nu seteaza variabila `CLAUDECODE`, nu sunt atinse (ADR 0026).
- Orice `git merge`, `push`, `rebase` sau `branch -D` (stergere fortata) al modelului: cere
  acordul omului in momentul rularii, prin lista `ask` din `.claude/settings.json`.

Ce ramane pe judecata: daca o schimbare merita ADR, daca o cerinta e bine formulata si daca
ghidul si README-ul mai explica corect ce fac scripturile (un pas din `/wrap-up`).

## Standalone sau folder intr-un monorepo

Ambele merg. Claude Code ia ca radacina folderul in care e pornit, deci CLAUDE.md,
`.claude/rules/`, settings, hooks si skills sunt per folder. Intr-un monorepo, hook-ul de pre-commit ruleaza `verify.ps1`
doar in proiectele atinse de fisierele din staging, iar un proiect fara `scripts/verify.ps1`
nu e verificat, deci proiectele existente pot adopta gate-ul pe rand. Detalii in
`monorepo/README.md`.

## Adoptare intr-un proiect existent

In trei straturi, fiecare util singur: gate-ul (o ora, fara cod: `scripts/` copiat din kit si un
`kit.json` scris pentru proiect), documentatia (cateva sesiuni, fara cod), legatura cerinte-teste
(gradual). Detalii si exemplul FlatCheck in `GHID.md`, sectiunea "Migrarea unui proiect existent".

## Cerinte de masina

.NET SDK 9.0.3xx (fixat in `global.json`), Windows PowerShell 5.1 sau `pwsh`, git. Prima rulare
a `scripts/mutate.ps1` descarca Stryker.NET prin `dotnet tool restore`; nu e nevoie de instalare globala.
