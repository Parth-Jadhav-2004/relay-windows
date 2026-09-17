using Microsoft.Data.Sqlite;

namespace Tinycast.Features.Clipboard;

public enum ClipboardKind { Text, Image, File }

public sealed class ClipboardItem
{
    public long Id { get; set; }
    public ClipboardKind Kind { get; set; }
    public string Text { get; set; } = "";
    public string? ImagePath { get; set; }
    public string? FilePath { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool Pinned { get; set; }
    public string? OcrText { get; set; }
    public string? SourceId { get; set; }

    public string Preview => ClipboardPresentation.ListTitle(this);
}

public sealed class ClipboardStore : IDisposable
{
    readonly object _gate = new();
    readonly SqliteConnection _db;
    readonly string _imageRoot;

    public ClipboardStore(string directory)
    {
        Directory.CreateDirectory(directory);
        _imageRoot = Path.Combine(directory, "images");
        Directory.CreateDirectory(_imageRoot);
        var dbPath = Path.Combine(directory, "clipboard.sqlite");
        _db = new SqliteConnection($"Data Source={dbPath};Mode=ReadWriteCreate;Pooling=False;Cache=Shared");
        _db.Open();
        using (var pragmas = _db.CreateCommand())
        {
            pragmas.CommandText = "PRAGMA busy_timeout=5000; PRAGMA journal_mode=WAL;";
            pragmas.ExecuteNonQuery();
        }
        using (var cmd = _db.CreateCommand())
        {
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS items (
                  id INTEGER PRIMARY KEY AUTOINCREMENT,
                  kind TEXT NOT NULL,
                  text TEXT NOT NULL DEFAULT '',
                  image_path TEXT,
                  file_path TEXT,
                  created_at TEXT NOT NULL,
                  pinned INTEGER NOT NULL DEFAULT 0,
                  ocr_text TEXT
                );
                CREATE VIRTUAL TABLE IF NOT EXISTS items_fts USING fts5(text, ocr_text, content='items', content_rowid='id');
                """;
            cmd.ExecuteNonQuery();
        }
        EnsureColumn("source_id", "TEXT");
    }

    void EnsureColumn(string name, string sqlType)
    {
        using (var info = _db.CreateCommand())
        {
            info.CommandText = "PRAGMA table_info(items)";
            using var reader = info.ExecuteReader();
            while (reader.Read())
            {
                if (string.Equals(reader.GetString(1), name, StringComparison.OrdinalIgnoreCase))
                    return;
            }
        }

        using var alter = _db.CreateCommand();
        alter.CommandText = $"ALTER TABLE items ADD COLUMN {name} {sqlType}";
        alter.ExecuteNonQuery();
    }

    public string ImageRoot => _imageRoot;

    public ClipboardItem Insert(
        ClipboardKind kind,
        string text,
        string? imagePath = null,
        string? filePath = null,
        string? sourceId = null)
    {
        lock (_gate)
        {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            INSERT INTO items (kind, text, image_path, file_path, created_at, pinned, source_id)
            VALUES ($kind, $text, $image, $file, $created, 0, $source);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("$kind", kind.ToString());
        cmd.Parameters.AddWithValue("$text", text);
        cmd.Parameters.AddWithValue("$image", (object?)imagePath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$file", (object?)filePath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$created", DateTime.UtcNow.ToString("o"));
        cmd.Parameters.AddWithValue("$source", (object?)sourceId ?? DBNull.Value);
        var id = (long)(cmd.ExecuteScalar() ?? 0L);
        using var fts = _db.CreateCommand();
        fts.CommandText = "INSERT INTO items_fts(rowid, text, ocr_text) VALUES ($id, $text, '');";
        fts.Parameters.AddWithValue("$id", id);
        fts.Parameters.AddWithValue("$text", text);
        try { fts.ExecuteNonQuery(); } catch (SqliteException) { }

        return GetLocked(id) ?? new ClipboardItem
        {
            Id = id,
            Kind = kind,
            Text = text,
            ImagePath = imagePath,
            FilePath = filePath,
            SourceId = sourceId,
            CreatedAt = DateTime.UtcNow,
        };
        }
    }

    public IReadOnlyList<ClipboardItem> Search(string query, int limit = 80)
    {
        lock (_gate)
        {
        using var cmd = _db.CreateCommand();
        if (string.IsNullOrWhiteSpace(query))
        {
            cmd.CommandText = "SELECT * FROM items ORDER BY pinned DESC, created_at DESC LIMIT $limit";
        }
        else
        {
            cmd.CommandText = """
                SELECT items.* FROM items
                JOIN items_fts ON items_fts.rowid = items.id
                WHERE items_fts MATCH $q
                ORDER BY items.pinned DESC, items.created_at DESC
                LIMIT $limit
                """;
            cmd.Parameters.AddWithValue("$q", SanitizeFts(query));
        }

        cmd.Parameters.AddWithValue("$limit", limit);
        try
        {
            var hits = ReadAll(cmd);
            if (hits.Count > 0 || string.IsNullOrWhiteSpace(query))
                return hits;
        }
        catch (SqliteException)
        {
        }

        using var fallback = _db.CreateCommand();
        fallback.CommandText = "SELECT * FROM items WHERE text LIKE $like OR IFNULL(ocr_text,'') LIKE $like ORDER BY pinned DESC, created_at DESC LIMIT $limit";
        fallback.Parameters.AddWithValue("$like", "%" + query + "%");
        fallback.Parameters.AddWithValue("$limit", limit);
        return ReadAll(fallback);
        }
    }

    public void PruneUnpinnedOlderThan(DateTime utcCutoff)
    {
        List<long> ids;
        lock (_gate)
        {
            using var cmd = _db.CreateCommand();
            cmd.CommandText = "SELECT id FROM items WHERE pinned = 0 AND created_at < $cut";
            cmd.Parameters.AddWithValue("$cut", utcCutoff.ToString("o"));
            ids = [];
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                ids.Add(reader.GetInt64(0));
        }

        foreach (var id in ids)
            Delete(id);
    }

    public IReadOnlyList<ClipboardItem> Pinned(int limit = 9)
    {
        lock (_gate)
        {
            using var cmd = _db.CreateCommand();
            cmd.CommandText = "SELECT * FROM items WHERE pinned = 1 ORDER BY created_at DESC LIMIT $limit";
            cmd.Parameters.AddWithValue("$limit", limit);
            return ReadAll(cmd);
        }
    }

    public ClipboardItem? Get(long id)
    {
        lock (_gate)
            return GetLocked(id);
    }

    ClipboardItem? GetLocked(long id)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT * FROM items WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        return ReadAll(cmd).FirstOrDefault();
    }

    public void TogglePin(long id)
    {
        lock (_gate)
        {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "UPDATE items SET pinned = CASE pinned WHEN 1 THEN 0 ELSE 1 END WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
        }
    }

    public void Delete(long id)
    {
        lock (_gate)
        {
        string? imagePath = null;
        using (var lookup = _db.CreateCommand())
        {
            lookup.CommandText = "SELECT image_path FROM items WHERE id = $id";
            lookup.Parameters.AddWithValue("$id", id);
            imagePath = lookup.ExecuteScalar() as string;
        }

        using var cmd = _db.CreateCommand();
        cmd.CommandText = "DELETE FROM items WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
        using var fts = _db.CreateCommand();
        fts.CommandText = "DELETE FROM items_fts WHERE rowid = $id";
        fts.Parameters.AddWithValue("$id", id);
        try { fts.ExecuteNonQuery(); } catch (SqliteException) { }

        DeleteOwnedImage(imagePath);
        }
    }

    public void SetOcr(long id, string text)
    {
        lock (_gate)
        {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "UPDATE items SET ocr_text = $t WHERE id = $id";
        cmd.Parameters.AddWithValue("$t", text);
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
        using var del = _db.CreateCommand();
        del.CommandText = "DELETE FROM items_fts WHERE rowid = $id";
        del.Parameters.AddWithValue("$id", id);
        try { del.ExecuteNonQuery(); } catch (SqliteException) { }
        var item = GetLocked(id);
        if (item is null)
            return;
        using var fts = _db.CreateCommand();
        fts.CommandText = "INSERT INTO items_fts(rowid, text, ocr_text) VALUES ($id, $text, $ocr)";
        fts.Parameters.AddWithValue("$id", id);
        fts.Parameters.AddWithValue("$text", item.Text + " " + text);
        fts.Parameters.AddWithValue("$ocr", text);
        try { fts.ExecuteNonQuery(); } catch (SqliteException) { }
        }
    }

    public bool IsDuplicateText(string text)
    {
        lock (_gate)
        {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT kind, text FROM items ORDER BY id DESC LIMIT 1";
        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            return false;
        if (!Enum.TryParse<ClipboardKind>(reader.GetString(0), out var kind) || kind != ClipboardKind.Text)
            return false;
        return reader.GetString(1) == text;
        }
    }

    void DeleteOwnedImage(string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            return;
        try
        {
            var full = Path.GetFullPath(imagePath);
            var root = Path.GetFullPath(_imageRoot);
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(full))
                return;
            File.Delete(full);
        }
        catch (Exception)
        {
        }
    }

    static string SanitizeFts(string query)
    {
        var cleaned = new string(query.Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c)).ToArray()).Trim();
        if (cleaned.Length == 0)
            return "\"\"";
        return string.Join(" AND ", cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(t => t + "*"));
    }

    static List<ClipboardItem> ReadAll(SqliteCommand cmd)
    {
        using var reader = cmd.ExecuteReader();
        var items = new List<ClipboardItem>();
        while (reader.Read())
        {
            items.Add(new ClipboardItem
            {
                Id = reader.GetInt64(reader.GetOrdinal("id")),
                Kind = Enum.Parse<ClipboardKind>(reader.GetString(reader.GetOrdinal("kind"))),
                Text = reader.GetString(reader.GetOrdinal("text")),
                ImagePath = Optional(reader, "image_path"),
                FilePath = Optional(reader, "file_path"),
                CreatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("created_at"))),
                Pinned = reader.GetInt64(reader.GetOrdinal("pinned")) != 0,
                OcrText = Optional(reader, "ocr_text"),
                SourceId = Optional(reader, "source_id"),
            });
        }

        return items;
    }

    static string? Optional(SqliteDataReader reader, string column)
    {
        try
        {
            var i = reader.GetOrdinal(column);
            return reader.IsDBNull(i) ? null : reader.GetString(i);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    public void Dispose()
    {
        lock (_gate)
            _db.Dispose();
    }

    public void Checkpoint()
    {
        lock (_gate)
        {
            using var cmd = _db.CreateCommand();
            cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
            try { cmd.ExecuteNonQuery(); } catch (SqliteException) { }
        }
    }
}
