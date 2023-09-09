using System.Xml.Serialization;

namespace SteamOid2.XRI.Models;

public class SteamOid2Xri
{
    public SteamOid2XriXrds? XRDS { get; set; }
}
public class SteamOid2XriXrds
{
    [XmlArray("Service")]
    public SteamOid2XriService[]? XRD { get; set; }
}
public class SteamOid2XriService
{
    [XmlAttribute("priority")]
    public int Priority { get; set; }

    public string? Type { get; set; }
    public string? URI { get; set; }
}
