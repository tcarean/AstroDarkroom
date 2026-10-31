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

namespace ImageVisualCompare.Core;

/// <summary>
/// Every text the application shows, in English and in Romanian (R-101, ADR 0038). Each member
/// gives both wordings in one call, English first, so that a text cannot exist in one language
/// only and the two are read side by side. The Romanian keeps the words the user documentation
/// used while it was in Romanian (ADR 0041): the divider is "linia", the canvas "pânza", and the
/// user is "tu". Numbers are put in as they are, the same in both languages.
/// </summary>
public sealed class UiText
{
    private readonly UiLanguage _language;

    /// <summary>Builds the table of one language.</summary>
    /// <param name="language">The language its members answer in.</param>
    private UiText(UiLanguage language) => _language = language;

    public static UiText English { get; } = new(UiLanguage.English);

    public static UiText Romanian { get; } = new(UiLanguage.Romanian);

    /// <summary>
    /// The table in use: English until the start-up chooses the language (R-100), once, before
    /// the first window. Every place that writes a text reads it from here when it writes it.
    /// </summary>
    public static UiText Current { get; set; } = English;

    /// <summary>
    /// The title of the message about a <c>--lang</c> that was not understood (R-102). English
    /// only, like the message: it announces that the interface is English.
    /// </summary>
    public static string UnknownLanguageTitle => "Unknown language";

    // The toolbar, right of the pair's names (R-060).
    public string Navigation => Pick("Navigation", "Navigare");

    public string Fit => Pick("Fit", "Încadrează");

    public string Open => Pick("Open…", "Deschide…");

    public string About => Pick("About", "Despre");

    // The two buttons beside the pair's names, each changing that image alone (R-065).
    public string ChangeA => Pick("Change A…", "Schimbă A…");

    public string ChangeB => Pick("Change B…", "Schimbă B…");

    /// <summary>Names the button that changes one image alone (R-065).</summary>
    /// <param name="side">The image the button changes.</param>
    /// <returns><see cref="ChangeA"/> or <see cref="ChangeB"/>.</returns>
    public string ChangeImage(ImageSide side) => side == ImageSide.A ? ChangeA : ChangeB;

    /// <summary>Titles the open dialog of one image (R-001, R-065).</summary>
    /// <param name="side">The image the dialog asks for.</param>
    /// <returns><see cref="SelectImageA"/> or <see cref="SelectImageB"/>.</returns>
    public string SelectImage(ImageSide side) => side == ImageSide.A ? SelectImageA : SelectImageB;

    // The two display-mode toggles (R-114), and the words the status bar's pair line ends with
    // in those modes.
    public string Histogram => Pick("Histogram", "Histogramă");

    public string FalseColours => Pick("False colours", "Culori false");

    /// <summary>
    /// Names a display mode as the toolbar and the status bar do (R-114).
    /// </summary>
    /// <param name="mode">The mode.</param>
    /// <returns>
    /// <see cref="Histogram"/> or <see cref="FalseColours"/>; null for
    /// <see cref="DisplayMode.Linear"/>, which the status bar does not name.
    /// </returns>
    public string? ModeName(DisplayMode mode) => mode switch
    {
        DisplayMode.Histogram => Histogram,
        DisplayMode.FalseColours => FalseColours,
        _ => null,
    };

    // The status bar's hint (R-054): what each gesture does, shortest first to go when trimmed.
    public string Hint => Pick(
        "wheel to zoom · drag the line to move it · drag to pan · ←/→ nudge · Ctrl+O open · H histogram · F false colours",
        "rotița: zoom · trage linia: o muți · trage în afara liniei: pan · ←/→: mută linia fin · Ctrl+O: deschide · H: histogramă · F: culori false");

    // The dialog titles of a pair that failed to load (R-005) and of the padding question (R-003).
    public string CannotCompareTitle => Pick("Cannot compare these files", "Fișierele nu pot fi comparate");

    public string DifferentSizesTitle => Pick("Images have different sizes", "Imaginile au dimensiuni diferite");

    // The loading overlay (R-006).
    public string Opening => Pick("Opening…", "Se deschide…");

    public string AlmostThere => Pick("Almost there…", "Aproape gata…");

    // The buttons of MessageDialog (R-062).
    public string Ok => Pick("OK", "OK");

    public string Yes => Pick("Yes", "Da");

    public string No => Pick("No", "Nu");

