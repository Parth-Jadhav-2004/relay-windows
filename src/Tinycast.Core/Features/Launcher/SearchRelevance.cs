using System.Globalization;
using System.Text;

namespace Tinycast.Features.Launcher;

public enum FuzzyTier
{
    Exact,
    Prefix,
    WordStart,
    Substring,
    Subsequence,
}

public readonly record struct FuzzyMatch(
    FuzzyTier Tier,
    int Offset,
    int QueryLength,
    int CandidateLength,
    int Spread)
{
    public bool IsLiteral => Tier != FuzzyTier.Subsequence;
    public bool IsAnchored => Tier is FuzzyTier.Exact or FuzzyTier.Prefix;

    public int RawScore => Tier switch
    {
        FuzzyTier.Exact => 100_000,
        FuzzyTier.Prefix => 90_000 - CandidateLength,
        FuzzyTier.WordStart => 80_000 - CandidateLength,
        FuzzyTier.Substring => 70_000 - CandidateLength,
        _ => Spread,
    };
}

public static class FuzzyMatcher
{
    public readonly record struct Query(string Text, char[] Characters)
    {
        public bool IsEmpty => Text.Length == 0;

        public static Query Parse(string raw)
        {
            var text = Normalized(raw);
            return new Query(text, text.ToCharArray());
        }
    }

    public static string Normalized(string value)
    {
        var folded = value.Normalize(NormalizationForm.FormC).ToLowerInvariant();
        var builder = new StringBuilder(folded.Length);
        foreach (var c in folded)
        {
            if (char.GetUnicodeCategory(c) == UnicodeCategory.Format)
                continue;
            builder.Append(c);
        }

        return builder.ToString();
    }

    public static FuzzyMatch? Match(string query, string candidate) =>
        Match(Query.Parse(query), candidate);

    public static FuzzyMatch? Match(Query query, string candidate)
    {
        var q = query.Text;
        var c = Normalized(candidate);
        var length = c.Length;
        if (q.Length == 0)
            return new FuzzyMatch(FuzzyTier.Exact, 0, 0, length, 0);
        if (c == q)
            return new FuzzyMatch(FuzzyTier.Exact, 0, query.Characters.Length, length, 0);
        if (c.StartsWith(q, StringComparison.Ordinal))
            return new FuzzyMatch(FuzzyTier.Prefix, 0, query.Characters.Length, length, 0);
        var index = c.IndexOf(q, StringComparison.Ordinal);
        if (index >= 0)
        {
            if (!IsWordStart(c, index))
            {
                var start = index + 1;
                while (start <= c.Length - q.Length)
                {
                    var next = c.IndexOf(q, start, StringComparison.Ordinal);
                    if (next < 0)
                        break;
                    if (IsWordStart(c, next))
                    {
                        index = next;
                        break;
                    }
                    start = next + 1;
                }
            }

            var tier = IsWordStart(c, index) ? FuzzyTier.WordStart : FuzzyTier.Substring;
            return new FuzzyMatch(tier, index, query.Characters.Length, length, 0);
        }

        var spread = SubsequenceScore(query.Characters, c);
        return spread is null
            ? null
            : new FuzzyMatch(FuzzyTier.Subsequence, 0, query.Characters.Length, length, spread.Value);
    }

    public static int? Score(string query, string candidate) => Match(query, candidate)?.RawScore;

    public static int ReferenceSpread(int queryLength)
    {
        if (queryLength <= 0)
            return 1;
        return 13 + (queryLength - 1) + 3 * queryLength * (queryLength - 1) / 2;
    }

    static bool IsWordStart(string s, int index)
    {
        if (index <= 0)
            return true;
        var before = s[index - 1];
        return !char.IsLetterOrDigit(before);
    }

    static int? SubsequenceScore(char[] q, string c)
    {
        var qi = 0;
        var score = 0;
        var run = 0;
        var prev = -2;
        var ci = 0;
        char? previous = null;
        foreach (var ch in c)
        {
            if (qi < q.Length && ch == q[qi])
            {
                var bonus = 1;
                if (ci == prev + 1)
                {
                    run++;
                    bonus += run * 3;
                }
                else
                {
                    run = 0;
                }

                if (ci == 0)
                    bonus += 12;
                else if (previous is char p && !char.IsLetterOrDigit(p))
                    bonus += 8;
                score += bonus;
                prev = ci;
                qi++;
                if (qi == q.Length)
                    break;
            }

            previous = ch;
            ci++;
        }

        return qi == q.Length ? score : null;
    }
}

public enum SearchRole
{
    Technical = 0,
    Owner = 1,
    Translation = 2,
    Name = 3,
    UserAlias = 4,
}

public enum SearchLooseness
{
    Fuzzy,
    Literal,
    Exact,
}

