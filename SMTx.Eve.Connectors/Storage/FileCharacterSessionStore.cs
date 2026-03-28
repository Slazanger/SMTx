#if !NET8_0_BROWSER
using System.Collections.Concurrent;
using System.Text.Json;

namespace SMTx.Eve.Connectors.Storage;

/// <summary>Persists character tokens to a JSON file (Desktop / Android / iOS).</summary>
public sealed class FileCharacterSessionStore : ICharacterSessionStore
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ConcurrentDictionary<long, CharacterSessionRecord> _byId = new();

    public FileCharacterSessionStore(string filePath)
    {
        _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _byId.Clear();
            if (!File.Exists(_filePath))
                return;

            await using var stream = File.OpenRead(_filePath);
            var list = await JsonSerializer.DeserializeAsync<List<CharacterSessionRecord>>(stream, JsonOpts, cancellationToken).ConfigureAwait(false);
            if (list == null)
                return;

            foreach (var r in list)
                _byId[r.CharacterId] = r;
        }
        finally
        {
            _lock.Release();
        }
    }

    public IReadOnlyList<CharacterSessionRecord> ListCharacters()
    {
        return _byId.Values.OrderBy(r => r.CharacterName).ToList();
    }

    public CharacterSessionRecord? Get(long characterId)
    {
        return _byId.TryGetValue(characterId, out var r) ? r : null;
    }

    public async Task UpsertAsync(CharacterSessionRecord record, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _byId[record.CharacterId] = record;
            await SaveUnlockedAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task RemoveAsync(long characterId, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _byId.TryRemove(characterId, out _);
            await SaveUnlockedAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _byId.Clear();
            await SaveUnlockedAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task SaveUnlockedAsync(CancellationToken cancellationToken)
    {
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var list = _byId.Values.ToList();
        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, list, JsonOpts, cancellationToken).ConfigureAwait(false);
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };
}
#endif