    // The titles of the two open dialogs (R-001): which image is picked, and on which side.
    public string SelectImageA => Pick(
        "Select image A  —  shown left of the divider",
        "Alege imaginea A  —  afișată în stânga liniei");

    public string SelectImageB => Pick(
        "Select image B  —  shown right of the divider",
        "Alege imaginea B  —  afișată în dreapta liniei");

    // The open dialogs' filter (R-001). The patterns are the same; only the names are worded.
    public string FileFilter =>
        Pick("Images", "Imagini") + " (*.png;*.tif;*.tiff;*.fit;*.fits;*.fts)|*.png;*.tif;*.tiff;*.fit;*.fits;*.fts|" +
        "PNG (*.png)|*.png|" +
        "TIFF (*.tif;*.tiff)|*.tif;*.tiff|" +
        "FITS (*.fit;*.fits;*.fts)|*.fit;*.fits;*.fts|" +
        Pick("All files", "Toate fișierele") + " (*.*)|*.*";

    // The About window's words (R-072, R-075, ADR 0045): its title, the tagline under the name,
    // the two cards' headings, the notice of GPLv3 section 5(d) as one paragraph, the link to the
    // licence's text, the paragraph over the components and Close. The licence itself stays the
    // English LICENSE file; the components' names, licences, copyright lines and addresses stay as
    // their authors wrote them, in the application's list, and only their roles are worded here.
    public string AboutTitle => Pick("About Image Visual Compare", "Despre Image Visual Compare");

    public string AboutTagline => Pick(
        "Before/after comparison of two versions of the same astrophotography image.",
        "Comparație înainte/după a două versiuni ale aceleiași imagini de astrofotografie.");

    public string AboutLicenceHeading => Pick("LICENCE", "LICENȚĂ");

    public string AboutWarranty => Pick(
        "This program comes with ABSOLUTELY NO WARRANTY. This is free software, and you are welcome to redistribute it "
        + "under the terms of the GNU General Public License, version 3 or later. The full licence text is in the LICENSE "
        + "file next to this program.",
        "Acest program vine fără ABSOLUT NICIO GARANȚIE. Este software liber și îl poți redistribui în condițiile "
        + "Licenței Publice Generale GNU, versiunea 3 sau ulterioară. Textul complet al licenței e în fișierul LICENSE "
        + "de lângă program.");

    public string AboutLicenceLink => Pick(
        "The full text of the GNU General Public License v3",
        "Textul complet al GNU General Public License v3");

    public string AboutThirdPartyHeading => Pick("THIRD-PARTY COMPONENTS", "COMPONENTE TERȚE");

    public string AboutThirdParty => Pick(
        "The program links components written by others, each compatible with GPLv3; the FITS reader, the display modes "
        + "and every computation are written by hand. Each licence's full text, and why it is compatible, are in "
        + "THIRD-PARTY-NOTICES.md, beside the source code.",
        "Programul leagă componente scrise de alții, fiecare compatibilă cu GPLv3; cititorul FITS, modurile de afișare "
        + "și fiecare calcul sunt scrise de mână. Textul complet al fiecărei licențe și motivul pentru care e compatibilă "
        + "sunt în THIRD-PARTY-NOTICES.md, lângă codul sursă.");

    // The role, in that card, of the component that writes the diagnostic log (R-075).
    public string DiagnosticLogRole => Pick(
        "writing the diagnostic log beside the program (the log folder)",
        "scrierea jurnalului de diagnostic lângă program (folderul log)");

    public string Close => Pick("Close", "Închide");

    // The FITS reader's refusals (R-019). Keywords and values stay as the file has them.
    public string FitsNoImage => Pick(
        "FITS: no image found in the file.",
        "FITS: fișierul nu conține nicio imagine.");

    public string FitsTileCompressed => Pick(
        "FITS: a tile-compressed image is not read.",
        "FITS: o imagine comprimată pe dale (tile-compressed) nu e suportată.");

    public string FitsInvalidHeader => Pick(
        "FITS: the header is not valid.",
        "FITS: antetul nu este valid.");

    public string FitsHeaderEndsEarly => Pick(
        "FITS: the header ends before its END card.",
        "FITS: antetul se termină înainte de cardul END.");

    public string FitsDataEndsEarly => Pick(
        "FITS: the file ends before the image data does.",
        "FITS: datele imaginii sunt incomplete: fișierul se termină prea devreme.");

