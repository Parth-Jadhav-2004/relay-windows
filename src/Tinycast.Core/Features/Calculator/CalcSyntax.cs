namespace Tinycast.Features.Calculator;

/// <summary>Dims joining words so the values they join read first.</summary>
public static class CalcSyntax
{
    static readonly HashSet<string> Connectors = new(StringComparer.OrdinalIgnoreCase)
    {
        "to", "of", "off", "on", "as", "from", "ago", "at", "tip", "ratio", "average", "avg",
        "mean", "sum", "total", "round", "nearest", "and", "is", "what", "the", "next", "last",
        "+", "-", "×", "÷", "^", "→", "->", "mod",
    };

    public static IReadOnlyList<(string Text, bool Dim)> Highlight(string text)
    {
        var words = text.Split(' ');
        var pieces = new List<(string, bool)>(words.Length * 2);
        for (var i = 0; i < words.Length; i++)
        {
            if (i > 0)
                pieces.Add((" ", false));
            pieces.Add((words[i], IsConnector(words[i], i, words)));
        }

        return pieces;
    }

    static bool IsConnector(string word, int index, string[] words)
    {
        if (Connectors.Contains(word))
            return true;
        if (!word.Equals("in", StringComparison.OrdinalIgnoreCase) || index <= 0 || index + 1 >= words.Length)
            return false;
        return !words[index + 1].Equals("in", StringComparison.OrdinalIgnoreCase);
    }
}
