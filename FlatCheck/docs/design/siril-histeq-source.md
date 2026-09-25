# Siril 1.4.4 - what the "Histogram" display mode does, in its source

**Covers:** `FlatCheck.Core/Analysis/HistEqAnalyzer.cs` (`HistEqLut.Build` and the
per-layer rule), which mirrors the code quoted below. The excerpt is from Siril tag
`1.4.4` and stays true for that tag; **it changes only if the mode starts mirroring another
Siril version**, in which case re-read the two source files at the new tag, update the
quotes, and re-check `HistEqLut` against them.

Moved here from the implementation plan of the `--histeq` mode when that plan was deleted; the
plan was a to-do list, this is the measurement it rested on. `HistEqAnalyzer.cs` and
`CLAUDE.md` ("5. Histogram equalisation") state the consequences; this file keeps the quoted
code so the claim can be checked against the tag it was read from.

## What Siril actually does (measured in the source, tag `1.4.4`)

The thing the display-mode combo calls **"Histogram"** is not a chart. It is the image
itself, rendered through a transfer function derived from its own cumulative histogram:
**histogram equalisation**. Every grey level on screen receives the same number of pixels.
The relevant code, quoted from `src/gui/image_display.c` and `src/gui/histogram.c` at
`1.4.4`:

```c
typedef enum { LINEAR_DISPLAY, LOG_DISPLAY, SQRT_DISPLAY, SQUARED_DISPLAY,
               ASINH_DISPLAY, STF_DISPLAY, HISTEQ_DISPLAY } display_mode;   // siril.h
```

```c
// histogram.c - one histogram per layer, 65536 bins (65535 + 1), zeros skipped
size_t get_histo_size(fits *fit) { ... return (size_t)USHRT_MAX; }
gsl_histogram* computeHisto(fits *fit, int layer) {
    size = get_histo_size(fit);
    gsl_histogram *histo = gsl_histogram_alloc(size + 1);
    gsl_histogram_set_ranges_uniform(histo, 0,
        fit->type == DATA_FLOAT ? 1.0 + 1.0 / size : size + 1);
    ...
        if (buf[i] == 0) continue;           // ushort and float alike
        gsl_histogram_increment(histo_thr, (double) buf[i]);
```

```c
// image_display.c, remap(vport) - the HISTEQ branch
if (gui.rendering_mode == HISTEQ_DISPLAY) {
    compute_histo_for_gfit();
    histo = com.layers_hist[vport];                 // THIS layer's histogram
    hist_nb_bins = gsl_histogram_bins(histo);
    nb_pixels = (double)(gfit.rx * gfit.ry);        // all pixels, zeros included
    index = gui.remap_index[0];
    index[0] = 0;
    hist_sum = gsl_histogram_get(histo, 0);
    for (i = 1; i < hist_nb_bins; i++) {
        hist_sum += gsl_histogram_get(histo, i);
        index[i] = round_to_BYTE((hist_sum / nb_pixels) * UCHAR_MAX_DOUBLE);
    }
    set_viewer_mode_widgets_sensitive(FALSE);       // lo/hi cutoff sliders are OFF
}
...
if (gfit.type == DATA_USHORT)      dst = index[src[src_i]];
else if (gfit.type == DATA_FLOAT)  dst = index[roundf_to_WORD(fsrc[src_i] * USHRT_MAX_SINGLE)];
```

So, in full:

1. **Per layer.** `remap(vport)` runs once per layer with that layer's own histogram; the
   RGB view is composited afterwards (`remaprgb`) from the three 8-bit buffers. On a
   debayered image each colour is equalised **independently**. On an undebayered file (a
   raw opened as CFA, or a FITS from the Conversion tab - the default in Siril 1.x) there is
   one layer: the mosaic is equalised as a single grey image, R/G/B populations and all.
2. **65536 bins.** 16-bit data bins on the ADU value itself; float data is first quantised
   as `round(v * 65535)`. A 12-bit NEF therefore uses bins 0..4095 and the rest stay empty;
   the mapping is the same as binning on the data's own range.
3. **LUT = CDF x 255**, cumulative *including* the bin itself, rounded to a byte;
   `index[0]` forced to 0. Pixels equal to zero are left out of the histogram but still
   count in the denominator - so an image with many dead pixels never reaches 255. On a
   flat there are no zeros and this is moot; we mirror it anyway because it is free.
4. **The cutoff sliders do nothing** in this mode, and the negative / rainbow toggles
   are applied after the LUT. Neither is in scope.

Why it shows what the linear view hides: a flat is a smooth, narrow distribution. The CDF
climbs from 0 to 1 across the vignetting range only, so the LUT is extremely steep there,
and a 0.3 % ripple in the illumination is spread over dozens of grey levels. The price is
that **amplitude information is destroyed by construction**: a 0.5 % pattern and a 10 %
pattern of the same shape render identically, and a perfectly clean flat shows its own
noise at full contrast. That is exactly the split the tool already makes for the residual
map - *the picture shows the shape, the numbers decide* - and the mode has to say so on
screen. **It can never vote.**

## Decisions (taken here; reopen only with a measurement)
