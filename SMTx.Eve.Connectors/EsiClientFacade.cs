using EVEStandard;
using EVEStandard.Models;
using EVEStandard.Models.API;
using EVEStandard.Models.SSO;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SMTx.Eve.Connectors.Options;
using SMTx.Eve.Connectors.Storage;

namespace SMTx.Eve.Connectors;

/// <summary>Thin wrapper over <see cref="EVEStandardAPI"/>; authenticated calls require an explicit character id and session from the store.</summary>
public sealed class EsiClientFacade
{
    private readonly ICharacterSessionStore _store;
    private readonly EveSsoService _sso;
    private readonly ILogger<EsiClientFacade> _logger;
    public EVEStandardAPI Api { get; }

    public EsiClientFacade(
        IOptions<EveEsiOptions> esiOptions,
        ICharacterSessionStore store,
        EveSsoService sso,
        ILogger<EsiClientFacade> logger,
        ILoggerFactory? loggerFactory = null)
    {
        _store = store;
        _sso = sso;
        _logger = logger;
        var o = esiOptions.Value;
        Api = new EVEStandardAPI(o.UserAgent, o.DataSource, o.CompatibilityDate, o.HttpTimeout);
        if (loggerFactory != null)
            Api.AddLogging(loggerFactory);
    }

    public async Task<CharacterInfo> GetCharacterPublicInfoAsync(long characterId, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        var dto = await Api.Character.GetCharacterPublicInfoAsync(characterId).ConfigureAwait(false);
        return dto.Model;
    }

    public async Task<CorporationInfo> GetCorporationPublicInfoAsync(int corporationId, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        var dto = await Api.Corporation.GetCorporationInfoAsync(corporationId).ConfigureAwait(false);
        return dto.Model;
    }

    public async Task<Alliance> GetAlliancePublicInfoAsync(int allianceId, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        var dto = await Api.Alliance.GetAllianceInfoAsync(allianceId).ConfigureAwait(false);
        return dto.Model;
    }

    public async Task<CharacterLocation> GetCharacterLocationAsync(long characterId, CancellationToken cancellationToken = default)
    {
        var auth = await CreateAuthAsync(characterId, cancellationToken).ConfigureAwait(false);
        var dto = await Api.Location.GetCharacterLocationAsync(auth, ifNoneMatch: null).ConfigureAwait(false);
        return dto.Model;
    }

    public async Task<CharacterShip> GetCurrentShipAsync(long characterId, CancellationToken cancellationToken = default)
    {
        var auth = await CreateAuthAsync(characterId, cancellationToken).ConfigureAwait(false);
        var dto = await Api.Location.GetCurrentShipAsync(auth, ifNoneMatch: null).ConfigureAwait(false);
        return dto.Model;
    }

    public async Task<string?> GetSolarSystemNameAsync(long solarSystemId, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        var dto = await Api.Universe.GetSolarSystemInfoAsync(solarSystemId, language: null, ifNoneMatch: null).ConfigureAwait(false);
        return dto.Model?.Name;
    }

    public async Task<string?> GetStationNameAsync(long stationId, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        var dto = await Api.Universe.GetStationInfoAsync(stationId, ifNoneMatch: null).ConfigureAwait(false);
        return dto.Model?.Name;
    }

    public async Task<string?> GetStructureNameAsync(long characterId, long structureId, CancellationToken cancellationToken = default)
    {
        var auth = await CreateAuthAsync(characterId, cancellationToken).ConfigureAwait(false);
        var dto = await Api.Universe.GetStructureInfoAsync(auth, structureId, ifNoneMatch: null).ConfigureAwait(false);
        return dto.Model?.Name;
    }

    public async Task<string?> GetShipTypeNameAsync(long typeId, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        var dto = await Api.Universe.GetTypeInfoAsync(typeId, language: null, ifNoneMatch: null).ConfigureAwait(false);
        return dto.Model?.Name;
    }

    /// <summary>Builds <see cref="AuthDTO"/> for EVEStandard; refreshes access token if near expiry.</summary>
    public async Task<AuthDTO> CreateAuthAsync(long characterId, CancellationToken cancellationToken = default)
    {
        var session = _store.Get(characterId) ?? throw new InvalidOperationException($"No EVE session for character {characterId}.");
        if (session.AccessTokenExpiresUtc <= DateTime.UtcNow.AddMinutes(2))
        {
            try
            {
                await _sso.RefreshAccessTokenAsync(characterId, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not refresh token for {CharacterId}", characterId);
                throw;
            }
            session = _store.Get(characterId) ?? throw new InvalidOperationException($"Session lost for {characterId}.");
        }

        return new AuthDTO
        {
            CharacterId = characterId,
            Scopes = session.Scopes,
            AccessToken = new AccessTokenDetails
            {
                AccessToken = session.AccessToken,
                ExpiresUtc = session.AccessTokenExpiresUtc
            }
        };
    }
}
