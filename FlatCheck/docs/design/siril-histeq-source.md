# Siril 1.4.4 - what the "Histogram" display mode does, in its source

**Covers:** `FlatCheck.Core/Analysis/HistEqAnalyzer.cs` (`HistEqLut.Build` and the
per-layer rule; `RainbowLut`, the false-colour table), which mirrors the code quoted below.
The excerpt is from Siril tag `1.4.4` and stays true for that tag; **it changes only if the
mode starts mirroring another Siril version**, in which case re-read the three source files
at the new tag, update the quotes, and re-check `HistEqLut` and `RainbowLut` against them.

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
   are applied after the LUT. The negative is not in scope; the rainbow is the section below.

## The false colours (the "color-map" toggle), in the same source

Read on 2026-10-26 at the same tag, from `src/gui/image_display.c` and `src/algos/colors.c`.
The toggle is a boolean GAction, `color-map`, read in `remap(vport)` as `color_map color`
(`NORMAL_COLOR`, `RAINBOW_COLOR` in `siril.h`). The table:

```c
// image_display.c
static int make_index_for_rainbow(BYTE index[][3]) {
	int i;
	double h, s, v, r, g, b;

	for (i = 0; i < UCHAR_MAX + 1; i++) {
		r = g = b = (double) i / UCHAR_MAX_DOUBLE;
		rgb_to_hsv(r, g, b, &h, &s, &v);
		double off = 300.0 / 360.0; /* Arbitrary: we want h from 300 to 0 deg */
		h = (off - (double) i * (off / UCHAR_MAX_DOUBLE));
		s = 1.;
		v = 1.; /* Saturation and Value are set to 100% */
		hsv_to_rgb(h, s, v, &r, &g, &b);
		index[i][0] = round_to_BYTE(r * UCHAR_MAX_DOUBLE);
		index[i][1] = round_to_BYTE(g * UCHAR_MAX_DOUBLE);
		index[i][2] = round_to_BYTE(b * UCHAR_MAX_DOUBLE);
	}
	return 0;
}
```

```c
// colors.c - the hue is a fraction of a turn ("all variables are between 0 and 1")
void hsv_to_rgb(double h, double s, double v, double *r, double *g, double *b) {
	double p, q, t, f;
	int i;

	if (h >= 1.0)
		h -= 1.0;
	h *= 6.0;
	i = (int)h;
	f = h - (double)i;
	p = v * (1.0 - s);
	q = v * (1.0 - (s * f));
	t = v * (1.0 - (s * (1.0 - f)));

	switch (i) {
		case 0: *r = v; *g = t; *b = p; break;
		case 1: *r = q; *g = v; *b = p; break;
		case 2: *r = p; *g = v; *b = t; break;
		case 3: *r = p; *g = q; *b = v; break;
		case 4: *r = t; *g = p; *b = v; break;
		case 5:
		default: *r = v; *g = p; *b = q; break;
	}
}
```

```c
// image_display.c, remap(vport) - after the LUT and the negative, per layer
if (color == RAINBOW_COLOR)
	make_index_for_rainbow(rainbow_index);
...
dst_pixel_value = inverted ? UCHAR_MAX - dst_pixel_value : dst_pixel_value;
switch (color) {
	default:
	case NORMAL_COLOR:
		*(guint32*)(dst + dst_index) = dst_pixel_value << 16 | dst_pixel_value << 8 | dst_pixel_value;
		break;
	case RAINBOW_COLOR:
		*(guint32*)(dst + dst_index) = rainbow_index[dst_pixel_value][0] << 16 | rainbow_index[dst_pixel_value][1] << 8 | rainbow_index[dst_pixel_value][2];
}
```

```c
// image_display.c, remaprgb() - the RGB tab takes one byte from each layer's pixel
dst[i] = (bufr[i] & 0xFF0000) | (bufg[i] & 0xFF00) | (bufb[i] & 0xFF);
```

What the code says, beyond ADR 0063's sentence:

1. **The hue runs from 300 degrees at level 0 to 0 at level 255**, saturation and value 1.
   Level 0 is therefore **magenta**, not black: 0 -> (255,0,255), 51 -> (0,0,255),
   102 -> (0,255,255), 153 -> (0,255,0), 204 -> (255,255,0), 255 -> (255,0,0), and in
   between, with the integer sectors of `hsv_to_rgb`, 1 -> (250,0,255), 64 -> (0,65,255),
   128 -> (0,255,125), 254 -> (255,5,0). `R104_TheRainbowTableIsSirils` pins these ten.
2. **The `rgb_to_hsv` call is dead code**: `h`, `s` and `v` are overwritten on the next
   lines. `RainbowLut.Build` does not reproduce it.
3. **Per layer, after everything else.** The table is applied to `dst_pixel_value`, the
   equalised (or otherwise stretched) byte of one layer, after the negative toggle. On an
   undebayered file there is one layer and the whole mosaic is coloured as one.
4. **The RGB tab does not colour the composite.** `remaprgb` masks the R byte of the R
   layer's 32-bit pixel, the G byte of the G layer's, the B byte of the B layer's: with the
   rainbow on, a pixel whose three layers equalised to the same level comes out as that
   level's colour, and a pixel whose layers differ comes out as a mix that is in no row of the
   table. Measured on the Samyang NEF (2026-10-26): off-palette colours wherever the channels'
   shapes differ. That is why FlatCheck's RGB view is never coloured (ADR 0063).

## Decisions (taken here; reopen only with a measurement)

Recorded as ADR 0025: per layer, the LUT, the two resolutions, the composite, no verdict, and
why the picture shows what the linear view hides while destroying amplitude by construction, so
it can never vote. ADR 0063: the false colours on one layer only, never on the composite.
`modes-histeq.md` keeps the reading of the picture.
