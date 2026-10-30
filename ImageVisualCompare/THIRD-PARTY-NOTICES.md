# Third-party components

Image Visual Compare is distributed under the **GNU General Public License v3 or later** (see
[LICENSE](LICENSE)). This file lists the components written by others that the application
ships, each one's licence and why that licence is compatible with GPLv3. The About window of
`ImageVisualCompare.Wpf.exe` shows each component's name, licence, role, copyright line and
address (`src/ImageVisualCompare.Wpf/ThirdParty.cs`), so the notice travels with the executable,
in a folder of its own or in the Astro Darkroom suite's; this file, beside the source, holds the
licences' whole text (R-075, ADR 0043, ADR 0045).

What is written by hand and is **not** third-party code: the FITS reader, the display modes, the
zoom, the divider, every computation. Decoding every other format is the Windows Imaging
Component's, which is part of Windows and is not shipped (ADR 0008).

---

## NLog

**Role.** Writes the diagnostic log, the text files in the `log` folder beside the executable
that a bug report carries (ADR 0043). It decides nothing and computes nothing: the application
hands it finished lines, and without its configuration every call is a no-op. Two packages of
the same project come with it: `NLog`, and `NLog.Targets.AtomicFile`, its file target with the
operating system's atomic append, so two windows writing one log at the same time lose no line
(R-123).

**Licence.** BSD-3-Clause, for both packages, the version taken being **6.2.2**; both are built
from the same commit of the same repository, and neither carries a native file.

**Compatibility.** BSD-3-Clause is permissive and compatible with GPLv3; the Free Software
Foundation lists the modified (3-clause) BSD licence as GPL-compatible. Of its three conditions,
the source one does not arise (no NLog source is copied here); the binary one asks the copyright
notice, the conditions and the disclaimer to go with the distribution, and the About window shows
the notice, the licence's name and its address, while this section reproduces the whole text,
word for word from `LICENSE.txt` at the commit 6.2.2 was built from (`d9ebf82`); the third
forbids using the authors' names to endorse Image Visual Compare, which nothing here does.

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
was measured against NLog's plain file target by FlatCheck, on 2026-10-27, before either tool took
it: two processes writing 2000 lines each at once lost 33 to 48 of the 4000 lines with the plain
target closed between lines, 67 to 72 with it kept open, and none with the atomic target
(FlatCheck's ADR 0070).

- Project: <https://nlog-project.org/>
- Licence: <https://github.com/NLog/NLog/blob/master/LICENSE.txt>
