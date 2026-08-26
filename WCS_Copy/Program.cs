using System.Text;
using System.Text.RegularExpressions;

namespace CopyWcs;

/// <summary>
/// Copiaza metadatele WCS (plate solving) si datele optice dintr-un FITS in altul.
/// Fara dependinte externe: headerul FITS e ASCII simplu, carduri de 80 de bytes
/// grupate in blocuri de 2880.
/// </summary>
internal static class Program
{
    private const int CardSize = 80;
    private const int BlockSize = 2880;

    // Cheile care descriu solutia astrometrica propriu-zisa.
    private static readonly string[] WcsKeys =
    {
        "WCSAXES", "CTYPE1", "CTYPE2", "CUNIT1", "CUNIT2",
        "CRPIX1", "CRPIX2", "CRVAL1", "CRVAL2",
        "CDELT1", "CDELT2", "CROTA1", "CROTA2",
        "CD1_1", "CD1_2", "CD2_1", "CD2_2",
        "PC1_1", "PC1_2", "PC2_1", "PC2_2",
        "EQUINOX", "RADESYS", "LONPOLE", "LATPOLE",
        "A_ORDER", "B_ORDER", "AP_ORDER", "BP_ORDER",
        "PLTSOLVD"
    };

    // Metadate optice / de observatie. Fara FOCALLEN si XPIXSZ un re-solve
    // ulterior nu are scara de pornire, deci merita copiate si ele.
    private static readonly string[] OpticalKeys =
    {
        "FOCALLEN", "FOCAL", "XPIXSZ", "YPIXSZ", "XBINNING", "YBINNING",
        "OBJCTRA", "OBJCTDEC", "RA", "DEC", "OBJECT",
        "SITELAT", "SITELONG", "SITEELEV", "DATE-OBS", "INSTRUME", "TELESCOP"
    };

    // Coeficientii SIP au nume variabile: A_0_2, B_1_1, AP_2_0 etc.
    private static readonly Regex SipCoefficient =
        new(@"^(A|B|AP|BP)_\d+_\d+$", RegexOptions.Compiled);

    private static int Main(string[] args)
    {
        Options opt;
        try
        {
            opt = Options.Parse(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            Options.PrintUsage();
            return 2;
        }

        if (opt.ShowHelp)
        {
            Options.PrintUsage();
            return 0;
        }

        FitsHeader source, dest;
        try
        {
            source = FitsHeader.ReadPrimary(opt.Source);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Nu pot citi sursa '{opt.Source}': {ex.Message}");
            return 1;
        }

        try
        {
            dest = FitsHeader.ReadPrimary(opt.Dest);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Nu pot citi destinatia '{opt.Dest}': {ex.Message}");
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
                Console.Error.WriteLine(msg + " WCS-ul ar fi gresit. Foloseste --force daca stii ce faci.");
                return 1;
            }
            Console.WriteLine($"ATENTIE: {msg} Continui oricum (--force).");
        }

        if (!source.Contains("CTYPE1"))
            Console.WriteLine("ATENTIE: sursa nu pare plate-solved (lipseste CTYPE1).");

        var wanted = new List<string>(WcsKeys);
        wanted.AddRange(source.Keywords.Where(k => SipCoefficient.IsMatch(k)));
        if (!opt.NoOptical) wanted.AddRange(OpticalKeys);

        var present = wanted.Distinct().Where(source.Contains).ToList();
        if (present.Count == 0)
        {
            Console.Error.WriteLine("Nicio cheie relevanta gasita in sursa. Nimic de copiat.");
            return 1;
        }

        if (opt.DryRun)
        {
            Console.WriteLine($"Chei care ar fi copiate ({present.Count}):");
            foreach (var k in present)
                Console.WriteLine("  " + source.GetCard(k)!.TrimEnd());
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
            Console.Error.WriteLine($"Eroare la scriere: {ex.Message}");
            return 1;
        }

        Console.WriteLine($"Copiate {present.Count} chei in {target}.");
        Console.WriteLine("Verifica vizual adnotarea: daca imaginea a fost oglindita/flip-uita " +
                          "pe parcursul procesarii, WCS-ul copiat va fi gresit fara sa dea eroare.");
        return 0;
    }

    /// <summary>Headerul HDU-ului primar, ca lista de carduri brute de 80 de caractere.</summary>
    private sealed class FitsHeader
    {
        private readonly List<string> _cards = new();

        /// <summary>Offsetul in fisier unde incepe zona de date (dupa headerul paddat).</summary>
        private long _dataOffset;

        public IEnumerable<string> Keywords => _cards.Select(KeywordOf).Where(k => k.Length > 0);

        public static FitsHeader ReadPrimary(string path)
        {
            var h = new FitsHeader();
            using var fs = File.OpenRead(path);

            var block = new byte[BlockSize];
            var sawEnd = false;
            long consumed = 0;

            while (!sawEnd)
            {
                var read = fs.Read(block, 0, BlockSize);
                if (read < BlockSize)
                    throw new InvalidDataException("Fisier trunchiat sau nu este FITS valid (cardul END lipseste).");
                consumed += BlockSize;

                for (var i = 0; i < BlockSize; i += CardSize)
                {
                    var card = Encoding.ASCII.GetString(block, i, CardSize);
                    if (KeywordOf(card) == "END")
                    {
                        sawEnd = true;
                        break;
                    }
                    h._cards.Add(card);
                }
            }

            if (h._cards.Count == 0 || KeywordOf(h._cards[0]) != "SIMPLE")
                throw new InvalidDataException("Fisierul nu incepe cu SIMPLE - nu pare FITS.");

            h._dataOffset = consumed;
            return h;
        }

