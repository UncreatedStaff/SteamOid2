using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SteamOid2.API;
using SteamOid2.Steam;
using SteamOid2.XRI;
using System.Collections.Specialized;
using System.Globalization;
using System.Text;
using System.Web;

namespace SteamOid2;

/// <inheritdoc cref="ISteamOid2Client"/>
public class SteamOid2Client : ISteamOid2Client
{
    private const string Spec = "http://specs.openid.net/auth/2.0";
    private const string IdentitySpec = "http://specs.openid.net/auth/2.0/identifier_select";

    private readonly ILogger<SteamOid2Client>? _logger;
    private readonly IConfiguration? _config;
    private readonly string? _realm;
    private readonly string? _callback;
    private SteamOid2Resource? _discoveredResource;

    /// <summary>
    /// Dependency-injection constructor to create a client from a <paramref name="configuration"/> with a section 'OID2' with properties '<see cref="Realm"/>' and '<see cref="CallbackUri"/>'.
    /// </summary>
    public SteamOid2Client(ILogger<SteamOid2Client> logger, IConfiguration configuration)
    {
        _logger = logger;
        _config = configuration;
    }

    /// <summary>
    /// Non dependency-injection constructor to create a client from a <paramref name="realm"/> and <paramref name="callback"/>.
    /// </summary>
    /// <param name="realm">Defines the domain name used in the Steam login page. Must be the same as the domain of <paramref name="callback"/>.</param>
    /// <param name="callback">Defines the callback URL that the user will be redirected to. Must be the same domain as <paramref name="realm"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="realm"/> or <paramref name="callback"/> was <see langword="null"/>.</exception>
    public SteamOid2Client(string realm, string callback)
    {
        _realm = realm ?? throw new ArgumentNullException(nameof(realm));
        _callback = callback ?? throw new ArgumentNullException(nameof(callback));
    }

    /// <inheritdoc/>
    public string Realm => _config == null ? _realm! : _config.GetSection("OID2")["Realm"] ?? "http://127.0.0.1:80/";

    /// <inheritdoc/>
    public string CallbackUri => _config == null ? _callback! : _config.GetSection("OID2")["CallbackUri"] ?? "http://127.0.0.1:80/login/";

    /// <inheritdoc/>
    public string ContentType => "x-www-urlencoded";

    /// <inheritdoc/>
    public async ValueTask<SteamOid2Resource> GetSteamOid2Resource(bool forceRefresh = false, CancellationToken token = default)
    {
        if (_discoveredResource == null || forceRefresh)
        {
            return await Discover(token).ConfigureAwait(false) ? _discoveredResource! : Constants.BackupSteamOid2Resource;
        }

        return _discoveredResource;
    }
    
    private async Task<bool> Discover(CancellationToken token = default)
    {
        // download the XRI resource from Steam to discover the OpenID Provider Endpoint URI
        HttpRequestMessage message = new HttpRequestMessage(HttpMethod.Get, Constants.SteamXRIProvider);

        using HttpClient httpClient = new HttpClient();

        HttpResponseMessage response = await httpClient.SendAsync(message, token).ConfigureAwait(false);
        string strContent = await response.Content.ReadAsStringAsync(token);
        

        SteamOid2ResourceReader reader = new SteamOid2ResourceReader(strContent);
        SteamOid2Resource? content = await reader.Read(response.Content.ReadAsStream(token), token);

        if (content == null)
        {
            _logger?.LogError($"Unable to get Steam OpenID 2.0 XRDS document from \"{Constants.SteamXRIProvider}\".");
            return false;
        }
        
        _discoveredResource = content;
        return true;
    }

    /// <inheritdoc/>
    public Oid2AuthenticationStatus CheckAuthorizationResponse(string keyValuePairContent, out string? invalidateHandle)
    {
        // read key/value pair list with colon separator
        string[] lines = keyValuePairContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        invalidateHandle = null;
        string? isValid = null;
        for (int i = 0; i < lines.Length; ++i)
        {
            string line = lines[i];
            int space = line.IndexOf(':', StringComparison.Ordinal);
            
            if (space == -1 || line.Length < space + 2)
                continue;
            
            if (string.Compare(line, 0, "invalidate_handle", 0, space, StringComparison.Ordinal) == 0)
                invalidateHandle = line.Substring(space + (line[space + 1] == ' ' ? 2 : 1));
            else if (string.Compare(line, 0, "is_valid", 0, space, StringComparison.Ordinal) == 0)
                isValid = line.Substring(space + (line[space + 1] == ' ' ? 2 : 1));
        }

        // check for isValid:true or false
        if (!string.Equals(isValid, "true", StringComparison.OrdinalIgnoreCase))
        {
            return isValid == null || !string.Equals(isValid, "false", StringComparison.OrdinalIgnoreCase)
                ? Oid2AuthenticationStatus.InvalidResponse

                // reject instead if theres an invalidate handle
                : (string.IsNullOrEmpty(invalidateHandle)
                    ? Oid2AuthenticationStatus.Invalid
                    : Oid2AuthenticationStatus.Reject);
        }
        
        // check for isValid:true or false
        return !string.IsNullOrEmpty(invalidateHandle) ? Oid2AuthenticationStatus.Reject : Oid2AuthenticationStatus.Valid;
    }

