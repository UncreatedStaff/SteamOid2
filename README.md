# SteamOid2
```
<PackageReference Include="SteamOid2" Version="*" />
```

Library for integrating with Steam's Open-ID provider in .NET or .NET Framework, as described here: https://partner.steamgames.com/doc/features/auth#website.

Available for .NET Framework 4.6.1+, .NET Standard 2.1+, and .NET 5.0+.

## Justification
This library is very bare-bones and is meant to support you in implementing your own Steam auth in whatever environment you want. It doesn't provide full authentication, just the helper methods you need to make your own.

## Usage
```cs
using SteamOid2;
using SteamOid2.API;

// A DI constructor is also available using ILogger and IConfiguration
ISteamOid2Client client = new SteamOid2Client("http://localhost:8001/", "http://localhost:8001/openid/login");
```

* Redirect **user** to `client.GetLoginUri()`;

* The user will log in, then a request will be sent to the backend at the callback URI.

* Use `client.ParseIdReponse(uri)` to see if the returned Status is successful.

* Send a **POST** request to `client.GetAuthorizeUri(uri)` to ask Steam to confirm that the Steam ID provided was actually logged in to (from the backend).

* Use `client.CheckAuthorizationResponse(response)` to check that the response from the **POST** indicates a valid login session.

* For more detailed information see the 2 available samples. The FullSample contains a dependency-injection example and the BasicSample is just a basic console application.

If you're seeing `Error` when you try to log in from Steam, make sure the realm domain name is the same as the callback domain name.

## Sample implementation with a console application and HttpListener
[https://github.com/UncreatedStaff/SteamOid2/blob/master/SteamOid2.FullSample/LoginHost.cs](https://github.com/UncreatedStaff/SteamOid2/blob/master/SteamOid2.FullSample/LoginHost.cs)

## Generating a Strong Name Key/Pair

SteamOid2.dll has a strong name, which means you may want to generate your own to recompile it.

Create a .snk file. This file or its contents should not be shared.

```
Developer Powershell > cd "Location outside of repository"
Developer Powershell > sn -k SteamOid2.dll.snk
Developer Powershell > sn -p SteamOid2.dll.snk SteamOid2.dll.publickey
```

Copy the public key to your fork and replace the existing one if you want.

Set the `StrongNameKeyPath` property in the .csproj file to the path of the .snk file.

### Steam Logo
©2024 Valve Corporation. Steam and the Steam logo are trademarks and/or registered trademarks of Valve Corporation in the U.S. and/or other countries.
