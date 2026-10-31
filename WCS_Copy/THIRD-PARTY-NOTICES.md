# Third-party notices

The components written by others that CopyWcs links, the licence each is taken under, and why
that licence is compatible with the GPLv3 CopyWcs is under (`LICENSE`). The FITS header is read
and written by hand; nothing else of CopyWcs comes from a package. The About window of
`CopyWcs.Wpf.exe` shows each component's name, role, licence, copyright line and link
(`src/CopyWcs.Core/ThirdParty.cs`), so the notice travels with the executable, alone or in the
Astro Darkroom suite's folder; this file, beside the source, holds the licences' full text (ADR
0009).

---

## NLog

**Role.** Writes the diagnostic log, the text files in the `log` folder beside each executable
that a bug report carries (ADR 0009). It decides nothing and computes nothing: the core and the
two applications hand it finished lines, and without its configuration every call is a no-op.
Two packages of the same project come with it: `NLog`, and `NLog.Targets.AtomicFile`, its file
target with the operating system's atomic append, so two windows writing one log at the same
time lose no line (R-045).

**Licence.** BSD-3-Clause, for both packages, the version taken being **6.2.2**, the one
FlatCheck takes; both are built from the same commit of the same repository, and neither carries
a native file.

**Compatibility.** BSD-3-Clause is permissive and compatible with GPLv3; the Free Software
Foundation lists the modified (3-clause) BSD licence as GPL-compatible. Of its three conditions,
the source one does not arise (no NLog source is copied here); the binary one asks the copyright
notice, the conditions and the disclaimer to go with the distribution, and the window's About
shows the notice, the licence's name and its link, while this section reproduces the whole text,
word for word from `LICENSE.txt` at the commit 6.2.2 was built from (`d9ebf82`); the third
forbids using the authors' names to endorse CopyWcs, which nothing here does.

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