        private static string KeywordOf(string card) =>
            card.Length < 8 ? string.Empty : card[..8].Trim();

        public bool Contains(string key) => IndexOf(key) >= 0;

        private int IndexOf(string key)
        {
            for (var i = 0; i < _cards.Count; i++)
                if (string.Equals(KeywordOf(_cards[i]), key, StringComparison.Ordinal))
                    return i;
            return -1;
        }

        public string? GetCard(string key)
        {
            var i = IndexOf(key);
            return i < 0 ? null : _cards[i];
        }

        /// <summary>Valoare intreaga. Sigur pentru NAXIS* si alte chei numerice.</summary>
        public int GetInt(string key)
        {
            var card = GetCard(key);
            if (card is null) return -1;

            var eq = card.IndexOf('=');
            if (eq < 0) return -1;

            var value = card[(eq + 1)..];
            var slash = value.IndexOf('/');
            if (slash >= 0) value = value[..slash];

            return int.TryParse(value.Trim(), out var n) ? n : -1;
        }

        /// <summary>Inlocuieste cardul daca exista, altfel il adauga la final.</summary>
        public void SetCard(string key, string rawCard)
        {
            var card = Pad(rawCard);
            var i = IndexOf(key);
            if (i >= 0) _cards[i] = card;
            else _cards.Add(card);
        }

        public void AddHistory(string text)
        {
            var body = text.Length > 72 ? text[..72] : text;
            _cards.Add(Pad("HISTORY " + body));
        }

        private static string Pad(string card) =>
            card.Length >= CardSize ? card[..CardSize] : card.PadRight(CardSize);

        /// <summary>
        /// Scrie headerul curent urmat de zona de date copiata din <paramref name="dataSource"/>.
        /// Daca tinta coincide cu sursa de date, se scrie intai intr-un fisier temporar
        /// (headerul poate creste peste blocul initial de 2880, deci nu se poate patch-ui in loc).
        /// </summary>
        public void WriteWithDataFrom(string dataSource, string target)
        {
            var inPlace = Path.GetFullPath(dataSource) == Path.GetFullPath(target);
            var temp = inPlace ? target + ".tmp" : target;

            using (var input = File.OpenRead(dataSource))
            using (var output = File.Create(temp))
            {
                foreach (var card in _cards)
                    output.Write(Encoding.ASCII.GetBytes(Pad(card)));

                output.Write(Encoding.ASCII.GetBytes("END".PadRight(CardSize)));

                // Padding cu spatii pana la urmatoarea granita de 2880.
                var remainder = (int)(output.Position % BlockSize);
                if (remainder != 0)
                {
                    var fill = new byte[BlockSize - remainder];
                    Array.Fill(fill, (byte)' ');
                    output.Write(fill);
                }

                input.Seek(_dataOffset, SeekOrigin.Begin);
                input.CopyTo(output);
            }

            if (inPlace) File.Move(temp, target, overwrite: true);
        }
    }

    private sealed class Options
    {
        public string Source = "";
        public string Dest = "";
        public string? Output;
        public bool DryRun;
        public bool Force;
        public bool NoOptical;
        public bool ShowHelp;

        public static Options Parse(string[] args)
        {
            var o = new Options();
            var positional = new List<string>();

            for (var i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-h":
                    case "--help":
                        o.ShowHelp = true;
                        return o;
                    case "-o":
                    case "--output":
                        if (++i >= args.Length) throw new ArgumentException("--output cere un nume de fisier.");
                        o.Output = args[i];
                        break;
                    case "--dry-run":
                        o.DryRun = true;
                        break;
                    case "--force":
                        o.Force = true;
                        break;
                    case "--no-optical":
                        o.NoOptical = true;
                        break;
                    default:
                        if (args[i].StartsWith('-'))
                            throw new ArgumentException($"Optiune necunoscuta: {args[i]}");
                        positional.Add(args[i]);
                        break;
                }
            }

            if (positional.Count != 2)
                throw new ArgumentException("Sunt necesare exact doua fisiere: sursa si destinatie.");

            o.Source = positional[0];
            o.Dest = positional[1];
            return o;
        }

        public static void PrintUsage()
        {
            Console.WriteLine("""
                Copiaza WCS + metadate optice dintr-un FITS sursa in unul destinatie.

                  copywcs <sursa.fit> <destinatie.fit> [optiuni]

                Optiuni:
                  -o, --output <fisier>  Scrie intr-un fisier nou in loc sa modifice destinatia in loc
                      --dry-run          Arata ce s-ar copia, fara sa scrie nimic
                      --force            Continua chiar daca dimensiunile imaginilor difera
                      --no-optical       Copiaza doar cheile WCS, fara metadatele optice
                  -h, --help             Afiseaza acest mesaj
                """);
        }
    }
}
