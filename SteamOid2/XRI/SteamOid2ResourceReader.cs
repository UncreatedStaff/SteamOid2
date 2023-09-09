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
    public string Text { get; }
    public XmlSerializer Serializer { get; }
    public SteamOid2ResourceReader(string text)
    {
        Text = text;
        Serializer = new XmlSerializer(typeof(SteamOid2Xri));
    }
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
            if (name.Equals("Type"))
                type = reader.Value;
            else if (name.Equals("URI"))
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
public class SteamOid2Resource
{
    public string ProtocolVersion { get; }
    public string OPEndpointURL { get; }
    
    public SteamOid2Resource(string protocolVersion, string opEndpointURL)
    {
        ProtocolVersion = protocolVersion;
        OPEndpointURL = opEndpointURL;
    }
}