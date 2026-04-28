# LabBoard Gateway API

BFF (Backend for Frontend) gateway built with ASP.NET Core + Ocelot.
Sits between the Angular UI and downstream microservices.
Handles OAuth 2.0 Authorization Code flow, JWT validation, and API proxying.

---

## Ports

| Service           | Port |
|-------------------|------|
| Angular UI        | 4200 |
| Gateway API       | 5200 |
| Auth API          | 5100 |
| TicketMaster API  | 5300 |

---

## Steps Completed So Far

### Step 1 — Project Setup
- Created `LabBoard.Gateway.Api` ASP.NET Core project
- Added **Ocelot** for API proxying
- Upgraded to **net9.0** to use built-in `Microsoft.AspNetCore.OpenApi`
- Added `launchSettings.json` with HTTP port 5200
- Added `ocelot.Development.json` with downstream routes

### Step 2 — OAuth Client Registration
- Registered Gateway as an OAuth client in Auth.Api's `clientAppStore.json`
  - `ClientId`: `e4a6c8f0b2d4e6a8c0f2b4d6e8a0c2f4`
  - `ClientSecret`: `f5b7d9e1a3c5f7b9d1e3a5c7f9b1d3e5`
  - `RedirectUri`: `http://localhost:5200/oauth/callback`
  - Scopes: `openid`, `profile`, `email`
- Filled in `OAuthClient` section in `appsettings.json`

### Step 3 — GatewayAuthMiddleware
- Created `GatewayAuthMiddleware` to protect all routes
- Public paths (no auth required): `/oauth/callback`, `/oauth/logout`, `/oauth/start`, `/openapi`
- OPTIONS requests bypass auth (CORS preflight)
- If `lb_session` cookie present and JWT valid → allow request through
- If no valid cookie:
  - XHR/API call (has `Origin` header) → return `401 JSON` so Angular interceptor can handle it
  - Browser navigation → `302 redirect` to Auth.Api login

### Step 4 — OAuthCallbackController
Three endpoints:

#### `GET /oauth/callback?code=&state=`
- Auth.Api redirects here after user logs in
- Gateway exchanges auth code for JWT (server-to-server call to Auth.Api `/oauth/token`)
- Stores JWT in `lb_session` HttpOnly cookie (Lax, no Secure in dev)
- Redirects browser back to Angular app using `state` parameter

#### `GET /oauth/start?returnUrl=`
- (Legacy) Builds authorize URL and redirects to Auth.Api
- No longer used — Angular interceptor now calls Auth.Api directly

#### `GET /oauth/logout`
- Clears `lb_session` cookie
- Redirects to Auth.Api login page

### Step 5 — CORS Configuration
- Added CORS policy `AllowAngular` for `http://localhost:4200`
- Allows any header, any method, with credentials (`AllowCredentials()`)
- `app.UseCors("AllowAngular")` placed **before** all other middleware

### Step 6 — OpenAPI (Built-in, no Swashbuckle)
- Used `builder.Services.AddOpenApi()` and `app.MapOpenApi()`
- Available at: `http://localhost:5200/openapi/v1.json`
- Removed Swashbuckle entirely

### Step 7 — Ocelot Proxy Routes (`ocelot.Development.json`)

| Upstream Path              | Downstream                  |
|----------------------------|-----------------------------|
| `/auth/{everything}`       | `localhost:5100/{everything}` |
| `/ticketmaster/{everything}` | `localhost:5300/{everything}` |
| `/redis/{everything}`      | `localhost:5000/{everything}` |
| `/observability/{everything}` | `localhost:5001/{everything}` |

### Step 8 — Middleware Pipeline Order (Critical)
```csharp
app.UseCors("AllowAngular");
app.UseRouting();                                          // must be explicit
app.UseMiddleware<GatewayAuthMiddleware>();
app.UseEndpoints(e => e.MapControllers());                 // runs BEFORE Ocelot
await app.UseOcelot();                                     // proxies everything else
```
`UseRouting()` + `UseEndpoints()` must be explicit so `/oauth/callback` is handled
by the controller **before** Ocelot can intercept and drop the connection.

---

## End-to-End OAuth + Booking Flow

