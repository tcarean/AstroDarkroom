// SPDX-License-Identifier: GPL-3.0-or-later
//
// Copyright (C) 2026 Tudor Carean
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

// MainViewModel.cs - what the window binds. Three things and no more (ADR 0007): the boxes and
// the options become a WcsCopyRequest; two facts are read from each header before any run; the
// WcsCopyOutcome is shown as the core gives it. No sentence about the copy is composed here; the
// band's two lines are the core's banner and copyright, passed through (ADR 0008).

using NLog;
using static CopyWcs.Ui;

namespace CopyWcs.Wpf;

/// <summary>What a look at a box's file found (R-052): whether the file exists, and the fact line under the box.</summary>
/// <param name="Exists">True when the path names an existing file.</param>
/// <param name="Facts">The line of <see cref="MainViewModel.Facts"/>; empty for a missing file, or one whose facts cannot be read.</param>
internal readonly record struct FileLook(bool Exists, string Facts);

/// <summary>
/// The main window's state: the band's two lines, the two files, the output choice, the two
/// options, the facts under the boxes, and the last outcome of the core (R-030 to R-033, R-038).
/// </summary>
public sealed class MainViewModel : ObservableObject
{
    /// <summary>The view-model's line in the diagnostic log: a header whose facts could not be read (R-043); a run logs itself, in the core.</summary>
    private static readonly Logger Log = LogManager.GetLogger(Loggers.Window);

    private string _source = "";
    private string _dest = "";
    private string _output = "";

    /// <summary>The source box's path made whole when its text was set (R-051): what the facts and the run read.</summary>
    private string _sourcePath = "";

    /// <summary>The destination box's path made whole when its text was set (R-051): what the facts and the run read, and the folder of a new file typed relative.</summary>
    private string _destPath = "";

    /// <summary>The number of the source box's latest look: a look that ends under another number was overtaken by a newer text, and is dropped (R-052).</summary>
    private int _sourceTurn;

    /// <summary>The number of the destination box's latest look, as <see cref="_sourceTurn"/>.</summary>
    private int _destTurn;

    /// <summary>The looks at the boxes' files that may still be in flight, which <see cref="FactsRead"/> waits for; the ones ended are taken out as a new one starts.</summary>
    private readonly List<Task> _looks = [];

    private bool _writeInPlace = true;
    private bool _includeOptical = true;
    private bool _force;
    private bool _sourceExists;
    private bool _destExists;
    private bool _running;

    /// <summary>True while the run in flight is a Copy, false for a Preview: the card's in-progress line (R-053).</summary>
    private bool _runningCopy;

    private string _sourceFacts = "";
    private string _destFacts = "";
    private WcsCopyOutcome? _outcome;

    // CA1822: XAML binds these two, and a binding needs an instance member; a static would not bind.
#pragma warning disable CA1822
    /// <summary>The band's first line: <c>copywcs</c> and the version, the console's line 1 (R-001, R-038).</summary>
    public string Banner => VersionInfo.Banner;

    /// <summary>The band's second line: the copyright, the console's line 2 (R-001, R-038).</summary>
    public string Copyright => LicenseInfo.Copyright;
#pragma warning restore CA1822

    /// <summary>The source box's text, as typed; setting it makes its path whole (R-051) and starts a look at its file off the window's thread (R-052).</summary>
    public string Source
    {
        get => _source;
        set
        {
            if (Set(ref _source, value))
            {
                _sourcePath = Whole(Clean(value), folder: null);
                LookAt(source: true, fresh: true);
                Clear();
            }
        }
    }

    /// <summary>The destination box's text, as typed; setting it makes its path whole (R-051) and starts a look at its file off the window's thread (R-052).</summary>
    public string Dest
    {
        get => _dest;
        set
        {
            if (Set(ref _dest, value))
            {
                _destPath = Whole(Clean(value), folder: null);
                LookAt(source: false, fresh: true);
                Clear();
            }
        }
    }

