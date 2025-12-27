# Yahoo Identity Provider (OAuth 2.0 + OIDC Authorization Code Flow) – Implementation Specs

> Scope: Add Yahoo as an external Identity Provider (IdP) using **Authorization Code Flow** with **OpenID Connect (OIDC)** where available.
>
> Target: ASP.NET Core backend (`CardGames.Poker.Api`) + Blazor UI (`CardGames.Poker.Web`). The repo already supports Google as an IdP; Yahoo should follow the same patterns.

## 1) Outcome

Add a new external provider named `Yahoo` such that:

1. User clicks “Sign in with Yahoo”.
2. Browser is redirected to Yahoo.
3. Yahoo redirects back to our callback endpoint with an authorization `code`.
4. Backend exchanges the `code` for tokens.
5. Backend signs user into the application and either creates or links a local account.

## 2) Provider capability check (must do first)

Yahoo’s developer offerings have changed over time. Before implementing code, confirm what Yahoo currently supports for new applications:

- **OIDC discovery** (`/.well-known/openid-configuration`)
- **ID token** issuance (`id_token` returned from token endpoint)
- **UserInfo endpoint** availability (for `email`, `picture`, richer profile)

If full OIDC is not supported, you can still implement Yahoo with OAuth 2.0 (`AddOAuth`) but it will not be “OIDC” in the strict sense.

## 3) Yahoo developer signup and app registration

### 3.1 Create/verify a Yahoo account

- Create or sign in to a Yahoo account.
- Ensure the account has recovery info (email/phone). Some portals require it.

### 3.2 Start at Yahoo’s developer portal

Use Yahoo’s official developer landing page and navigate to OAuth/OIDC app registration:

- https://developer.yahoo.com/

Look for sections labeled:

- OAuth 2.0
- OpenID Connect
- “Sign in with Yahoo”
- “YConnect” (older branding)

### 3.3 Create an application/client

In the Yahoo developer console:

1. Create a new application (sometimes called “App”, “Project”, or “Client”).
2. Enable **OAuth 2.0**.
3. If available, enable **OpenID Connect** / allow `openid` scope.
4. Fill out any required fields:
   - application name
   - application description
   - contact email
   - privacy policy URL (often required for production)
   - terms of service URL (sometimes required)
5. Create credentials and record:
   - `ClientId`
   - `ClientSecret`

### 3.4 Configure redirect/callback URIs

Register the callback URI(s) Yahoo will be allowed to redirect to.

Recommended callback path in ASP.NET Core:

- `/signin-yahoo`

Examples to register:

- `https://localhost:<port>/signin-yahoo`
- `https://<devtunnel-host>/signin-yahoo`
- `https://<prod-host>/signin-yahoo`

Rules:

- Redirect URI must match exactly (scheme/host/port/path).
- Prefer HTTPS except when explicitly supported for `localhost`.

### 3.5 Gather OIDC and OAuth endpoints

Ideal case (OIDC Discovery supported):

- Determine `Authority` / `Issuer` base URL.
- Verify the discovery document is reachable:
  - `https://<issuer>/.well-known/openid-configuration`

Required fields from discovery:

- `issuer`
- `authorization_endpoint`
- `token_endpoint`
- `jwks_uri`
- optionally `userinfo_endpoint`

If discovery isn’t available, you must gather these endpoints manually from Yahoo documentation/console and configure them explicitly.

## 4) Protocol requirements (Authorization Code Flow)

### 4.1 Authorization request requirements

Our authorization request must include:

- `response_type=code`
- `client_id`
- `redirect_uri`
- `scope` (space-separated)
- `state` (CSRF protection)
- `nonce` (OIDC replay protection)

Recommended scopes (subject to Yahoo support):

- `openid`
- `profile`
- `email`

### 4.2 Token exchange requirements

The backend performs a server-to-server POST to the token endpoint with:

