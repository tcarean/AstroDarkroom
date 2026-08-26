namespace CopyWcs;

internal sealed class Options
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

    public static void PrintUsage(TextWriter writer)
    {
        writer.WriteLine("""
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
