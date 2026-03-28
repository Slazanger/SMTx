using System.Net.Http;
using System.Text.Json;
using EVEStandard;
using EVEStandard.Models.SSO;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SMTx.Eve.Connectors.Auth;
using SMTx.Eve.Connectors.Options;
using SMTx.Eve.Connectors.Security;
using SMTx.Eve.Connectors.Storage;

namespace SMTx.Eve.Connectors;

public sealed class EveSsoService
{
    private readonly IOptions<EveOAuthOptions> _oauthOptions;
    private readonly IOptions<EveEsiOptions> _esiOptions;
    private readonly ICharacterSessionStore _store;
    private readonly ILogger<EveSsoService> _logger;
    private readonly IEveAuthorizationUiCoordinator? _uiCoordinator;
    private readonly IBrowserOAuthPkceStore? _browserPkce;
    private readonly bool _useBrowserSplitFlow;

    /// <summary>True when running WASM browser OAuth (split redirect flow).</summary>
    public bool UsesBrowserSplitFlow => _useBrowserSplitFlow;

    public EveSsoService(
        IOptions<EveOAuthOptions> oauthOptions,
        IOptions<EveEsiOptions> esiOptions,
        ICharacterSessionStore store,
        ILogger<EveSsoService> logger,
        IEveAuthorizationUiCoordinator? uiCoordinator = null,
        IBrowserOAuthPkceStore? browserPkce = null,
        bool useBrowserSplitFlow = false)
    {
        _oauthOptions = oauthOptions;
        _esiOptions = esiOptions;
        _store = store;
        _logger = logger;
        _uiCoordinator = uiCoordinator;
        _browserPkce = browserPkce;
        _useBrowserSplitFlow = useBrowserSplitFlow;
    }

    private SSOv2 CreateSso()
    {
        // EVEStandard 4.x SSOv2 only assigns its static HttpClient when the ctor's httpClient argument is null.
        // Passing our injected client leaves that field unset → NullReferenceException inside VerifyAuthorizationForPKCEAuthAsync.
        return new SSOv2(
            _esiOptions.Value.DataSource,
            _oauthOptions.Value.RedirectUri,
            _oauthOptions.Value.ClientId,
            string.IsNullOrWhiteSpace(_oauthOptions.Value.ClientSecret) ? null : _oauthOptions.Value.ClientSecret,
            httpClient: null);
    }