    /// <summary>The new file's box, as typed, used when <see cref="WriteInPlace"/> is false; a relative path is taken beside the destination (R-051).</summary>
    public string Output
    {
        get => _output;
        set
        {
            if (Set(ref _output, value))
            {
                RaiseCanRun();
                Clear();
            }
        }
    }

    /// <summary>True to rewrite the destination in place (the default); false to write <see cref="Output"/>.</summary>
    public bool WriteInPlace
    {
        get => _writeInPlace;
        set
        {
            if (Set(ref _writeInPlace, value))
            {
                Raise(nameof(WritesNewFile));
                RaiseCanRun();
                Clear();
            }
        }
    }

    /// <summary>The other half of <see cref="WriteInPlace"/>, for the second radio button and the output box.</summary>
    public bool WritesNewFile
    {
        get => !_writeInPlace;
        set => WriteInPlace = !value;
    }

    /// <summary>True to copy the optical metadata keys beside the WCS keys (the default); a change empties the card (R-053).</summary>
    public bool IncludeOptical
    {
        get => _includeOptical;
        set
        {
            if (Set(ref _includeOptical, value))
            {
                Clear();
            }
        }
    }

    /// <summary>True to copy even when the two images' dimensions differ or the destination's own plate solve is mirrored (R-005, R-017); a change empties the card (R-053).</summary>
    public bool Force
    {
        get => _force;
        set
        {
            if (Set(ref _force, value))
            {
                Clear();
            }
        }
    }

    /// <summary>The source's size and whether it is plate-solved; empty when the file cannot be read.</summary>
    public string SourceFacts
    {
        get => _sourceFacts;
        private set => Set(ref _sourceFacts, value);
    }

    /// <summary>The destination's size and whether it is already plate-solved; empty when the file cannot be read.</summary>
    public string DestFacts
    {
        get => _destFacts;
        private set => Set(ref _destFacts, value);
    }

    /// <summary>True when both paths name existing files and no run is in flight, so a preview can run (R-030).</summary>
    public bool CanPreview => !_running && _sourceExists && _destExists;

    /// <summary>True when a preview can run and, for a new file, its path is set (R-030).</summary>
    public bool CanCopy => CanPreview && (_writeInPlace || !string.IsNullOrWhiteSpace(_output));

    /// <summary>True while a Preview or a Copy runs; the window holds a close until it is false again (R-050).</summary>
    public bool IsRunning => _running;

    /// <summary>
    /// The copy run the two commands call, off the window's thread: <see cref="WcsCopy.Run"/>,
    /// which a test replaces to hold a run in flight (R-050).
    /// </summary>
    internal Func<WcsCopyRequest, WcsCopyOutcome> RunCore { get; set; } = WcsCopy.Run;

    /// <summary>
    /// The look at a box's file: <see cref="Look"/>, given the whole path and true for the
    /// source's box, which a test replaces to hold a look in flight (R-052).
    /// </summary>
    internal Func<string, bool, FileLook> LookCore { get; set; } = Look;

    /// <summary>Completes once every look in flight at a box's file has ended and its facts are shown, or dropped for a text that changed since (R-052).</summary>
    internal Task FactsRead
    {
        get
        {
            lock (_looks)
            {
                return Task.WhenAll([.. _looks]);
            }
        }
    }

    /// <summary>True once a preview or a copy has run; the outcome card is shown then.</summary>
    public bool HasOutcome => _outcome is not null;

    /// <summary>True while the card has something to show: an outcome, or a run in flight (R-053).</summary>
    public bool HasCard => HasOutcome || _running;

    /// <summary>The card's line while a run is in flight, the window's own pair for the run's kind (R-053); empty otherwise.</summary>
    public string RunningLine => !_running ? ""
        : _runningCopy ? T("Copy in progress...", "Copiere in curs...")
        : T("Preview in progress...", "Previzualizare in curs...");

    /// <summary>The core's one line of a refusal; null when the last run was not refused.</summary>
    public string? Error => _outcome?.Error;

    public bool HasError => Error is not null;

