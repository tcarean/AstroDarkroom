# Third-party components

DarksCheck is distributed under the **GNU General Public License v3 or later** (see
[LICENSE](LICENSE)). This file lists the third-party code it links, the licence of each
component and why it is compatible with GPLv3. A package added without an entry here is a
package whose compatibility nobody checked; `ThirdPartyTests` binds the list here, the list in
`ThirdParty.cs` (what the "About" window shows) and the packages of `DarksCheck.Core.csproj`.

The whole text of every licence named here, with the notices of the code each component says it
contains, is in the Astro Darkroom suite's notices: `THIRD-PARTY-NOTICES.txt` in the suite's
folder, beside the programs, and `AstroDarkroom/THIRD-PARTY-NOTICES.md` in this repository. That
file names DarksCheck among the programs that ship each of the six components below, and the
About window points to it (R-120, ADR 0027); `ThirdPartyTests` holds the two lists equal. This
file keeps NLog's text, and libjpeg-turbo's statement for the Independent JPEG Group.

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

## LibRaw (`raw_r.dll`, from `Sdcb.LibRaw.runtime.*`)

**Role.** The decoding of the raw formats itself: `raw_r.dll`, LibRaw 0.21.1. Its Windows package
ships it with the three libraries it calls, each a section below: libjpeg-turbo (`jpeg8.dll`),
Little CMS (`lcms2.dll`) and zlib (`zlib1.dll`). The four are inside the published single file,
which extracts them at start, and beside the window in the Astro Darkroom folder. They import
Microsoft's Visual C++ v14 runtime (x64), `vcruntime140.dll`, `vcruntime140_1.dll`,
`msvcp140.dll` and `vcomp140.dll`, which neither ships: it is Microsoft's, installed on most
computers, and a raw's error names it when it is missing or too old (R-110, ADR 0026).

**Licence.** Dual, at the user's choice: **LGPL-2.1-only OR CDDL-1.0**.

**Compatibility - a decision is made here.** DarksCheck uses LibRaw under **LGPL-2.1**, the same
choice as FlatCheck. CDDL-1.0 is **not** compatible with the GPL, so choosing the other option
would break the tool's licence. LGPL-2.1 is compatible: its section 3 explicitly allows applying
the terms of the ordinary GPL, "version 2 of the ordinary GNU General Public License or any later
version", to a copy, which brings the code into a GPLv3 work. LGPL-2.1 section 6 requires that,
if the program displays its copyright notices at run time (it does, on the second line of every
run), the LibRaw notice appear among them: that is why the list in `ThirdParty.cs` and the
"About" window exist. The same section asks for a reference directing the user to the copy of
the licence: About's lead points to the suite's notices, which hold the LGPL-2.1's text.

- Project: <https://www.libraw.org/>

## libjpeg-turbo (`jpeg8.dll`)

**Role.** Decodes, for LibRaw, the JPEG-compressed image data some raw files hold, lossy DNG
among them: `jpeg8.dll`, libjpeg-turbo 2.1.3, from `Sdcb.LibRaw.runtime.win64`.

**Licence.** Three, each for a part of the code: the IJG licence, for the code that comes from
the Independent JPEG Group's libjpeg; the modified (3-clause) BSD licence, for the TurboJPEG API
and the build system; the zlib licence, for the SIMD extensions.

**Compatibility.** The Free Software Foundation lists all three as compatible with the GNU GPL.
The IJG licence asks a program distributed as executables to state, in its documentation:

This software is based in part on the work of the Independent JPEG Group.

The About window's line for libjpeg-turbo quotes that sentence, since the single file has no
other documentation. The BSD licence asks its notice, its conditions and its disclaimer to go
with the binaries: the About window shows the notice, and the suite's notices carry the whole
text; the zlib licence asks nothing of a binary distribution.

- Project: <https://libjpeg-turbo.org/>

## Little CMS (`lcms2.dll`)

**Role.** The colour management inside LibRaw, its ICC profile transforms: `lcms2.dll`, Little
CMS 2.12, from `Sdcb.LibRaw.runtime.win64`.

**Licence.** MIT.

**Compatibility.** MIT is permissive and compatible with GPLv3. It asks its copyright notice and
its permission notice to go with every copy: the About window shows the notice, and the suite's
notices carry the whole text.

- Project: <https://www.littlecms.com/>

## zlib (`zlib1.dll`)

**Role.** Decompresses, for LibRaw, the Deflate-compressed data some raw files hold, DNG among
them: `zlib1.dll`, zlib 1.2.11, from `Sdcb.LibRaw.runtime.win64`.

**Licence.** zlib.

**Compatibility.** The zlib licence is permissive, and the Free Software Foundation lists it as
compatible with the GPL; it asks nothing of a binary distribution.

- Project: <https://zlib.net/>

## NLog

**Role.** Writes the diagnostic log, the text files in the `log` folder beside each executable
that a bug report carries (ADR 0025). It decides nothing and computes nothing: the core and the
two applications hand it finished lines, and without its configuration every call is a no-op.
Two packages of the same project come with it: `NLog`, and `NLog.Targets.AtomicFile`, its file
target with the operating system's atomic append, so two windows writing one log at the same
time lose no line (R-103).

**Licence.** BSD-3-Clause, for both packages, the version taken being **6.2.2**; both are built
from the same commit of the same repository, and neither carries a native file.

**Compatibility.** BSD-3-Clause is permissive and compatible with GPLv3; the Free Software
Foundation lists the modified (3-clause) BSD licence as GPL-compatible. Of its three conditions,
the source one does not arise (no NLog source is copied here); the binary one asks the copyright
notice, the conditions and the disclaimer to go with the distribution, and the window's About
shows the notice, the licence's name and its link, as it does for the MIT wrapper, while this
section reproduces the whole text, word for word from `LICENSE.txt` at the commit 6.2.2 was
built from (`d9ebf82`), as FlatCheck read it for the same packages (its ADR 0070); the third
forbids using the authors' names to endorse DarksCheck, which nothing here does.

> Copyright (c) 2004-2024 Jaroslaw Kowalski <jaak@jkowalski.net>, Kim Christensen, Julian Verdurmen
>
> All rights reserved.
>
> Redistribution and use in source and binary forms, with or without
> modification, are permitted provided that the following conditions
> are met:
>
> * Redistributions of source code must retain the above copyright notice,
>   this list of conditions and the following disclaimer.
>
> * Redistributions in binary form must reproduce the above copyright notice,
>   this list of conditions and the following disclaimer in the documentation
>   and/or other materials provided with the distribution.
>
> * Neither the name of Jaroslaw Kowalski nor the names of its
>   contributors may be used to endorse or promote products derived from this
>   software without specific prior written permission.
>
> THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
> AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
> IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
> ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE
> LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
> CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
> SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
> INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
> CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
> ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF
> THE POSSIBILITY OF SUCH DAMAGE.

The package's own metadata carries a second notice, `Copyright (c) 2004-2026 NLog Project -
https://nlog-project.org/`.

**The alternatives evaluated.** `Microsoft.Extensions.Logging` needs a second package for a file
sink; Serilog does the same work, and the owner asked for NLog by name. `NLog.Targets.AtomicFile`
was measured by FlatCheck against NLog's plain file target before it was taken: two processes
writing 2000 lines each at once lost 33 to 48 of the 4000 lines with the plain target closed
between lines, 67 to 72 with it kept open, and none with the atomic target (FlatCheck's ADR
0070).

- Project: <https://nlog-project.org/>
- Licence: <https://github.com/NLog/NLog/blob/master/LICENSE.txt>

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