- `grant_type=authorization_code`
- `code`
- `redirect_uri` (must match)
- client authentication:
  - typical: HTTP Basic (`client_id:client_secret`)
  - or form body `client_id` + `client_secret` if required

### 4.3 PKCE requirements

- Enable PKCE (`S256`) unless Yahoo explicitly disallows it.
- PKCE is recommended even for confidential web apps.

## 5) Application configuration requirements

Add a configuration section:

```json
"Authentication": {
  "Yahoo": {
    "Enabled": true,
    "ClientId": "...",
    "ClientSecret": "...",
    "Authority": "https://...",
    "CallbackPath": "/signin-yahoo",
    "Scopes": ["openid", "profile", "email"],
    "SaveTokens": true,
    "GetClaimsFromUserInfoEndpoint": true
  }
}
```

Secrets via environment variables:

- `Authentication__Yahoo__ClientId`
- `Authentication__Yahoo__ClientSecret`

## 6) ASP.NET Core implementation specs (backend)

### 6.1 Preferred implementation: `AddOpenIdConnect`

Register a scheme named `Yahoo`.

Required `OpenIdConnectOptions` behaviors:

- `Authority` (from config)
- `ClientId`, `ClientSecret` (from config)
- `CallbackPath = /signin-yahoo`
- `ResponseType = code`
- `UsePkce = true`
- `SaveTokens = true` when we need access token / refresh token later
- Explicit scopes from configuration
- `GetClaimsFromUserInfoEndpoint = true` if Yahoo provides userinfo and claims are missing in the ID token

### 6.2 Fallback implementation: `AddOAuth`

If Yahoo does not provide OIDC discovery / ID tokens, implement with `AddOAuth("Yahoo", ...)` using Yahoo’s OAuth endpoints and map the profile from whatever endpoint is available.

This fallback still uses Authorization Code flow, but it is not OIDC.

### 6.3 Claim mapping requirements

Normalize Yahoo identity to local expectations.

Required identity keys:

- Stable external user id: `sub` (OIDC)
  - must be persisted as `Provider=Yahoo`, `Subject=sub`

Desired profile fields:

- display name: `name` (or `preferred_username`, or derived from `given_name`/`family_name`)
- email: `email` (may be absent depending on scopes/consent)
- avatar: `picture` (may be absent)

Rules:

- Set `ClaimTypes.NameIdentifier` from `sub`.
- Set `ClaimTypes.Name` from a best-effort display name.
- Do not assume `email` exists.
- If `email_verified` exists, preserve it (for linking decisions).

### 6.4 Error handling

- Handle remote failures (access denied, invalid scope, misconfigured redirect URI) by redirecting the user to a friendly error page.
- Log provider error details on the server.

## 7) Blazor UI requirements

- Add a “Yahoo” sign-in button alongside Google.
- The button should initiate an auth challenge for scheme `Yahoo`.

## 8) Local account creation/linking requirements

On Yahoo sign-in completion:

1. If an external login record exists (`Yahoo`, `sub`), sign in that local user.
2. Otherwise create a new local user and persist the external login record.
3. Optional: if `email` is present and verified, offer/perform linking to an existing local user with that email.

Collision policy:

- If the email matches an existing account already linked to a different Yahoo `sub`, do not auto-link.

## 9) Testing requirements

Manual tests:

- successful sign-in
- user cancels/denies consent
- invalid client secret
- redirect URI mismatch
- missing `openid` scope / no ID token

Automated tests (as feasible):

- configuration binding tests
- auth challenge produces provider redirect
- callback endpoint is registered and protected by state/nonce

## 10) Risks / unknowns

- Yahoo may not support full OIDC (discovery + ID token + userinfo) for new apps.
- Available scopes and profile fields may differ.
- Some providers restrict `localhost` redirect URIs.

If OIDC cannot be confirmed, implement Yahoo via OAuth (`AddOAuth`) and treat it as a non-OIDC external provider while keeping the same UI/UX.