    /// <summary>The heading over the error line; the window's one word, a pair.</summary>
    // CA1822: XAML binds it, and a binding needs an instance member; a static would not bind.
#pragma warning disable CA1822
    public string RefusedHeading => T("Refused", "Refuzat");
#pragma warning restore CA1822

    /// <summary>The core's warning lines, in order; empty when there was none.</summary>
    public IReadOnlyList<string> Warnings => _outcome?.Warnings ?? [];

    public bool HasWarnings => Warnings.Count > 0;

    /// <summary>The core's heading of a preview (the count of keys); empty after a copy or a refusal.</summary>
    public string Heading => _outcome is { Kind: WcsCopyKind.Previewed } ? string.Join(" ", _outcome.Notes) : "";

    public bool HasHeading => Heading.Length > 0;

    /// <summary>The cards selected, each trimmed of its padding, in the source's order; empty when the run was refused.</summary>
    public IReadOnlyList<string> Cards { get; private set; } = [];

    public bool HasCards => Cards.Count > 0;

    /// <summary>
    /// The core's lines under the cards, in the console's order: the line naming the
    /// destination's old plate-solve keys dropped, or a preview would drop (R-016), when there
    /// is one, then a copy's closing lines (the count and the reminder); empty after a refusal.
    /// </summary>
    public IReadOnlyList<string> Notes => _outcome switch
    {
        { Kind: WcsCopyKind.Copied, Dropped: { } dropped } o => [dropped, .. o.Notes],
        { Kind: WcsCopyKind.Copied } o => o.Notes,
        { Kind: WcsCopyKind.Previewed, Dropped: { } dropped } => [dropped],
        _ => [],
    };

    public bool HasNotes => Notes.Count > 0;

    /// <summary>Runs the core's dry run on the boxes and shows its outcome (R-031).</summary>
    /// <returns>Completes once the outcome is shown.</returns>
    public Task Preview() => RunAsync(dryRun: true);

    /// <summary>Runs the copy on the boxes, shows its outcome and looks at the destination again (R-032, R-052).</summary>
    /// <returns>Completes once the outcome is shown and the destination's facts are read.</returns>
    public async Task Copy()
    {
        await RunAsync(dryRun: false).ConfigureAwait(true);
        LookAt(source: false, fresh: false);
        await FactsRead.ConfigureAwait(true);
    }

    /// <summary>
    /// Runs the core on a thread-pool thread, the commands off meanwhile (R-030): an in-place
    /// copy streams the whole data area through a temp file, seconds on a large stack, and the
    /// window must stay live. The request is built here, on the caller's thread; the card is
    /// emptied and shows the run's in-progress line (R-053); the run starts once every look at
    /// the boxes' files in flight has ended (R-052), and the outcome is shown back on the
    /// caller's thread; <see cref="IsRunning"/> is raised as the run starts and after its outcome
    /// is shown (R-050).
    /// </summary>
    /// <param name="dryRun">True to list the cards and write nothing.</param>
    /// <returns>Completes once the outcome is shown.</returns>
    private async Task RunAsync(bool dryRun)
    {
        WcsCopyRequest request = Request(dryRun);
        Func<WcsCopyRequest, WcsCopyOutcome> run = RunCore;
        _running = true;
        _runningCopy = !dryRun;
        Clear();
        RaiseRunning();
        try
        {
            // A look logs the reader's read line: waiting for the looks in flight keeps the lines
            // between the run's start and done lines the run's own (R-043).
            await FactsRead.ConfigureAwait(true);
            Show(await Task.Run(() => run(request)).ConfigureAwait(true));
        }
        finally
        {
            _running = false;
            RaiseRunning();
        }
    }

    /// <summary>Raises what follows the running state: the two commands, <see cref="IsRunning"/>, the card's visibility and its in-progress line.</summary>
    private void RaiseRunning()
    {
        RaiseCanRun();
        Raise(nameof(IsRunning));
        Raise(nameof(HasCard));
        Raise(nameof(RunningLine));
    }

