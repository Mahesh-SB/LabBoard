# Interview Preparation Notes — LabBoard Project Concepts

---

## 1. OAuth 2.0 Authorization Code Flow

**Q: What is the Authorization Code Flow and why is it used?**

The Authorization Code Flow is the most secure OAuth 2.0 grant type for user login. It works in 3 steps:

1. `GET /oauth/authorize` — client redirects browser to auth server with `response_type=code`
2. User logs in — auth server issues a short-lived `authorization_code` and redirects back
3. `POST /oauth/token` — client exchanges `code` for a JWT access token server-to-server

The `code` is short-lived (10 minutes), single-use, and travels only through the browser redirect. The actual token exchange happens server-to-server — the token never touches the browser URL.

**Q: What does the `state` parameter do in OAuth?**

`state` is an opaque value the client sends on `/oauth/authorize` and gets back on the redirect. It serves two purposes:
- **CSRF protection** — verifies the redirect came from the expected auth server
- **Navigation** — carries the original URL so the user lands back on the right page after login

> Key point: `state` is for navigation context only — never store sensitive payload in it.

**Q: What is stored in the auth code entity?**

`clientId`, `userId`, `redirectUri`, `scopes[]`, `state`, `ExpiresAt` (10 min), `IsUsed` flag.
The `IsUsed` flag prevents replay attacks — once consumed it cannot be used again.

---

## 2. Grant Types — When to Use Which

**Q: Explain the three main OAuth grant types.**

| Grant Type | When to Use | Client Type |
|---|---|---|
| `authorization_code` | User login with browser redirect | Confidential (has secret) |
| `authorization_code + PKCE` | User login, no server to hold secret | Public (SPA, mobile) |
| `client_credentials` | Machine-to-machine, no user involved | Confidential |

**Q: What is a confidential vs public client?**

- **Confidential** — runs on a server, can safely store `client_secret` (e.g., Gateway BFF, backend service)
- **Public** — runs in browser or on device, `client_secret` would be exposed in JS bundle or decompiled app (e.g., Angular SPA, mobile app)

---

## 3. PKCE — Proof Key for Code Exchange (RFC 7636)

**Q: What problem does PKCE solve?**

Public clients (SPAs, mobile apps) cannot store a `client_secret` safely. Without PKCE, an attacker who intercepts the authorization code can exchange it for a token with no secret needed.

PKCE replaces `client_secret` with a one-time cryptographic proof:

```
1. Client generates:
   code_verifier  = random 32 bytes (kept in memory, never sent yet)
   code_challenge = BASE64URL(SHA256(code_verifier))

2. GET /oauth/authorize
   ?code_challenge=xyz&code_challenge_method=S256
   (auth server stores code_challenge with the code)

3. POST /oauth/token
   code=xxx&code_verifier=original_bytes
   (auth server computes SHA256(verifier) and compares with stored challenge)
```

**Q: Why can't an attacker use an intercepted code even without PKCE?**

They can if there is no PKCE — that is exactly the vulnerability PKCE solves. With PKCE, the attacker has the code but not the `code_verifier`, which was never sent over the wire until the token request. The auth server rejects any token request where `SHA256(verifier) != stored challenge`.

**Q: When would you use PKCE in a real project?**

When the frontend (Angular/React/mobile) calls the Auth server directly with no BFF/Gateway in between. If a Gateway BFF exists, it acts as a confidential client and handles OAuth — PKCE is not needed.

---

## 4. BFF — Backend for Frontend Pattern

**Q: What is the BFF pattern and why is it used?**

BFF (Backend for Frontend) is a server-side proxy that acts as the OAuth confidential client on behalf of the frontend. The frontend never handles tokens directly.

```
Browser/Angular → Gateway (BFF) → Auth.Api
                      ↑
              - Registers as confidential client
              - Stores client_secret on server
              - Exchanges code for JWT
              - Stores JWT in HttpOnly cookie
              - Angular never sees the token
```

**Q: What are the security benefits of BFF over a pure SPA flow?**

