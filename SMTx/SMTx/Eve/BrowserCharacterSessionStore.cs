#if NET8_0_BROWSER
using System.Collections.Concurrent;
using System.Runtime.Versioning;
using System.Text.Json;
using SMTx.Eve.Connectors.Storage;

namespace SMTx.Eve;

/// <summary>Persists EVE tokens in localStorage (XSS-sensitive; see plan).</summary>
[SupportedOSPlatform("browser")]
public sealed class BrowserCharacterSessionStore : ICharacterSessionStore
{
    private const string StorageKey = "smtx_eve_characters_v1";
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ConcurrentDictionary<long, CharacterSessionRecord> _byId = new();

    public Task LoadAsync(CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        _byId.Clear();
        var json = WasmLocalStorageInterop.GetItem(StorageKey);
        if (string.IsNullOrEmpty(json))
            return Task.CompletedTask;

        try
        {
            var list = JsonSerializer.Deserialize<List<CharacterSessionRecord>>(json, JsonOpts);
            if (list == null)
                return Task.CompletedTask;
            foreach (var r in list)
                _byId[r.CharacterId] = r;
        }
        catch
        {
            // ignore corrupt storage
        }

        return Task.CompletedTask;
    }

    public IReadOnlyList<CharacterSessionRecord> ListCharacters() =>
        (IReadOnlyList<CharacterSessionRecord>)_byId.Values.OrderBy(r => r.CharacterName).ToList();

    public CharacterSessionRecord? Get(long characterId) =>
        _byId.TryGetValue(characterId, out var r) ? r : null;

    public async Task UpsertAsync(CharacterSessionRecord record, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _byId[record.CharacterId] = record;
            PersistUnlocked();
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
            PersistUnlocked();
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
            PersistUnlocked();
        }
        finally
        {
            _lock.Release();
        }
    }

    private void PersistUnlocked()
    {
        var list = _byId.Values.ToList();
        var json = JsonSerializer.Serialize(list, JsonOpts);
        WasmLocalStorageInterop.SetItem(StorageKey, json);
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };
}
#endif
