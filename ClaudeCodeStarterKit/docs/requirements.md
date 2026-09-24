# Requirements

The source of truth for behaviour. Code follows this file, not the reverse. A behaviour change is
first an edit here (status `draft`), then a test, then code (status `implemented`).

Rules for entries:

- IDs are `R-NNN`, assigned once, never reused. A dropped requirement keeps its ID with status `dropped`.
- Status is one of `draft`, `accepted`, `implemented`, `dropped`.
- Describe observable behaviour, with one concrete example that includes numbers. No adjectives.
- Tests carry the ID in their name: `R001_Median_OddCount_ReturnsMiddleValue`.
- Error behaviour is a requirement too.

| ID | Status | Requirement |
| --- | --- | --- |
| R-001 | implemented | `Median` of a non-empty sequence: the middle value when the count is odd, the mean of the two middle values when even. Example: `[4,1,3,2]` gives `2.5`. Empty input throws `ArgumentException`. |
| R-002 | implemented | `RobustSigma` is the median absolute deviation from the median, times `1.4826`. Example: `1..10` gives `1.4826 * 2.5 = 3.7065`. |
| R-003 | implemented | A numbers file has one value per line, parsed with the invariant culture. Blank lines and lines starting with `#` are ignored. A line that does not parse throws `FormatException` naming the file line number, comments included. |
| R-004 | implemented | The CLI takes exactly one argument, the file path. Exit `0` and three lines `count`, `median`, `sigma` (four decimals) on success. Exit `1` and `error: <message>` on stderr for a missing or malformed file. Exit `2` and `usage:` on stderr for any other argument count. |

## Dropped

None yet. Move rows here with status `dropped` and one line saying why.
