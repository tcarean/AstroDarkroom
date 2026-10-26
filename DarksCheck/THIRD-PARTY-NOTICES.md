# Third-party components

DarksCheck is distributed under the **GNU General Public License v3 or later** (see
[LICENSE](LICENSE)). This file lists the third-party code it links, the licence of each
component and why it is compatible with GPLv3. A package added without an entry here is a
package whose compatibility nobody checked; `ThirdPartyTests` binds the list here, the list in
`ThirdParty.cs` (what the "About" window shows) and the packages of `DarksCheck.Core.csproj`.

What is written by hand and is **not** third-party code: the FITS reader, the PNG encoder, every
computation and every threshold. The FITS reader, the raw reader and the PNG encoder are copied
from LightsCheck, which copied them from FlatCheck: the same author, the same licence (ADR 0003).

---

## Sdcb.LibRaw

**Role.** Reads the undebayered Bayer mosaic, the exact white and black levels, the colour order
of the CFA and the shot's metadata (exposure, ISO, time, focal length, aperture, lens) from a
camera raw file.

**Licence.** MIT (the C# wrapper).

**Compatibility.** MIT is permissive and compatible with GPLv3.

- Project: <https://github.com/sdcb/Sdcb.LibRaw>

## LibRaw (the native libraries of `Sdcb.LibRaw.runtime.*`)

**Role.** The decoding of the raw formats itself. It comes as native DLLs (`raw_r`, `jpeg8`,
`lcms2`, `zlib1`), shipped with the executable (inside it, in the published single file).

**Licence.** Dual, at the user's choice: **LGPL-2.1-only OR CDDL-1.0**.

**Compatibility - a decision is made here.** DarksCheck uses LibRaw under **LGPL-2.1**, the same
choice as FlatCheck. CDDL-1.0 is **not** compatible with the GPL, so choosing the other option
would break the tool's licence. LGPL-2.1 is compatible: its section 3 explicitly allows applying
the terms of the ordinary GPL, "version 2 of the ordinary GNU General Public License or any later
version", to a copy, which brings the code into a GPLv3 work. LGPL-2.1 section 6 requires that,
if the program displays its copyright notices at run time (it does, on the second line of every
run), the LibRaw notice appear among them: that is why the list in `ThirdParty.cs` and the
"About" window exist.

- Project: <https://www.libraw.org/>

---

## Test components

Used only to build and run the tests, so they are not distributed with the tool.

| package | licence |
|---|---|
| xUnit | Apache-2.0 |
| Microsoft.NET.Test.Sdk | MIT |

---

## How to add an entry

See the kit's rule "Packages" (`.claude/rules/kit/process.md`) and the rule "Package notices" in
[CLAUDE.md](CLAUDE.md). In short: a package is discussed before, not after; its compatibility
with GPLv3 is shown, not assumed; and the reason is written here.
