using System.Text;
using LevelDB;

namespace Brickwork.Exporters.Foundry;

/// <summary>
/// Read-only access to a Foundry ClassicLevel directory (<c>keyEncoding: utf8</c>,
/// <c>valueEncoding: json</c>).
/// </summary>
/// <remarks>
/// LevelDB uses an exclusive process lock (and exclusively opens MANIFEST / the active log).
/// There is no true multi-process read-only mode in stock LevelDB, and a directory copy
/// cannot be opened while Foundry has that world loaded. Leave the world (Foundry setup
/// screen is enough) so the lock is released.
/// </remarks>
public sealed class FoundryLevelDbStore : IDisposable
{
    private readonly DB _db;
    private bool _disposed;

    private FoundryLevelDbStore(DB db)
    {
        _db = db;
    }

    /// <summary>
    /// Opens <paramref name="directory"/> for reading. Throws
    /// <see cref="FoundryWorldDataLockedException"/> when another process holds the DB lock
    /// (typically a loaded Foundry world).
    /// </summary>
    public static FoundryLevelDbStore OpenReadOnly(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"Foundry LevelDB directory not found: {directory}");
        }

        try
        {
            var db = new DB(directory, new Options { CreateIfMissing = false });
            return new FoundryLevelDbStore(db);
        }
        catch (Exception ex) when (IsLikelyLockFailure(ex))
        {
            throw new FoundryWorldDataLockedException(
                "Could not open the Foundry world database. Leave that world in Foundry (return to the setup screen) or close Foundry, then try again.",
                ex);
        }
    }

    public string? TryGet(string key)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        try
        {
            return _db.Get(key);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Enumerates all entries as UTF-8 string keys and JSON string values.</summary>
    public IEnumerable<KeyValuePair<string, string>> EnumerateEntries()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        using var iterator = _db.CreateIterator();
        iterator.SeekToFirst();
        while (iterator.Valid())
        {
            var key = iterator.StringKey() ?? Encoding.UTF8.GetString(iterator.Key() ?? []);
            var value = iterator.StringValue() ?? Encoding.UTF8.GetString(iterator.Value() ?? []);
            yield return new KeyValuePair<string, string>(key, value);
            iterator.Next();
        }
    }

    public IEnumerable<KeyValuePair<string, string>> EnumerateByKeyPrefix(string prefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);

        foreach (var entry in EnumerateEntries())
        {
            if (entry.Key.StartsWith(prefix, StringComparison.Ordinal))
            {
                yield return entry;
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _db.Dispose();
    }

    private static bool IsLikelyLockFailure(Exception ex)
    {
        var message = ex.Message ?? string.Empty;
        return message.Contains("lock", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("LOCK", StringComparison.Ordinal) ||
               message.Contains("Resource temporarily unavailable", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("IO error", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class FoundryWorldDataLockedException : InvalidOperationException
{
    public FoundryWorldDataLockedException(string message, Exception? inner = null)
        : base(message, inner)
    {
    }
}
