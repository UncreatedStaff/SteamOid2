using SteamOid2.API;
using SteamOid2.Steam;
using SteamOid2.XRI;
using System.Collections.Specialized;
using System.Globalization;
using System.Text;
using System.Web;
#if NETFRAMEWORK
using System.Net.Http;
#else
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
#endif

namespace SteamOid2;

/// <inheritdoc cref="ISteamOid2Client"/>
public class SteamOid2Client : ISteamOid2Client
{
    private const string Spec = "http://specs.openid.net/auth/2.0";
    private const string IdentitySpec = "http://specs.openid.net/auth/2.0/identifier_select";

#if !NETFRAMEWORK
    private readonly ILogger<SteamOid2Client>? _logger;
    private readonly IConfiguration? _config;
#endif
    private readonly string? _realm;
    private readonly string? _callback;
    private SteamOid2Resource? _discoveredResource;

    /// <summary>
    /// Creates a <see cref="ISteamOid2Client"/> that requires you to pass the realm and callback when you call <see cref="GetLoginUri(string, string, CancellationToken)"/> instead of at construction time.
    /// </summary>
    public SteamOid2Client()
    {

    }

    /// <summary>
    /// Non dependency-injection constructor to create a client from a <paramref name="realm"/> and <paramref name="callback"/>.
    /// </summary>
    /// <param name="realm">Defines the domain name used in the Steam login page. Must be the same as the domain of <paramref name="callback"/>.</param>
    /// <param name="callback">Defines the callback URL that the user will be redirected to. Must be the same domain as <paramref name="realm"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="realm"/> or <paramref name="callback"/> was <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Callback must be part of the same domain as realm.</exception>
    public SteamOid2Client(string realm, string callback)
    {
        _realm = realm ?? throw new ArgumentNullException(nameof(realm));
        _callback = callback ?? throw new ArgumentNullException(nameof(callback));
        if (!_callback.StartsWith(_realm, StringComparison.Ordinal))
            throw new ArgumentException("Callback must be part of the same domain as realm.", nameof(_callback));
    }

#if !NETFRAMEWORK
    /// <summary>
    /// Creates a <see cref="ISteamOid2Client"/> that requires you to pass the realm and callback when you call <see cref="GetLoginUri(string, string, CancellationToken)"/> instead of at construction time.
    /// </summary>
    /// <remarks>This constructor is not available in .NET Framework.</remarks>
    public SteamOid2Client(ILogger<SteamOid2Client>? logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Dependency-injection constructor to create a client from a <paramref name="configuration"/> with a section 'OID2' with properties '<see cref="Realm"/>' and '<see cref="CallbackUri"/>'.
    /// </summary>
    /// <remarks>This constructor is not available in .NET Framework.</remarks>
    /// <exception cref="ArgumentException">Callback must be part of the same domain as realm.</exception>
    public SteamOid2Client(ILogger<SteamOid2Client>? logger, IConfiguration configuration) : this(logger)
    {
        _config = configuration ?? throw new ArgumentNullException(nameof(configuration));
        if (!CallbackUri.StartsWith(Realm, StringComparison.Ordinal))
            throw new ArgumentException("Callback must be a part of the same domain as realm.", nameof(_callback));
    }

    /// <summary>
    /// Non dependency-injection constructor to create a client from a <paramref name="realm"/> and <paramref name="callback"/>.
    /// </summary>
    /// <remarks>This constructor is not available in .NET Framework.</remarks>
    /// <param name="realm">Defines the domain name used in the Steam login page. Must be the same as the domain of <paramref name="callback"/>.</param>
    /// <param name="callback">Defines the callback URL that the user will be redirected to. Must be the same domain as <paramref name="realm"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="realm"/> or <paramref name="callback"/> was <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Callback must be part of the same domain as realm.</exception>
    public SteamOid2Client(string realm, string callback, ILogger<SteamOid2Client>? logger) : this(realm, callback)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public string Realm => _config == null
        ? _realm ?? throw new InvalidOperationException("Realm was not passed in constructor.")
        : _config.GetSection("OID2")["Realm"] ?? "http://127.0.0.1:80/";

    /// <inheritdoc/>
    public string CallbackUri => _config == null
        ? _callback ?? throw new InvalidOperationException("Callback was not passed in constructor.")
        : _config.GetSection("OID2")["CallbackUri"] ?? "http://127.0.0.1:80/login/";
#else
    /// <inheritdoc/>
    public string Realm => _realm ?? throw new InvalidOperationException("Realm was not passed in constructor.");

    /// <inheritdoc/>
    public string CallbackUri => _callback ?? throw new InvalidOperationException("Callback was not passed in constructor.");
#endif

    /// <inheritdoc/>
    public string ContentType => "x-www-urlencoded";

#if NETFRAMEWORK
    /// <inheritdoc/>
    public virtual ValueTask<SteamOid2Resource> GetSteamOid2Resource(bool forceRefresh = false, CancellationToken token = default)
    {
        if (_discoveredResource != null && !forceRefresh)
            return new ValueTask<SteamOid2Resource>(_discoveredResource);

        return new ValueTask<SteamOid2Resource>(DiscoverIntl(this, token));

        static async Task<SteamOid2Resource> DiscoverIntl(SteamOid2Client client, CancellationToken token)
        {
            return await client.Discover(token).ConfigureAwait(false) ? client._discoveredResource! : Constants.BackupSteamOid2Resource;
        }
    }
#else
    /// <inheritdoc/>
    public async ValueTask<SteamOid2Resource> GetSteamOid2Resource(bool forceRefresh = false, CancellationToken token = default)
    {
        if (_discoveredResource == null || forceRefresh)
        {
            return await Discover(token).ConfigureAwait(false) ? _discoveredResource! : Constants.BackupSteamOid2Resource;
        }

        return _discoveredResource;
    }
#endif

    protected virtual async Task<bool> Discover(CancellationToken token = default)
    {
        // download the XRI resource from Steam to discover the OpenID Provider Endpoint URI
        HttpRequestMessage message = new HttpRequestMessage(HttpMethod.Get, Constants.SteamXRIProvider);

        using HttpClient httpClient = new HttpClient();

        HttpResponseMessage response = await httpClient.SendAsync(message, token).ConfigureAwait(false);

        SteamOid2ResourceReader reader = new SteamOid2ResourceReader();
        SteamOid2Resource? content = await reader.Read(
#if NET5_0_OR_GREATER
            await response.Content.ReadAsStreamAsync(token)
#else
            await response.Content.ReadAsStreamAsync()
#endif
            , token);

        if (content == null)
        {
#if !NETFRAMEWORK
            _logger?.LogError($"Unable to get Steam OpenID 2.0 XRDS document from \"{Constants.SteamXRIProvider}\".");
#else
            Console.Error.WriteLine($"Unable to get Steam OpenID 2.0 XRDS document from \"{Constants.SteamXRIProvider}\".");
#endif
            return false;
        }
        
        _discoveredResource = content;
        return true;
    }
#if NETFRAMEWORK
    private static readonly char[] SplitChars = [ '\n' ];
#endif

    /// <inheritdoc/>
    public virtual Oid2AuthenticationStatus CheckAuthorizationResponse(string keyValuePairContent, out string? invalidateHandle)
    {
        // read key/value pair list with colon separator

#if NET8_0_OR_GREATER
        ReadOnlySpan<char> span = keyValuePairContent;
        int lineCount = span.Count('\n');
        Span<Range> lines = lineCount > 32 ? new Range[lineCount] : stackalloc Range[lineCount];
        lineCount = span.Split(lines, '\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
#else
#if NETFRAMEWORK
        string[] lines = keyValuePairContent.Split(SplitChars, StringSplitOptions.RemoveEmptyEntries);
#else
        string[] lines = keyValuePairContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
#endif
        int lineCount = lines.Length;
#endif
        invalidateHandle = null;
        ReadOnlySpan<char> isValid = default;
        for (int i = 0; i < lineCount; ++i)
        {
#if NET8_0_OR_GREATER
            ReadOnlySpan<char> line = span[lines[i]];
#else
            ReadOnlySpan<char> line = lines[i].AsSpan();
#endif
            if (line.Length == 0)
                continue;
            if (line[0] == '\r')
                line = line.Slice(1);
            if (line[line.Length - 1] == '\r')
                line = line.Slice(0, line.Length - 1);

            int space = line.IndexOf(':');
            
            if (space == -1 || line.Length < space + 2)
                continue;

            if (line.Slice(0, space).Equals("invalidate_handle".AsSpan(), StringComparison.Ordinal))
            {
                ReadOnlySpan<char> invHandle = line.Slice(space + (line[space + 1] == ' ' ? 2 : 1));
#if NETFRAMEWORK
                invalidateHandle = invHandle.ToString();
#else
                invalidateHandle = new string(invHandle);
#endif
                if (isValid.Length > 0)
                    break;
            }
            else if (line.Slice(0, space).Equals("is_valid".AsSpan(), StringComparison.Ordinal))
            {
                isValid = line.Slice(space + (line[space + 1] == ' ' ? 2 : 1));
                if (invalidateHandle is not null)
                    break;
            }
        }

        // check for isValid:true or false
        if (isValid.Length == 0 || !isValid.Equals("true".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            return isValid.Length == 0 || !isValid.Equals("false".AsSpan(), StringComparison.OrdinalIgnoreCase)
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
    public SteamOid2Response ParseIdReponse(Uri uri) => ParseIdReponse(CallbackUri.AsSpan(), uri);

    /// <inheritdoc/>
    public virtual SteamOid2Response ParseIdReponse(ReadOnlySpan<char> expectedCallbackUri, Uri uri)
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

        if (!expectedCallbackUri.Equals(queryParameters["openid.return_to"].AsSpan(), StringComparison.Ordinal))
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

    private const string OidStrNs = "?openid.ns=";
    private const string OidClaimedId = "&openid.claimed_id=";
    private const string OidIdentity = "&openid.identity=";
    private const string OidMode = "&openid.mode=checkid_setup";
    private const string OidRealm = "&openid.realm=";
    private const string OidCallback = "&openid.return_to=";

    private static string MakeUrl(ReadOnlySpan<char> realmUri, ReadOnlySpan<char> callbackUri, ReadOnlySpan<char> opEndpointUrl)
    {
        ReadOnlySpan<char> oidStrNs = OidStrNs.AsSpan();
        ReadOnlySpan<char> oidClaimedId = OidClaimedId.AsSpan();
        ReadOnlySpan<char> oidIdentity = OidIdentity.AsSpan();
        ReadOnlySpan<char> oidMode = OidMode.AsSpan();
        ReadOnlySpan<char> oidRealm = OidRealm.AsSpan();
        ReadOnlySpan<char> oidCallback = OidCallback.AsSpan();
        ReadOnlySpan<char> spec = Spec.AsSpan();
        ReadOnlySpan<char> identitySpec = IdentitySpec.AsSpan();

        int len = oidStrNs.Length + oidClaimedId.Length + oidIdentity.Length + oidMode.Length + oidRealm.Length + oidCallback.Length
                  + spec.Length + identitySpec.Length * 2 + realmUri.Length + callbackUri.Length + opEndpointUrl.Length;
        Span<char> newText = stackalloc char[len];
        int index = 0;
        opEndpointUrl.CopyTo(newText.Slice(index, opEndpointUrl.Length));
        index += opEndpointUrl.Length;
        oidStrNs.CopyTo(newText.Slice(index, oidStrNs.Length));
        index += oidStrNs.Length;
        spec.CopyTo(newText.Slice(index, spec.Length));
        index += spec.Length;
        oidClaimedId.CopyTo(newText.Slice(index, oidClaimedId.Length));
        index += oidClaimedId.Length;
        identitySpec.CopyTo(newText.Slice(index, identitySpec.Length));
        index += identitySpec.Length;
        oidIdentity.CopyTo(newText.Slice(index, oidIdentity.Length));
        index += oidIdentity.Length;
        identitySpec.CopyTo(newText.Slice(index, identitySpec.Length));
        index += identitySpec.Length;
        oidMode.CopyTo(newText.Slice(index, oidMode.Length));
        index += oidMode.Length;
        oidRealm.CopyTo(newText.Slice(index, oidRealm.Length));
        index += oidRealm.Length;
        realmUri.CopyTo(newText.Slice(index, realmUri.Length));
        index += realmUri.Length;
        oidCallback.CopyTo(newText.Slice(index, oidCallback.Length));
        index += oidCallback.Length;
        callbackUri.CopyTo(newText.Slice(index));

#if NETFRAMEWORK
        return newText.ToString();
#else
        return new string(newText);
#endif
    }

    /// <inheritdoc/>
    public ValueTask<Uri> GetLoginUri(CancellationToken token = default) => GetLoginUri(Realm, CallbackUri, token);

#if NETFRAMEWORK
    /// <inheritdoc/>
    public virtual ValueTask<Uri> GetLoginUri(string realmUri, string callbackUri, CancellationToken token = default)
    {
        if (!callbackUri.StartsWith(realmUri, StringComparison.Ordinal))
            throw new ArgumentException("Callback must be part of the same domain as realm.", nameof(_callback));
        ValueTask<SteamOid2Resource> resx = GetSteamOid2Resource(false, token);
        if (!resx.IsCompleted)
            return new ValueTask<Uri>(GetLoginUriIntl(realmUri, callbackUri, resx));

        string url = MakeUrl(realmUri.AsSpan(), callbackUri.AsSpan(), resx.Result.OPEndpointURL.AsSpan());
        return new ValueTask<Uri>(new Uri(url));

        static async Task<Uri> GetLoginUriIntl(string realmUri, string callbackUri, ValueTask< SteamOid2Resource> resx)
        {
            SteamOid2Resource resource = await resx.ConfigureAwait(false);
            string url = MakeUrl(realmUri.AsSpan(), callbackUri.AsSpan(), resource.OPEndpointURL.AsSpan());
            return new Uri(url);
        }
    }
#else
    /// <inheritdoc/>
    public virtual async ValueTask<Uri> GetLoginUri(string realmUri, string callbackUri, CancellationToken token = default)
    {
        if (!callbackUri.StartsWith(realmUri, StringComparison.Ordinal))
            throw new ArgumentException("Callback must be part of the same domain as realm.", nameof(_callback));
        SteamOid2Resource resource = await GetSteamOid2Resource(false, token).ConfigureAwait(false);

        string url = MakeUrl(realmUri.AsSpan(), callbackUri.AsSpan(), resource.OPEndpointURL.AsSpan());
        return new Uri(url);
    }
#endif

#if NETFRAMEWORK
    /// <inheritdoc/>
    public virtual ValueTask<Uri> GetAuthorizeUri(Uri idResponseUri, CancellationToken token = default)
    {
        ValueTask<SteamOid2Resource> resx = GetSteamOid2Resource(false, token);
        if (!resx.IsCompleted)
            return new ValueTask<Uri>(GetAuthorizeUriIntl(idResponseUri, resx));

        return new ValueTask<Uri>(GetAuthorizedUriIntl(idResponseUri, resx.Result));

        static async Task<Uri> GetAuthorizeUriIntl(Uri idResponseUri, ValueTask< SteamOid2Resource> resx)
        {
            SteamOid2Resource resource = await resx.ConfigureAwait(false);
            return GetAuthorizedUriIntl(idResponseUri, resource);
        }
    }
#else
    /// <inheritdoc/>
    public virtual async ValueTask<Uri> GetAuthorizeUri(Uri idResponseUri, CancellationToken token = default)
    {
        SteamOid2Resource resource = await GetSteamOid2Resource(false, token).ConfigureAwait(false);
        return GetAuthorizedUriIntl(idResponseUri, resource);
    }
#endif
    private static Uri GetAuthorizedUriIntl(Uri idResponseUri, SteamOid2Resource resource)
    {
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