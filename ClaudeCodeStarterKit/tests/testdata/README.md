# Test data

Two kinds, one manifest.

- `fixtures/` is in the repo: small files, hand-checked, each listed in `manifest.json` with its
  SHA-256, where it came from and which requirement or validation case it demonstrates.
  `RepoInvariantTests` fails if a fixture is not in the manifest.
- `large/` is git-ignored: masters, raw captures, anything over a few hundred kilobytes. The
  manifest lists them too, with `"location": "large"`. `scripts/check-testdata.ps1` reports an
  absent large file as a warning locally and as an error in CI only when `requiredInCi` is true.
  `STARTER_TESTDATA_LARGE` overrides the directory. Tests reach them through `TestData.Large(name)`
  and skip with a clear message when the file is absent.

After changing a fixture on purpose:

```bash
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/check-testdata.ps1 -Update
```

Then explain the change in the commit and, if a validation case depends on it, in `docs/validation.md`.
Generating data in the test (a synthetic writer) beats a stored file whenever the generator is
simpler than the file is large.
