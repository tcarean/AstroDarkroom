using System.Text;

namespace CopyWcs.Tests;

/// <summary>
/// Construieste carduri si fisiere FITS sintetice pentru teste. Foloseste propriile
/// constante 80/2880 (nu cele din FitsHeader) ca o constanta gresita in productie
/// sa fie prinsa de teste, nu confirmata de ele.
/// </summary>
internal static class FitsTestData
{
    private const int CardSize = 80;
    private const int BlockSize = 2880;

    public static string Card(string keyword, string value, string? comment = null)
    {
        var line = $"{keyword,-8}= {value}";
        if (comment != null) line += $" / {comment}";
        return Pad(line);
    }

    public static string Pad(string card) =>
        card.Length >= CardSize ? card[..CardSize] : card.PadRight(CardSize);

    /// <summary>Cardul fara padding la 80 - util pentru a testa padding/trunchiere explicit.</summary>
    public static string UnpaddedCard(string keyword, string value) => $"{keyword,-8}= {value}";

    /// <summary>Carduri padate la 80, concatenate, FARA card END si fara padding la 2880.</summary>
    public static byte[] RawBytes(IEnumerable<string> cards)
    {
        using var ms = new MemoryStream();
        foreach (var c in cards) ms.Write(Encoding.ASCII.GetBytes(Pad(c)));
        return ms.ToArray();
    }

    public static byte[] HeaderBlock(IEnumerable<string> cards)
    {
        using var ms = new MemoryStream();
        foreach (var c in cards)
            ms.Write(Encoding.ASCII.GetBytes(Pad(c)));
        ms.Write(Encoding.ASCII.GetBytes(Pad("END")));

        var remainder = (int)(ms.Position % BlockSize);
        if (remainder != 0)
        {
            var fill = new byte[BlockSize - remainder];
            Array.Fill(fill, (byte)' ');
            ms.Write(fill);
        }
        return ms.ToArray();
    }

    public static byte[] MinimalFitsFile(IEnumerable<string> cards, byte[]? data = null)
    {
        using var ms = new MemoryStream();
        ms.Write(HeaderBlock(cards));
        if (data != null) ms.Write(data);
        return ms.ToArray();
    }

    public static byte[] DeterministicData(int length)
    {
        var data = new byte[length];
        for (var i = 0; i < length; i++) data[i] = (byte)(i % 256);
        return data;
    }

    /// <summary>Header minimal plate-solved: WCS standard, un coeficient SIP si chei optice.</summary>
    public static List<string> PlateSolvedCards(int naxis1 = 100, int naxis2 = 80) => new()
    {
        Card("SIMPLE", "T"),
        Card("BITPIX", "16"),
        Card("NAXIS", "2"),
        Card("NAXIS1", naxis1.ToString()),
        Card("NAXIS2", naxis2.ToString()),
        Card("CTYPE1", "'RA---TAN-SIP'", "TAN + SIP"),
        Card("CTYPE2", "'DEC--TAN-SIP'"),
        Card("CRPIX1", "50.0"),
        Card("CRPIX2", "40.0"),
        Card("CRVAL1", "83.633083"),
        Card("CRVAL2", "22.014500"),
        Card("CDELT1", "-0.000305"),
        Card("CDELT2", "0.000305"),
        Card("A_0_2", "1.2e-06"),
        Card("BP_1_1", "-3.4e-07"),
        Card("PLTSOLVD", "T"),
        Card("FOCALLEN", "700.0"),
        Card("XPIXSZ", "3.76"),
        Card("OBJECT", "'M42'"),
        Card("TELESCOP", "'My Scope'"),
    };

    /// <summary>Header de imagine procesata: fara WCS, aceleasi dimensiuni ca sursa implicita.</summary>
    public static List<string> ProcessedCards(int naxis1 = 100, int naxis2 = 80) => new()
    {
        Card("SIMPLE", "T"),
        Card("BITPIX", "16"),
        Card("NAXIS", "2"),
        Card("NAXIS1", naxis1.ToString()),
        Card("NAXIS2", naxis2.ToString()),
        Card("OBJECT", "'M42'"),
    };
}
