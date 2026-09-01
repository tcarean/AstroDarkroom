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

namespace FocalLenghChecker;

/// <summary>
/// A range of full-frame-equivalent focal lengths, in millimetres.
/// Written as inclusive whole millimetres (e.g. 16-23), which is how the
/// ranges are configured in <see cref="FocalLengthRanges"/>.
/// </summary>
public sealed record FocalLengthRange(int MinFf, int MaxFf)
{
    /// <summary>
    /// The exclusive upper bound actually used for matching.
    /// A range written as 16-23 matches everything up to (but not including) 24,
    /// so fractional focal lengths cannot fall into the gap between 23 and 24.
    /// A 15.5 mm DX lens, for instance, is 23.25 mm full-frame equivalent.
    /// </summary>
    public double UpperBoundExclusive => MaxFf + 1;

    /// <summary>Does this range contain the given full-frame-equivalent focal length?</summary>
    public bool Contains(double ffMillimetres) =>
        ffMillimetres >= MinFf && ffMillimetres < UpperBoundExclusive;

    /// <summary>The range as written, in full-frame millimetres: "16 - 23 mm".</summary>
    public string FfLabel => $"{MinFf} - {MaxFf} mm";

    /// <summary>The same range expressed in DX millimetres: "10.7 - 15.3 mm".</summary>
    public string DxLabel(double cropFactor) =>
        $"{FormatMm(MinFf / cropFactor)} - {FormatMm(MaxFf / cropFactor)} mm";

    internal static string FormatMm(double millimetres) =>
        millimetres.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
}