    /// <summary>Gives the table of a language.</summary>
    /// <param name="language">The language.</param>
    /// <returns><see cref="Romanian"/> for Romanian, <see cref="English"/> otherwise.</returns>
    public static UiText For(UiLanguage language) => language == UiLanguage.Romanian ? Romanian : English;

    /// <summary>
    /// Words the message about a <c>--lang</c> that was not understood (R-102), in English: the
    /// interface it announces is English.
    /// </summary>
    /// <param name="value">The value given, as typed; empty when <c>--lang</c> had none.</param>
    /// <returns>Two sentences and a blank line between them.</returns>
    public static string UnknownLanguage(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return string.Join(
            Environment.NewLine,
            value.Length == 0
                ? "--lang was given without a language, so the interface is in English."
                : $"The language \"{value}\" is not one this program has, so the interface is in English.",
            string.Empty,
            "Use --lang=en for English or --lang=ro for Romanian.");
    }

    /// <summary>Words the overlay's line while one image decodes (R-006).</summary>
    /// <param name="fileName">The file being decoded, without its folder.</param>
    /// <returns>The line, ending with an ellipsis.</returns>
    public string Decoding(string fileName) => Pick($"Decoding {fileName}…", $"Se decodează {fileName}…");

    /// <summary>Words the canvas of a padded pair, first on the status bar (R-050).</summary>
    /// <param name="canvas">The canvas's dimensions, already written as "W × H".</param>
    /// <returns>The canvas, said to be one.</returns>
    public string PaddedCanvas(string canvas) => Pick($"{canvas} padded", $"pânză {canvas}");

    /// <summary>Words the memory readout of the status bar (R-052).</summary>
    /// <param name="managed">The managed heap, already written as "N MB".</param>
    /// <param name="process">The process's private bytes, already written as "N MB".</param>
    /// <returns>The readout.</returns>
    public string Memory(string managed, string process) =>
        Pick($"managed {managed}, process {process}", $"memorie .NET {managed}, proces {process}");

    /// <summary>
    /// Words the question asked before a mismatched pair is padded (R-003), in short lines: the
    /// dialog wraps anything longer, and a resolution split across two lines is exactly the part
    /// being read.
    /// </summary>
    /// <param name="beforeName">Image A's file name.</param>
    /// <param name="beforeSize">Image A's dimensions, written as "W × H".</param>
    /// <param name="afterName">Image B's file name.</param>
    /// <param name="afterSize">Image B's dimensions, written as "W × H".</param>
    /// <param name="canvas">The canvas's dimensions, written as "W × H".</param>
    /// <returns>The question, its lines joined with the platform's line break.</returns>
    public string PadQuestion(string beforeName, string beforeSize, string afterName, string afterSize, string canvas) =>
        string.Join(
            Environment.NewLine,
            Pick("The two images have different dimensions.", "Cele două imagini au dimensiuni diferite."),
            $"A: {beforeName} — {beforeSize}",
            $"B: {afterName} — {afterSize}",
            string.Empty,
            Pick($"Both can be padded with black to {canvas},", $"Amândouă pot fi completate cu negru până la {canvas},"),
            Pick("anchored top-left, and compared on that canvas.", "ancorate în stânga-sus și comparate pe acea pânză."),
            Pick("The image files themselves are not modified.", "Fișierele imaginilor nu sunt modificate."),
            string.Empty,
            Pick("Continue?", "Continui?"));

    /// <summary>Words the first line of a file that could not be read (R-005).</summary>
    /// <param name="fileName">The file, without its folder.</param>
    /// <returns>The line; the reason follows on the next one.</returns>
    public string CouldNotRead(string fileName) =>
        Pick($"Could not read {fileName}.", $"Fișierul {fileName} nu poate fi citit.");

    /// <summary>Words the refusal of an image too large for WIC or WPF to hold (R-005).</summary>
    /// <param name="width">The image's width in pixels, as its header gives it.</param>
    /// <param name="height">The image's height in pixels, as its header gives it.</param>
    /// <returns>The sentence, naming the size.</returns>
    public string ImageTooLarge(long width, long height) => Pick(
        $"An image of {width} × {height} is too large to show.",
        $"O imagine de {width} × {height} este prea mare ca să fie afișată.");

    /// <summary>Words the FITS reader's refusal of an image too large to decode (R-019).</summary>
    /// <param name="width">The image's width, as its header gives it.</param>
    /// <param name="height">The image's height, as its header gives it.</param>
    /// <returns>The sentence, naming the size.</returns>
    public string FitsImageTooLarge(long width, long height) => Pick(
        $"FITS: an image of {width} × {height} is too large to show.",
        $"FITS: o imagine de {width} × {height} este prea mare ca să fie afișată.");

