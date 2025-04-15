using SteamOid2.API;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Security;
using System.Text;
using System.Web;

namespace SteamOid2.BasicSample;
internal class LoginHost : IDisposable
{
    private readonly HttpListener _httpListener;
    private readonly HttpClient _httpClient;
    private readonly ISteamOid2Client _client;

    public LoginHost(ISteamOid2Client steamOid2Client)
    {
        _httpListener = new HttpListener();
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5d)
        };
        _httpListener.Prefixes.Add("http://localhost:8001/");
        _httpListener.Prefixes.Add("http://127.0.0.1:8001/");
        _client = steamOid2Client;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // start an HttpListener to listen for the callback from steam, this acts as our website backend
        try
        {
            _httpListener.Start();
        }
        catch (HttpListenerException ex)
        {
            if (ex.Message.StartsWith("Access is denied.", StringComparison.Ordinal))
            {
                const string msg = "This app (or visual studio) must be started with administrator permissions.";
                Console.WriteLine(msg);
                throw new SecurityException(msg, ex);
            }
            throw;
        }

        // asynchronously listen for requests
        _httpListener.BeginGetContext(GetContext, _httpListener);
        Console.WriteLine("Started HTTP listener.");

        try
        {
            // returns a Uri that the user will be redirected to. This would go in the 'Login with Steam' button's Uri.
            Uri uri = await _client.GetLoginUri(cancellationToken);

            Console.WriteLine("Realm:    " + _client.Realm);
            Console.WriteLine("Callback: " + _client.CallbackUri);
            Console.WriteLine("Client URL: \"" + uri + "\"");

            NameValueCollection queryProperties = HttpUtility.ParseQueryString(uri.Query);

            foreach (string key in queryProperties)
                Console.WriteLine(" " + key + " = " + Uri.UnescapeDataString(queryProperties[key]!));
            
            // Launch your browser to simulate pressing the button
            Process.Start(new ProcessStartInfo(uri.OriginalString) { UseShellExecute = true });

            Console.WriteLine("Done initiating OpenID 2.0 authentication. Press enter after the callback is received.");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error initiating OpenID 2.0 authentication.");
            Console.WriteLine(ex);
        }

        // give the HttpListener time to receive the callback
        Console.ReadLine();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            _httpListener.Stop();
            Console.WriteLine("Stopped HTTP listener.");
        }
        catch (ObjectDisposedException)
        {
            // ignored
        }

        return Task.CompletedTask;
    }

    private void GetContext(IAsyncResult ar)
    {
        if (ar.AsyncState is not HttpListener listener)
            return;
        try
        {
            // HttpListenerContext is used to intercept a request and define a response. We'll be intercepting the a user requesting the callback page from Steam.
            HttpListenerContext context = listener.EndGetContext(ar);

            if (context.Request.Url == null)
            {
                Console.WriteLine("Received null raw URL from HttpListener.");
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            }

            // request filter to only process the steam callback and not other requests (like for the favicon, etc).
            else if (context.Request.Url.AbsolutePath.StartsWith("/openid/login", StringComparison.OrdinalIgnoreCase))
            {
                Uri? uri = context.Request.Url;

                Console.WriteLine("Client URL: \"" + uri + "\"");

                // parses the ?www=xxx&yyy=zzz section of the URI
                NameValueCollection queryProperties = HttpUtility.ParseQueryString(uri.Query);

                foreach (string key in queryProperties)
                    Console.WriteLine(" " + key + " = " + Uri.UnescapeDataString(queryProperties[key]!));

                Console.WriteLine(string.Empty);

                // log the status of the response
                SteamOid2Response response = _client.ParseIdReponse(uri);
                Console.WriteLine($"Response status:  {response.Status}.");
                if (response.Status == Oid2Status.Success)
                    Console.WriteLine($"Response Steam64: {response.Steam64}.");
                else if (response.Error != null)
                    Console.WriteLine($"Response error:   {response.Error}.");
                if (response.Handle != null)
                    Console.WriteLine($"Response handle:  {response.Handle}");

                // respond to the request with plain text: Status: xxx - 76500000000000000
                context.Response.StatusCode = (int)(response.Status switch
                {
                    Oid2Status.Success => HttpStatusCode.OK,
                    Oid2Status.Cancelled => HttpStatusCode.BadRequest,
                    _ => HttpStatusCode.InternalServerError
                });

                byte[] utf8 = Encoding.UTF8.GetBytes($"Status: {response.Status} - {response.Steam64}");
                context.Response.ContentLength64 = utf8.Length; // length must be set before writing to the stream
                context.Response.OutputStream.Write(utf8, 0, utf8.Length);
                context.Response.OutputStream.Close();
                context.Response.ContentType = "text/plain";
                context.Response.ContentEncoding = Encoding.UTF8;

                if (response.Status == Oid2Status.Success)
                {
                    Task.Run(async () =>
                    {
                        // ask steam to verify the responded data.
                        // This is done server-side to make sure the data the client sent is actually correct.
                        // Otherwise it would be very easy for someone to just put in a random Steam64 ID.
                        Uri authUri = await _client.GetAuthorizeUri(uri);

                        HttpRequestMessage httpRequest = new HttpRequestMessage(HttpMethod.Post, authUri);
                        HttpResponseMessage httpResponse = await _httpClient.SendAsync(httpRequest);

                        // the response is received in the following format (in plain text):
                        //   ns:protocol_url
                        //   is_valid:true

                        string content = await httpResponse.Content.ReadAsStringAsync();

                        Oid2AuthenticationStatus status = _client.CheckAuthorizationResponse(content, out string? invalidateHandle);

                        Console.WriteLine($"Auth response status: {status}.");
                        if (invalidateHandle != null)
                            Console.WriteLine($"Invalidate handle: {invalidateHandle}.");

                        if (status == Oid2AuthenticationStatus.Valid)
                        {
                            // this user is 100% the owner of (or at least logged in to) the linked steam account from 'response.Steam64'.
                            Console.WriteLine("Validated.");
                        }
                        else
                        {
                            // this user is either trying to fake their Steam account or the request took too long.
                            Console.WriteLine("Unable to validate Steam account.");
                        }

                        Console.WriteLine("Press Ctrl + C to exit.");
                    });
                }
            }
        }
        catch (HttpListenerException ex)
        {
            // happens when you press Ctrl + C
            if (ex.Message.StartsWith("The I/O operation has been aborted because of either a thread exit or an application request.", StringComparison.Ordinal))
                return;

            Console.WriteLine("HTTP Listener threw an error:");
            Console.WriteLine(ex);
        }

        // start listening again
        _httpListener.BeginGetContext(GetContext, _httpListener);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _httpListener.Close();
    }
}
