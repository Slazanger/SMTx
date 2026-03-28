namespace SMTx.Eve.Connectors.Storage;

public interface ICharacterSessionStore
{
    IReadOnlyList<CharacterSessionRecord> ListCharacters();

    CharacterSessionRecord? Get(long characterId);

    Task UpsertAsync(CharacterSessionRecord record, CancellationToken cancellationToken = default);

    Task RemoveAsync(long characterId, CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);

    Task LoadAsync(CancellationToken cancellationToken = default);
}
