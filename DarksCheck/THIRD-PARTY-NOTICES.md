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
