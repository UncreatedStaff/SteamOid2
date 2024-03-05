using SteamOid2.API;

namespace SteamOid2.Sample;

internal class Program
{
    private static async Task Main(string[] args)
    {
        ISteamOid2Client client = new SteamOid2Client("http://localhost:8001/", "http://localhost:8001/openid/login");

        using LoginHost host = new LoginHost(client);

        await host.StartAsync(CancellationToken.None);

        Console.WriteLine("Press [enter] to stop listening.");
        Console.ReadLine();

        await host.StopAsync(CancellationToken.None);
    }
}
