# LabBoard.Auth.Api — Story

## Overview
A standalone ASP.NET Core Web API project for Authentication and Authorization.
Handles user registration and client app registration for OAuth2/OIDC flows.
Data is persisted in JSON files under the `Database/` folder (to be migrated to a real database later).

---

## What We Have Done

### 1. Project Setup
- Created `LabBoard.Auth.Api` as a separate Web API project
- Added to `LabBoard.sln`
- Added `Swashbuckle.AspNetCore` for Swagger UI

### 2. Folder Structure
```
LabBoard.Auth.Api/
├── Controllers/
├── Database/
├── Entities/
├── Enums/
├── Models/
│   ├── User/
│   └── Client/
├── Services/
│   ├── User/
│   └── Client/
└── Program.cs
```

### 3. User Registration
- Fields: `FullName`, `Gender`, `Age`, `Email`, `Phone`, `Password`, `Role`
- Password is SHA-256 hashed before storage — never stored in plaintext
- Email uniqueness enforced — returns 409 Conflict on duplicate
- `Role` is optional, defaults to `Viewer`
- Data saved to `Database/userStore.json`

### 4. Client App Registration
- Fields: `AppName`, `AppDescription`, `GrantTypes`, `RedirectUris`, `AdditionalOpenIdScopes`, `ApiScopes`, `TokenExpiry`
- `ClientId` and `ClientSecret` are auto-generated on registration
- App name uniqueness enforced — returns 409 Conflict on duplicate
- Data saved to `Database/clientAppStore.json`

### 5. Scopes Design
- Two enums defined: `OpenIdScope` and `ApiScope`
- **OpenIdScope**: `OpenId`, `Profile`, `Email`, `Phone`, `Address`, `OfflineAccess`
- **ApiScope**: `Admin`, `Add`, `Update`, `Delete`
- Default OpenID scopes `[OpenId, Profile, Email]` are always auto-applied
- Client app only sends `AdditionalOpenIdScopes` for extras like `Phone`, `Address`
- User consent at login will be handled in a future flow

### 6. Roles Design
- `UserRole` enum: `Admin`, `Editor`, `Viewer`
- Stored on `User` entity, set at registration
- Scope-to-role mapping deferred to Admin section

### 7. Architecture Decisions
- `Entities/` — internal classes mapped to JSON store (will map to DB later)
- `Models/User/` and `Models/Client/` — public DTOs for API input/output
- `Services/User/` and `Services/Client/` — business logic separated by domain
- Entities marked `internal` — never exposed outside the project
- `UserService` uses `UserEntity` alias to avoid namespace conflict with `Services.User`

### 8. OAuth Authorization Code Flow (Grant Type: authorization_code)

Implemented the real browser-based OAuth 2.0 Authorization Code flow — no JSON responses, no credentials in the API body.

**Step 1 — `GET /oauth/authorize?response_type=code&client_id=xxx&redirect_uri=xxx&scope=xxx&state=xxx`**
- Auth server validates: `response_type=code`, client exists + active, `authorization_code` grant present, `redirectUri` is registered, all scopes are allowed
- On success → returns an **HTML login page** (inline, no Razor/views)
- On error → returns an **HTML error page** (never redirects on client validation failure — security)

**Step 2 — `POST /oauth/login`** (browser form submission, `application/x-www-form-urlencoded`)
- Hidden fields carry `clientId`, `redirectUri`, `scope`, `state` from step 1
- Re-validates client params (prevents tampering)
- Validates user credentials via `ValidateCredentialsAsync`
- On wrong password → re-renders login page with inline error message
- On success → generates a cryptographically secure 32-byte base64url code, persists it, issues **HTTP 302 Redirect** to `redirect_uri?code=xxx&state=xxx`
- The client app receives the code via the redirect — never sees user credentials

**Auth code persisted to `Database/authCodeStore.json`:**
- `clientId`, `userId`, `redirectUri`, `scopes[]`, `state`
- `ExpiresAt` = 10 minutes, `IsUsed` flag for single-use at token exchange

**New files:**
- `Entities/AuthCode.cs` — auth code entity
- `Models/OAuth/AuthorizeQueryParams.cs` — GET query params
- `Models/OAuth/LoginFormRequest.cs` — POST form fields
- `Helpers/LoginPageHtml.cs` — inline HTML builder (login page + error page)
- `Services/OAuth/IAuthCodeService.cs` + `AuthCodeService.cs` — validate client / generate code
- `Controllers/OAuthController.cs` — GET authorize + POST login

**Updated:**
- `IUserService` + `UserService` — added `ValidateCredentialsAsync(email, password)`

---

### 9. LabBoard.Gateway.Api — Ocelot API Gateway

- New project `src/LabBoard.Gateway.Api` targeting **net8.0** with **Ocelot 23.4.2**
- Single entry point for all downstream services — clients talk only to the gateway
- **Routing strategy** (path-prefix based):

