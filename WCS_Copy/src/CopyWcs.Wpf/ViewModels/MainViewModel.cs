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
// WcsCopyOutcome is shown as the core gives it. No sentence about the copy is composed here.

using static CopyWcs.Ui;

namespace CopyWcs.Wpf;

/// <summary>
/// The main window's state: the two files, the output choice, the two options, the facts
/// under the boxes, and the last outcome of the core (R-030 to R-033).
/// </summary>
public sealed class MainViewModel : ObservableObject
{
    private string _source = "";
    private string _dest = "";
    private string _output = "";
    private bool _writeInPlace = true;
    private bool _includeOptical = true;
    private bool _force;
    private string _sourceFacts = "";
    private string _destFacts = "";
    private WcsCopyOutcome? _outcome;

    /// <summary>The path of the plate-solved source; setting it reads the facts under its box.</summary>
    public string Source
    {
        get => _source;
        set
        {
            if (Set(ref _source, value))
            {
                SourceFacts = Facts(value, source: true);
                RaiseCanRun();
            }
        }
    }

    /// <summary>The path of the destination; setting it reads the facts under its box.</summary>
    public string Dest
    {
        get => _dest;
        set
        {
            if (Set(ref _dest, value))
            {
                DestFacts = Facts(value, source: false);
                RaiseCanRun();
            }
        }
    }

    /// <summary>The path of the new file, used when <see cref="WriteInPlace"/> is false.</summary>
    public string Output
    {
        get => _output;
        set
        {
            if (Set(ref _output, value))
            {
                RaiseCanRun();
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
            }
        }
    }

    /// <summary>The other half of <see cref="WriteInPlace"/>, for the second radio button and the output box.</summary>
    public bool WritesNewFile
    {
        get => !_writeInPlace;
        set => WriteInPlace = !value;
    }

    /// <summary>True to copy the optical metadata keys beside the WCS keys (the default).</summary>
    public bool IncludeOptical
    {
        get => _includeOptical;
        set => Set(ref _includeOptical, value);
    }

    /// <summary>True to copy even when the two images' dimensions differ.</summary>
    public bool Force
    {
        get => _force;
        set => Set(ref _force, value);
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

    /// <summary>True when both paths name existing files, so a preview can run (R-030).</summary>
    public bool CanPreview => File.Exists(_source) && File.Exists(_dest);

    /// <summary>True when a preview can run and, for a new file, its path is set (R-030).</summary>
    public bool CanCopy => CanPreview && (_writeInPlace || !string.IsNullOrWhiteSpace(_output));

    /// <summary>True once a preview or a copy has run; the outcome card is shown then.</summary>
    public bool HasOutcome => _outcome is not null;

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

    /// <summary>The core's closing lines of a copy (the count and the reminder); empty otherwise.</summary>
    public IReadOnlyList<string> Notes => _outcome is { Kind: WcsCopyKind.Copied } ? _outcome.Notes : [];

    public bool HasNotes => Notes.Count > 0;

    /// <summary>Runs the core's dry run on the boxes and shows its outcome (R-031).</summary>
    public void Preview() => Show(WcsCopy.Run(Request(dryRun: true)));

    /// <summary>Runs the copy on the boxes, shows its outcome and reads the destination's facts again (R-032).</summary>
    public void Copy()
    {
        Show(WcsCopy.Run(Request(dryRun: false)));
        DestFacts = Facts(_dest, source: false);
    }

    /// <summary>The request the boxes and the options describe.</summary>
    /// <param name="dryRun">True to list the cards and write nothing.</param>
    /// <returns>The request for <see cref="WcsCopy.Run"/>.</returns>
    private WcsCopyRequest Request(bool dryRun)
        => new(_source, _dest, _writeInPlace ? null : _output, _includeOptical, _force, dryRun);

    /// <summary>Keeps an outcome and raises every property the outcome card binds.</summary>
    /// <param name="outcome">The core's outcome; not null.</param>
    private void Show(WcsCopyOutcome outcome)
    {
        _outcome = outcome;
        Cards = outcome.Cards.Select(c => c.TrimEnd()).ToList();
        foreach (string name in new[]
                 {
                     nameof(HasOutcome), nameof(Error), nameof(HasError), nameof(Warnings), nameof(HasWarnings),
                     nameof(Heading), nameof(HasHeading), nameof(Cards), nameof(HasCards), nameof(Notes), nameof(HasNotes),
                 })
        {
            Raise(name);
        }
    }

    /// <summary>Raises the two enabling properties after a path or the output choice changed.</summary>
    private void RaiseCanRun()
    {
        Raise(nameof(CanPreview));
        Raise(nameof(CanCopy));
    }

    /// <summary>
    /// The two facts of a header, worded for the box they sit under: the size, and whether
    /// <c>CTYPE1</c> is present. The only reading of a file the window does itself (ADR 0007).
    /// </summary>
    /// <param name="path">The file's path; empty, missing or unreadable gives an empty string.</param>
    /// <param name="source">True for the source's wording, false for the destination's.</param>
    /// <returns>One line, or an empty string when the file cannot be read: the run will say why.</returns>
    internal static string Facts(string path, bool source)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return "";
        }

        FitsHeader header;
        try
        {
            header = FitsHeader.ReadPrimary(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        {
            return "";
        }

        int width = header.GetInt("NAXIS1");
        int height = header.GetInt("NAXIS2");
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
