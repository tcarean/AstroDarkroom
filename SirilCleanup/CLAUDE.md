# SirilCleanup

Script PowerShell 5.1 (Windows) care muta fisierele ne-necesare dupa procesarea cu Siril
intr-un folder `safe-to-delete`, pastrand structura originala. Documentatia de utilizare
este in `README.md`; acest fisier tine regulile pentru modificari.

## Fisiere

- `SirilCleanup.ps1` - scriptul; fara parametri, ruleaza din `$PSScriptRoot`
- `SirilCleanup.bat` - lansator pentru dublu click (`powershell.exe -NoProfile -ExecutionPolicy Bypass -File`)
- `README.md` - instructiuni pentru utilizator

## Reguli care nu se schimba

- **Nu se sterge nimic, niciodata.** Doar `Move-Item`, nu `Remove-Item`. Folderele goale ramase pe loc se lasa asa.
- **Nu se suprascrie nimic.** Daca destinatia exista deja, fisierul se sare si se raporteaza `SARIT`.
- Structura relativa fata de radacina se pastreaza identic sub `safe-to-delete`, ca mutarea inapoi sa fie triviala.
- Fara parametri de consola. Singura intrare este intrebarea `Y/N` de la pornire (Enter = N).
- La final se asteapta o tasta (`Wait-ForKey`), ca fereastra sa nu se inchida inainte ca utilizatorul sa vada lista.
- Codul ramane simplu si comentat pe sectiuni; utilizatorul vrea sa-l poata valida citindu-l.

## Detalii de implementare

- Decizia "e ne-necesar?" e centralizata in `Test-IsJunk`. Regulile 1+2 (`process`, `masters`) verifica
  fiecare segment de folder din calea relativa, nu doar parintele direct. Regula 3 (`result.fit`)
  se aplica doar cand `$CleanResult` e `$true`.
- Despartirea caii se face cu `String.Split` pe `'\'` si `'/'`, nu cu regex: un regex cu `\` a pierdut
  un backslash la scriere si a facut regula 1+2 sa nu prinda nimic. Verifica linia asta dupa orice editare.
- `Wait-ForKey` foloseste `Console.ReadKey`, cu fallback pe `Read-Host` cand stdin e redirectionat,
  ca scriptul sa poata fi testat automat (`"y" | powershell.exe -File ...`).
- Fisierele deja aflate sub `safe-to-delete` si scriptul insusi sunt excluse din cautare.
- Se folosesc `-LiteralPath` peste tot, ca numele cu `[ ]` sa nu fie interpretate ca wildcard.
- Fisierul `.ps1` este salvat fara BOM si fara diacritice in comentarii, ca PowerShell 5.1 sa-l citeasca corect.

## Cum se testeaza

Construieste un arbore de proba in scratchpad cu `process/`, `process/seq/`, `masters/`, `result.fit`
si un `lights/` care trebuie sa ramana neatins, copiaza scriptul acolo si ruleaza cu raspunsul pe stdin:

```powershell
"" | powershell.exe -NoProfile -ExecutionPolicy Bypass -File "<test>\SirilCleanup.ps1"   # Enter = N
"y" | powershell.exe -NoProfile -ExecutionPolicy Bypass -File "<test>\SirilCleanup.ps1"  # Y
```

Scenarii de acoperit: N muta doar process/masters; Y muta si `result.fit` in aceeasi rulare;
o a doua rulare cu fisiere recreate raporteaza `SARIT` pentru cele care exista deja in destinatie;
`lights/` si un fisier numit `process` (nu folder) raman pe loc.
