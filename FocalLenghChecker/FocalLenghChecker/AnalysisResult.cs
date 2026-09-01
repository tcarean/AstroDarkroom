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

/// <summary>How many files fell into one configured range.</summary>
public sealed record RangeCount(FocalLengthRange Range, int Count, double Percentage);

/// <summary>How many files used one distinct focal length, shown both ways.</summary>
public sealed record FocalLengthCount(double DxMm, double FfMm, int Count, double Percentage);

/// <summary>Everything the report needs, with no I/O involved in producing it.</summary>
public sealed record AnalysisResult(
    bool SourceIsFullFrame,
    double CropFactor,
    IReadOnlyList<RangeCount> Ranges,
    int OutsideRangesCount,
    double OutsideRangesPercentage,
    IReadOnlyList<FocalLengthCount> FocalLengths,
    int FilesWithFocalLength,
    int FilesWithoutFocalLength)
{
    public int FilesScanned => FilesWithFocalLength + FilesWithoutFocalLength;
}
