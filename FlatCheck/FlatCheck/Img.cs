// Img.cs - rasterul 2D si transformarile pure pe el.

using System;

namespace FlatCheck
{
    // ---------------- imagine 2D simpla ----------------
    sealed class Img
    {
        public int H, W;
        public double[] D; // row-major, lungime H*W
        public Img(int h, int w) { H = h; W = w; D = new double[h * w]; }
        public double At(int y, int x) => D[y * W + x];
        public void Set(int y, int x, double v) => D[y * W + x] = v;

        // ========================================================= preprocesare
        internal static Img BlockMean(Img a, int k)
        {
            if (k <= 1) return a;
            int h = a.H / k, w = a.W / k;
            var o = new Img(h, w);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    double s = 0;
                    for (int dy = 0; dy < k; dy++)
                        for (int dx = 0; dx < k; dx++)
                            s += a.At(y * k + dy, x * k + dx);
                    o.Set(y, x, s / (k * k));
                }
            return o;
        }

        // block-mean 2x2 (omoara CFA Bayer) apoi downsample la ~1200px pe latura lunga
        internal static Img Prepare(Img data, int targetLong = 1200)
        {
            Img img = BlockMean(data, 2);
            int k = Math.Max(1, (int)(Math.Max(img.H, img.W) / (double)targetLong));
            return BlockMean(img, k);
        }

        // Imparte doua rastere, cu prag pe numitor (ca np.maximum(b, 1e-12)).
        internal static Img Divide(Img a, Img b)
        {
            var o = new Img(a.H, a.W);
            for (int i = 0; i < a.D.Length; i++) o.D[i] = a.D[i] / Math.Max(b.D[i], 1e-12);
            return o;
        }
    }
}