    /// <inheritdoc/>
    public SteamOid2Response ParseIdReponse(Uri uri)
    {
        // converts the ?www=xxx&yyy=zzz section of the URI to a dictionary.
        NameValueCollection queryParameters = HttpUtility.ParseQueryString(uri.Query);

        // invalidate handle is passed when a handle needs to be rejected.
        string? assocHandle = queryParameters["openid.assoc_handle"] ?? queryParameters["openid.invalidate_handle"];

        string? mode = queryParameters["openid.mode"];
        if (!string.Equals(mode, "id_res", StringComparison.Ordinal))
        {
            if (string.Equals(mode, "cancel", StringComparison.Ordinal))
                return SteamOid2Response.Cancelled;
            if (string.Equals(mode, "error", StringComparison.Ordinal))
                return new SteamOid2Response(Oid2Status.Error, 0ul, queryParameters["openid.error"] ?? "Unknown error", assocHandle);

            return new SteamOid2Response(Oid2Status.InvalidResponse, 0ul, $"Invalid mode: {mode}", assocHandle);
        }

        if (!string.Equals(queryParameters["openid.return_to"], CallbackUri, StringComparison.Ordinal))
            return new SteamOid2Response(Oid2Status.InvalidResponse, 0ul, $"Mismatched return_to: {mode}", assocHandle);
        
        if (string.IsNullOrEmpty(queryParameters["openid.response_nonce"]))
            return new SteamOid2Response(Oid2Status.InvalidResponse, 0ul, "Missing nonce", assocHandle);
        
        if (string.IsNullOrEmpty(queryParameters["openid.signed"]))
            return new SteamOid2Response(Oid2Status.InvalidResponse, 0ul, "Missing signed", assocHandle);
        
        if (string.IsNullOrEmpty(queryParameters["openid.sig"]))
            return new SteamOid2Response(Oid2Status.InvalidResponse, 0ul, "Missing sig", assocHandle);

        if (string.IsNullOrEmpty(assocHandle))
            return new SteamOid2Response(Oid2Status.InvalidResponse, 0ul, "Missing assoc_handle", assocHandle);

        // claimed_id format: https://steamcommunity.com/openid/id/76500000000000000

        string? steamId = queryParameters["openid.claimed_id"];
        if (steamId == null)
            return new SteamOid2Response(Oid2Status.InvalidResponse, 0ul, "Missing claimed_id", assocHandle);
        if (!steamId.StartsWith(Constants.SteamClaimedIdPrefix, StringComparison.Ordinal))
            return new SteamOid2Response(Oid2Status.InvalidResponse, 0ul, $"Invalid claimed_id: {steamId}", assocHandle);

        steamId = steamId.Substring(Constants.SteamClaimedIdPrefix.Length);

        if (!ulong.TryParse(steamId, NumberStyles.Number, CultureInfo.InvariantCulture, out ulong s64) || !SteamUtility.IsIndividualSteam64(s64))
            return new SteamOid2Response(Oid2Status.InvalidResponse, 0ul, $"Invalid Individual Steam64 ID: {steamId}", assocHandle);

        return new SteamOid2Response(Oid2Status.Success, s64, null, assocHandle);
    }

    /// <inheritdoc/>
    public async ValueTask<Uri> GetLoginUri(CancellationToken token = default)
    {
        SteamOid2Resource resource = await GetSteamOid2Resource(false, token).ConfigureAwait(false);

        string url = resource.OPEndpointURL +
                     "?openid.ns=" + Spec +
                     "&openid.claimed_id=" + IdentitySpec +
                     "&openid.identity=" + IdentitySpec +
                     "&openid.mode=checkid_setup" +
                     "&openid.realm=" + Realm +
                     "&openid.return_to=" + CallbackUri;

        return new Uri(url);
    }

    /// <inheritdoc/>
    public async ValueTask<Uri> GetAuthorizeUri(Uri idResponseUri, CancellationToken token = default)
    {
        SteamOid2Resource resource = await GetSteamOid2Resource(false, token).ConfigureAwait(false);

        NameValueCollection queryParameters = HttpUtility.ParseQueryString(idResponseUri.Query);

        StringBuilder uriBuilder = new StringBuilder(idResponseUri.Query.Length + 63);
        uriBuilder.Append(resource.OPEndpointURL);
        bool first = true;
        foreach (string key in queryParameters)
        {
            if (first)
            {
                uriBuilder.Append('?');
                first = false;
            }
            else
                uriBuilder.Append('&');

            uriBuilder.Append(key)
                .Append('=')
                .Append(key.Equals("openid.mode", StringComparison.Ordinal)
                    ? "check_authentication"
                    : Uri.EscapeDataString(queryParameters[key]!));
        }

        return new Uri(uriBuilder.ToString());
    }
}
