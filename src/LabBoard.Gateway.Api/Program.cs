using LabBoard.Gateway.Api.Configuration;
using LabBoard.Gateway.Api.Middleware;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;

var builder = WebApplication.CreateBuilder(args);

var ocelotFile = builder.Environment.IsDevelopment()
    ? "ocelot.Development.json"
    : "ocelot.json";

builder.Configuration.AddJsonFile(ocelotFile, optional: false, reloadOnChange: true);

// BFF options
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<OAuthClientOptions>(builder.Configuration.GetSection("OAuthClient"));

// Named HttpClient for server-to-server calls to Auth.Api
builder.Services.AddHttpClient("AuthApi", (sp, client) =>
{
    var opts = sp.GetRequiredService<IConfiguration>()["OAuthClient:AuthApiBaseUrl"]!;
    client.BaseAddress = new Uri(opts);
});

builder.Services.AddCors(options =>
    options.AddPolicy("AllowAngular", policy =>
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()));

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddOcelot(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("AllowAngular");

// Routing must be explicit so controllers resolve BEFORE Ocelot sees the request
app.UseRouting();

// 1. Trace every incoming request before any auth or routing logic
app.UseMiddleware<RequestTraceMiddleware>();

// 2. BFF cookie auth — redirect to Auth.Api login if lb_session is missing or invalid
app.UseMiddleware<GatewayAuthMiddleware>();

// 3. Gateway-local endpoints (/oauth/callback, /oauth/start, /oauth/logout)
//    UseEndpoints here ensures these run before Ocelot's middleware
app.UseEndpoints(endpoints => endpoints.MapControllers());

// 4. Proxy everything else to downstream services
await app.UseOcelot();

app.Run();
