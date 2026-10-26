# Test data

Every FITS a test reads is built in memory by `FitsTestData` (a header of hand-written cards
and a small data area) and written to a temporary folder by `TempWorkspace`; no generated file
is kept here.

- `fixtures/` is in the repo: the header blocks of a real Siril plate solve, every 2880-byte
  block up to and including the one that holds `END`, nothing of the image (the tool reads
  nothing past the header). It is listed in `manifest.json` with its SHA-256, where it came
  from and which validation case it demonstrates; `scripts/check-testdata.ps1` checks the hash
  as a step of the gate, and `RepoInvariantTests` fails on a fixture missing from the
  manifest. `.gitattributes` marks fixtures `-text`, so git never converts their bytes (the
  kit's ADR 0014).
- `large/` is git-ignored and empty: nothing here needs a large file. `COPYWCS_TESTDATA_LARGE`
  would name another folder for it.

After changing a fixture on purpose:

```bash
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/check-testdata.ps1 -Update
```

Then explain the change in the commit and, if a validation case depends on it, in
`docs/validation.md`.
