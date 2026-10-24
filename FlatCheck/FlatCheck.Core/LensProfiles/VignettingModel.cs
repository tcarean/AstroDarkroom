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

// The vignetting model of a lens profile, as a value: the factor V(x, y) by which the lens darkens
// a point of the sensor against the axis. Two coordinate conventions, both checked from the
// sources before being written here (2026-09-17):
//
//  - Adobe (LCP): V = 1 + a1 r^2 + a2 r^4 + a3 r^6, with r^2 = xd^2 + yd^2 and
//    xd = (u - u0) / fx, yd = (v - v0) / fy - the pixel minus the principal point, divided by
//    the focal length in pixels ("Adobe Camera Model", section Vignette Model, and the
//    descriptors on page 18: FocalLengthX = fx / Dmax, ImageXCenter = u0 / Dmax, Dmax = the
//    image's long side in pixels; when FocalLengthX is missing, the focal length is the nominal
//    one in mm, and since the sensor's long side is 36 / SensorFormatFactor mm,
//    fx / Dmax = f * SFF / 36). In short: r = distance in mm / focal length in mm. Checked on
//    the profile of the Tokina 11-20: the "piecewise" tables in the same file have their last
//    point at r = 0.7094, that is exactly the DX corner (14.1 mm / 20 mm = 0.705), and there the
//    table gives 0.603 and the polynomial 0.597. "Note that these model parameters are identical
//    for all color channels" - the model is achromatic, and the report says so.
//
//    The distance in mm comes from YOUR CAMERA's crop (long side = 36 / crop), not from the
//    profile's SFF: the SFF is the sensor Adobe calibrated the profile on, and the first version
//    took it for everyone's sensor. On a D3300 with the DX profile they coincided within 1.5 %;
//    on a full frame with a profile calibrated on DX, or the other way round, the corner would
//    have fallen at the wrong radius without any warning. The profile's focal length in pixels
//    (FocalLengthX) is in units of the calibration camera's Dmax, so it goes through mm
//    (FocalX * 36 / SFF), and the decentring (ImageXCenter) is the lens's, in mm too. Without a
//    crop the SFF is taken, as before, and the report says so. The end of the "piecewise" table,
//    r = 0.7094, falls at 14.19 mm: exactly the half-diagonal of a sensor of 36 / 1.523 =
//    23.64 mm, not of the 24 mm one SFF 1.5 implies (14.42 mm, r = 0.721) - the real crop is what
//    Adobe used too.
//
//  - lensfun ("pa", Pablo D'Angelo / Hugin): the same polynomial, but r is normalised to the
//    half-diagonal of a 36 x 24 frame divided by the crop of the CALIBRATION camera
//    (mod-color.cpp: hugin_scale_in_millimeters = hypot(36, 24) / CropFactor / 2), so
//    r = distance in mm / (21.63 mm / calibration_crop). On a camera with another crop the
//    corner can fall beyond r = 1 (D3300 1.523 against 1.61 for the Tokina: 1.05, a small
//    extrapolation the report mentions). The distance in mm is taken from your camera's crop:
//    long side = 36 / crop.
//
// In both cases the polynomial IS the vignetting factor: multiplied it applies the vignetting,
// divided it corrects it (lensfun: Cd = Cs * (...) for "vignetting", / for "devignetting"). The
// synthetic flat is V itself, with 1.0 on the axis.
//
// The input coordinate is shared: (xN, yN) = the pixel's position relative to the CENTRE of the
// image, in units of Dmax. It is what the generator knows without knowing the sensor; the
// conversion to mm is the model's, each by its convention.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace FlatCheck
{
    /// <summary>Which coordinate convention a vignetting model follows.</summary>
    internal enum VignettingModelKind { Adobe, Lensfun }

    /// <summary>
    /// A lens profile's vignetting factor V(x, y): a polynomial in r^2 or an Adobe table, with
    /// the coordinate convention of its source.
    /// </summary>
    internal sealed class VignettingModel
    {
        internal readonly VignettingModelKind Kind;

        /// <summary>
        /// a1..a3 (Adobe) or k1..k3 (lensfun); null when the profile has only a table.
        /// </summary>
        internal readonly double[] Coefficients;

        /// <summary>
        /// The (r, V) table of an Adobe "piecewise" profile, on the same r axis; null otherwise.
        /// </summary>
        internal readonly IReadOnlyList<(double R, double V)> Table;

        /// <summary>The nominal focal length in mm (the profile's).</summary>
        internal readonly double FocalMm;

        /// <summary>
        /// Adobe: the profile's SensorFormatFactor. lensfun: the calibration camera's crop.
        /// </summary>
        internal readonly double CropFactor;

        /// <summary>
        /// Adobe: fx / Dmax, fy / Dmax if the profile gives them; otherwise f * SFF / 36.
        /// </summary>
        internal readonly double? FocalX, FocalY;

        /// <summary>Adobe: u0 / Dmax, v0 / Dmax; 0.5 when missing.</summary>
        internal readonly double CenterX, CenterY;

        /// <summary>
        /// A model. Throws an ArgumentException without coefficients or a table of at least two
        /// points, or with a focal length or crop of 0 or less.
        /// </summary>
        /// <param name="kind">The source's convention.</param>
        /// <param name="coefficients">The three polynomial coefficients; null for a table-only
        /// profile.</param>
        /// <param name="table">The (r, V) table, r ascending; null when there is none.</param>
        /// <param name="focalMm">The profile's nominal focal length, in mm, above 0.</param>
        /// <param name="cropFactor">The profile's SFF (Adobe) or calibration crop (lensfun), above
        /// 0.</param>
        /// <param name="focalX">Adobe's fx / Dmax; null when the profile gives none.</param>
        /// <param name="focalY">Adobe's fy / Dmax; null to use the x one.</param>
        /// <param name="centerX">Adobe's u0 / Dmax.</param>
        /// <param name="centerY">Adobe's v0 / Dmax.</param>
        internal VignettingModel(VignettingModelKind kind, double[] coefficients, IReadOnlyList<(double R, double V)> table,
                                 double focalMm, double cropFactor,
                                 double? focalX = null, double? focalY = null, double centerX = 0.5, double centerY = 0.5)
        {
            if (coefficients == null && (table == null || table.Count < 2))
                throw new ArgumentException("un model de vignetare are coeficienti sau un tabel cu cel putin doua puncte");
            if (focalMm <= 0) throw new ArgumentException("focala trebuie sa fie pozitiva");
            if (cropFactor <= 0) throw new ArgumentException("crop-ul trebuie sa fie pozitiv");
            Kind = kind;
            Coefficients = coefficients;
            Table = table;
            FocalMm = focalMm;
            CropFactor = cropFactor;
            FocalX = focalX;
            FocalY = focalY;
            CenterX = centerX;
            CenterY = centerY;
        }

        /// <summary>
        /// The vignetting factor at the point (xN, yN): the position relative to the image's
        /// centre, in units of Dmax (the long side in pixels). <paramref name="cameraCrop"/> is the
        /// crop of the camera it is applied on: lensfun requires it, Adobe uses it when given and
        /// takes the profile's SFF when it is not (0).
        /// </summary>
        /// <param name="xN">The horizontal position, in units of Dmax, 0 at the centre.</param>
        /// <param name="yN">The vertical position, in units of Dmax, 0 at the centre.</param>
        /// <param name="cameraCrop">Your camera's crop factor; 0 for none (Adobe only).</param>
        /// <returns>V, 1.0 on the axis, below 1 where the lens darkens.</returns>
        internal double Factor(double xN, double yN, double cameraCrop)
        {
            double r = Radius(xN, yN, cameraCrop);
            return Coefficients != null ? Polynomial(r * r) : Interpolate(r);
        }

        /// <summary>
        /// r in the model's units, for the report (where the corner falls, for example). The
        /// lensfun model without a crop throws an ArgumentException.
        /// </summary>
        /// <param name="xN">The horizontal position, in units of Dmax, 0 at the centre.</param>
        /// <param name="yN">The vertical position, in units of Dmax, 0 at the centre.</param>
        /// <param name="cameraCrop">Your camera's crop factor; 0 for none (Adobe only).</param>
        /// <returns>The radius in the model's units.</returns>
        internal double Radius(double xN, double yN, double cameraCrop)
        {
            if (Kind == VignettingModelKind.Adobe)
            {
                // Everything in mm: your frame is 36 / crop wide (36 / SFF without a crop), the
                // profile's focal length in pixels is in the calibration camera's Dmax
                // (36 / SFF mm), and the decentring the same. At crop == SFF it reduces to
                // (xN + 0.5 - CenterX) / fx.
                double calibMm = 36.0 / CropFactor;
                double longSideMm = 36.0 / (cameraCrop > 0 ? cameraCrop : CropFactor);
                double fxMm = FocalX.HasValue ? FocalX.Value * calibMm : FocalMm;
                double fyMm = FocalY.HasValue ? FocalY.Value * calibMm : fxMm;
                double xd = (xN * longSideMm - (CenterX - 0.5) * calibMm) / fxMm;
                double yd = (yN * longSideMm - (CenterY - 0.5) * calibMm) / fyMm;
                return Math.Sqrt(xd * xd + yd * yd);
            }
            else
            {
                if (cameraCrop <= 0) throw new ArgumentException("modelul lensfun are nevoie de crop-ul aparatului");
                double longSideMm = 36.0 / cameraCrop;
                double xMm = xN * longSideMm, yMm = yN * longSideMm;
                double huginMm = Math.Sqrt(36.0 * 36.0 + 24.0 * 24.0) / CropFactor / 2.0;
                return Math.Sqrt(xMm * xMm + yMm * yMm) / huginMm;
            }
        }

        /// <summary>
        /// The radius, in the model's units, of the corner of the sensor the profile was
        /// calibrated on: 1 for lensfun by definition, the half-diagonal of 36 x 24 / SFF over the
        /// focal length for Adobe. Beyond it the polynomial extrapolates, and a sixth-degree
        /// polynomial extrapolated by 50 % (a full frame on a DX profile) gives meaningless
        /// values, even negative ones.
        /// </summary>
        /// <returns>The calibration corner's radius.</returns>
        internal double CalibrationCornerRadius()
        {
            if (Kind != VignettingModelKind.Adobe) return 1.0;
            double calibMm = 36.0 / CropFactor;
            double fxMm = FocalX.HasValue ? FocalX.Value * calibMm : FocalMm;
            return Math.Sqrt(36.0 * 36.0 + 24.0 * 24.0) / 2.0 / CropFactor / fxMm;
        }

        /// <summary>
        /// The corner of a w x h frame against the corner of the calibration sensor; above 1 =
        /// extrapolation.
        /// </summary>
        /// <param name="w">The frame's width in pixels.</param>
        /// <param name="h">The frame's height in pixels.</param>
        /// <param name="cameraCrop">Your camera's crop factor; 0 for none (Adobe only).</param>
        /// <returns>The ratio of the two radii.</returns>
        internal double Extrapolation(int w, int h, double cameraCrop)
        {
            double dmax = Math.Max(w, h);
            return Radius(w / 2.0 / dmax, h / 2.0 / dmax, cameraCrop) / CalibrationCornerRadius();
        }

        /// <summary>1 + c1 r^2 + c2 r^4 + c3 r^6.</summary>
        /// <param name="r2">r^2, in the model's units.</param>
        /// <returns>V.</returns>
        double Polynomial(double r2)
        {
            double v = 1, p = 1;
            foreach (double c in Coefficients) { p *= r2; v += c * p; }
            return v;
        }

        /// <summary>The table read linearly, held at its end values outside it.</summary>
        /// <param name="r">The radius, in the model's units.</param>
        /// <returns>V.</returns>
        double Interpolate(double r)
        {
            if (r <= Table[0].R) return Table[0].V;
            for (int i = 1; i < Table.Count; i++)
                if (r <= Table[i].R)
                {
                    double t = (r - Table[i - 1].R) / (Table[i].R - Table[i - 1].R);
                    return Table[i - 1].V + t * (Table[i].V - Table[i - 1].V);
                }
            return Table[Table.Count - 1].V;
        }

        /// <summary>
        /// The model in one line, for the report: its formula, coefficients and axis.
        /// </summary>
        /// <returns>The Romanian description.</returns>
        internal string Describe()
        {
            string coeffs = Coefficients != null
                ? string.Join(", ", Array.ConvertAll(Coefficients, c => c.ToString("0.000000", CultureInfo.InvariantCulture)))
                : $"tabel cu {Table.Count} puncte";
            return Kind == VignettingModelKind.Adobe
                ? $"Adobe, V = 1 + a1 r^2 + a2 r^4 + a3 r^6 in unitati de focala; a = {coeffs}; SFF {CropFactor.ToString("0.###", CultureInfo.InvariantCulture)}"
                  + (FocalX.HasValue || CenterX != 0.5 || CenterY != 0.5
                        ? $"; fx/Dmax {(FocalX ?? 0).ToString("0.####", CultureInfo.InvariantCulture)}, centru ({CenterX.ToString("0.####", CultureInfo.InvariantCulture)}, {CenterY.ToString("0.####", CultureInfo.InvariantCulture)})"
                        : "")
                : $"lensfun pa, V = 1 + k1 r^2 + k2 r^4 + k3 r^6, r la semidiagonala 36x24 / {CropFactor.ToString("0.###", CultureInfo.InvariantCulture)}; k = {coeffs}";
        }
    }
}
