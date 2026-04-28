using Prometheus;
using StackExchange.Redis;
using LabBoard.Observability.Collectors;

var builder = WebApplication.CreateBuilder(args);

// ── Redis ─────────────────────────────────────────────────────────────────
var redisConnStr = builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(redisConnStr));

// ── Background service that scrapes Redis INFO and updates Prometheus gauges
builder.Services.AddHostedService<RedisInfoCollector>();

// ── Health check ──────────────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddRedis(redisConnStr, name: "redis");

var app = builder.Build();

// Expose /metrics for Prometheus to scrape
app.UseHttpMetrics();
app.MapMetrics();
app.MapHealthChecks("/health");

// Simple status endpoint so you can verify the service is alive
app.MapGet("/", () => new
{
    service = "LabBoard.Observability",
    status  = "running",
    metrics = "/metrics",
    health  = "/health"
});

app.Run();
