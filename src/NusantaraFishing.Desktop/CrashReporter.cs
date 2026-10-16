using System.Text.Json;

namespace NusantaraFishing.Desktop;

/// <summary>Opt-in, local-only diagnostic reports. No network transport.</summary>
public static class CrashReporter
{
    public static bool Enabled { get; set; }
    private static string? folder;
    private static bool registered;
    public static void Configure(string directory, bool enabled)
    {
        folder = directory; Enabled = enabled;
        if (registered) return;
        registered = true;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (!Enabled || folder is null) return;
            try
            {
                Directory.CreateDirectory(folder);
                foreach (var old in Directory.GetFiles(folder, "crash-*.json").OrderDescending().Skip(9)) File.Delete(old);
                var error = args.ExceptionObject.ToString() ?? "Unknown failure";
                if (error.Length > 32000) error = error[..32000];
                var report = new
                {
                    Timestamp = DateTimeOffset.UtcNow,
                    Version = typeof(App).Assembly.GetName().Version?.ToString(),
                    OperatingSystem = Environment.OSVersion.ToString(),
                    Runtime = Environment.Version.ToString(),
                    Error = error
                };
                File.WriteAllText(Path.Combine(folder, "crash-" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + ".json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* Diagnostics must not trigger a second crash. */ }
        };
    }
}
