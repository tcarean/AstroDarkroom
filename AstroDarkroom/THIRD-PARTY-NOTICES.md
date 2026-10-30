# Third-party components

Astro Darkroom is distributed under the **GNU General Public License v3 or later** (see
[LICENSE](LICENSE)). This file lists the third-party code the launcher links, each component's
licence and why that licence is compatible with GPLv3. A package added without an entry here is
a package whose compatibility nobody has checked: `ThirdPartyTests` binds this file, the list in
`src/AstroDarkroom.Core/ThirdParty.cs` (what "Despre" shows) and the packages of the projects
under `src/` (R-015, ADR 0013).

The tools the suite bundles are not third-party code: they are the photo repository's own
projects, under the same licence, each with its own notices.

---

## NLog

**Role.** Writes the diagnostic log, the text files in the `log` folder beside
`AstroDarkroom.exe` that a bug report carries (R-011 to R-014, ADR 0012). It decides nothing and
computes nothing: the core and the window hand it finished lines, and without its configuration
every call is a no-op. Two packages of the same project come with it: `NLog`, and
`NLog.Targets.AtomicFile`, its file target with the operating system's atomic append, so two
launchers writing one log at the same time lose no line (R-014). FlatCheck's and LightsCheck's
windows ship the same two DLLs into the suite folder, at the same version.

**Licence.** BSD-3-Clause, for both packages, the version taken being **6.2.2**; both are built
from the same commit of the same repository, and neither carries a native file.

**Compatibility.** BSD-3-Clause is permissive and compatible with GPLv3; the Free Software
Foundation lists the modified (3-clause) BSD licence as GPL-compatible. Of its three conditions,
the source one does not arise (no NLog source is copied here); the binary one asks the copyright
notice, the conditions and the disclaimer to go with the distribution, and the window's "Despre"
shows the notice, the licence's name and the project's address (R-015), while this section
reproduces the whole text, word for word from `LICENSE.txt` at the commit 6.2.2 was built from
(`d9ebf82`), as FlatCheck read it for the same packages; the third forbids using the authors'
names to endorse Astro Darkroom, which nothing here does.

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

**The alternatives evaluated.** FlatCheck's, for the same log (its ADR 0070):
`Microsoft.Extensions.Logging` needs a second package for a file sink; Serilog does the same
work, and the owner asked for NLog by name; NLog's plain file target lost lines between two
processes writing one file, where `NLog.Targets.AtomicFile` lost none.

- Project: <https://nlog-project.org/>
- Licence: <https://github.com/NLog/NLog/blob/master/LICENSE.txt>
