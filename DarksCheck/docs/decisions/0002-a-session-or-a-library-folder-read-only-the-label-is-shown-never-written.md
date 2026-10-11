# 0002. The input is a Siril session or a bare folder of darks, read only; the label is shown, never written

Date: 2026-10-11
Status: accepted 2026-10-11

## Context

The owner's two uses: a Siril session (`darks/`, `biases/`, `lights/`, `flats/`,
`process/`, the stacked `result.fit` at the root) and a library of darks taken on their own
(`Flats and Darks\Darks\2026-10-10-ISO1600-120s-15.8celsius\darks`, 30 raws and a
`conditii.txt`). The label the tool proposes for a library (`idx<NN>`, ADR 0004) must reach
the folder's name. The frames are the owner's originals, and LightsCheck's rule (its ADR 0002)
is that nothing is written into their folders. On 2026-10-11 the owner chose: the tool shows
the label, the owner renames the folder.

## Decision

The console and the window take one folder. With `darks/` it is a session: the darks are the
frames of `darks/` (empty, an error, never a fallback to the root, where `result.fit` and the
other stacked FITS live), the biases those of `biases/` and the lights those of `lights/`,
both optional, `flats/` and the rest never opened. Without `darks/` but with `lights/`,
`biases/` or `flats/` it is a session without darks and an error naming the subfolder found.
Without any of them the frames at the root are the darks, a library, and no biases or lights
are looked for. A folder's frames are its camera raws when there are any, otherwise its FITS
files, never both. Nothing is ever written into the folder given or its subfolders: the
reports, CSV files and maps go to a folder the user names (`--out DIR`, a dialog in the
window), and the label is printed and shown with a copy button for the owner to rename the
folder by hand. A second folder, `--against`, is the partner for matching (plan step 3) and
is read the same way.

## Alternatives rejected

- **A sidecar file in the library folder.** Offered as `darkscheck.json` on a button; the
  owner kept the folder read-only, and a file beside the raws would be one more thing Siril's
  conversion scripts could trip on.
- **Renaming the folder from the tool.** A modification of the input, and the owner's naming
  convention may change.
- **Session folders only.** The library is the first use case; a library has no session
  layout.
- **Falling back to the root when `darks/` is empty.** The root holds the stacked results,
  which would be read as darks.

## Consequences

`SessionFolder` is the one place that knows the layout (R-002). Every requirement that
writes a file names where it writes. `SessionFixture` builds the three subfolders for the
tests.
