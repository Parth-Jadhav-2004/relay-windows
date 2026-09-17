using System.Text.Json;
using Tinycast.Features.Calendar;
using Tinycast.Features.Commands;
using Windows.ApplicationModel.Appointments;
using Windows.Security.Credentials;

namespace Tinycast.Platform;

internal static class CredentialStore
{
    const string Resource = "Tinycast";

    public static void Set(string user, string secret)
    {
        var vault = new PasswordVault();
        try
        {
            var existing = vault.Retrieve(Resource, user);
            vault.Remove(existing);
        }
        catch (Exception) { }

        if (string.IsNullOrWhiteSpace(secret))
            return;
        vault.Add(new PasswordCredential(Resource, user, secret));
    }

    public static string? Get(string user)
    {
        try
        {
            var vault = new PasswordVault();
            var cred = vault.Retrieve(Resource, user);
            cred.RetrievePassword();
            return cred.Password;
        }
        catch (Exception)
        {
            return null;
        }
    }
}

public sealed class AiConfig
{
    public string Endpoint { get; set; } = "https://api.openai.com/v1/chat/completions";
    public string Model { get; set; } = "gpt-4o-mini";
}

public static class AiClient
{
    static readonly HttpClient Http = CreateHttp();

    static HttpClient CreateHttp()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(45) };
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Tinycast/0.1");
        return client;
    }

    public static AiConfig Load()
    {
        try
        {
            if (!File.Exists(AppPaths.AiConfigFile))
                return new AiConfig();
            return JsonSerializer.Deserialize<AiConfig>(File.ReadAllText(AppPaths.AiConfigFile)) ?? new AiConfig();
        }
        catch (Exception)
        {
            return new AiConfig();
        }
    }

    public static void Save(AiConfig config)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(AppPaths.AiConfigFile)!);
        File.WriteAllText(AppPaths.AiConfigFile, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static async Task<string> Complete(IReadOnlyList<AiChatMessage> messages, string apiKey, AiConfig config, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("AI API key is missing.");
        var payload = new
        {
            model = config.Model,
            messages = messages.Select(m => new { role = m.Role, content = m.Content }).ToArray(),
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, config.Endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + apiKey);
        using var response = await Http.SendAsync(request, token);
        var body = await response.Content.ReadAsStringAsync(token);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException("AI HTTP " + (int)response.StatusCode + ": " + Truncate(body));
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("AI returned a non-JSON response.");
        }

        using (doc)
        {
            if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var message = choices[0].GetProperty("message").GetProperty("content").GetString();
                if (!string.IsNullOrWhiteSpace(message))
                    return message.Trim();
            }
        }

        throw new InvalidOperationException("AI response had no content.");
    }

    static string Truncate(string text) => text.Length <= 180 ? text : text[..180] + "…";
}

internal static class CalendarService
{
    public static async Task<IReadOnlyList<MeetingEvent>> UpcomingAsync(int days = 7)
    {
        try
        {
            var store = await AppointmentManager.RequestStoreAsync(AppointmentStoreAccessType.AllCalendarsReadOnly);
            var start = DateTimeOffset.Now;
            var appointments = await store.FindAppointmentsAsync(start, TimeSpan.FromDays(days));
            return appointments.Select(a =>
            {
                var startLocal = a.StartTime.LocalDateTime;
                var end = startLocal + a.Duration;
                var link = MeetingLink.Detect([a.Subject, a.Location, a.Details]);
                var id = string.IsNullOrWhiteSpace(a.LocalId)
                    ? "cal:" + (a.CalendarId ?? "") + ":" + a.StartTime.UtcTicks + ":" + (a.Subject ?? "") + ":" + a.AllDay
                    : a.LocalId;
                return new MeetingEvent(id, a.Subject ?? "Event", startLocal, end, link, a.AllDay, a.CalendarId);
            }).ToList();
        }
        catch (Exception ex)
        {
            Log.Write("calendar: " + ex.Message);
            throw new InvalidOperationException("Calendar access failed: " + ex.Message, ex);
        }
    }
}

internal static class McpHost
{
    public static IReadOnlyList<McpServerSpec> Load() => JsonList.Load<McpServerSpec>(AppPaths.McpFile);

    public static void Save(List<McpServerSpec> specs) => JsonList.Save(AppPaths.McpFile, specs);

    public static string Status(IReadOnlyList<McpServerSpec> specs, bool enabled)
    {
        if (!enabled)
            return "MCP is off. Enabling it can start local processes listed in Settings.";
        var trusted = specs.Count(s => s.Trusted);
        return trusted == 0
            ? "No trusted MCP servers. Mark a spec trusted before it can run."
            : trusted + " trusted MCP server(s). Stdio sessions stay off until a command asks.";
    }
}
