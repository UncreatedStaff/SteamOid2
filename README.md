# SteamOid2
Simple library for interfacing with Steam's OpenID 2.0 implementation.

## Usage
```cs
using SteamOid2;
using SteamOid2.API;

// A DI constructor is also available using IConfiguration
ISteamOid2Client client = new SteamOid2Client("http://localhost:8001/", "http://localhost:8001/openid/login");
```

Redirect **user** to `client.GetLoginUri()`;

The user will log in, then a request will be sent to the backend at the callback URI.

Use `client.ParseIdReponse(uri)` to see if the returned Status is successful.

Send a **POST** request to `client.GetAuthorizeUri(uri)` to ask Steam to confirm that the Steam ID provided was actually logged in to (from the backend).

Use `client.CheckAuthorizationResponse(response)` to check that the response from the **POST** indicates a valid login session.

If you're seeing `Error` when you try to log in from Steam, make sure the realm domain name is the same as the callback domain name.

### Sample implementation with a console application and HttpListener
https://github.com/UncreatedStaff/SteamOid2/blob/master/LoginHost.cs
