using System.Text;

namespace CopyWcs;

/// <summary>Headerul HDU-ului primar, ca lista de carduri brute de 80 de caractere.</summary>
internal sealed class FitsHeader
{
    private const int CardSize = 80;
    private const int BlockSize = 2880;

    private readonly List<string> _cards = new();

    /// <summary>Offsetul in fisier unde incepe zona de date (dupa headerul paddat).</summary>
    private long _dataOffset;

    public IEnumerable<string> Keywords => _cards.Select(KeywordOf).Where(k => k.Length > 0);

    public static FitsHeader ReadPrimary(string path)
    {
        using var fs = File.OpenRead(path);
        return ReadFrom(fs);
    }

    public static FitsHeader ReadFrom(Stream stream)
    {
        var h = new FitsHeader();

        var block = new byte[BlockSize];
        var sawEnd = false;
        long consumed = 0;

        while (!sawEnd)
        {
            var read = ReadFully(stream, block);
            if (read < BlockSize)
                throw new InvalidDataException("Fisier trunchiat sau nu este FITS valid (cardul END lipseste).");
            consumed += BlockSize;

            for (var i = 0; i < BlockSize; i += CardSize)
            {
                var card = Encoding.ASCII.GetString(block, i, CardSize);
                if (KeywordOf(card) == "END")
                {
                    sawEnd = true;
                    break;
                }
                h._cards.Add(card);
            }
        }

        if (h._cards.Count == 0 || KeywordOf(h._cards[0]) != "SIMPLE")
            throw new InvalidDataException("Fisierul nu incepe cu SIMPLE - nu pare FITS.");

        h._dataOffset = consumed;
        return h;
    }

    /// <summary>Citeste pana umple bufferul sau da de EOF: Stream.Read poate intoarce mai putini bytes decat cere apelul, chiar in mijlocul fluxului.</summary>
    private static int ReadFully(Stream stream, byte[] buffer)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = stream.Read(buffer, total, buffer.Length - total);
            if (read == 0) break;
            total += read;
        }
        return total;
    }

    private static string KeywordOf(string card) =>
        card.Length < 8 ? string.Empty : card[..8].Trim();

    public bool Contains(string key) => IndexOf(key) >= 0;

    private int IndexOf(string key)
    {
        for (var i = 0; i < _cards.Count; i++)
            if (string.Equals(KeywordOf(_cards[i]), key, StringComparison.Ordinal))
                return i;
        return -1;
    }

    public string? GetCard(string key)
    {
        var i = IndexOf(key);
        return i < 0 ? null : _cards[i];
    }

    /// <summary>Valoare intreaga. Sigur pentru NAXIS* si alte chei numerice.</summary>
    public int GetInt(string key)
    {
        var card = GetCard(key);
        if (card is null) return -1;

        var eq = card.IndexOf('=');
        if (eq < 0) return -1;

        var value = card[(eq + 1)..];
        var slash = value.IndexOf('/');
        if (slash >= 0) value = value[..slash];

        return int.TryParse(value.Trim(), out var n) ? n : -1;
    }

    /// <summary>Inlocuieste cardul daca exista, altfel il adauga la final.</summary>
    public void SetCard(string key, string rawCard)
    {
        var card = Pad(rawCard);
        var i = IndexOf(key);
        if (i >= 0) _cards[i] = card;
        else _cards.Add(card);
    }

    public void AddHistory(string text)
    {
        var body = text.Length > 72 ? text[..72] : text;
        _cards.Add(Pad("HISTORY " + body));
    }

    private static string Pad(string card) =>
        card.Length >= CardSize ? card[..CardSize] : card.PadRight(CardSize);

    /// <summary>
    /// Scrie headerul curent urmat de zona de date copiata din <paramref name="dataSource"/>.
    /// Daca tinta coincide cu sursa de date, se scrie intai intr-un fisier temporar
    /// (headerul poate creste peste blocul initial de 2880, deci nu se poate patch-ui in loc).
    /// </summary>
    public void WriteWithDataFrom(string dataSource, string target)
    {
        var inPlace = Path.GetFullPath(dataSource) == Path.GetFullPath(target);
        var temp = inPlace ? target + ".tmp" : target;

        using (var input = File.OpenRead(dataSource))
        using (var output = File.Create(temp))
        {
            foreach (var card in _cards)
                output.Write(Encoding.ASCII.GetBytes(Pad(card)));

            output.Write(Encoding.ASCII.GetBytes("END".PadRight(CardSize)));

            // Padding cu spatii pana la urmatoarea granita de 2880.
            var remainder = (int)(output.Position % BlockSize);
            if (remainder != 0)
            {
                var fill = new byte[BlockSize - remainder];
                Array.Fill(fill, (byte)' ');
                output.Write(fill);
            }

            input.Seek(_dataOffset, SeekOrigin.Begin);
            input.CopyTo(output);
        }

        if (inPlace) File.Move(temp, target, overwrite: true);
    }
}
