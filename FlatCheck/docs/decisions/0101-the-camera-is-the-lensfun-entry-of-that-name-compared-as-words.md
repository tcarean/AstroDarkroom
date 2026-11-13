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

## Decision

The owner's choice of 2026-11-13, between this rule and "the exact name only":

1. Names are compared as **words**: runs of letters and digits, upper-cased. `PENTAX K-70` is
   PENTAX, K, 70.
2. The **exact name** wins: the first entry whose model or alias has exactly the file's words.
3. Otherwise the entry one of whose names **stands whole in the file's name**, word for word
   and side by side (`Nikon D3300` in `Nikon Corporation NIKON D3300`); the name with the most
   letters wins, the first of two as long.
4. **Never the reverse**: a table name that only contains the file's name is no match, and
   neither is a shared part of a word. A name with no letter or digit finds nothing.
5. The crop's source in the report and in the notes prints the **table's model**, so the entry
   that gave the crop can be read.

## Alternatives rejected

- **The exact name only.** Stricter, but a file that writes the maker before the model
  (`Nikon Corporation NIKON D3300`, which a test has pinned since the first version) would lose
  its crop and ask for `--crop`.
- **Keeping the containment on letters and adding exceptions** (a minimum alias length, the
  longer name allowed only when no exact one exists). The D750 has an exact entry, but `Canon
  EOS R5 Mark II` asked of a table that lacks it would still fall on any longer name; words
  make the rule one sentence.

## Consequences

- A body the table does not hold gets the entry whose words its name contains: `Canon EOS R5
  Mark II` on a table with only `Canon EOS R5` reads the R5's crop. Whether two such bodies
  always share a format was not measured; the report names the model taken, so a wrong one is
  visible, and `--crop` overrides it.
- Four names of the shipped table are carried by entries with different crops (`FinePix F810`,
  `DMC-LX1`, `DMC-LX3`, the alias `Cybershot`: one compact in several aspect ratios); the first
  entry counts, as before.
- `LensIdentification.PreferCamera` still matches an Adobe file's body by containment (the
  review's cleanup list); it is not changed here.
- R-133 holds the behaviour; `R133_...` in `LensProfileTests` pins the crossed pairs and walks
  the whole shipped table.
