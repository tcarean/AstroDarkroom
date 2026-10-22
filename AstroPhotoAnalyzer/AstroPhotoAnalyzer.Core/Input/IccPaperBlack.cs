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

// IccPaperBlack.cs - the Dmax of a paper on a printer, read from the pair's ICC profile: the
// device's black (every channel at its dark end) passed through the profile's colorimetric
// table, relative to the paper's white (ADR 0028, ADR 0030).
//
// WHY RELATIVE COLORIMETRIC AND NOTHING ELSE. The colorimetric table is the measurement: what
// colour comes out on the paper. The perceptual one is a convention - it puts the black of any
// profile on the ICC reference medium, L* 3.14 (Dmax 2.46), whether the paper is baryta or
// canvas - and the intent written in the profile's header is usually the perceptual one. Here
// the intent is asked for explicitly; a test on a profile with a weak black fails if someone
// changes it.
//
// WHY RELATIVE TO THE PAPER'S WHITE. The image's white prints as the paper's white, so the
// question "which L* of the image falls under black" is asked on that scale, not on the
// absolute one (which would give ~0.03 more on a baryta).
//
// The tables are evaluated by Wacton.Unicolour (v2 lut8/lut16, v4 lutAToB); the profile's name
// comes from IccProfileInfo, from the 'desc' tag. The package does not throw on a file that is
// not a profile: it puts an error in the configuration and goes on computing "naively" (L* 100)
// - which is why the header is checked here, beforehand, and its error after.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Wacton.Unicolour;
using Wacton.Unicolour.Icc;

namespace AstroPhotoAnalyzer
{
    /// <summary>What an ICC profile says about the paper's black, or why it cannot say.</summary>
    internal sealed record IccBlack
    {
        internal string Path { get; init; }

        /// <summary>
        /// The name in the 'desc' tag; the file's name when the profile has none.
        /// </summary>
        internal string Description { get; init; }

        /// <summary>Why the black could not be read, for the user; null when it could.</summary>
        internal string Problem { get; init; }
        internal bool Readable => Problem == null;

        /// <summary>
        /// A reservation about the figure, for the user (the CMYK profiles); null when there is
        /// none.
        /// </summary>
        internal string Note { get; init; }

        /// <summary>The device's black, relative to the paper's white.</summary>
        internal double BlackLstar { get; init; }

        /// <summary>
        /// -log10(Y) of the same black, rounded to the two decimals a Dmax is shown with.
        /// </summary>
        internal double Dmax { get; init; }
    }

    /// <summary>
    /// Reads the Dmax of a paper on a printer from the pair's ICC profile, and lists the printer
    /// profiles installed.
    /// </summary>
    internal static class IccPaperBlack
    {
        /// <summary>
        /// The folder where Windows keeps the installed profiles; it does not exist on another
        /// system.
        /// </summary>
        internal static string InstalledFolder
            => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "spool", "drivers", "color");

        static readonly string[] Extensions = { ".icc", ".icm" };

