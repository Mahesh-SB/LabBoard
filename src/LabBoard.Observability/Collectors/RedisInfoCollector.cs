using Prometheus;
using StackExchange.Redis;

namespace LabBoard.Observability.Collectors;

/// <summary>
/// Background service that periodically calls Redis INFO and updates
/// Prometheus gauges / counters so Grafana can visualize Redis server health.
///
/// Redis INFO sections used:
///   # clients  → connected_clients, blocked_clients
///   # memory   → used_memory, used_memory_rss
///   # stats    → total_commands_processed, keyspace_hits, keyspace_misses
///               total_connections_received, rejected_connections
///   # server   → uptime_in_seconds
/// </summary>
public class RedisInfoCollector : BackgroundService
{
    private readonly IConnectionMultiplexer _mux;
    private readonly ILogger<RedisInfoCollector> _logger;
    private readonly TimeSpan _interval;

    // ── Prometheus metric definitions ─────────────────────────────────────

    private static readonly Gauge ConnectedClients = Metrics.CreateGauge(
        "labboard_redis_connected_clients",
        "Number of client connections (not counting connections from replicas)");

    private static readonly Gauge BlockedClients = Metrics.CreateGauge(
        "labboard_redis_blocked_clients",
        "Number of clients pending a blocking command");

    private static readonly Gauge UsedMemoryBytes = Metrics.CreateGauge(
        "labboard_redis_used_memory_bytes",
        "Total bytes allocated by Redis using its allocator");

    private static readonly Gauge UsedMemoryRssBytes = Metrics.CreateGauge(
        "labboard_redis_used_memory_rss_bytes",
        "Bytes that Redis allocated as seen by the OS (RSS = Resident Set Size)");

    private static readonly Counter CommandsProcessed = Metrics.CreateCounter(
        "labboard_redis_commands_processed_total",
        "Total number of commands processed by the server");

    private static readonly Counter KeyspaceHits = Metrics.CreateCounter(
        "labboard_redis_keyspace_hits_total",
        "Number of successful lookups of keys in the main dictionary");

    private static readonly Counter KeyspaceMisses = Metrics.CreateCounter(
        "labboard_redis_keyspace_misses_total",
        "Number of failed lookups of keys in the main dictionary");

    private static readonly Counter ConnectionsReceived = Metrics.CreateCounter(
        "labboard_redis_connections_received_total",
        "Total number of connections accepted by the server");

    private static readonly Counter RejectedConnections = Metrics.CreateCounter(
        "labboard_redis_rejected_connections_total",
        "Number of connections rejected because of maxclients limit");

    private static readonly Gauge UptimeSeconds = Metrics.CreateGauge(
        "labboard_redis_uptime_seconds",
        "Number of seconds since Redis server start");

    // Track previous counter values so we can compute deltas (Redis INFO returns
    // cumulative totals, Prometheus counters must only increase).
    private long _prevCommandsProcessed;
    private long _prevKeyspaceHits;
    private long _prevKeyspaceMisses;
    private long _prevConnectionsReceived;
    private long _prevRejectedConnections;

    public RedisInfoCollector(IConnectionMultiplexer mux,
                              IConfiguration config,
                              ILogger<RedisInfoCollector> logger)
    {
        _mux      = mux;
        _logger   = logger;
        _interval = TimeSpan.FromSeconds(
            config.GetValue<int>("Metrics:ScrapeIntervalSeconds", 10));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RedisInfoCollector started — scraping every {Interval}s",
            _interval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ScrapeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to scrape Redis INFO");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }

    private async Task ScrapeAsync()
    {
        // IServer is needed to call INFO
        var server = _mux.GetServers().FirstOrDefault();
        if (server is null) return;

        // INFO returns a bulk string split into sections separated by \r\n#
        var info = await server.InfoAsync();

        // StackExchange.Redis parses INFO into IGrouping<string, KeyValuePair<string,string>>
        var flat = info.SelectMany(g => g)
                       .ToDictionary(kv => kv.Key, kv => kv.Value);

        // ── Clients ───────────────────────────────────────────────────────
        ConnectedClients.Set(ParseLong(flat, "connected_clients"));
        BlockedClients.Set(ParseLong(flat, "blocked_clients"));

        // ── Memory ────────────────────────────────────────────────────────
        UsedMemoryBytes.Set(ParseLong(flat, "used_memory"));
        UsedMemoryRssBytes.Set(ParseLong(flat, "used_memory_rss"));

        // ── Stats (cumulative → delta → counter.Inc) ─────────────────────
        var cmdTotal  = ParseLong(flat, "total_commands_processed");
        var ksHits    = ParseLong(flat, "keyspace_hits");
        var ksMisses  = ParseLong(flat, "keyspace_misses");
        var connTotal = ParseLong(flat, "total_connections_received");
        var rejTotal  = ParseLong(flat, "rejected_connections");

        CommandsProcessed.Inc(Math.Max(0, cmdTotal  - _prevCommandsProcessed));
        KeyspaceHits.Inc(     Math.Max(0, ksHits    - _prevKeyspaceHits));
        KeyspaceMisses.Inc(   Math.Max(0, ksMisses  - _prevKeyspaceMisses));
        ConnectionsReceived.Inc(Math.Max(0, connTotal - _prevConnectionsReceived));
        RejectedConnections.Inc(Math.Max(0, rejTotal  - _prevRejectedConnections));

        _prevCommandsProcessed  = cmdTotal;
        _prevKeyspaceHits       = ksHits;
        _prevKeyspaceMisses     = ksMisses;
        _prevConnectionsReceived = connTotal;
        _prevRejectedConnections = rejTotal;

        // ── Server ────────────────────────────────────────────────────────
        UptimeSeconds.Set(ParseLong(flat, "uptime_in_seconds"));

        _logger.LogDebug("Redis INFO scraped — clients={C} memory={M}MB",
            ParseLong(flat, "connected_clients"),
            ParseLong(flat, "used_memory") / 1024 / 1024);
    }

    private static long ParseLong(Dictionary<string, string> data, string key)
        => data.TryGetValue(key, out var raw) && long.TryParse(raw, out var v) ? v : 0;
}
