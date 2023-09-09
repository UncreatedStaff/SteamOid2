using SteamOid2.XRI;
using System.Diagnostics.Contracts;

namespace SteamOid2.API;

/// <summary>
/// Service used to interact with the Steam OpenID 2.0 interface for logging into steam through a third party website.
/// </summary>
public interface ISteamOid2Client
{
    /// <summary>
    /// Defines the domain name used in the Steam login page.
    /// </summary>
    /// <remarks>Must be the same as the domain of <see cref="CallbackUri"/>.</remarks>
    string Realm { get; }

    /// <summary>
    /// Defines the callback URL that the user will be redirected to.
    /// </summary>
    /// <remarks>Must be the same domain as <see cref="Realm"/>.</remarks>
    string CallbackUri { get; }

    /// <summary>
    /// Defines the content type to use for the returned <seealso cref="Uri"/>s.
    /// </summary>
    string ContentType { get; }

    /// <summary>
    /// Gets the XRI resource information for steam from <see cref="Constants.SteamXRIProvider"/>. May return a cached version if <paramref name="forceRefresh"/> is <see langword="false"/>.
    /// </summary>
    /// <param name="forceRefresh">Force the resource to be redownloaded.</param>
    [Pure]
    ValueTask<SteamOid2Resource> GetSteamOid2Resource(bool forceRefresh = false, CancellationToken token = default);

    /// <summary>
    /// Construct a <seealso cref="Uri"/> that will redirect the user to the Steam login page with the necessary data in the query parameters.
    /// </summary>
    [Pure]
    ValueTask<Uri> GetLoginUri(CancellationToken token = default);

    /// <summary>
    /// Construct a <seealso cref="Uri"/> that will authorize the response from the user after they've logged in. Check for a valid response with <see cref="ParseIdReponse"/> first.
    /// </summary>
    [Pure]
    ValueTask<Uri> GetAuthorizeUri(Uri idResponseUri, CancellationToken token = default);

    /// <summary>
    /// Parse the query string of the <paramref name="uri"/> to check for a structurally-valid response from the callback uri, and if it's valid get the Steam64 ID.
    /// </summary>
    /// <param name="uri"><seealso cref="Uri"/> returned from the callback uri.</param>
    [Pure]
    SteamOid2Response ParseIdReponse(Uri uri);

    /// <summary>
    /// Parse the query string of the <paramref name="uri"/> to check for a structurally-valid response from the validation API.
    /// </summary>
    /// <param name="uri"></param>
    /// <param name="invalidateHandle"></param>
    /// <returns></returns>
    [Pure]
    Oid2AuthenticationStatus CheckAuthorizationResponse(string keyValuePairContent, out string? invalidateHandle);
}

/// <summary>
/// Represents the response of an ID request.
/// </summary>
public readonly struct SteamOid2Response
{
    internal static readonly SteamOid2Response Cancelled = new SteamOid2Response(Oid2Status.Cancelled, 0ul, "cancelled", null);

    /// <summary>
    /// Status of the response.
    /// </summary>
    public Oid2Status Status { get; }

    /// <summary>
    /// The Steam64 ID logged into, or zero if the login wasn't successful.
    /// </summary>
    public ulong Steam64 { get; }

    /// <summary>
    /// Human-readable error if the login wasn't successful.
    /// </summary>
    public string? Error { get; }

    /// <summary>
    /// 'assoc_handle' value, should be invalidated if the login wasn't successful.
    /// </summary>
    public string? Handle { get; }

    internal SteamOid2Response(Oid2Status status, ulong steam64, string? error, string? handle)
    {
        Status = status;
        Steam64 = steam64;
        Error = error;
        Handle = handle;
    }
}

/// <summary>
/// Represents the status of an ID request.
/// </summary>
public enum Oid2Status
{
    /// <summary>
    /// Login successfully completed.
    /// </summary>
    Success,

    /// <summary>
    /// Login was cancelled by the user.
    /// </summary>
    Cancelled,

    /// <summary>
    /// There was some kind of error on Steam's side.
    /// </summary>
    Error,

    /// <summary>
    /// Unexpected response format.
    /// </summary>
    InvalidResponse
}

/// <summary>
/// Represents the status of a Check Authentication request.
/// </summary>
public enum Oid2AuthenticationStatus
{
    /// <summary>
    /// Data matches the expected signature.
    /// </summary>
    Valid,
    
    /// <summary>
    /// Data does not match the expected signature.
    /// </summary>
    Invalid,

    /// <summary>
    /// Handle needs to be rejected, possibly from abuse.
    /// </summary>
    Reject,
    
    /// <summary>
    /// Unexpected response format.
    /// </summary>
    InvalidResponse
}