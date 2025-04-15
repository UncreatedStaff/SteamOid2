using System.Xml;
using System.Xml.Serialization;
using SteamOid2.XRI.Models;

namespace SteamOid2.XRI;
internal class SteamOid2ResourceReader
{
    private static readonly XmlReaderSettings DefaultSettings = new XmlReaderSettings
    {
        Async = true,
        ValidationType = ValidationType.None
    };
    public XmlSerializer Serializer { get; } = new XmlSerializer(typeof(SteamOid2Xri));
    public async Task<SteamOid2Resource?> Read(Stream stream, CancellationToken token = default)
    {
        using XmlReader reader = XmlReader.Create(stream, DefaultSettings);

        while (reader.NodeType == XmlNodeType.None || !reader.Name.Equals("Type", StringComparison.Ordinal) && !reader.Name.Equals("URI", StringComparison.Ordinal))
        {
            if (!await reader.ReadAsync().ConfigureAwait(false))
                return null;
            token.ThrowIfCancellationRequested();
        }

        string? type = null, uri = null;
        for (int i = 0; i < 2; ++i)
        {
            string name = reader.Name;
            await reader.ReadAsync().ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            if (name.Equals("Type", StringComparison.OrdinalIgnoreCase))
                type = reader.Value;
            else if (name.Equals("URI", StringComparison.OrdinalIgnoreCase))
                uri = reader.Value;
            if (i != 1)
            {
                while (await reader.ReadAsync().ConfigureAwait(false) && reader.NodeType != XmlNodeType.Element) ;
            }
        }

        if (type == null || uri == null)
            return null;
        SteamOid2Resource content = new SteamOid2Resource(type, uri);
        return content;
    }
}

/// <summary>
/// OpenID 2.0 resource for Steam.
/// </summary>
public class SteamOid2Resource
{
    /// <summary>
    /// OpenID 2.0 protocol version.
    /// </summary>
    public string ProtocolVersion { get; }

    /// <summary>
    /// OpenID 2.0 provider endpoint URL.
    /// </summary>
    public string OPEndpointURL { get; }

    /// <summary>
    /// Creates a new <see cref="SteamOid2Resource"/> given a protocol version and endpoint URL.
    /// </summary>
    public SteamOid2Resource(string protocolVersion, string opEndpointURL)
    {
        ProtocolVersion = protocolVersion;
        OPEndpointURL = opEndpointURL;
    }
}