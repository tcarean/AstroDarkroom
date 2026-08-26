// Options.cs - parsarea argumentelor din linia de comanda.

using System;
using System.Globalization;
using System.Linq;

namespace FlatCheck
{
    internal sealed class Options
    {
        internal string Image;
        internal string Ratio;
        internal bool Light;
        internal bool Expo;
        internal bool Sky;
        internal string OutPng;
        internal double? Sat;
        internal double? Black;
        internal bool Help;

        // ATENTIE: parsare pozitionala. args[0] e INTOTDEAUNA calea imaginii, deci
        // "flatcheck --expo f.fit" nu merge - flagul trebuie sa vina DUPA fisier.
        // Comportament pastrat identic cu versiunea de dinainte de refactorizare.
        internal static Options Parse(string[] args)
        {
            var o = new Options();
            if (args.Length == 0 || args.Contains("-h") || args.Contains("--help"))
            {
                o.Help = true;
                return o;
            }

            o.Image = args[0];
            o.Ratio = GetOpt(args, "--ratio");
            o.Light = args.Contains("--light");
            o.Expo = args.Contains("--expo");
            o.Sky = args.Contains("--sky");
            o.OutPng = GetOpt(args, "-o") ?? GetOpt(args, "--out") ?? "flatcheck.png";
            o.Sat = GetOptD(args, "--sat");
            o.Black = GetOptD(args, "--black");
            return o;
        }

        internal static string GetOpt(string[] a, string key)
        {
            int i = Array.IndexOf(a, key);
            return (i >= 0 && i + 1 < a.Length) ? a[i + 1] : null;
        }

        internal static double? GetOptD(string[] a, string key)
        {
            string s = GetOpt(a, key);
            if (s != null && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v)) return v;
            return null;
        }
    }
}
