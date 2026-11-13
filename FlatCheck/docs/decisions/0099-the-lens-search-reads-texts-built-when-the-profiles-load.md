# 0099. The lens search reads texts built when the profiles load

Date: 2026-11-12
Status: accepted 2026-11-12

## Context

On 2026-11-12 the owner opened a session whose flats name no lens, typed a lens name in the
Sintetic tab's lens field and found every keystroke slow. Each keystroke is one
`LensCatalog.Search`, on the window's thread. For every lens of the list it walked the whole
list of parsed profiles to gather that lens's aliases. Measured that day on the owner's
catalogue, 3799 lenses (3087 Adobe, 712 lensfun) and 18902 parsed profiles, in a Release build,
the least of three runs for each prefix of "samyang 135":

| At one keystroke | Before | After |
|---|---|---|
| `LensCatalog.Search` | 505 to 611 ms | 0.3 to 0.5 ms |
| The view-model's setter (search, rows, binding) | 523 to 608 ms | 1.5 to 11 ms |
| The layout of the list after it | 6 to 23 ms | 15 to 31 ms |

The list itself was never the cost: it is virtualized, 8 row containers for 3799 rows, and its
layout code did not change, so the two layout ranges differ only from one run to the next. Building
one text per lens for the whole catalogue, in one pass over the profiles, took 8 to 13 ms. The
number of lenses found was the same before and after for the empty text and for each of the ten
prefixes that do not end in a space (3799, then 3076, 423, 354, 161, 161, 161, 161, 129, 7, 7).

The first proposal built the texts at the first search. The owner answered that they should be
built when the application loads its data, where a progress line is shown anyway, so that the
first search is not the one that pays.

## Decision

`LensCatalog` keeps one search text per lens (name, maker, mount, calibration, the aliases of
its parsed profiles), built once in one pass over the profiles, and `Search` reads those texts
only. `ProfileLocator.Locate` builds them (`LensCatalog.PrepareSearch`) before it returns, so in
the window they are part of the background load of the profiles at the opening of the first
session, and in the console part of the one load of a run. A catalogue made without the locator
builds them at its first search. The lenses found and their order do not change (R-132).

## Alternatives rejected

- **Building the texts at the first search only.** The first proposal; 8 to 13 ms would not be
  felt there, but the owner chose the load, and the load is where the rest of the catalogue is
  built.
- **Building them in the `LensCatalog` constructor.** The lens list itself is built on first
  read on purpose, and the many catalogues the tests make would each pay for texts no test
  searches.
- **Searching on a background thread, or waiting for a pause in the typing.** Needed only while
  a search took half a second; at under a millisecond it would add a delay and a race for
  nothing.
- **A test with a time limit.** A limit fails by chance on a busy machine. The test counts
  instead: a search after the first does not walk the profile list.

## Consequences

The texts hold the aliases of the profiles the catalogue was given parsed, which are lensfun's;
an Adobe file parsed later for its lens does not add its aliases to the search, as it did not
before (`Search` never read the files parsed on demand). The console's `--lens` text and
`--lenses` use the same search and are half a second faster too. A catalogue costs one more array
of 3799 strings.
