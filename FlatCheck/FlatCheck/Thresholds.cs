// Thresholds.cs - pragurile de verdict, in procente.
// ATENTIE: nu toate pragurile sunt aici. --light foloseste 2.0/5.0 inline, iar --expo
// benzile 30/50/65/80 (si 15/50/60 pt --sky) ca locale. Vezi CLAUDE.md.

namespace FlatCheck
{
    // ---------------- praguri (procente) ----------------
    static class Th
    {
        public const double CornerOk = 1.5, CornerBad = 4.0;
        public const double AzimOk = 1.5, AzimBad = 4.0;
        public const double CentroidOk = 4.0, CentroidBad = 10.0;
        public const double RatioOk = 1.0, RatioBad = 3.0;
        public const double ExpoLo = 50.0, ExpoHi = 60.0;  // tinta canal cel mai luminos
        public const double ExpoClip = 0.05;               // % pixeli saturati = clipping
    }
}
