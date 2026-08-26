namespace CopyWcs;

/// <summary>
/// Copiaza metadatele WCS (plate solving) si datele optice dintr-un FITS in altul.
/// Fara dependinte externe: headerul FITS e ASCII simplu, carduri de 80 de bytes
/// grupate in blocuri de 2880.
/// </summary>
internal static class Program
{
    private static int Main(string[] args) => CopyWcsCli.Run(args, Console.Out, Console.Error);
}