```
1.  User navigates to Angular booking page
2.  Angular calls  →  GET/POST http://localhost:5200/ticketmaster/api/tickets
3.  Gateway checks →  lb_session cookie present?
4.  No cookie      →  Returns 401 JSON  (Origin header detected = XHR call)
5.  Angular interceptor catches 401
6.  Interceptor navigates browser to:
        http://localhost:5100/oauth/authorize
            ?response_type=code
            &client_id=<clientId>
            &redirect_uri=http://localhost:5200/oauth/callback
            &scope=openid profile email
            &state=http://localhost:4200/tickets/book   ← Angular return URL
7.  Auth.Api shows login page
8.  User enters credentials → POST /oauth/login
9.  Auth.Api validates credentials → generates auth code
10. Auth.Api redirects →  http://localhost:5200/oauth/callback?code=xxx&state=xxx
11. Gateway /oauth/callback:
        a. Receives auth code
        b. Calls Auth.Api POST /oauth/token (server-to-server)
        c. Auth.Api returns JWT (access_token)
        d. Gateway sets HttpOnly lb_session cookie
        e. Redirects browser back to http://localhost:4200/tickets/book
12. Angular booking page loads (user is now authenticated)
13. User fills form → clicks Confirm Booking
14. Angular POST → http://localhost:5200/ticketmaster/api/tickets  (cookie sent automatically)
15. Gateway validates lb_session JWT  →  valid
16. Ocelot proxies →  http://localhost:5300/api/tickets
17. TicketMaster saves ticket → returns 201
18. Angular shows success toast  ✅
```

---

## Bug Fixes Along the Way

### Snake_case Model Binding
OAuth standard uses `snake_case` params (`grant_type`, `client_id`, `redirect_uri`).
ASP.NET Core model binding does **not** auto-convert underscores to PascalCase.

Fixed by adding explicit name attributes:
- `AuthorizeQueryParams` → `[FromQuery(Name = "response_type")]` etc.
- `TokenRequest` → `[FromForm(Name = "grant_type")]` etc.

### JSON Deserialization
Auth.Api returns `access_token` (snake_case via `[JsonPropertyName]`).
Gateway's `TokenResult` had no mapping → `AccessToken` was always null.

Fixed by adding `[JsonPropertyName("access_token")]` to `TokenResult`.

### Ocelot Intercepting OAuth Callback
`app.MapControllers()` without explicit `UseRouting()` + `UseEndpoints()` caused
Ocelot to intercept `/oauth/callback` before the controller could handle it.
Ocelot dropped the connection → Chrome showed `chrome-error://chromewebdata/`.

Fixed by using explicit `UseRouting()` and `UseEndpoints()` before `UseOcelot()`.

### Chrome Error from Extra Redirect Hop
Original flow: `Angular → Gateway /oauth/start → Auth.Api`
When `/oauth/start` redirect failed, Chrome got stuck on `chrome-error://chromewebdata/`
and subsequent navigations were blocked.

Fixed by removing the `/oauth/start` hop. Angular interceptor now navigates
directly to `http://localhost:5100/oauth/authorize` using params from `environment.ts`.

### XHR vs Browser Navigation Detection
`GatewayAuthMiddleware` was returning 302 redirect for all unauthenticated requests.
Angular's XHR cannot follow cross-origin redirects → silent failure.

Fixed by detecting `Origin` header (present on XHR, absent on browser navigation):
- XHR → return `401 JSON`
- Browser navigation → `302 redirect` to Auth.Api

---

## Key Files

| File | Purpose |
|------|---------|
| `Middleware/GatewayAuthMiddleware.cs` | JWT cookie validation, 401 vs 302 logic |
| `Controllers/OAuthCallbackController.cs` | OAuth callback, logout endpoints |
| `Configuration/OAuthClientOptions.cs` | Typed config for OAuth client settings |
| `Configuration/JwtOptions.cs` | Typed config for JWT validation settings |
| `ocelot.Development.json` | Ocelot upstream → downstream route mappings |
| `appsettings.json` | JWT secret, OAuth client credentials, Auth.Api URL |
| `Program.cs` | Middleware pipeline (CORS → Routing → Auth → Endpoints → Ocelot) |