    /// <summary>Words the refusal of an image with more axes than are read (R-019).</summary>
    /// <param name="axes">The header's <c>NAXIS</c>.</param>
    /// <returns>The sentence, with what is read.</returns>
    public string FitsAxes(long axes) => Pick(
        $"FITS: an image with {axes} axes is not read; 2, or 3 with 1 or 3 planes, are.",
        $"FITS: o imagine cu NAXIS = {axes} nu e suportată; sunt suportate cele cu 2 axe sau cu 3 axe și 1 sau 3 planuri.");

    /// <summary>Words the refusal of a cube of other than 1 or 3 planes (R-019).</summary>
    /// <param name="planes">The header's <c>NAXIS3</c>.</param>
    /// <returns>The sentence, with what is read.</returns>
    public string FitsPlanes(long planes) => Pick(
        $"FITS: an image with {planes} planes is not read; 1 or 3 are.",
        $"FITS: o imagine cu NAXIS3 = {planes} nu e suportată; sunt suportate cele cu 1 sau 3 planuri.");

    /// <summary>Words the refusal of a sample type outside the five read (R-019).</summary>
    /// <param name="bitPix">The header's <c>BITPIX</c>.</param>
    /// <returns>The sentence.</returns>
    public string FitsBitPix(long bitPix) => Pick(
        $"FITS: BITPIX {bitPix} is not read.",
        $"FITS: BITPIX {bitPix} nu e suportat.");

    /// <summary>
    /// Words a file's format, without the range of R-016 (R-011, R-018, R-090): the depth, float
    /// or not, and the channels.
    /// </summary>
    /// <param name="bits">
    /// Bits of one channel; for a format the table does not know, the bits of a whole pixel.
    /// </param>
    /// <param name="isFloat">True when the samples are floating point.</param>
    /// <param name="channels">The channel layout; Unknown words the depth alone.</param>
    /// <returns>For example "16-bit RGB" or "RGB pe 16 biți".</returns>
    public string Format(int bits, bool isFloat, ColorChannels channels)
    {
        string? englishChannels = channels switch
        {
            ColorChannels.Gray => "gray",
            ColorChannels.GrayAlpha => "gray + alpha",
            ColorChannels.Rgb => "RGB",
            ColorChannels.Rgba => "RGBA",
            ColorChannels.Indexed => "indexed",
            _ => null,
        };
        string? romanianChannels = channels switch
        {
            ColorChannels.Gray => "gri",
            ColorChannels.GrayAlpha => "gri + alfa",
            ColorChannels.Rgb => "RGB",
            ColorChannels.Rgba => "RGBA",
            ColorChannels.Indexed => "paletizat",
            _ => null,
        };

        // English puts the depth first ("16-bit RGB"); Romanian names the channels and then
        // what they are stored on ("RGB pe 16 biți"), as Romanian says "PNG pe 16 biți".
        string englishDepth = isFloat ? $"{bits}-bit float" : $"{bits}-bit";
        string romanianDepth = Bits(bits);
        string romanianFloat = isFloat ? "float " : string.Empty;

        return Pick(
            englishChannels is null ? englishDepth : $"{englishDepth} {englishChannels}",
            romanianChannels is null
                ? (isFloat ? $"float pe {romanianDepth}" : romanianDepth)
                : $"{romanianChannels} {romanianFloat}pe {romanianDepth}");
    }

    /// <summary>
    /// Words a count of bits in Romanian, where a number whose last two digits are 0 or 20 to 99
    /// takes "de" before its noun: "16 biți", "32 de biți", "1 bit".
    /// </summary>
    /// <param name="count">The count, 0 or more.</param>
    /// <returns>The count and its noun.</returns>
    private static string Bits(int count)
    {
        int lastTwo = count % 100;
        string of = count != 0 && (lastTwo == 0 || lastTwo >= 20) ? "de " : string.Empty;
        return count == 1 ? "1 bit" : $"{count} {of}biți";
    }

    /// <summary>Picks this table's wording of a text.</summary>
    /// <param name="english">The English wording.</param>
    /// <param name="romanian">The Romanian wording of the same text.</param>
    /// <returns>The wording in this table's language.</returns>
    private string Pick(string english, string romanian) => _language == UiLanguage.Romanian ? romanian : english;
}
