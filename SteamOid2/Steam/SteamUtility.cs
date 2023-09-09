namespace SteamOid2.Steam;
public static class SteamUtility
{
    public static bool IsIndividualSteam64(ulong steamId) => ((long)(steamId >> 52) & 15L) == 1L;
}
