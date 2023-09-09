using SteamOid2.XRI;

namespace SteamOid2.Steam;
public static class Constants
{
    public const string SteamXRIProvider = "https://steamcommunity.com/openid";
    public const string SteamClaimedIdPrefix = "https://steamcommunity.com/openid/id/";
    internal static readonly SteamOid2Resource BackupSteamOid2Resource = new SteamOid2Resource("http://specs.openid.net/auth/2.0/server", "https://steamcommunity.com/openid/login");
}
