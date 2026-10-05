// SPDX-License-Identifier: GPL-3.0-or-later
//
// Copyright (C) 2026 Tudor Carean
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Windows.Media;
using ImageVisualCompare.Core;
using ImageVisualCompare.Wpf.Imaging;
using Xunit.Abstractions;

namespace ImageVisualCompare.Tests;

/// <summary>
/// The figures measured on real pairs of images, read from docs/validation.json so the document
/// and the test cannot drift apart (ADR 0005). One kind of case, <c>pair</c>: what the application
/// does when it opens a pair, without a window. Both files are probed and compared, the pair is
/// loaded with the padding question answered yes, then dropped and released, and the private
/// memory is read before the load, after it and after the release.
///
/// Not in the gate: trait Category=Validation; the files are copies on one machine. A case whose
/// file is absent returns early with a message (xUnit 2 has no dynamic skip). A case with
/// <c>expected</c> null is unmeasured and only prints what it found. Every case prints its
/// figures as one JSON line (test output, and the file named by IVC_VALIDATION_DUMP when set),
/// which is how validation.json gets filled: never by editing the expected values to match (the
/// kit's rule "No test edited to pass").
/// </summary>
[Trait("Category", "Validation")]
public class ValidationTests(ITestOutputHelper output)
{
    private static readonly JsonSerializerOptions Readable = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private static string ValidationJson => Path.Combine(TestData.RepoDir(), "docs", "validation.json");

    public static IEnumerable<object[]> Cases()
    {
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(ValidationJson));
        return doc.RootElement.GetProperty("cases").EnumerateArray()
            .Select(c => new object[] { c.GetProperty("id").GetString()! })
            .ToList();
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void ValidationCase_MatchesRecordedFigures(string id)
    {
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(ValidationJson));
        JsonElement c = doc.RootElement.GetProperty("cases").EnumerateArray()
            .Single(e => e.GetProperty("id").GetString() == id);
        Assert.Equal("pair", c.GetProperty("kind").GetString());

        string beforeName = c.GetProperty("before").GetString()!;
        string afterName = c.GetProperty("after").GetString()!;
        string? before = TestData.Large(beforeName);
        string? after = TestData.Large(afterName);
        if (before is null || after is null)
        {
            output.WriteLine($"NOT MEASURED {id}: {(before is null ? beforeName : afterName)} is absent under {TestData.LargeDir()}");
            return;
        }

        Dictionary<string, object> measured = Measure(before, after);

        string line = id + " " + JsonSerializer.Serialize(measured, Readable);
        output.WriteLine("MEASURED " + line);
        string? dump = Environment.GetEnvironmentVariable("IVC_VALIDATION_DUMP");
        if (!string.IsNullOrEmpty(dump))
        {
            File.AppendAllText(dump, line + "\n");
        }

        if (!c.TryGetProperty("expected", out JsonElement expected) || expected.ValueKind == JsonValueKind.Null)
        {
            output.WriteLine($"UNMEASURED {id}: no expected figures recorded yet");
            return;
        }

        double tolerance = c.GetProperty("tolerance").GetDouble();
        var failures = new List<string>();
        foreach (JsonProperty p in expected.EnumerateObject())
        {
            if (!measured.TryGetValue(p.Name, out object? got))
            {
                failures.Add($"{p.Name}: not measured");
                continue;
            }

            switch (p.Value.ValueKind)
            {
                case JsonValueKind.Number when got is int or long:
                    if (p.Value.GetInt64() != Convert.ToInt64(got, CultureInfo.InvariantCulture))
                    {
                        failures.Add($"{p.Name}: expected {p.Value.GetInt64()} got {got}");
                    }

                    break;
                case JsonValueKind.Number when got is double have:
                    double want = p.Value.GetDouble();
                    if (Math.Abs(have - want) > tolerance)
                    {
                        failures.Add($"{p.Name}: expected {want} got {have} (tolerance {tolerance})");
                    }

                    break;
                case JsonValueKind.True or JsonValueKind.False when got is bool b:
                    if (b != p.Value.GetBoolean())
                    {
                        failures.Add($"{p.Name}: expected {p.Value.GetBoolean()} got {b}");
                    }

                    break;
                case JsonValueKind.String:
                    if (!string.Equals(p.Value.GetString(), Convert.ToString(got, CultureInfo.InvariantCulture), StringComparison.Ordinal))
                    {
                        failures.Add($"{p.Name}: expected '{p.Value.GetString()}' got '{got}'");
                    }

                    break;
                default:
                    failures.Add($"{p.Name}: expected {p.Value} got {got} (a different kind)");
                    break;
            }
        }