public readonly record struct SearchAlias(string Text, SearchRole Role, SearchLooseness Looseness)
{
    public static SearchAlias Name(string text) => new(text, SearchRole.Name, SearchLooseness.Fuzzy);
    public static SearchAlias Translation(string text) => new(text, SearchRole.Translation, SearchLooseness.Fuzzy);
    public static SearchAlias Owner(string text) => new(text, SearchRole.Owner, SearchLooseness.Literal);
    public static SearchAlias Technical(string text) => new(text, SearchRole.Technical, SearchLooseness.Literal);
    public static SearchAlias User(string text) => new(text, SearchRole.UserAlias, SearchLooseness.Literal);

    public bool Accepts(FuzzyTier tier) => Looseness switch
    {
        SearchLooseness.Fuzzy => true,
        SearchLooseness.Literal => tier != FuzzyTier.Subsequence,
        _ => tier == FuzzyTier.Exact,
    };
}

public sealed class SearchFields
{
    public List<SearchAlias> Aliases { get; } = [];

    public SearchFields() { }

    public SearchFields(params SearchAlias[] aliases) => Aliases.AddRange(aliases);

    public void Add(SearchAlias alias) => Aliases.Add(alias);
}

public static class SearchRelevance
{
    public const int ProtectionFloor = 6_500;
    public const int PoolTop = 3_100;
    public const int PoolBottom = 600;
    public const int ShapeSpan = 99;
    public const int UsageCeiling = 3_000;

    public static int? Cell(SearchRole role, FuzzyTier tier) => (role, tier) switch
    {
        (SearchRole.UserAlias, FuzzyTier.Exact) => 7_000,
        (SearchRole.Name, FuzzyTier.Exact) => ProtectionFloor,
        (SearchRole.UserAlias, FuzzyTier.Prefix) => PoolTop,
        (SearchRole.Name, FuzzyTier.Prefix) => 3_000,
        (SearchRole.Translation, FuzzyTier.Exact) => 2_700,
        (SearchRole.Owner, FuzzyTier.Exact) => 2_500,
        (SearchRole.Name, FuzzyTier.WordStart) => 2_400,
        (SearchRole.Translation, FuzzyTier.Prefix) => 2_200,
        (SearchRole.Owner, FuzzyTier.Prefix) => 2_000,
        (SearchRole.Name, FuzzyTier.Substring) => 1_800,
        (SearchRole.Translation, FuzzyTier.WordStart) => 1_700,
        (SearchRole.Owner, FuzzyTier.WordStart) => 1_500,
        (SearchRole.Technical, FuzzyTier.Exact) => 1_400,
        (SearchRole.Translation, FuzzyTier.Substring) => 1_200,
        (SearchRole.Owner, FuzzyTier.Substring) => 1_100,
        (SearchRole.Name, FuzzyTier.Subsequence) => 1_000,
        (SearchRole.Technical, FuzzyTier.Prefix) => 900,
        (SearchRole.Translation, FuzzyTier.Subsequence) => 800,
        (SearchRole.Technical, FuzzyTier.WordStart) => 700,
        (SearchRole.Technical, FuzzyTier.Substring) => PoolBottom,
        (SearchRole.UserAlias, _) => null,
        (SearchRole.Owner, FuzzyTier.Subsequence) => null,
        (SearchRole.Technical, FuzzyTier.Subsequence) => null,
        _ => null,
    };

    public static int Shape(FuzzyMatch match)
    {
        if (match.CandidateLength <= 0)
            return 0;
        var coverage = Math.Min(1, match.QueryLength / (double)match.CandidateLength);
        var positional = match.Tier == FuzzyTier.Subsequence
            ? Math.Min(1, match.Spread / (double)FuzzyMatcher.ReferenceSpread(match.QueryLength))
            : 1 / (1 + match.Offset / 4.0);
        return (int)Math.Round(ShapeSpan * (0.6 * coverage + 0.4 * positional));
    }

    public static int? Quality(string query, SearchFields fields) =>
        Quality(FuzzyMatcher.Query.Parse(query), fields);

    public static int? Quality(FuzzyMatcher.Query query, SearchFields fields)
    {
        if (query.IsEmpty)
            return 0;
        int? best = null;
        foreach (var alias in fields.Aliases)
        {
            var match = FuzzyMatcher.Match(query, alias.Text);
            if (match is null || !alias.Accepts(match.Value.Tier))
                continue;
            var role = alias.Role == SearchRole.UserAlias && !match.Value.IsAnchored
                ? SearchRole.Translation
                : alias.Role;
            var cell = Cell(role, match.Value.Tier);
            if (cell is null)
                continue;
            var score = cell.Value + Shape(match.Value);
            best = best is int current ? Math.Max(current, score) : score;
        }

        return best;
    }

    public static int Total(int quality, int usage) => quality + Math.Clamp(usage, 0, UsageCeiling - 1);

    public static bool P1Holds => ProtectionFloor > PoolTop + ShapeSpan + (UsageCeiling - 1);
    public static bool P2Holds => 100 > ShapeSpan;
    public static bool P3Holds => PoolBottom + (UsageCeiling - 1) > PoolTop + ShapeSpan;
}
