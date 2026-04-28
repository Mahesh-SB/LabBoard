using System.Net;

namespace LabBoard.Auth.Api.Helpers;

internal static class LoginPageHtml
{
    public static string Build(
        string appName,
        string clientId,
        string redirectUri,
        string scope,
        string state,
        string? error = null)
    {
        var errorBlock = error is null ? "" : $"""
            <div class="alert-error">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" width="16" height="16"><circle cx="12" cy="12" r="10"/><line x1="12" y1="8" x2="12" y2="12"/><line x1="12" y1="16" x2="12.01" y2="16"/></svg>
                {WebUtility.HtmlEncode(error)}
            </div>
            """;

        var scopeTags = scope
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => $"<span class=\"scope-tag\">{WebUtility.HtmlEncode(s)}</span>");

        return $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <meta charset="UTF-8">
                <meta name="viewport" content="width=device-width, initial-scale=1.0">
                <title>Sign in — {{WebUtility.HtmlEncode(appName)}}</title>
                <style>
                    *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }

                    body {
                        font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
                        min-height: 100vh;
                        display: flex;
                        align-items: center;
                        justify-content: center;
                        background: #0d0d1a;
                        overflow: hidden;
                        position: relative;
                    }

                    /* Animated background orbs */
                    body::before, body::after {
                        content: '';
                        position: fixed;
                        border-radius: 50%;
                        filter: blur(80px);
                        opacity: 0.35;
                        animation: float 8s ease-in-out infinite;
                        pointer-events: none;
                    }
                    body::before {
                        width: 500px; height: 500px;
                        background: radial-gradient(circle, #6c3bff, #3b0aff);
                        top: -150px; left: -150px;
                    }
                    body::after {
                        width: 400px; height: 400px;
                        background: radial-gradient(circle, #ff3b8b, #ff006a);
                        bottom: -120px; right: -120px;
                        animation-delay: -4s;
                    }

                    @keyframes float {
                        0%, 100% { transform: translateY(0) scale(1); }
                        50%       { transform: translateY(30px) scale(1.05); }
                    }

                    /* Card */
                    .card {
                        position: relative;
                        width: 100%;
                        max-width: 420px;
                        margin: 1rem;
                        background: rgba(255,255,255,0.04);
                        border: 1px solid rgba(255,255,255,0.1);
                        border-radius: 20px;
                        padding: 2.5rem 2.25rem;
                        backdrop-filter: blur(24px);
                        -webkit-backdrop-filter: blur(24px);
                        box-shadow: 0 25px 60px rgba(0,0,0,0.5), inset 0 1px 0 rgba(255,255,255,0.08);
                        animation: slideUp .45s cubic-bezier(.16,1,.3,1) both;
                    }

                    @keyframes slideUp {
                        from { opacity: 0; transform: translateY(28px); }
                        to   { opacity: 1; transform: translateY(0); }
                    }

                    /* Logo */
                    .logo {
                        display: flex;
                        align-items: center;
                        gap: .6rem;
                        margin-bottom: 1.75rem;
                    }
                    .logo-icon {
                        width: 40px; height: 40px;
                        background: linear-gradient(135deg, #7c3aed, #db2777);
                        border-radius: 10px;
                        display: flex; align-items: center; justify-content: center;
                        font-size: 1.2rem;
                        box-shadow: 0 4px 16px rgba(124,58,237,.5);
                    }
                    .logo-text {
                        font-size: 1.15rem;
                        font-weight: 700;
                        color: #fff;
                        letter-spacing: -.3px;
                    }
                    .logo-text span { color: #a78bfa; }

                    /* Heading */
                    .heading { margin-bottom: 1.5rem; }
                    .heading h1 {
                        font-size: 1.6rem;
                        font-weight: 700;
                        color: #fff;
                        letter-spacing: -.4px;
                        margin-bottom: .35rem;
                    }
                    .heading p {
                        font-size: .88rem;
                        color: rgba(255,255,255,0.45);
                        line-height: 1.5;
                    }
                    .heading p strong { color: rgba(255,255,255,0.7); }

                    /* Scope pills */
                    .scope-row {
                        display: flex;
                        flex-wrap: wrap;
                        gap: .4rem;
                        margin-bottom: 1.5rem;
                        padding: .75rem 1rem;
                        background: rgba(124,58,237,.12);
                        border: 1px solid rgba(124,58,237,.25);
                        border-radius: 10px;
                    }
                    .scope-label {
                        width: 100%;
                        font-size: .7rem;
                        font-weight: 600;
                        text-transform: uppercase;
                        letter-spacing: .08em;
                        color: rgba(255,255,255,.35);
                        margin-bottom: .2rem;
                    }
                    .scope-tag {
                        display: inline-flex;
                        align-items: center;
                        gap: .3rem;
                        background: rgba(124,58,237,.25);
                        color: #c4b5fd;
                        border: 1px solid rgba(124,58,237,.4);
                        border-radius: 20px;
                        padding: .2rem .65rem;
                        font-size: .75rem;
                        font-weight: 500;
                    }
                    .scope-tag::before { content: '✓'; font-size: .7rem; opacity: .8; }

                    /* Error */
                    .alert-error {
                        display: flex;
                        align-items: center;
                        gap: .5rem;
                        background: rgba(239,68,68,.12);
                        border: 1px solid rgba(239,68,68,.3);
                        color: #fca5a5;
                        border-radius: 10px;
                        padding: .75rem 1rem;
                        margin-bottom: 1.25rem;
                        font-size: .85rem;
                    }

                    /* Fields */
                    .field { margin-bottom: 1.1rem; }
                    .field label {
                        display: block;
                        font-size: .8rem;
                        font-weight: 600;
                        color: rgba(255,255,255,.55);
                        margin-bottom: .45rem;
                        letter-spacing: .02em;
                        text-transform: uppercase;
                    }
                    .input-wrap {
                        position: relative;
                        display: flex;
                        align-items: center;
                    }
                    .input-icon {
                        position: absolute;
                        left: .85rem;
                        color: rgba(255,255,255,.25);
                        pointer-events: none;
                        display: flex;
                    }
                    .field input {
                        width: 100%;
                        padding: .75rem .85rem .75rem 2.6rem;
                        background: rgba(255,255,255,.06);
                        border: 1px solid rgba(255,255,255,.1);
                        border-radius: 10px;
                        font-size: .95rem;
                        color: #fff;
                        outline: none;
                        transition: border-color .2s, background .2s, box-shadow .2s;
                    }
                    .field input::placeholder { color: rgba(255,255,255,.2); }
                    .field input:focus {
                        border-color: #7c3aed;
                        background: rgba(124,58,237,.08);
                        box-shadow: 0 0 0 3px rgba(124,58,237,.2);
                    }

                    /* Submit button */
                    .btn-signin {
                        width: 100%;
                        padding: .85rem;
                        margin-top: .4rem;
                        background: linear-gradient(135deg, #7c3aed, #db2777);
                        color: #fff;
                        border: none;
                        border-radius: 10px;
                        font-size: 1rem;
                        font-weight: 600;
                        cursor: pointer;
                        letter-spacing: .01em;
                        transition: opacity .2s, transform .15s, box-shadow .2s;
                        box-shadow: 0 4px 20px rgba(124,58,237,.4);
                    }
                    .btn-signin:hover {
                        opacity: .92;
                        transform: translateY(-1px);
                        box-shadow: 0 8px 28px rgba(124,58,237,.5);
                    }
                    .btn-signin:active { transform: translateY(0); }

                    /* Footer */
                    .footer {
                        text-align: center;
                        margin-top: 1.5rem;
                        font-size: .75rem;
                        color: rgba(255,255,255,.2);
                    }
                    .footer strong { color: rgba(255,255,255,.35); }
                </style>
            </head>
            <body>
                <div class="card">

                    <div class="logo">
                        <div class="logo-icon">🎟️</div>
                        <div class="logo-text">Lab<span>Board</span></div>
                    </div>

                    <div class="heading">
                        <h1>Welcome back</h1>
                        <p><strong>{{WebUtility.HtmlEncode(appName)}}</strong> is requesting access to your account</p>
                    </div>

                    <div class="scope-row">
                        <div class="scope-label">Permissions requested</div>
                        {{string.Join(" ", scopeTags)}}
                    </div>

                    {{errorBlock}}

                    <form method="post" action="/oauth/login">
                        <input type="hidden" name="clientId"    value="{{WebUtility.HtmlEncode(clientId)}}" />
                        <input type="hidden" name="redirectUri" value="{{WebUtility.HtmlEncode(redirectUri)}}" />
                        <input type="hidden" name="scope"       value="{{WebUtility.HtmlEncode(scope)}}" />
                        <input type="hidden" name="state"       value="{{WebUtility.HtmlEncode(state)}}" />

                        <div class="field">
                            <label for="email">Email</label>
                            <div class="input-wrap">
                                <span class="input-icon">
                                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" width="16" height="16">
                                        <rect x="2" y="4" width="20" height="16" rx="2"/>
                                        <path d="m22 7-8.97 5.7a1.94 1.94 0 0 1-2.06 0L2 7"/>
                                    </svg>
                                </span>
                                <input type="email" id="email" name="email"
                                       placeholder="you@example.com" required autocomplete="email" />
                            </div>
                        </div>

                        <div class="field">
                            <label for="password">Password</label>
                            <div class="input-wrap">
                                <span class="input-icon">
                                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" width="16" height="16">
                                        <rect x="3" y="11" width="18" height="11" rx="2"/>
                                        <path d="M7 11V7a5 5 0 0 1 10 0v4"/>
                                    </svg>
                                </span>
                                <input type="password" id="password" name="password"
                                       placeholder="••••••••" required autocomplete="current-password" />
                            </div>
                        </div>

                        <button type="submit" class="btn-signin">Sign in to continue</button>
                    </form>

                    <div class="footer">
                        Secured by <strong>LabBoard Auth</strong> &middot; Your credentials are never shared
                    </div>

                </div>
            </body>
            </html>
            """;
    }

    public static string Error(string message) => $$"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
            <meta charset="UTF-8">
            <meta name="viewport" content="width=device-width, initial-scale=1.0">
            <title>Authorization Error — LabBoard</title>
            <style>
                *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }
                body {
                    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
                    min-height: 100vh;
                    display: flex; align-items: center; justify-content: center;
                    background: #0d0d1a;
                }
                body::before {
                    content: '';
                    position: fixed;
                    width: 400px; height: 400px;
                    background: radial-gradient(circle, #7f1d1d, #450a0a);
                    top: -100px; right: -100px;
                    border-radius: 50%;
                    filter: blur(80px);
                    opacity: .4;
                    pointer-events: none;
                }
                .card {
                    position: relative;
                    width: 100%; max-width: 400px;
                    margin: 1rem;
                    background: rgba(255,255,255,.04);
                    border: 1px solid rgba(239,68,68,.2);
                    border-radius: 20px;
                    padding: 2.5rem 2.25rem;
                    backdrop-filter: blur(24px);
                    box-shadow: 0 25px 60px rgba(0,0,0,.5);
                    text-align: center;
                    animation: slideUp .4s cubic-bezier(.16,1,.3,1) both;
                }
                @keyframes slideUp {
                    from { opacity: 0; transform: translateY(24px); }
                    to   { opacity: 1; transform: translateY(0); }
                }
                .err-icon {
                    font-size: 3rem;
                    margin-bottom: 1rem;
                    display: block;
                }
                h1 {
                    font-size: 1.4rem;
                    font-weight: 700;
                    color: #fca5a5;
                    margin-bottom: .75rem;
                }
                p {
                    color: rgba(255,255,255,.45);
                    font-size: .9rem;
                    line-height: 1.6;
                }
            </style>
        </head>
        <body>
            <div class="card">
                <span class="err-icon">🔒</span>
                <h1>Authorization Error</h1>
                <p>{{WebUtility.HtmlEncode(message)}}</p>
            </div>
        </body>
        </html>
        """;
}