    /// <summary>Desktop / mobile: run full PKCE login and persist the character session.</summary>
    public async Task<EveCharacterSummary?> AddCharacterAsync(CancellationToken cancellationToken = default)
    {
        if (_useBrowserSplitFlow)
            throw new InvalidOperationException("On browser, call BeginBrowserAuthorization then TryCompleteBrowserAuthorizationAsync.");

        if (_uiCoordinator == null)
            throw new InvalidOperationException("IEveAuthorizationUiCoordinator is not configured for this platform.");

        var oauth = _oauthOptions.Value;
        var state = Guid.NewGuid().ToString("N");
        var verifier = PkceHelper.CreateCodeVerifier();
        var challenge = PkceHelper.CreateCodeChallenge(verifier);

        var sso = CreateSso();
        var scopes = oauth.Scopes?.Count > 0 ? oauth.Scopes : new List<string>();
        var authorizeUri = new Uri(sso.AuthorizeToSSOPKCEUri(state, challenge, scopes));

        var callbackUri = await _uiCoordinator.StartAuthorizationAndWaitForCallbackAsync(authorizeUri, cancellationToken).ConfigureAwait(false);

        return await CompleteAuthorizationFromCallbackAsync(callbackUri, verifier, state, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Browser: persist PKCE and navigate away to CCP (caller supplies navigation).</summary>
    public (Uri AuthorizeUri, string State, string Verifier) PrepareBrowserAuthorization()
    {
        if (!_useBrowserSplitFlow)
            throw new InvalidOperationException("PrepareBrowserAuthorization is only for browser split flow.");

        var oauth = _oauthOptions.Value;
        var state = Guid.NewGuid().ToString("N");
        var verifier = PkceHelper.CreateCodeVerifier();
        var challenge = PkceHelper.CreateCodeChallenge(verifier);
        _browserPkce?.SaveVerifierForState(state, verifier);

        var sso = CreateSso();
        var scopes = oauth.Scopes?.Count > 0 ? oauth.Scopes : new List<string>();
        var url = sso.AuthorizeToSSOPKCEUri(state, challenge, scopes);
        return (new Uri(url), state, verifier);
    }

    /// <summary>Browser: after redirect back, complete login if query contains code/state.</summary>
    public async Task<EveCharacterSummary?> TryCompleteBrowserAuthorizationAsync(Uri currentPage, CancellationToken cancellationToken = default)
    {
        if (!_useBrowserSplitFlow)
            return null;

        var query = ParseQuery(currentPage.Query);
        if (!query.TryGetValue("code", out var code) || string.IsNullOrEmpty(code))
            return null;
        if (!query.TryGetValue("state", out var state) || string.IsNullOrEmpty(state))
            return null;

        var verifier = _browserPkce?.GetVerifierForState(state);
        if (string.IsNullOrEmpty(verifier))
        {
            _logger.LogWarning("Browser OAuth: no PKCE verifier for state {State}", state);
            return null;
        }

        try
        {
            return await CompleteAuthorizationFromCallbackAsync(currentPage, verifier, state, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _browserPkce?.ClearVerifierForState(state);
        }
    }

    private async Task<EveCharacterSummary?> CompleteAuthorizationFromCallbackAsync(Uri callbackUri, string verifier, string expectedState, CancellationToken cancellationToken)
    {
        var query = ParseQuery(callbackUri.Query);
        if (!query.TryGetValue("code", out var code) || string.IsNullOrEmpty(code))
            throw new InvalidOperationException("OAuth callback missing code.");
        if (!query.TryGetValue("state", out var state) || state != expectedState)
            throw new InvalidOperationException("OAuth callback state mismatch.");

        var sso = CreateSso();
        AccessTokenDetails token;
        try
        {
            token = await sso.VerifyAuthorizationForPKCEAuthAsync(code, verifier).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token exchange failed");
            throw;
        }

        if (string.IsNullOrEmpty(token.AccessToken))
            throw new InvalidOperationException("Token response missing access_token.");

        var details = await sso.GetCharacterDetailsAsync(token.AccessToken).ConfigureAwait(false);

        var scopesJoined = details.Scopes != null && details.Scopes.Count > 0
            ? string.Join(' ', details.Scopes)
            : string.Join(' ', _oauthOptions.Value.Scopes ?? new List<string>());

        var record = new CharacterSessionRecord
        {
            CharacterId = details.CharacterId,
            CharacterName = details.CharacterName ?? "",
            RefreshToken = token.RefreshToken ?? "",
            AccessToken = token.AccessToken,
            AccessTokenExpiresUtc = token.ExpiresUtc,
            Scopes = scopesJoined
        };

        await _store.UpsertAsync(record, cancellationToken).ConfigureAwait(false);
        return new EveCharacterSummary(record.CharacterId, record.CharacterName);
    }

    public async Task RemoveCharacterAsync(long characterId, CancellationToken cancellationToken = default) =>
        await _store.RemoveAsync(characterId, cancellationToken).ConfigureAwait(false);

    public async Task LogoutAllAsync(CancellationToken cancellationToken = default) =>
        await _store.ClearAsync(cancellationToken).ConfigureAwait(false);

    public async Task RestoreSessionsAsync(CancellationToken cancellationToken = default)
    {
        await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
        foreach (var c in _store.ListCharacters())
        {
            if (string.IsNullOrEmpty(c.RefreshToken))
                continue;
            if (c.AccessTokenExpiresUtc > DateTime.UtcNow.AddMinutes(2))
                continue;

            try
            {
                await RefreshAccessTokenAsync(c.CharacterId, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Removing character {Id} after failed token refresh", c.CharacterId);
                await _store.RemoveAsync(c.CharacterId, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public async Task RefreshAccessTokenAsync(long characterId, CancellationToken cancellationToken = default)
    {
        var session = _store.Get(characterId) ?? throw new InvalidOperationException($"No session for character {characterId}.");
        if (string.IsNullOrEmpty(session.RefreshToken))
            throw new InvalidOperationException("No refresh token.");

        var sso = CreateSso();
        var scopes = _oauthOptions.Value.Scopes?.Count > 0 ? _oauthOptions.Value.Scopes : null;
        AccessTokenDetails token;
        try
        {
            token = await sso.GetNewPKCEAccessAndRefreshTokenAsync(session.RefreshToken, scopes).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            await HandleRefreshFailure(characterId, ex, cancellationToken).ConfigureAwait(false);
            throw;
        }
        catch (JsonException ex)
        {
            await HandleRefreshFailure(characterId, ex, cancellationToken).ConfigureAwait(false);
            throw;
        }

        if (string.IsNullOrEmpty(token.AccessToken))
        {
            await _store.RemoveAsync(characterId, cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException("Refresh returned no access token.");
        }

        session.AccessToken = token.AccessToken;
        session.AccessTokenExpiresUtc = token.ExpiresUtc;
        if (!string.IsNullOrEmpty(token.RefreshToken))
            session.RefreshToken = token.RefreshToken;

        await _store.UpsertAsync(session, cancellationToken).ConfigureAwait(false);
    }

    private async Task HandleRefreshFailure(long characterId, Exception ex, CancellationToken cancellationToken)
    {
        _logger.LogWarning(ex, "Refresh failed for {CharacterId}", characterId);
        await _store.RemoveAsync(characterId, cancellationToken).ConfigureAwait(false);
    }

    public CharacterSessionRecord? GetSession(long characterId) => _store.Get(characterId);

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(query))
            return d;
        var q = query.StartsWith('?') ? query[1..] : query;
        foreach (var part in q.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var i = part.IndexOf('=');
            if (i <= 0)
                continue;
            var key = Uri.UnescapeDataString(part[..i]);
            var val = Uri.UnescapeDataString(part[(i + 1)..]);
            d[key] = val;
        }
        return d;
    }
}

public sealed record EveCharacterSummary(long CharacterId, string CharacterName);
