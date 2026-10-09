# Test data

Mindfulness Reminder has none (ADR 0005). Every test runs on fakes: `FakeRegistryStore` for
the registry, and the clock, timer, session monitor, presenter and activity monitor inside
`ReminderCoordinatorTests`. The application reads no file, decodes nothing and measures
nothing on data; the numbers it carries are constants pinned by unit tests.

`manifest.json` lists no file, so `scripts/check-testdata.ps1` has nothing to check and
passes. If a large file is ever needed (a registry export, a recording of `--detect` readings),
it goes under `large/`, which git ignores, with an entry in the manifest;
`MINDFULNESS_TESTDATA_LARGE` may name another folder, as the script allows. Then:

```bash
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/check-testdata.ps1 -Update
```