| Gateway upstream path | Downstream service | Example |
|---|---|---|
| `/auth/{everything}` | `labboard-auth-api:8080` | `GET /auth/api/users` |
| `/redis/{everything}` | `labboard-redis-api:8080` | `GET /redis/api/cache/string/key` |
| `/observability/{everything}` | `labboard-observability:8080` | `GET /observability/metrics` |

- **Two Ocelot config files** — loaded based on environment:
  - `ocelot.json` — Docker (uses container names as hostnames)
  - `ocelot.Development.json` — local dev (uses `localhost` + per-service ports)
- `Program.cs` selects the correct file: `IsDevelopment() ? "ocelot.Development.json" : "ocelot.json"`
- Added `Dockerfile` for Gateway.Api (net8.0 images) and **Auth.Api** (net10.0 images)
- Added both `labboard-auth-api` (port 5100) and `labboard-gateway` (port 5200) to `docker-compose.yml`
- Gateway `depends_on` all three downstream services

---

### 10. Token Exchange + BFF (Backend for Frontend) Pattern

Completed the full OAuth 2.0 Authorization Code flow end-to-end with the **BFF pattern** — the Gateway acts as the confidential client, keeping the JWT entirely server-side.

**Step 3 — `POST /oauth/token`** (Auth.Api, server-to-server only)
- Validates `grant_type=authorization_code`, `client_id`, `client_secret`, `redirect_uri`
- Looks up client by `client_id`, checks `client_secret` and `IsActive`
- Calls `AuthCodeService.ConsumeAsync` — validates expiry, single-use (`IsUsed` flag), client + redirect match
- Looks up user by `UserId` from the consumed code
- Calls `TokenService.Generate` — returns a signed JWT (`HMAC-SHA256`) containing:
  - Claims: `sub` (userId), `email`, `name`, `role`, `scope`
  - Expiry: `client.TokenExpiry` seconds (default 3600)
- Auth code is marked `IsUsed = true` immediately after consumption — cannot be replayed

**JWT config (`appsettings.json` → `Jwt` section):**
```
SecretKey  : LabBoard@SuperSecretKey#256bits@2026!!
Issuer     : LabBoard
Audience   : LabBoard.Gateway
```

**New files in Auth.Api:**
- `Models/OAuth/TokenRequest.cs` — `grant_type`, `code`, `client_id`, `client_secret`, `redirect_uri`
- `Models/OAuth/TokenResponse.cs` — `access_token`, `token_type`, `expires_in`, `scope`
- `Models/OAuth/ConsumedAuthCode.cs` — record returned after code consumption
- `Services/OAuth/ITokenService.cs` + `TokenService.cs` — JWT generation
- `Configuration/JwtOptions.cs` — typed options for JWT config

**Package added to Auth.Api:**
- `System.IdentityModel.Tokens.Jwt 7.6.3`

---

**BFF Gateway — how it works end-to-end:**

```
Browser                    Gateway (BFF)              Auth.Api
  │                             │                         │
  │── GET /any-route ──────────>│                         │
  │                        [GatewayAuthMiddleware]        │
  │                        no lb_session cookie?          │
  │<─ 302 /oauth/authorize?... ─│                         │
  │                             │                         │
  │── GET /oauth/authorize ─────────────────────────────>│
  │<─ HTML login page ──────────────────────────────────-│
  │                             │                         │
  │── POST /oauth/login ────────────────────────────────>│
  │<─ 302 /oauth/callback?code= ────────────────────────-│
  │                             │                         │
  │── GET /oauth/callback?code= │                         │
  │                        [OAuthCallbackController]      │
  │                        POST /oauth/token ────────────>│
  │                             │<─ { access_token } ─────│
  │                        Set-Cookie: lb_session=<JWT>   │
  │<─ 302 / ────────────────────│                         │
  │                             │                         │
  │── GET / (+ cookie) ────────>│                         │
  │                        [GatewayAuthMiddleware]        │
  │                        validates JWT from cookie      │
  │                        Ocelot proxies request ───────>│
```

**New/updated files in Gateway.Api:**
- `Configuration/JwtOptions.cs` — typed options for JWT validation
- `Configuration/OAuthClientOptions.cs` — `ClientId`, `ClientSecret`, `RedirectUri`, `Scope`, `AuthApiBaseUrl`
- `Controllers/OAuthCallbackController.cs` — exchanges code for JWT via `HttpClient`, sets `lb_session` HTTP-only cookie, redirects to original URL
- `Middleware/GatewayAuthMiddleware.cs` — reads `lb_session` cookie, validates JWT via `JsonWebTokenHandler`, redirects to Auth.Api authorize if missing/invalid; encodes current path as `state`
- `Program.cs` — wires `AddHttpClient("AuthApi")`, `Configure<JwtOptions>`, `Configure<OAuthClientOptions>`; no JwtBearer middleware
- `appsettings.json` — added `OAuthClient` section

