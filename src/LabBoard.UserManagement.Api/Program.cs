using LabBoard.UserManagement.Api.Configuration;
using LabBoard.UserManagement.Api.Infrastructure;
using LabBoard.UserManagement.Api.Middleware;
using LabBoard.UserManagement.Api.Services.User;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()!;
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularDev", policy =>
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddOpenApi();
builder.Services.AddScoped<IUserManagementService, UserManagementService>();

// ── JWT key fetching ──────────────────────────────────────────────────────────
builder.Services.AddHttpClient();
builder.Services.AddSingleton<JwksFetcher>();

// Captured by the IssuerSigningKeyResolver closure — set after builder.Build()
WebApplication? builtApp = null;

// ── Authentication ────────────────────────────────────────────────────────────
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.RequireHttpsMetadata = false; // dev only — remove in production
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer              = jwtOptions.Issuer,
            ValidAudience            = jwtOptions.Audience,
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime         = true,
            // Keys are loaded lazily from Auth API's JWKS endpoint and cached for 1 h
            IssuerSigningKeyResolver = (_, _, _, _) =>
                builtApp!.Services.GetRequiredService<JwksFetcher>().GetKeys()
        };
    });

// ── Authorization policies ────────────────────────────────────────────────────
builder.Services.AddAuthorization(opts =>
{
    // Requires a valid service token that includes the "user_management_app:read" scope
    opts.AddPolicy("ServiceRead", policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim("scope", "user_management_app:read"));
});

var app = builder.Build();
builtApp = app; // resolve the JWKS fetcher closure

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
        options.WithTitle("LabBoard UserManagement API"));
}

app.UseMiddleware<RequestTraceMiddleware>();
app.UseCors("AllowAngularDev");
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
