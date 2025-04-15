using System.ComponentModel;
using System.Xml.Serialization;

namespace SteamOid2.XRI.Models;

/// <summary>
/// Model representing the Steam XML resource.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public class SteamOid2Xri
{
    /// <summary>
    /// Model representing the Steam XML resource.
    /// </summary>
    public SteamOid2XriXrds? XRDS { get; set; }
}

/// <summary>
/// Model representing the Steam XML resource.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public class SteamOid2XriXrds
{
    /// <summary>
    /// Model representing the Steam XML resource.
    /// </summary>
    [XmlArray("Service")]
    public SteamOid2XriService[]? XRD { get; set; }
}

/// <summary>
/// Model representing the Steam XML resource.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public class SteamOid2XriService
{
    /// <summary>
    /// Model representing the Steam XML resource.
    /// </summary>
    [XmlAttribute("priority")]
    public int Priority { get; set; }

    /// <summary>
    /// Model representing the Steam XML resource.
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Model representing the Steam XML resource.
    /// </summary>
    public string? URI { get; set; }
}