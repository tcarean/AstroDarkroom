// Version.cs - versiunea uneltei, citita din assembly.
//
// SURSA DE ADEVAR e <Version> din FlatCheck.csproj. Nu hardcoda nimic aici: MSBuild
// transforma <Version> in AssemblyInformationalVersion, iar de acolo o citim. Asa exista
// un singur loc de schimbat pe partea de C#.
//
// Se incrementeaza MANUAL la fiecare feature/fix, si trebuie sa ramana identica cu
// __version__ din flatcheck.py - VersionTests verifica asta.

using System;
using System.Reflection;

namespace FlatCheck
{
    internal static class VersionInfo
    {
        internal static string Value { get; } = Read();

        /// <summary>Numele si versiunea, exact cum apar pe prima linie a fiecarei rulari.</summary>
        internal static string Banner => "flatcheck " + Value;

        static string Read()
        {
            Assembly asm = typeof(VersionInfo).Assembly;

            // InformationalVersion vine direct din <Version> si pastreaza forma "1.0.1".
            // AssemblyVersion ar da "1.0.1.0", care nu e ce vrem sa aratam.
            string v = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                       ?? asm.GetName().Version?.ToString();
            if (string.IsNullOrWhiteSpace(v)) return "0.0.0";

            // Unele build-uri lipesc metadata de sursa: "1.0.1+9a8b7c6". Taie de la '+'.
            int plus = v.IndexOf('+');
            return plus >= 0 ? v.Substring(0, plus) : v;
        }
    }
}