**Setup step before testing:**
Register the Gateway as a confidential client app via `POST /clients/register` on Auth.Api, then paste the returned `clientId` + `clientSecret` into `appsettings.json` → `OAuthClient`.

---

## OAuth Flow Reference — When to Use What

### Authorization Code + `client_secret` (Confidential Client)
Use when the client runs **on a server** and can safely store a secret.

```
Angular → Gateway (BFF) → Auth.Api
```

- Gateway registers as a confidential client with `client_secret`
- `client_secret` is stored safely in `appsettings.json` on the server
- Gateway handles all redirects, exchanges code for JWT
- JWT stored in `HttpOnly` cookie — Angular never sees the token
- Angular has zero OAuth knowledge — just talks to Gateway

---

### Authorization Code + PKCE (Public Client)
Use when the client runs **in the browser or on a device** and cannot store a secret safely.

```
Angular → Auth.Api directly  (no Gateway / BFF)
```

- Angular registers itself as a public client (no `client_secret`)
- Angular generates `code_verifier` (random, kept in memory) and `code_challenge = SHA256(verifier)`
- Sends `code_challenge` on `/oauth/authorize`, sends `code_verifier` on `/oauth/token`
- Auth server verifies: `SHA256(code_verifier) === stored code_challenge`
- Attacker who intercepts the code cannot use it — they never had `code_verifier`
- JWT stored in memory or `sessionStorage` — visible to JavaScript

| | With Gateway BFF | Without Gateway (PKCE) |
|---|---|---|
| OAuth client | Gateway (confidential) | Angular (public) |
| `client_secret` | Yes — safe on server | No — can't store in browser |
| Token storage | `HttpOnly` cookie | Memory / `sessionStorage` |
| Angular sees token | Never | Yes |
| Security | Higher | Lower (token in browser) |
| Architecture | Gateway required | Simpler, no BFF needed |

> **Rule:** Use PKCE when the frontend handles OAuth directly with no BFF/Gateway in between.
> PKCE replaces the `client_secret` that a browser app can never safely hold.

---

### Client Credentials (Machine to Machine)
Use when **no user is involved** — a backend service calling another backend service.

- No login page, no redirect, no user identity
- Service authenticates with `client_id` + `client_secret` directly
- Receives a token scoped to API permissions only (`scope` claim, no `sub`)

---

## Pending / Future Work

- [ ] Move JSON store to a real database (EF Core)
- [ ] Add refresh token support
- [ ] Add user consent screen for scope approval
- [ ] Move Role and Scope mapping under Admin section
- [ ] Add Client App update endpoint
- [ ] Add rate limiting / request aggregation to Gateway

---

## Next Tasks

### Task 1 — Sign-Out Flow
Implement a proper sign-out mechanism that ends the user session cleanly.
- Gateway `GET /oauth/logout` clears the `lb_session` HttpOnly cookie
- Optionally call Auth.Api to invalidate or blacklist the issued JWT
- Redirect the user to the Auth.Api login page after sign-out
- Angular navbar "Sign Out" button triggers this endpoint via full browser redirect
- Ensure sign-out works even if the JWT has already expired

---

### Task 2 — Public Client Authorization Code Flow with PKCE (RFC 7636)
Support the Authorization Code flow for **public clients** — SPAs and mobile apps
that cannot safely store a `client_secret`. PKCE replaces the client secret with a
cryptographic challenge-verifier pair generated by the client.

**Required changes:**
- Add `ClientType` enum (`Confidential` | `Public`) to `ClientApp` entity and registration request
- Add `CodeChallenge` and `CodeChallengeMethod` fields to the `AuthCode` entity
- `GET /oauth/authorize` — accept optional `code_challenge` + `code_challenge_method=S256`; store on auth code
- `POST /oauth/login` — pass `code_challenge` / `code_challenge_method` through hidden form fields
- `POST /oauth/token` — for public clients, skip `client_secret` validation; require `code_verifier` instead; compute `BASE64URL(SHA256(code_verifier))` and compare against stored `code_challenge`
- `LoginPageHtml.Build` — add hidden input fields for PKCE params
- No changes needed to `ConsumedAuthCode` or `TokenService`

---

### Task 3 — Client Credentials Grant Type
Implement the `client_credentials` grant for **machine-to-machine (M2M)** API access.
No user is involved — the client authenticates directly using its own credentials
and receives an access token scoped to API permissions only.

**Required changes:**
- `POST /oauth/token` — add a new branch for `grant_type=client_credentials`
- Validate `client_id` + `client_secret`, confirm `client_credentials` is in the client's `GrantTypes`
- Issue a JWT with API scope claims only (no `sub`, no user identity claims)
- No auth code or redirect URI involved — response is a direct token JSON

