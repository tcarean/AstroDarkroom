namespace FlatCheck.Tests;

/// <summary>Builds in-memory rasters directly, skipping the FITS round trip.</summary>
internal static class TestImages
{
    internal static Img From(int w, int h, Func<int, int, double> f)
    {
        var img = new Img(h, w);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                img.Set(y, x, f(x, y));
        return img;
    }

    /// <summary>
    /// A CFA frame whose brightest channel (the two "green" positions) sits at exactly
    /// <paramref name="bright"/> ADU everywhere, so the central-window median is known
    /// exactly and the verdict bands can be dialled in. The other two positions are
    /// scaled down enough to keep the CFA spread well clear of the 1.02 sanity gate.
    /// </summary>
    internal static Img FlatCfa(int w, int h, double bright, double rGain = 0.62, double bGain = 0.48)
        => From(w, h, (x, y) =>
        {
            if (y % 2 == 0 && x % 2 == 0) return bright * rGain;
            if (y % 2 == 1 && x % 2 == 1) return bright * bGain;
            return bright;
        });

    /// <summary>Background that RISES toward the edges - the over-correction signature.</summary>
    internal static Func<int, int, double> InvertedRadial(int w, int h, double center = 30000, double k = 0.45)
    {
        double cx = w / 2.0, cy = h / 2.0;
        double rmax = Math.Sqrt(cx * cx + cy * cy);
        return (x, y) =>
        {
            double dx = x - cx, dy = y - cy;
            double rn = Math.Sqrt(dx * dx + dy * dy) / rmax;
            return center * (1.0 + k * rn * rn);
        };
    }
}
