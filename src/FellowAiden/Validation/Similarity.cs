namespace FellowAiden;

/// <summary>
/// Port of Python's <c>difflib.SequenceMatcher(None, a, b).ratio()</c> — the
/// Ratcliff/Obershelp "gestalt pattern matching" ratio used for fuzzy title
/// matching. Returns a value in [0, 1].
/// </summary>
public static class Similarity
{
    public static double Ratio(string a, string b)
    {
        var total = a.Length + b.Length;
        if (total == 0)
        {
            return 1.0;
        }

        // Map each character in b to the indices where it occurs.
        var b2j = new Dictionary<char, List<int>>();
        for (var j = 0; j < b.Length; j++)
        {
            if (!b2j.TryGetValue(b[j], out var list))
            {
                list = new List<int>();
                b2j[b[j]] = list;
            }

            list.Add(j);
        }

        var matched = CountMatches(a, b2j, 0, a.Length, 0, b.Length);
        return 2.0 * matched / total;
    }

    private static int CountMatches(
        string a, Dictionary<char, List<int>> b2j, int alo, int ahi, int blo, int bhi)
    {
        var (besti, bestj, bestsize) = FindLongestMatch(a, b2j, alo, ahi, blo, bhi);
        if (bestsize == 0)
        {
            return 0;
        }

        return bestsize
            + CountMatches(a, b2j, alo, besti, blo, bestj)
            + CountMatches(a, b2j, besti + bestsize, ahi, bestj + bestsize, bhi);
    }

    private static (int Besti, int Bestj, int Bestsize) FindLongestMatch(
        string a, Dictionary<char, List<int>> b2j, int alo, int ahi, int blo, int bhi)
    {
        int besti = alo, bestj = blo, bestsize = 0;
        var j2len = new Dictionary<int, int>();

        for (var i = alo; i < ahi; i++)
        {
            var newj2len = new Dictionary<int, int>();
            if (b2j.TryGetValue(a[i], out var indices))
            {
                foreach (var j in indices)
                {
                    if (j < blo)
                    {
                        continue;
                    }

                    if (j >= bhi)
                    {
                        break;
                    }

                    var k = (j2len.TryGetValue(j - 1, out var prev) ? prev : 0) + 1;
                    newj2len[j] = k;
                    if (k > bestsize)
                    {
                        besti = i - k + 1;
                        bestj = j - k + 1;
                        bestsize = k;
                    }
                }
            }

            j2len = newj2len;
        }

        return (besti, bestj, bestsize);
    }
}
