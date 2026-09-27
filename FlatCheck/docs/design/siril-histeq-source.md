# Siril 1.4.4 - what the "Histogram" display mode does, in its source

**Covers:** `FlatCheck.Core/Analysis/HistEqAnalyzer.cs` (`HistEqLut.Build` and the
per-layer rule), which mirrors the code quoted below. The excerpt is from Siril tag
`1.4.4` and stays true for that tag; **it changes only if the mode starts mirroring another
Siril version**, in which case re-read the two source files at the new tag, update the
quotes, and re-check `HistEqLut` against them.

Moved here from the implementation plan of the `--histeq` mode when that plan was deleted, then
cut on 2026-09-27 to what neither CLAUDE.md nor a decision record says (ADR 0051): the plan was
a to-do list, this is the measurement it rested on. ADR 0025 and `modes-histeq.md` ("5.
Histogram equalisation") state the consequences; this file keeps the quoted code so the claim
can be checked against the tag it was read from.

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

ADR 0025 states the rule this code amounts to (one histogram per layer, 65536 bins, zeros
skipped but counted, the inclusive CDF times 255 with `index[0] = 0`, float quantised as
`round(v * 65535)`). What the code adds to that sentence:

1. **Per layer.** `remap(vport)` runs once per layer with that layer's own histogram; the
   RGB view is composited afterwards (`remaprgb`) from the three 8-bit buffers. On an
   undebayered file (a raw opened as CFA, or a FITS from the Conversion tab - the default in
   Siril 1.x) there is one layer: the mosaic is equalised as a single grey image, R/G/B
   populations and all.
2. **65536 bins** on the ADU value itself. A 12-bit NEF therefore uses bins 0..4095 and the
   rest stay empty; the mapping is the same as binning on the data's own range.
3. **Zeros.** Left out of the histogram but counted in the denominator, so an image with many
   dead pixels never reaches 255. On a flat there are no zeros and this is moot; we mirror it
   anyway because it is free.
4. **The cutoff sliders do nothing** in this mode, and the negative / rainbow toggles
   are applied after the LUT. Neither is in scope.

## Decisions (taken here; reopen only with a measurement)

Recorded as ADR 0025: per layer, the LUT, the two resolutions, the composite, no verdict, and
why the picture shows what the linear view hides while destroying amplitude by construction, so
it can never vote. `modes-histeq.md` keeps the reading of the picture.
