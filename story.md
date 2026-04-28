# LabBoard — Story Backlog

## Next Task

### JWKS Endpoint — Standard Public Key Discovery

**Why:**
Currently the RSA public key is hardcoded in `Gateway.Api/appsettings.json` and shared manually.
This is not the industry standard. Real OAuth providers (Google, Auth0, Okta) expose a JWKS
endpoint so any verifier can fetch the public key automatically — no manual key sharing, no
redeployment on key rotation.

**What to implement:**

1. **Auth.Api — expose `GET /.well-known/jwks.json`**
   - Return the RSA public key in standard JWK format (kty, use, alg, kid, n, e)
   - Add a `WellKnownController` with the JWKS endpoint

2. **Auth.Api — remove `PrivateKey` from `appsettings.json`**
   - Generate RSA key pair on first run and persist to a secure local file (dev)
   - Or load from environment variable / secrets manager (production)

3. **Gateway.Api — fetch public key from JWKS endpoint on startup**
   - Remove `PublicKey` from `appsettings.json`
   - On startup call `GET http://localhost:5100/.well-known/jwks.json`
   - Parse the JWK and build `RsaSecurityKey` dynamically
   - Support periodic refresh for key rotation (no redeployment needed)

**Flow after implementation:**
```
Client App registers → GET /.well-known/jwks.json → fetch public key → verify JWTs
Auth.Api holds private key (never leaves the service)
Gateway.Api fetches public key automatically at startup
```

**Files to change:**
- `src/LabBoard.Auth.Api/Controllers/WellKnownController.cs`  (new)
- `src/LabBoard.Auth.Api/appsettings.json`                    (remove PrivateKey)
- `src/LabBoard.Auth.Api/Program.cs`                          (register key gen service)
- `src/LabBoard.Gateway.Api/appsettings.json`                 (remove PublicKey)
- `src/LabBoard.Gateway.Api/Program.cs`                       (fetch JWKS on startup)
- `src/LabBoard.Gateway.Api/Middleware/GatewayAuthMiddleware.cs` (use fetched key)
