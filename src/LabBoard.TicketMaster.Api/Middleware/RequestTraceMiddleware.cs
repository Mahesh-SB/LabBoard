using System.Diagnostics;
using System.Text;

namespace LabBoard.TicketMaster.Api.Middleware;

public class RequestTraceMiddleware(RequestDelegate next)
{
    private static readonly string ServiceName =
        System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name ?? "Unknown";

    private static readonly string LogPath = ResolveLogPath();

    static RequestTraceMiddleware()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
        File.AppendAllText(LogPath,
            $"{'=',90}{Environment.NewLine}" +
            $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff} UTC]  SERVICE STARTED: {System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name}{Environment.NewLine}" +
            $"  Log file : {LogPath}{Environment.NewLine}" +
            $"{new string('-', 90)}{Environment.NewLine}{Environment.NewLine}");
    }

    private static string ResolveLogPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (dir.GetFiles("*.sln").Length > 0)
                return Path.Combine(dir.FullName, "logs", "request-trace.log");
            dir = dir.Parent;
        }
        return Path.Combine(AppContext.BaseDirectory, "logs", "request-trace.log");
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Request.EnableBuffering();

        string requestBody = string.Empty;
        if (context.Request.ContentLength > 0 || context.Request.ContentType != null)
        {
            using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
            requestBody = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;
        }

        var sw = Stopwatch.StartNew();
        string? errorMessage = null;

        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            throw;
        }
        finally
        {
            sw.Stop();
            await WriteLogAsync(context, requestBody, context.Response.StatusCode, sw.ElapsedMilliseconds, errorMessage);
        }
    }

    private static async Task WriteLogAsync(HttpContext ctx, string body, int status, long ms, string? error)
    {
        var req = ctx.Request;
        var sb  = new StringBuilder();

        sb.AppendLine(new string('=', 90));
        sb.AppendLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff} UTC]  SERVICE: {ServiceName}");
        sb.AppendLine($"  Method     : {req.Method}");
        sb.AppendLine($"  Full URL   : {req.Scheme}://{req.Host}{req.Path}{req.QueryString}");
        sb.AppendLine($"  Decoded URL: {Uri.UnescapeDataString($"{req.Scheme}://{req.Host}{req.Path}{req.QueryString}")}");
        sb.AppendLine($"  Path       : {req.Path}");

        if (req.QueryString.HasValue)
        {
            sb.AppendLine($"  QueryString: {req.QueryString.Value}");
            sb.AppendLine("  Params     :");
            foreach (var (key, value) in req.Query)
                sb.AppendLine($"    {key,-22} = {value}");
        }

        sb.AppendLine("  Headers    :");
        string[] trackedHeaders =
        [
            "Host", "Origin", "Referer", "Content-Type", "Content-Length",
            "Authorization", "X-Forwarded-For", "Cookie", "User-Agent"
        ];
        foreach (var h in trackedHeaders)
        {
            if (!req.Headers.TryGetValue(h, out var val)) continue;
            string display = h switch
            {
                "Authorization" => "[REDACTED]",
                "Cookie"        => FormatCookie(val.ToString()),
                _               => val.ToString()
            };
            sb.AppendLine($"    {h,-22} = {display}");
        }

        if (!string.IsNullOrWhiteSpace(body))
        {
            sb.AppendLine($"  Body       : {body.Trim()}");

            if (req.ContentType?.Contains("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase) == true)
            {
                sb.AppendLine("  Form Fields:");
                foreach (var pair in body.Split('&', StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = pair.Split('=', 2);
                    var key   = Uri.UnescapeDataString(parts[0]);
                    var value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
                    sb.AppendLine($"    {key,-22} = {value}");
                }
            }
        }

        sb.AppendLine($"  Status     : {status}");
        sb.AppendLine($"  Duration   : {ms}ms");

        if (error != null)
            sb.AppendLine($"  Error      : {error}");

        sb.AppendLine(new string('-', 90));
        sb.AppendLine();

        var entry = sb.ToString();
        Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);

        for (int attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                await using var stream = new FileStream(LogPath, FileMode.Append, FileAccess.Write, FileShare.None);
                await using var writer = new StreamWriter(stream, Encoding.UTF8);
                await writer.WriteAsync(entry);
                return;
            }
            catch (IOException) when (attempt < 4)
            {
                await Task.Delay(10 * (attempt + 1));
            }
        }
    }

    private static string FormatCookie(string raw)
    {
        var names = raw.Split(';', StringSplitOptions.RemoveEmptyEntries)
                       .Select(c => c.Split('=')[0].Trim());
        return $"[{string.Join(", ", names)}]";
    }
}
