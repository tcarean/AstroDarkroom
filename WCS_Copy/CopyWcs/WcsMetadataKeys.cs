using System.Text.RegularExpressions;

namespace CopyWcs;

internal static class WcsMetadataKeys
{
    // Cheile care descriu solutia astrometrica propriu-zisa.
    internal static readonly string[] WcsKeys =
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
    internal static readonly string[] OpticalKeys =
    {
        "FOCALLEN", "FOCAL", "XPIXSZ", "YPIXSZ", "XBINNING", "YBINNING",
        "OBJCTRA", "OBJCTDEC", "RA", "DEC", "OBJECT",
        "SITELAT", "SITELONG", "SITEELEV", "DATE-OBS", "INSTRUME", "TELESCOP"
    };

    // Coeficientii SIP au nume variabile: A_0_2, B_1_1, AP_2_0 etc.
    private static readonly Regex SipCoefficient =
        new(@"^(A|B|AP|BP)_\d+_\d+$", RegexOptions.Compiled);

    internal static List<string> SelectKeysToCopy(FitsHeader source, bool includeOptical)
    {
        var wanted = new List<string>(WcsKeys);
        wanted.AddRange(source.Keywords.Where(k => SipCoefficient.IsMatch(k)));
        if (includeOptical) wanted.AddRange(OpticalKeys);

        return wanted.Distinct().Where(source.Contains).ToList();
    }
}