---

### Task 4 — JWKS Endpoint — Standard Public Key Discovery

Replace the current hardcoded RSA key approach with the industry-standard JWKS discovery
pattern used by Google, Auth0, and Okta. No manual key sharing — Gateway fetches the
public key from Auth.Api automatically at startup.

**Why:**
- Private key is currently stored in `appsettings.json` and committed to source control
- Public key is manually copied to Gateway's `appsettings.json` — breaks on key rotation
- Any new service that needs to verify JWTs must be given the key manually

**Required changes:**

- **Auth.Api — `GET /.well-known/jwks.json`** (new `WellKnownController`)
  - Return RSA public key in standard JWK format: `kty`, `use`, `alg`, `kid`, `n`, `e`
  - Extract modulus (`n`) and exponent (`e`) from the RSA key as Base64Url values

- **Auth.Api — remove `PrivateKey` from `appsettings.json`**
  - Generate RSA key pair on first run, persist private key to a local secure file (dev)
  - Load from environment variable / secrets manager in production

- **Gateway.Api — remove `PublicKey` from `appsettings.json`**
  - On startup, call `GET http://localhost:5100/.well-known/jwks.json`
  - Parse JWK response, reconstruct `RsaSecurityKey` from `n` and `e`
  - Use fetched key in `GatewayAuthMiddleware` for JWT validation
  - Support periodic refresh to handle key rotation without redeployment

**Flow after implementation:**
```
Auth.Api                              Gateway.Api
────────────────────────────          ──────────────────────────────
Generates RSA key pair on startup     On startup:
Holds private key (never shared)        GET /.well-known/jwks.json
Exposes public key at JWKS endpoint ──────────────────────────────►
                                      Builds RsaSecurityKey from JWK
                                      Validates JWTs automatically
```

**Files to change:**
- `src/LabBoard.Auth.Api/Controllers/WellKnownController.cs`     (new)
- `src/LabBoard.Auth.Api/appsettings.json`                       (remove PrivateKey)
- `src/LabBoard.Auth.Api/Program.cs`                             (register key gen service)
- `src/LabBoard.Gateway.Api/appsettings.json`                    (remove PublicKey)
- `src/LabBoard.Gateway.Api/Program.cs`                          (fetch JWKS on startup)
- `src/LabBoard.Gateway.Api/Middleware/GatewayAuthMiddleware.cs` (use fetched key)

---

### Task 5 — Sign-In Page Enhancements
Improve the Auth.Api login experience with security and usability improvements.
- Add CSRF token to the login form to protect against cross-site request forgery
- Implement login attempt rate limiting per IP or per email (e.g., lock after 5 failed attempts)
- Add a "Remember me" option that extends the `lb_session` cookie lifetime
- Show a clear error count warning after repeated failed attempts
- Return a generic error message (never reveal whether the email exists)

---

### Task 6 — Admin UI — User & Client App Management Pages
Build an admin-facing section within the Angular app to manage users and registered client applications.
- **Users page** — list all registered users with name, email, role, and active status
- **Client Apps page** — list all registered OAuth client applications with their scopes and grant types
- **Register App page** — form to create a new client application (name, description, grant types, redirect URIs, scopes)
- **Edit User page** — update user role, activate/deactivate account
- **Edit Client App page** — update redirect URIs, grant types, scopes, token expiry, and active flag
- All pages accessible only to users with the `Admin` role

---

### Task 7 — API Scope Enforcement
Define and enforce how API scopes (`admin`, `add`, `update`, `delete`) control access
to protected downstream endpoints.

**Design decisions to implement:**
- JWT issued by Auth.Api includes a `scope` claim listing the granted API scopes
- Gateway middleware or a downstream policy extracts the `scope` claim from the JWT
- Each downstream endpoint declares the required scope (e.g., `POST /api/tickets` requires `add`)
- Requests with insufficient scopes receive `403 Forbidden` — not `401 Unauthorized`
- Scope enforcement can live in Gateway middleware (centralized) or in each downstream service (decentralized — more secure)
- Document the chosen approach and update Ocelot route config if needed

---

### Task 8 — Admin Role & User/Scope Management
Introduce a dedicated `Admin` role with elevated permissions to manage the platform.

**Admin capabilities:**
- View and search all registered users and client applications
- Activate or deactivate any user account or client application
- Assign or revoke `UserRole` (`Admin`, `Editor`, `Viewer`) for any user
- Assign or revoke OpenID scopes and API scopes on any client application
- Create the first admin user via a secure bootstrap endpoint or CLI seed
- All admin actions are protected by `Admin` role claim validation in the Gateway or Auth.Api
- Audit log of admin actions stored to a separate `adminAuditStore.json` (or DB table later)

