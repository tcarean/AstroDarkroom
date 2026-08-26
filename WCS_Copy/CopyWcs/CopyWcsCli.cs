namespace CopyWcs;

internal static class CopyWcsCli
{
    internal static int Run(string[] args, TextWriter stdout, TextWriter stderr)
    {
        Options opt;
        try
        {
            opt = Options.Parse(args);
        }
        catch (ArgumentException ex)
        {
            stderr.WriteLine(ex.Message);
            Options.PrintUsage(stdout);
            return 2;
        }

        if (opt.ShowHelp)
        {
            Options.PrintUsage(stdout);
            return 0;
        }

        FitsHeader source, dest;
        try
        {
            source = FitsHeader.ReadPrimary(opt.Source);
        }
        catch (Exception ex)
        {
            stderr.WriteLine($"Nu pot citi sursa '{opt.Source}': {ex.Message}");
            return 1;
        }

        try
        {
            dest = FitsHeader.ReadPrimary(opt.Dest);
        }
        catch (Exception ex)
        {
            stderr.WriteLine($"Nu pot citi destinatia '{opt.Dest}': {ex.Message}");
            return 1;
        }

        // Verificare geometrie: WCS-ul e valabil doar daca grila de pixeli e aceeasi.
        var srcDim = (source.GetInt("NAXIS1"), source.GetInt("NAXIS2"));
        var dstDim = (dest.GetInt("NAXIS1"), dest.GetInt("NAXIS2"));
        if (srcDim != dstDim)
        {
            var msg = $"Dimensiuni diferite: sursa ({srcDim.Item1}, {srcDim.Item2}) " +
                      $"vs destinatie ({dstDim.Item1}, {dstDim.Item2}).";
            if (!opt.Force)
            {
                stderr.WriteLine(msg + " WCS-ul ar fi gresit. Foloseste --force daca stii ce faci.");
                return 1;
            }
            stdout.WriteLine($"ATENTIE: {msg} Continui oricum (--force).");
        }

        if (!source.Contains("CTYPE1"))
            stdout.WriteLine("ATENTIE: sursa nu pare plate-solved (lipseste CTYPE1).");

        var present = WcsMetadataKeys.SelectKeysToCopy(source, includeOptical: !opt.NoOptical);
        if (present.Count == 0)
        {
            stderr.WriteLine("Nicio cheie relevanta gasita in sursa. Nimic de copiat.");
            return 1;
        }

        if (opt.DryRun)
        {
            stdout.WriteLine($"Chei care ar fi copiate ({present.Count}):");
            foreach (var k in present)
                stdout.WriteLine("  " + source.GetCard(k)!.TrimEnd());
            return 0;
        }

        // Se copiaza cardul brut de 80 de bytes, nu valoarea reparsata:
        // asa se pastreaza formatarea si comentariul exact ca in sursa.
        foreach (var k in present)
            dest.SetCard(k, source.GetCard(k)!);

        dest.AddHistory("WCS copiat din " + Path.GetFileName(opt.Source));

        var target = opt.Output ?? opt.Dest;
        try
        {
            dest.WriteWithDataFrom(opt.Dest, target);
        }
        catch (Exception ex)
        {
            stderr.WriteLine($"Eroare la scriere: {ex.Message}");
            return 1;
        }

        stdout.WriteLine($"Copiate {present.Count} chei in {target}.");
        stdout.WriteLine("Verifica vizual adnotarea: daca imaginea a fost oglindita/flip-uita " +
                          "pe parcursul procesarii, WCS-ul copiat va fi gresit fara sa dea eroare.");
        return 0;
    }
}
