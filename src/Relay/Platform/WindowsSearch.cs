using Relay.Features.FileSearch;

namespace Relay.Platform;

internal static class WindowsSearch
{
    public static bool TrySearch(
        string query,
        IReadOnlyList<string> scopes,
        FileSearchIgnoreList ignore,
        FileSearchFilter filter,
        int cap,
        out IReadOnlyList<FileSearchResult> results)
    {
        results = [];
        var clause = FileSearchAqs.FileNameClause(query);
        if (clause.Length == 0)
            return false;
        var scoped = ScopeClause(scopes);
        var sql = "SELECT TOP " + FileSearchQuery.SoftCap
                  + " System.ItemPathDisplay FROM SystemIndex WHERE "
                  + (scoped.Length == 0 ? clause : "(" + scoped + ") AND (" + clause + ")");
        if (!TryQuery(sql, out var paths))
            return false;
        results = Materialize(paths, ignore, filter, cap);
        return true;
    }

    public static bool TryRecents(
        IReadOnlyList<string> scopes,
        FileSearchIgnoreList ignore,
        FileSearchFilter filter,
        out IReadOnlyList<FileSearchResult> results)
    {
        results = [];
        if (scopes.Count == 0)
            return false;
        var changed = DateTime.Now.AddDays(-3).ToString("yyyy-MM-dd HH:mm:ss");
        var used = DateTime.Now.AddDays(-30).ToString("yyyy-MM-dd HH:mm:ss");
        var sql = "SELECT TOP " + FileSearchQuery.SoftCap
                  + " System.ItemPathDisplay FROM SystemIndex WHERE ("
                  + ScopeClause(scopes) + ") AND (System.DateModified >= '" + changed
                  + "' OR System.DateAccessed >= '" + used + "')";
        if (!TryQuery(sql, out var paths))
            return false;
        results = Materialize(paths, ignore, filter, FileSearchQuery.RecentLimit)
            .OrderByDescending(r => r.Modified ?? DateTime.MinValue)
            .Take(FileSearchQuery.RecentLimit)
            .ToList();
        return true;
    }

    static string ScopeClause(IReadOnlyList<string> scopes) =>
        string.Join(" OR ", scopes
            .Where(Directory.Exists)
            .Select(s => "SCOPE='file:" + s.Replace("'", "''").TrimEnd('\\') + "'"));

    static bool TryQuery(string sql, out List<string> paths)
    {
        paths = [];
        try
        {
            var type = Type.GetTypeFromProgID("ADODB.Connection");
            if (type is null)
                return false;
            dynamic connection = Activator.CreateInstance(type)!;
            connection.Open("Provider=Search.CollatorDSO;Extended Properties=\"Application=Windows\"");
            try
            {
                dynamic recordset = connection.Execute(sql);
                while (recordset is not null && !(bool)recordset.EOF)
                {
                    string? value = null;
                    try { value = recordset.Fields[0].Value as string; }
                    catch (Exception) { }
                    if (!string.IsNullOrWhiteSpace(value))
                        paths.Add(value);
                    recordset.MoveNext();
                    if (paths.Count >= FileSearchQuery.SoftCap)
                        break;
                }

                try { recordset?.Close(); } catch (Exception) { }
            }
            finally
            {
                try { connection.Close(); } catch (Exception) { }
            }

            return true;
        }
        catch (Exception ex)
        {
            Log.Write("windows search: " + ex.Message);
            return false;
        }
    }

    static IReadOnlyList<FileSearchResult> Materialize(
        IEnumerable<string> paths, FileSearchIgnoreList ignore, FileSearchFilter filter, int cap)
    {
        var results = new List<FileSearchResult>();
        foreach (var path in paths)
        {
            if (results.Count >= cap)
                break;
            if (FileSearchQuery.IsExcludedPath(path, ignore))
                continue;
            bool isDir;
            try { isDir = Directory.Exists(path); }
            catch (Exception) { continue; }
            if (!filter.Accepts(path, isDir))
                continue;
            DateTime? modified = null;
            long? size = null;
            try
            {
                modified = isDir ? Directory.GetLastWriteTime(path) : File.GetLastWriteTime(path);
                if (!isDir && File.Exists(path))
                    size = new FileInfo(path).Length;
            }
            catch (Exception) { }

            results.Add(new FileSearchResult(path, Path.GetFileName(path.TrimEnd('\\')), modified, isDir, false, size));
        }

        return results;
    }
}