- JWT is stored in an `HttpOnly` cookie — JavaScript cannot read it (XSS protection)
- `client_secret` never leaves the server
- Token refresh can happen server-side silently
- Frontend complexity is reduced — Angular just calls Gateway

**Q: What is the `lb_session` cookie and why is it HttpOnly?**

`lb_session` is the cookie set by the Gateway after token exchange. `HttpOnly` means JavaScript cannot access it via `document.cookie`, protecting against XSS attacks. `SameSite=Lax` protects against CSRF.

---

## 5. JWT Signing — Symmetric vs Asymmetric

**Q: What is the difference between HMAC-SHA256 and RSA-SHA256 for JWT signing?**

| | HMAC-SHA256 (Symmetric) | RSA-SHA256 (Asymmetric) |
|---|---|---|
| Keys | Same secret key signs and verifies | Private key signs, public key verifies |
| Key sharing | Both services must share the secret | Public key can be shared freely |
| Security | Secret leaks if any service is compromised | Private key never leaves Auth server |
| Use case | Single service / simple setup | Distributed services |

**Q: In a microservices architecture, which should you use and why?**

RSA asymmetric. Auth.Api holds the private key and signs the JWT. Any downstream service (Gateway, APIs) only needs the public key to verify — they cannot forge tokens. If a downstream service is compromised, the attacker only has the public key, which is useless for signing.

**Q: What is a JWKS endpoint and why is it the industry standard?**

`GET /.well-known/jwks.json` — an endpoint that exposes the public key in a standard JWK (JSON Web Key) format containing `kty`, `use`, `alg`, `kid`, `n` (modulus), `e` (exponent).

Benefits:
- No manual key copying between services
- Gateway fetches the key automatically at startup
- Key rotation is seamless — services re-fetch without redeployment
- Used by Google, Auth0, Okta

---

## 6. Browser Mechanics — Origin Header and Redirects

**Q: What is the `Origin` header and when does the browser send it?**

`Origin` is a CORS header the browser adds automatically on JavaScript-initiated HTTP requests (`fetch()` / `XMLHttpRequest`). It identifies where the request came from.

The browser does NOT send `Origin` on:
- Address bar navigation
- F5 / refresh
- Clicking `<a href>` links
- Bookmarks

> Key point: `Origin` is not specific to Angular or React — it is sent by any JavaScript HTTP call.

**Q: Why does the Gateway return 401 for XHR requests instead of 302?**

When Angular's `HttpClient` makes a request and the server returns 302, the browser follows the redirect silently and automatically. Angular receives the final response (HTML login page) — it never sees the 302 and cannot act on it.

The solution:
- Gateway detects `Origin` header → returns **401**
- Angular HTTP interceptor catches 401 → does `window.location.href = authorizeUrl` manually
- Now it becomes a real browser navigation → 302 works perfectly

**Q: When does the Gateway's 302 redirect actually work then?**

When the browser makes a direct navigation request (address bar, F5, bookmark). In this case:
- Angular is not loaded — there is no JavaScript running
- The browser follows 302 natively
- The whole login flow (login page → form submit → callback → cookie → redirect back) completes
- Angular only loads at the very end when the browser navigates back to the app URL

---

## 7. Session & State Management in SPA

**Q: How do you preserve form data across an OAuth redirect?**

Use `sessionStorage` to persist the form before the redirect and restore it on return.

```typescript
// Before redirect (on 401)
sessionStorage.setItem('pending_booking', JSON.stringify(this.form));

// On return — ngOnInit checks and auto-retries
ngOnInit(): void {
  const pending = sessionStorage.getItem('pending_booking');
  if (pending) {
    this.form = JSON.parse(pending);
    this.submitBooking();
  }
}
```

Clear sessionStorage on success or non-401 error — only keep it alive across the redirect.

**Q: Why sessionStorage and not localStorage?**

`sessionStorage` is scoped to the browser tab and cleared when the tab closes. `localStorage` persists forever — stale pending data could retry unexpectedly in a future session.

---

## 8. Ocelot API Gateway

**Q: What is Ocelot and what does it do?**

Ocelot is an ASP.NET Core API Gateway library. It acts as a single entry point — all client requests go to the Gateway, which routes them to the correct downstream service based on URL prefix.

