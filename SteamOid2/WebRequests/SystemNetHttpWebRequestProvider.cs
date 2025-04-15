using SteamOid2.API;
using SteamOid2.XRI;
using System.Net.Http;

namespace SteamOid2.WebRequests;

/// <summary>
/// Implementation of <see cref="ISteamOid2WebRequestProvider"/> using <see cref="HttpClient"/>.
/// </summary>
public class SystemNetHttpWebRequestProvider : ISteamOid2WebRequestProvider
{
    /// <inheritdoc />
    public async Task<SteamOid2Resource?> GetSteamOid2Resource(string url, CancellationToken token = default)
    {
        using HttpRequestMessage message = new HttpRequestMessage(HttpMethod.Get, url);

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

        return content;
    }
}