        Assert.True(failures.Count == 0, $"{id} drifted from docs/validation.json:\n  " + string.Join("\n  ", failures));
    }

    /// <summary>Every figure a case may guard, named by what it describes; see docs/validation.md.</summary>
    private static Dictionary<string, object> Measure(string beforePath, string afterPath)
    {
        var figures = new Dictionary<string, object>(StringComparer.Ordinal);
        var loader = new RoutingImageLoader();

        // The headers first, as the application reads them: this also loads the two codecs, so
        // their own cost is not counted as the pair's below.
        ImageInfo beforeInfo = loader.Probe(beforePath);
        ImageInfo afterInfo = loader.Probe(afterPath);
        Describe(figures, "before", beforeInfo, beforePath);
        Describe(figures, "after", afterInfo, afterPath);

        PadPlan pad = ImagePairValidator.Inspect(beforeInfo, afterInfo);
        figures["pad.width"] = pad.Width;
        figures["pad.height"] = pad.Height;
        figures["pad.requiresPadding"] = pad.RequiresPadding;
        figures["pad.dimensions"] = pad.Dimensions;

        MemoryHousekeeping.ReleaseImageBuffers();
        long baseline = MemoryHousekeeping.Snapshot().PrivateBytes;

        LoadAndDescribe(loader, beforePath, afterPath, baseline, figures);

        // LoadAndDescribe has returned, so nothing references the pair any more.
        MemoryHousekeeping.ReleaseImageBuffers();
        figures["memory.privateAfterReleaseDeltaMb"] = Megabytes(MemoryHousekeeping.Snapshot().PrivateBytes - baseline);
        return figures;
    }

    /// <summary>
    /// Loads the pair, records what it is and what it costs, and returns holding no reference to
    /// it, so that the release which follows can give its buffers back. Not inlined, for the same
    /// reason.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void LoadAndDescribe(
        IImageLoader loader, string beforePath, string afterPath, long baseline, Dictionary<string, object> figures)
    {
        var stopwatch = Stopwatch.StartNew();

        // ImagePairLoader awaits with ConfigureAwait(false) throughout and the answer is already
        // complete, so waiting for it here cannot deadlock.
        ImagePairLoadResult result = new ImagePairLoader(loader)
            .LoadAsync(beforePath, afterPath, progress: null, confirmPadding: _ => Task.FromResult(true))
            .GetAwaiter()
            .GetResult();
        stopwatch.Stop();

        figures["decode.seconds"] = Math.Round(stopwatch.Elapsed.TotalSeconds, 2);
        figures["load.success"] = result.IsSuccess;
        if (result.Pair is { } pair)
        {
            figures["load.displayBytes"] = pair.DisplayBytes;
            figures["load.beforeIsBgra32"] = pair.Before.Bitmap.Format == PixelFormats.Bgra32;
            figures["load.afterIsBgra32"] = pair.After.Bitmap.Format == PixelFormats.Bgra32;
            figures["load.frozen"] = pair.Before.Bitmap.IsFrozen && pair.After.Bitmap.IsFrozen;
            figures["load.beforeFormat"] = pair.Before.Info.FormatDescription;
            figures["load.afterFormat"] = pair.After.Info.FormatDescription;
        }

        figures["memory.privateDeltaMb"] = Megabytes(MemoryHousekeeping.Snapshot().PrivateBytes - baseline);
    }

    private static void Describe(Dictionary<string, object> figures, string prefix, ImageInfo info, string path)
    {
        figures[prefix + ".width"] = info.PixelWidth;
        figures[prefix + ".height"] = info.PixelHeight;
        figures[prefix + ".bitsPerChannel"] = info.BitsPerChannel;
        figures[prefix + ".channels"] = info.Channels.ToString();
        figures[prefix + ".format"] = info.FormatDescription;
        figures[prefix + ".displayBytes"] = info.DisplayBytes;
        figures[prefix + ".fileBytes"] = new FileInfo(path).Length;
    }

    /// <summary>Bytes as megabytes of 1 048 576 bytes, one decimal.</summary>
    private static double Megabytes(long bytes) => Math.Round(bytes / 1048576.0, 1);
}
