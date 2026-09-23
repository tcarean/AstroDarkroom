# SirilCleanup

Script PowerShell care face curatenie dupa procesarea cu Siril. **Nu sterge nimic**:
muta fisierele ne-necesare intr-un folder `safe-to-delete`, pastrand exact structura
de foldere originala, ca sa poata fi mutate inapoi daca a fost o greseala.

## Ce este considerat ne-necesar

1. tot ce se afla intr-un folder numit `process/` (oriunde in cale, inclusiv subfoldere)
2. tot ce se afla intr-un folder numit `masters/`
3. fisierele `result.fit`, **doar** daca raspunzi `Y` la intrebarea de la pornire

Numele de foldere si fisiere se compara fara a tine cont de majuscule
(`Process`, `MASTERS`, `Result.FIT` sunt prinse la fel).

Regulile 1 si 2 se aplica intotdeauna. Regula 3 se adauga peste ele cand raspunzi `Y`.

## Cum se foloseste

1. Copiaza `SirilCleanup.ps1` si `SirilCleanup.bat` in folderul radacina care contine
   sesiunile Siril (cautarea este recursiva, deci pot fi oricate sesiuni in subfoldere).
   Cele doua fisiere trebuie sa stea in acelasi folder.
2. Dublu click pe `SirilCleanup.bat`.
3. La intrebarea `Sa fac cleanup si la result.fit? [y/N]` raspunde:
   - `Y` sau `y` = da, muta si `result.fit`
   - `N`, `n` sau doar Enter = nu (valoarea implicita)
   - orice altceva repeta intrebarea
4. Scriptul afiseaza fiecare fisier mutat, un sumar (numar de fisiere, marime), apoi
   asteapta o tasta inainte sa se inchida, ca sa poti vedea ce s-a mutat.

Scriptul nu are parametri de consola.

## Unde ajung fisierele

In folderul `safe-to-delete`, creat langa script, cu aceeasi structura relativa:

```
<radacina>\M31\process\pp_light_00001.fit   ->   <radacina>\safe-to-delete\M31\process\pp_light_00001.fit
<radacina>\M31\masters\flat_stacked.fit     ->   <radacina>\safe-to-delete\M31\masters\flat_stacked.fit
<radacina>\M31\result.fit                   ->   <radacina>\safe-to-delete\M31\result.fit   (doar cu Y)
```

Folderele `process/` si `masters/` originale raman pe loc, goale.

Dupa ce ai verificat ca totul e in regula, poti sterge manual folderul `safe-to-delete`.

## Cum anulezi

Muta continutul din `safe-to-delete` inapoi peste structura originala (de exemplu
copiaza folderele din `safe-to-delete` peste radacina, in Explorer). Structura fiind
identica, fiecare fisier ajunge exact de unde a plecat.

## Rulari repetate

Scriptul poate fi rulat de mai multe ori:

- ignora tot ce este deja in `safe-to-delete` si propriul fisier;
- nu suprascrie niciodata: daca in destinatie exista deja un fisier cu acelasi nume
  (dintr-o rulare anterioara), il lasa pe loc si il raporteaza ca `SARIT`.

## De ce exista fisierul .bat

Windows deschide un `.ps1` in editor la dublu click, nu il ruleaza. Fisierul `.bat`
porneste PowerShell cu `-ExecutionPolicy Bypass` si ruleaza scriptul din folderul
in care se afla, indiferent de folderul curent al consolei.