        /// <summary>
        /// The paper's black in the profile of a file, as <see cref="Read(byte[], string)"/>
        /// reads it; a file that cannot be read gives a <see cref="IccBlack.Problem"/>, not an
        /// exception.
        /// </summary>
        /// <param name="path">The profile's file.</param>
        /// <returns>The black and its Dmax, or the problem.</returns>
        internal static IccBlack Read(string path)
        {
            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(path);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                return new IccBlack { Path = path, Description = System.IO.Path.GetFileName(path), Problem = "fisierul nu se poate citi: " + e.Message };
            }
            return Read(bytes, path);
        }

        /// <summary>
        /// The paper's black in a profile's bytes: the header checked by hand (the 'acsp'
        /// signature, the class 'prtr', the device space RGB or CMYK), then the device's black
        /// through the profile's relative colorimetric table, in XYZ at D50. Every refusal, the
        /// package's errors included, is a <see cref="IccBlack.Problem"/>, not an exception.
        /// </summary>
        /// <param name="bytes">The file's bytes; null is not a profile.</param>
        /// <param name="path">
        /// The file's path, kept on the result; its file name is the description when the profile
        /// has no 'desc' name.
        /// </param>
        /// <returns>The black and its Dmax, or the problem.</returns>
        internal static IccBlack Read(byte[] bytes, string path)
        {
            string file = System.IO.Path.GetFileName(path);
            var result = new IccBlack { Path = path, Description = IccProfileInfo.Describe(bytes) ?? file };
            if (!IsProfile(bytes)) return result with { Problem = "nu e un profil ICC" };

            string deviceClass = Signature(bytes, 12), space = Signature(bytes, 16);
            if (deviceClass != "prtr")
                return result with { Problem = $"nu e un profil de imprimanta (clasa '{Printable(deviceClass)}'); negrul hartiei e in profilul hartiei pe imprimanta ei" };

            // The device's black: no light on RGB, every ink on CMYK.
            double[] black;
            string note = null;
            switch (space)
            {
                case "RGB ": black = new double[3]; break;
                case "CMYK":
                    black = new[] { 1.0, 1.0, 1.0, 1.0 };
                    note = "profil CMYK: negrul e cu toate cernelurile la maximum; o imprimanta reala pune mai putina cerneala, deci Dmax-ul ei e ceva mai mic";
                    break;
                default:
                    return result with { Problem = $"profil de imprimanta '{Printable(space)}': se citesc doar profilele RGB si CMYK" };
            }

            try
            {
                var icc = new IccConfiguration(new Profile(bytes, file), Intent.RelativeColorimetric, file);
                if (!string.IsNullOrEmpty(icc.Error)) return result with { Problem = "profilul nu se poate citi: " + icc.Error };
                var colour = new Unicolour(new Configuration(xyzConfig: XyzConfiguration.D50, iccConfig: icc), new Channels(black));
                double y = colour.Xyz.Y;
                // The package swallows a damaged table and returns some Y; a black that is not
                // between 0 and the white, or a Dmax that --dmax would refuse, is its sign.
                if (double.IsNaN(y) || y <= 0 || y >= 1)
                    return result with { Problem = "tabela colorimetrica a profilului nu da un negru intre 0 si albul hartiei; profilul e probabil stricat" };
                double dmax = Math.Round(-Math.Log10(y), 2);
                if (dmax <= 0 || dmax > Thresholds.DmaxMax)
                    return result with { Problem = $"profilul da Dmax {Fmt.Dmax(dmax)}, in afara intervalului (0, {Fmt.F0(Thresholds.DmaxMax)}]; profilul e probabil stricat" };
                return result with { BlackLstar = ColorMath.LstarOfY(y), Dmax = dmax, Note = note };
            }
            catch (Exception e)
            {
                // The package throws ArgumentException, with the cause inside, on a damaged
                // profile.
                return result with { Problem = "profilul nu se poate citi: " + (e.InnerException ?? e).Message };
            }
        }

        /// <summary>
        /// The printer profiles in <paramref name="folder"/> (by default Windows' own), by name.
        /// The other classes (monitor, working space) and the files that are not profiles are
        /// skipped; a printer profile whose black cannot be read stays in the list, with its
        /// reason. An absent folder gives an empty list.
        /// </summary>
        /// <param name="folder">The folder; null for <see cref="InstalledFolder"/>.</param>
        /// <returns>
        /// The profiles read, ordered by description then path; empty when the folder is absent
        /// or cannot be listed.
        /// </returns>
        internal static IReadOnlyList<IccBlack> Installed(string folder = null)
        {
            folder ??= InstalledFolder;
            if (!Directory.Exists(folder)) return Array.Empty<IccBlack>();
            string[] files;
            try
            {
                files = Directory.GetFiles(folder);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // A folder that will not be listed is like an empty one: the window says
                // "niciun profil" and keeps "Alege fisier...".
                return Array.Empty<IccBlack>();
            }
            var found = new List<IccBlack>();
            foreach (string path in files.Where(f => Extensions.Contains(System.IO.Path.GetExtension(f).ToLowerInvariant())))
            {
                byte[] bytes;
                try { bytes = File.ReadAllBytes(path); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { continue; }
                if (!IsProfile(bytes) || Signature(bytes, 12) != "prtr") continue;
                found.Add(Read(bytes, path));
            }
            return found.OrderBy(p => p.Description, StringComparer.OrdinalIgnoreCase).ThenBy(p => p.Path, StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>
        /// The sentence about a profile, for the window's list: "Dmax 2.41 (negrul la L* 3.5
        /// fata de albul hartiei)", or why it could not be read.
        /// </summary>
        /// <param name="b">The profile as read.</param>
        /// <returns>The sentence, in Romanian, as the user sees it.</returns>
        internal static string Describe(IccBlack b)
            => b.Readable ? $"Dmax {Fmt.Dmax(b.Dmax)} (negrul la L* {Fmt.F1(b.BlackLstar)} fata de albul hartiei)" : b.Problem;

        /// <summary>
        /// Whether the bytes hold a profile's header: 132 bytes at least, 'acsp' at 36.
        /// </summary>
        /// <param name="b">The bytes; null is not a profile.</param>
        /// <returns><c>true</c> for a profile.</returns>
        static bool IsProfile(byte[] b) => b != null && b.Length >= 132 && Signature(b, 36) == "acsp";

        /// <summary>The 4-byte signature at a position of the header, as ASCII.</summary>
        /// <param name="b">The profile's bytes.</param>
        /// <param name="at">The signature's offset, in bytes.</param>
        /// <returns>The 4 characters, padding spaces included.</returns>
        static string Signature(byte[] b, int at) => Encoding.ASCII.GetString(b, at, 4);

        /// <summary>
        /// A 4-byte signature, to show to the user: without the padding spaces and the control
        /// bytes; a non-ASCII byte, which the ASCII decoding turned into '?', shows as '?'.
        /// </summary>
        /// <param name="signature">The signature as <see cref="Signature"/> read it.</param>
        /// <returns>What is left of it, possibly empty.</returns>
        static string Printable(string signature) => new string(signature.Where(c => c > ' ' && c < 127).ToArray());
    }
}
