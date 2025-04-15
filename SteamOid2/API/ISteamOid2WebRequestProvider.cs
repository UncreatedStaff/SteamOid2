using SteamOid2.Steam;
using SteamOid2.WebRequests;
using SteamOid2.XRI;

namespace SteamOid2.API;

/// <summary>
/// API for implementing a web request to <see cref="Constants.SteamXRIProvider"/>.
/// </summary>
/// <remarks>Default implementation is <see cref="SystemNetHttpWebRequestProvider"/>.</remarks>
public interface ISteamOid2WebRequestProvider
{
    /// <summary>
    /// Send a request for the <see cref="SteamOid2Resource"/> living at <paramref name="url"/>.
    /// </summary>
    /// <remarks>Retrying is handled by the caller and should not be implemented.</remarks>
    /// <returns>The resource, or <see langword="null"/> if it failed.</returns>
    Task<SteamOid2Resource?> GetSteamOid2Resource(string url, CancellationToken token = default);
}