```
/auth/{everything}          → Auth.Api        :5100
/redis/{everything}         → Redis.Api       :5101
/observability/{everything} → Observability   :5102
```

**Q: How do you handle local dev vs Docker routing in Ocelot?**

Two config files:
- `ocelot.Development.json` — uses `localhost` + port numbers
- `ocelot.json` — uses Docker container names as hostnames

`Program.cs` selects based on environment:
```csharp
IsDevelopment() ? "ocelot.Development.json" : "ocelot.json"
```

---

## 9. Cross-Process File Writing

**Q: How do you safely write to a shared log file from multiple processes?**

Use an exclusive file lock with a retry loop. Each process tries to open the file with `FileShare.None` — if another process holds it, retry with increasing backoff.

```csharp
for (int i = 0; i < 5; i++)
{
    try
    {
        using var fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(fs);
        writer.Write(content);
        return;
    }
    catch (IOException) { await Task.Delay(10 * (i + 1)); }
}
```

---

## 10. General Security Principles

**Q: Why should error messages never reveal whether an email exists?**

Prevents user enumeration attacks. "Invalid email or password" is the correct message — never "email not found" or "wrong password" separately, as that confirms account existence to an attacker.

**Q: What is a CSRF attack and how does SameSite=Lax protect against it?**

CSRF (Cross-Site Request Forgery) tricks a user's browser into making an unintended request to a site where they are authenticated. `SameSite=Lax` tells the browser to only send the cookie on same-site navigation and top-level GET requests — cross-site POST requests (the typical CSRF vector) will not include the cookie.

**Q: Why is `client_secret` in appsettings.json a security concern?**

If the repository is public or the config file is leaked, the secret is exposed. In production, secrets should come from environment variables or a secrets manager (Azure Key Vault, AWS Secrets Manager) — never committed to source control.

---

## 11. API Documentation — OpenAPI, Swagger, Scalar

**Q: What is OpenAPI?**

OpenAPI (formerly known as the Swagger Specification) is a **language-agnostic standard** for describing REST APIs in JSON or YAML. It defines endpoints, request/response models, authentication, and more.

> Key point: OpenAPI is the **specification standard** — not a tool or library.

**Q: What is the difference between Swagger, Swagger UI, and Scalar?**

A common interview mistake is using "Swagger" to mean everything. They are three separate concerns:

| Layer | What it is | Examples |
|---|---|---|
| **OpenAPI Specification** | The standard/format (JSON or YAML) | `openapi/v1.json` |
| **Spec Generator** | Library that reads your code and produces the spec | `Microsoft.AspNetCore.OpenApi`, `Swashbuckle` |
| **UI Explorer** | Reads the spec and renders an interactive browser UI | **Scalar**, Swagger UI, Redoc |

**Q: What is Scalar and why is it preferred over Swagger UI?**

Scalar is a modern **API Reference UI / OpenAPI UI Explorer**. It replaces Swagger UI as the interactive documentation layer.

| | Swagger UI | Scalar |
|---|---|---|
| UI design | Functional but dated | Clean, modern |
| Dark mode | No | Yes |
| Request builder | Basic | Richer — shows curl, code snippets |
| Active development | Slowing down | Actively maintained |
| .NET default | Was default pre-.NET 9 | Default in .NET 9/10 templates |

> In our project: `Microsoft.AspNetCore.OpenApi` **generates** the spec at `/openapi/v1.json`. Scalar **displays** it at `/scalar/v1`.

**Q: How do you describe the full setup in one sentence?**

> "We use `Microsoft.AspNetCore.OpenApi` to generate the OpenAPI specification from our controllers, and Scalar as the UI explorer to browse and test endpoints interactively."

---

## Quick Reference — Flow Decision Tree

```
Does the flow involve a user logging in?
    │
    ├── YES
    │     │
    │     ├── Is there a server-side BFF / Gateway?
    │     │       YES → Authorization Code + client_secret
    │     │       NO  → Authorization Code + PKCE
    │
    └── NO (service-to-service)
              → Client Credentials
```
