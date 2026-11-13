# 0101. The camera is the lensfun entry of that name, compared as words

Date: 2026-11-13
Status: accepted 2026-11-13

## Context

The camera's crop sets the frame's physical size for both vignetting models, and its mount
filters the lensfun candidates (R-116). Both come from `LensfunData.CameraOf`, which took the
table name `n` for the file's name `wanted` when `wanted.Contains(n) || n.Contains(wanted)`, on
letters and digits only, the longest name winning. The review of 2026-11-13 (CR-001) found that
the second half lets a longer model name that contains the camera's whole name beat the exact
one, and that the first half finds a one-letter alias inside any name.

Measured on 2026-11-13 on the shipped table (1053 cameras with a crop), each camera asked for
by each of its own names:

| Rule | Another body | Of which a crop more than 3 % off | Not found |
|---|---:|---:|---:|
| Before | 402 | 137 | 0 |
| The one chosen | 1 (two entries share an alias) | 0 | 0 |

Before: `Nikon D750` read the D7500's 1.534 (true 1.0), `Nikon D700` the D7000's 1.523, `Nikon
D80` the D800E's 1.0 (true 1.523), `Nikon Z f` the Z fc's 1.531, `Canon EOS R5` the R50 V's
1.613, `Pentax K-1` the K100D Super's 1.531; every `QHY...` name read the Pentax Q's 5.53 (its
alias is `Q`), and a name without a letter or digit read the longest name of the table. The
D3300 was not affected. The names the validation raws give (`NIKON D3300`, `Canon EOS 7D Mark
II`, `Sony ILCE-7M3`, `Pentax K-70`, `Canon EOS R6`, `Canon EOS R5m2`, `Canon EOS 40D`) find
the same crop under both rules.

The review of the branch, the same day, found what that count cannot see: a body the table
does not hold. `Canon EOS Kiss X10` (the Japanese 250D) is not there; its name holds the word
`X10`, which is Fujifilm's compact, crop 3.93, where the rule before had landed on `Canon EOS
Kiss X10i`, 1.613, right by luck. So each camera was also taken out of the table in turn,
with every other entry of the same model (the aspect-ratio twins), and asked for by the names
a file gives it: its model and each alias, and each of those again with the first word of its
canonical maker in front when the name does not begin with that word. That is 2929 names; a
crop counts as wrong when it is more than 3 % from the body's own:

| Rule | Wrong crop (more than 3 % off) | Right crop | Not found |
|---|---:|---:|---:|
| Before | 668 | 988 | 1273 |
| Words, without the maker (the branch's first commit) | 93 | 296 | 2540 |
| Words, the maker asked for on a name found inside | 40 | 182 | 2707 |

The 40 left are siblings of one maker (`Canon PowerShot G3 X` on `Canon PowerShot G3`).

A second review, of that change alone, found what this count cannot see either, since it puts
lensfun's own maker in front of each name: a maker the file spells otherwise. The first version
of the maker condition asked for the first word of the maker's name. lensfun says `Konica
Minolta` where a raw is expected to say `Minolta` (not measured: no such raw is at hand), so 14
bodies of the table (the Dynax and Maxxum 5D and 7D, ten DiMAGE and Revio compacts) lost a crop
they had before. The maker names of the table hold 59 distinct words in 57 names; 18 of them
say nothing about who the maker is (`AG`, `Camera`, `Co`, `Company`, `Computer`, `Corp`,
`Corporation`, `Digital`, `DV`, `Imaging`, `Inc`, `Ltd`, `Mobile`, `One`, `Optical`,
`Solutions`, `System`, `Technology`) and 41 are a maker's own. With any of those 41 counting,
the three figures of the last row stay as they are and `Minolta DYNAX 5D` reads 1.531 again.

## Decision

The owner's choices of 2026-11-13: first between this rule and "the exact name only"; then,
after the review, between asking for the maker (item 4) and leaving the rule as it was; then,
after the second review, that any word of the maker counts except the generic ones, on a list
of exclusions:

1. Names are compared as **words**: runs of letters and digits, upper-cased. `PENTAX K-70` is
   PENTAX, K, 70.
2. The **exact name** wins: the first entry whose model or alias has exactly the file's words.
3. Otherwise the entry one of whose names **stands whole in the file's name**, word for word
   and side by side (`Nikon D3300` in `Nikon Corporation NIKON D3300`); the name with the most
   letters wins, the first of two as long.
4. Such an entry counts only when the file's name also **carries its maker**: a word of one
   of the maker's names in the table, the canonical one or an alias, that is not a generic
   company word (`Nikon` of `Nikon Corporation`, `Kodak` beside `Eastman Kodak Company`,
   `Minolta` of `Konica Minolta`; never `Corporation`). The generic words are
   `LensfunData.GenericMakerWords`: the 18 met in the table and four legal forms a later copy
   may bring (`GmbH`, `KK`, `Limited`, `LLC`). The exact name needs no maker.
5. **Never the reverse**: a table name that only contains the file's name is no match, and
   neither is a shared part of a word. A name with no letter or digit finds nothing.
6. The crop's source in the report, in the notes and under the window's crop field prints the
   **table's model**, so the entry that gave the crop can be read.

## Alternatives rejected

- **The exact name only.** Stricter, but a file that writes the maker before the model
  (`Nikon Corporation NIKON D3300`, which a test has pinned since the first version) would lose
  its crop and ask for `--crop`.
- **Keeping the containment on letters and adding exceptions** (a minimum alias length, the
  longer name allowed only when no exact one exists). The D750 has an exact entry, but `Canon
  EOS R5 Mark II` asked of a table that lacks it would still fall on any longer name; words
  make the rule one sentence.

## Consequences

- A body the table does not hold gets the entry of its own maker whose words its name
  contains: `Canon EOS R5 Mark II` on a table with only `Canon EOS R5` reads the R5's crop.
  Such a pair does not always share a format: 40 of the 2929 names of the count above land on
  a sibling with another crop. The report names the model taken, so a wrong one is visible,
  and `--crop` overrides it.
- A body the table does not hold and whose name holds no whole entry of its maker gets no crop
  from the table, where the rule before often found a sibling by a part of a word (`Canon EOS
  R6m3`, `Olympus E-M1X`, `Canon EOS 60Da`): 2707 of the 2929 names against 1273. For an Adobe
  profile the calibration sensor then stands in, and a lensfun profile asks for `--crop`, as
  for any camera the table does not know.
- A maker word new to the table counts as the maker's own until it is put on the list, so a
  generic one could let another maker's entry through. `R133_TheGenericMakerWordsAreTheOnesOfTheShippedTable`
  fails when the table's maker words change, so that an update of `lensfun/`
  asks for the decision.
- A two-letter word of a maker (`OM`, `LG`, `YI`) carries it like any other. No name of the
  count above reached another maker's entry through one.
- Four names of the shipped table are carried by entries with different crops (`FinePix F810`,
  `DMC-LX1`, `DMC-LX3`, the alias `Cybershot`: one compact in several aspect ratios); the first
  entry counts, as before.
- `LensIdentification.PreferCamera` still matches an Adobe file's body by containment (the
  review's cleanup list); it is not changed here.
- R-133 holds the behaviour; `R133_...` in `LensProfileTests` pins the crossed pairs, the
  maker condition and the order of two names found inside, and walks the whole shipped table,
  each name bare and with its maker's first word in front.
