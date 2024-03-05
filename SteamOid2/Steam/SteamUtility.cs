namespace SteamOid2.Steam;

/// <summary>
/// Utilities for working with Steam64 IDs.
/// </summary>
public static class SteamUtility
{
    /// <summary>
    /// Check if a Steam64 ID is a valid Individual's Steam ID.
    /// </summary>
    public static bool IsIndividualSteam64(ulong steamId) => ((long)(steamId >> 52) & 15L) == 1L;
}
