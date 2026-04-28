using Prometheus;
using StackExchange.Redis;
using LabBoard.Redis.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Redis ─────────────────────────────────────────────────────────────────
var redisConnStr = builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(redisConnStr));
builder.Services.AddSingleton<IRedisCacheService, RedisCacheService>();

// ── Health Checks ─────────────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddRedis(redisConnStr, name: "redis", tags: ["ready"]);

// ── API ───────────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "LabBoard Redis API", Version = "v1" });
});

var app = builder.Build();

// ── Middleware pipeline ───────────────────────────────────────────────────
app.UseSwagger();
app.UseSwaggerUI();

// prometheus-net: automatically records HTTP request duration + count
app.UseHttpMetrics();

app.MapControllers();
app.MapHealthChecks("/health");

// Expose /metrics for Prometheus to scrape
app.MapMetrics();

app.Run();