    /// <summary>
    /// The request the boxes and the options describe, each path whole (R-051): the source and
    /// the destination as they were made whole when their texts were set, the new file made
    /// whole against the destination's folder, so a bare name goes beside the destination.
    /// </summary>
    /// <param name="dryRun">True to list the cards and write nothing.</param>
    /// <returns>The request for <see cref="WcsCopy.Run"/>.</returns>
    private WcsCopyRequest Request(bool dryRun)
        => new(_sourcePath, _destPath, _writeInPlace ? null : Whole(Clean(_output), Path.GetDirectoryName(_destPath)),
            _includeOptical, _force, dryRun);

    /// <summary>Keeps an outcome and raises every property the outcome card binds.</summary>
    /// <param name="outcome">The core's outcome; not null.</param>
    private void Show(WcsCopyOutcome outcome)
    {
        _outcome = outcome;
        Cards = outcome.Cards.Select(c => c.TrimEnd(' ')).ToList();
        RaiseOutcome();
    }

    /// <summary>
    /// Empties the card of its outcome (R-053): a box's text, the in-place or new-file choice or
    /// an option changed, so it no longer says what a run would do, or a run starts. Nothing is
    /// raised when there is no outcome to take away.
    /// </summary>
    private void Clear()
    {
        if (_outcome is null)
        {
            return;
        }

        _outcome = null;
        Cards = [];
        RaiseOutcome();
    }

    /// <summary>Raises every property the outcome card binds, after an outcome is kept or taken away.</summary>
    private void RaiseOutcome()
    {
        foreach (string name in new[]
                 {
                     nameof(HasOutcome), nameof(HasCard), nameof(Error), nameof(HasError), nameof(Warnings), nameof(HasWarnings),
                     nameof(Heading), nameof(HasHeading), nameof(Cards), nameof(HasCards), nameof(Notes), nameof(HasNotes),
                 })
        {
            Raise(name);
        }
    }

    /// <summary>Raises the two enabling properties after a path, the output choice or the running state changed.</summary>
    private void RaiseCanRun()
    {
        Raise(nameof(CanPreview));
        Raise(nameof(CanCopy));
    }

    /// <summary>
    /// A path as typed or pasted, without the whitespace around it and then without one pair of
    /// double quotes around what is left (R-030): a box is typed into, a name with a space in
    /// front is not the file meant, and File Explorer's "Copy as path" puts the path in quotes,
    /// which no Windows file name holds.
    /// </summary>
    /// <param name="path">The box's text; empty is allowed.</param>
    /// <returns>The path trimmed at both ends, its one pair of surrounding quotes taken off.</returns>
    private static string Clean(string path)
    {
        string trimmed = path.Trim();
        return trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"' ? trimmed[1..^1] : trimmed;
    }

    /// <summary>
    /// A box's cleaned path made whole (R-051), once, so that a working folder that moves later
    /// changes nothing it names: against the working folder, or against a folder given.
    /// </summary>
    /// <param name="path">The path, cleaned (<see cref="Clean"/>); empty stays empty.</param>
    /// <param name="folder">The whole folder a relative path is taken from; null or empty for the working folder.</param>
    /// <returns>The whole path; the path as it was when the system cannot make one of it (a NUL inside, a folder that is itself not whole), so the run refuses it in its own words.</returns>
    private static string Whole(string path, string? folder)
    {
        if (path.Length == 0)
        {
            return path;
        }

        try
        {
            return string.IsNullOrEmpty(folder) ? Path.GetFullPath(path) : Path.GetFullPath(path, folder);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException)
        {
            return path;
        }
    }

