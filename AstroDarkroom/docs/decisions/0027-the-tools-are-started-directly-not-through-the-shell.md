# 0027. The tools are started directly, not through the shell

Date: 2026-11-14
Status: accepted 2026-11-14

## Context

The window started each tool with `Process.Start` and `UseShellExecute = true`; the design note
recorded it and gave no reason. The code review of 2026-11-13 (CR-003, `to measure`) read what
that means for a zip downloaded from the release page (ADR 0015) and taken out with Explorer,
which puts the mark of the web on every file it extracts: the shell applies Windows' check of
downloaded files to every marked executable, so after the user has accepted the question for
`AstroDarkroom.exe`, each of the six tools asks again at its first start; a user who answers
"Don't run" there got the launcher's warning box and an `ERROR` line for their own choice. The
READMEs say the question comes "at the first start", once.

Measured on 2026-11-14, on Windows 10 with .NET 8, in a throw-away program outside the
repository: a small executable, once plain and once with the mark Explorer writes
(`Zone.Identifier`, `ZoneId=3`), asked of Windows' security manager and then started.

```
mark read back: [ZoneTransfer] | ZoneId=3 |
plain: zone 0 (hr 0x0); shell-start policy 0x0 (hr 0x0; 0 allow, 1 ask, 3 refuse)
downloaded: zone 3 (hr 0x0); shell-start policy 0x1 (hr 0x1; 0 allow, 1 ask, 3 refuse)
plain: direct start -> process 25504, exited True code 0 after 305 ms; marker: ran with --lang=ro in <folder>\plain
downloaded: direct start -> process 28968, exited True code 0 after 326 ms; marker: ran with --lang=ro in <folder>\downloaded
missing file, direct start: Win32Exception 2: An error occurred trying to start process ... The system cannot find the file specified.
```

So Windows files the marked executable under the Internet zone, and its rule for starting such
a file through the shell (`URLACTION_SHELL_SHELLEXECUTE`) is "ask", per file; the same marked
file started directly (`UseShellExecute = false`) runs at once, with its arguments and its
working folder, and no question. A direct start of a file that is not there throws the
`Win32Exception` the window already catches. The question itself was not brought up on the
screen: it would have appeared on the owner's desktop.

Asked on 2026-11-14 with the direct start recommended, the owner chose it.

## Decision

The window starts a tool directly: `Launcher.StartInfo(plan)` in the core gives the
`ProcessStartInfo`, the plan's file, working folder and arguments with `UseShellExecute` false,
and `MainWindow.OpenApp` hands it to `Process.Start` (R-003). The link to Microsoft's download
of the Visual C++ runtime still opens through the shell, which a web address needs.

## Alternatives rejected

- **The shell kept, a cancelled start passed over in silence, the READMEs saying the question
  comes once per program.** Seven questions for one package the user already chose to run; the
  file is the launcher's neighbour in the same folder, its full path known, and nothing the
  shell adds (a file association, an elevation prompt) is needed to start it.
- **Removing the mark from the tools at the launcher's first start.** The launcher would write
  into its own folder, which may be read-only, and change files it does not own, to get what a
  direct start gives without touching them.

## Consequences

After the question for `AstroDarkroom.exe`, the tools of a downloaded zip start without another;
"Don't run" can no longer be answered for a tool, so that box of a failed start is not shown for
it. A tool started by hand from the folder, not from the launcher, is still asked about, once.
`R003_TheStartIsDirect_NotThroughTheShell` pins the start information; the window's use of it
was checked on 2026-11-14 by clicking the real window's Open button, off the screen, on a
stand-in executable marked as downloaded, which ran with `--lang=ro` and `--lang=en` in the
suite folder. A tool that one day needs elevation could not be started this way (Windows refuses
a direct start of such a file with error 740); none does. A direct start also lets the tool
inherit the launcher's inheritable handles, which a shell start did not (.NET 8 asks Windows for
it at every direct start): a launcher started by a double click has none as far as a reading
shows (.NET opens its files and pipes as not inheritable; not measured), but one started with
its output redirected to a file or a pipe keeps that file or pipe open in every tool it starts,
until the tool closes. The suite's wiki still says Windows
may ask "the first time you start them"; it is the owner's to reword.
