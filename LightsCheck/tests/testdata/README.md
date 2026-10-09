# Test data

LightsCheck keeps no fixture in the repo: every frame a test reads is generated in the test
(`SessionFixture` writes a synthetic Siril session of small FITS lights with the cards Siril
writes, `FitsTestData` builds any FITS). `manifest.json` therefore lists no file; it stays so
that `scripts/check-testdata.ps1` and `RepoInvariantTests` keep their contract if a fixture
ever appears (then list it here with its SHA-256; `.gitattributes` marks fixtures `-text`, the
kit's ADR 0014).

The real data are whole sessions of camera raws, gigabytes each, kept by the owner outside git
(`E:\Photografie\Astro`). `LIGHTSCHECK_TESTDATA_LARGE` names that folder; `docs/validation.json`
names each session relative to it, and `ValidationTests` (`Category=Validation`, out of the
gate) reads them through `TestData.Large(name)`, which returns null when a session is absent
so the test returns early with a message (ADR 0004). A single real raw for the decode test:
`LIGHTSCHECK_NEF=<file.NEF>` (`RawReaderTests`).
