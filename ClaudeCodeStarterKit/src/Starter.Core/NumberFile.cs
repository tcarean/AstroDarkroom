using System.Globalization;

namespace Starter.Core;

/// <summary>R-003: one number per line, invariant culture; blank lines and '#' comments are ignored.</summary>
public static class NumberFile
{
    /// <summary>
    /// Reads every number in a numbers file, in file order. A line that is not a number throws a
    /// <see cref="FormatException"/> that names its line number, counting blank and comment lines.
    /// </summary>
    /// <param name="reader">The file's text, read to its end; not null. The caller disposes it.</param>
    /// <returns>The numbers in file order; empty when the file holds none.</returns>
    public static IReadOnlyList<double> Parse(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        List<double> values = [];
        string? line;
        int lineNumber = 0;
        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;
            string trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                continue;
            }

            if (!double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            {
                throw new FormatException($"Line {lineNumber}: '{trimmed}' is not a number.");
            }

            values.Add(value);
        }

        return values;
    }
}