    /// <summary>
    /// Starts a look at a box's file off the window's thread (R-052), through
    /// <see cref="LookCore"/>, numbered as the box's latest: a look of a new text first empties
    /// the box's fact line and turns its existence off, so the commands wait for it; a look again
    /// keeps both as they are until it ends. What it finds is shown on the caller's thread, unless
    /// a newer look of the same box was started meanwhile.
    /// </summary>
    /// <param name="source">True for the source's box, false for the destination's.</param>
    /// <param name="fresh">True when the box's text changed: its old facts no longer apply.</param>
    private void LookAt(bool source, bool fresh)
    {
        int turn = source ? ++_sourceTurn : ++_destTurn;
        string path = source ? _sourcePath : _destPath;
        if (fresh)
        {
            Found(source, new FileLook(false, ""));
        }

        Func<string, bool, FileLook> look = LookCore;
        Task shown = ShowWhenFound(source, turn, Task.Run(() => look(path, source)));
        lock (_looks)
        {
            _looks.RemoveAll(t => t.IsCompleted);
            _looks.Add(shown);
        }
    }

    /// <summary>Shows what a look found once it ends, on the caller's thread, unless its box has a newer look by then (R-052).</summary>
    /// <param name="source">True for the source's box, false for the destination's.</param>
    /// <param name="turn">The look's number, compared with the box's latest when it ends.</param>
    /// <param name="looking">The look, running off the window's thread.</param>
    /// <returns>Completes once the look is shown or dropped.</returns>
    private async Task ShowWhenFound(bool source, int turn, Task<FileLook> looking)
    {
        FileLook found = await looking.ConfigureAwait(true);
        if (turn == (source ? _sourceTurn : _destTurn))
        {
            Found(source, found);
        }
    }

    /// <summary>Puts what a look found under its box: the existence the commands follow, and the fact line.</summary>
    /// <param name="source">True for the source's box, false for the destination's.</param>
    /// <param name="found">What the look found; an empty look for a text whose look has not ended.</param>
    private void Found(bool source, FileLook found)
    {
        if (source)
        {
            _sourceExists = found.Exists;
            SourceFacts = found.Facts;
        }
        else
        {
            _destExists = found.Exists;
            DestFacts = found.Facts;
        }

        RaiseCanRun();
    }

    /// <summary>A look at a box's file (R-052): whether it exists, and if it does, the facts of its header.</summary>
    /// <param name="path">The box's whole path; empty is allowed, and names no file.</param>
    /// <param name="source">True for the source's box, false for the destination's.</param>
    /// <returns>What the look found.</returns>
    internal static FileLook Look(string path, bool source)
        => File.Exists(path) ? new FileLook(true, Facts(path, source)) : new FileLook(false, "");

    /// <summary>
    /// The two facts of a header, worded for the box they sit under: the size, and whether
    /// <c>CTYPE1</c> is present. The only reading of a file the window does itself (ADR 0007).
    /// </summary>
    /// <param name="path">The file's path; one that cannot be read, or whose header has no <c>NAXIS1</c> or <c>NAXIS2</c>, gives an empty string.</param>
    /// <param name="source">True for the source's wording, false for the destination's.</param>
    /// <returns>One line, or an empty string when there is no size to show: the run will say why.</returns>
    internal static string Facts(string path, bool source)
    {
        FitsHeader header;
        try
        {
            header = FitsHeader.ReadPrimary(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        {
            Log.Debug(ex, "no facts under the " + (source ? "source" : "destination") + " box: the header of " + path + " could not be read");
            return "";
        }

        // -1 is the reader's "no such key": a header without an image size has no fact to show.
        int width = header.GetInt("NAXIS1");
        int height = header.GetInt("NAXIS2");
        if (width < 0 || height < 0)
        {
            Log.Debug("no facts under the " + (source ? "source" : "destination") + " box: " + path + " has no NAXIS1 or NAXIS2");
            return "";
        }

        bool solved = header.Contains("CTYPE1");
        if (source)
        {
            return solved
                ? T($"Source: {width} x {height}, plate-solved", $"Sursa: {width} x {height}, plate-solved")
                : T($"Source: {width} x {height}, not plate-solved", $"Sursa: {width} x {height}, fara plate solve");
        }

        return solved
            ? T($"Destination: {width} x {height}, already plate-solved", $"Destinatia: {width} x {height}, deja plate-solved")
            : T($"Destination: {width} x {height}", $"Destinatia: {width} x {height}");
    }
}
