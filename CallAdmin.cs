using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Cvars;
using System.Text;
using System.Text.Json;

namespace CallAdmin;

public class CallAdmin : BasePlugin, IPluginConfig<CallAdminConfig>
{
    public override string ModuleName => "CallAdmin";
    public override string ModuleVersion => "1.0.0";
    public override string ModuleAuthor => "Luky";
    public override string ModuleDescription => "Call admin system with Discord/API integration";

    public CallAdminConfig Config { get; set; } = new();

    // Cooldown tracking - SteamID -> Last report time
    private readonly Dictionary<ulong, DateTime> _cooldowns = new();

    // Reusable HTTP client
    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    public void OnConfigParsed(CallAdminConfig config)
    {
        Config = config;
    }

    public override void Load(bool hotReload)
    {
        Console.WriteLine($"[CallAdmin] Plugin v{ModuleVersion} loaded!");

        // Register the calladmin command
        AddCommand("css_calladmin", "Call an admin for help", OnCallAdminCommand);

        Console.WriteLine($"[CallAdmin] Server: {Config.ServerName}, Language: {Config.Language}");
    }

    public override void Unload(bool hotReload)
    {
        Console.WriteLine("[CallAdmin] Plugin unloaded.");
    }

    [CommandHelper(minArgs: 1, usage: "<reason> or @<player> <reason>", whoCanExecute: CommandUsage.CLIENT_ONLY)]
    private void OnCallAdminCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid)
            return;

        // Check cooldown
        if (IsOnCooldown(player.SteamID, out int remainingSeconds))
        {
            string cooldownMsg = GetMessage("OnCooldown").Replace("{seconds}", remainingSeconds.ToString());
            player.PrintToChat($" {ChatColors.Red}{Config.ChatPrefix}{ChatColors.Default} {cooldownMsg}");
            return;
        }

        // Get the full argument string
        string fullArgs = command.GetCommandString;

        // Remove the command itself from the string
        int firstSpace = fullArgs.IndexOf(' ');
        if (firstSpace == -1)
        {
            string noReasonMsg = GetMessage("NoReason");
            player.PrintToChat($" {ChatColors.Red}{Config.ChatPrefix}{ChatColors.Default} {noReasonMsg}");
            return;
        }

        string argsOnly = fullArgs.Substring(firstSpace + 1).Trim();

        if (string.IsNullOrWhiteSpace(argsOnly))
        {
            string noReasonMsg = GetMessage("NoReason");
            player.PrintToChat($" {ChatColors.Red}{Config.ChatPrefix}{ChatColors.Default} {noReasonMsg}");
            return;
        }

        // Check if first argument starts with @ (target player)
        CCSPlayerController? targetPlayer = null;
        string reason = argsOnly;

        if (argsOnly.StartsWith("@"))
        {
            // Parse @player mention
            string[] parts = argsOnly.Split(' ', 2);
            string targetName = parts[0].Substring(1); // Remove @

            if (string.IsNullOrWhiteSpace(targetName))
            {
                string noReasonMsg = GetMessage("NoReason");
                player.PrintToChat($" {ChatColors.Red}{Config.ChatPrefix}{ChatColors.Default} {noReasonMsg}");
                return;
            }

            // Find target player
            targetPlayer = FindPlayerByName(targetName);

            if (targetPlayer == null)
            {
                string notFoundMsg = GetMessage("PlayerNotFound").Replace("{name}", targetName);
                player.PrintToChat($" {ChatColors.Yellow}{Config.ChatPrefix}{ChatColors.Default} {notFoundMsg}");
                return;
            }

            // Get reason (everything after @player)
            reason = parts.Length > 1 ? parts[1].Trim() : "No reason specified";

            if (string.IsNullOrWhiteSpace(reason) || reason == "No reason specified")
            {
                reason = Config.Language == "cs" ? "Bez udání důvodu" : "No reason specified";
            }
        }

        // Set cooldown
        _cooldowns[player.SteamID] = DateTime.UtcNow;

        // Send report to API
        SendReportAsync(player, targetPlayer, reason);

        // Send confirmation message
        if (targetPlayer != null)
        {
            string confirmMsg = GetMessage("ReportWithTarget")
                .Replace("{target}", targetPlayer.PlayerName)
                .Replace("{reason}", reason);
            player.PrintToChat($" {ChatColors.Green}{Config.ChatPrefix}{ChatColors.Default} {confirmMsg}");
        }
        else
        {
            string confirmMsg = GetMessage("ReportSent");
            player.PrintToChat($" {ChatColors.Green}{Config.ChatPrefix}{ChatColors.Default} {confirmMsg}");
        }

        Console.WriteLine($"[CallAdmin] Report from {player.PlayerName}: {reason}" +
            (targetPlayer != null ? $" (Target: {targetPlayer.PlayerName})" : ""));
    }

    private bool IsOnCooldown(ulong steamId, out int remainingSeconds)
    {
        remainingSeconds = 0;

        if (!_cooldowns.TryGetValue(steamId, out DateTime lastReport))
            return false;

        TimeSpan elapsed = DateTime.UtcNow - lastReport;
        int cooldownSeconds = Config.CooldownSeconds;

        if (elapsed.TotalSeconds < cooldownSeconds)
        {
            remainingSeconds = cooldownSeconds - (int)elapsed.TotalSeconds;
            return true;
        }

        return false;
    }

    private CCSPlayerController? FindPlayerByName(string partialName)
    {
        var players = Utilities.GetPlayers()
            .Where(p => p.IsValid && !p.IsBot && !p.IsHLTV)
            .ToList();

        // Exact match first
        var exactMatch = players.FirstOrDefault(p =>
            p.PlayerName.Equals(partialName, StringComparison.OrdinalIgnoreCase));

        if (exactMatch != null)
            return exactMatch;

        // Partial match
        var partialMatch = players.FirstOrDefault(p =>
            p.PlayerName.Contains(partialName, StringComparison.OrdinalIgnoreCase));

        return partialMatch;
    }

    private string GetMessage(string key)
    {
        bool isEnglish = Config.Language.ToLower() == "en";

        return key switch
        {
            "ReportSent" => isEnglish ? Config.Messages.ReportSentEn : Config.Messages.ReportSent,
            "OnCooldown" => isEnglish ? Config.Messages.OnCooldownEn : Config.Messages.OnCooldown,
            "NoReason" => isEnglish ? Config.Messages.NoReasonEn : Config.Messages.NoReason,
            "PlayerNotFound" => isEnglish ? Config.Messages.PlayerNotFoundEn : Config.Messages.PlayerNotFound,
            "ReportFailed" => isEnglish ? Config.Messages.ReportFailedEn : Config.Messages.ReportFailed,
            "ReportWithTarget" => isEnglish ? Config.Messages.ReportWithTargetEn : Config.Messages.ReportWithTarget,
            _ => key
        };
    }

    private void SendReportAsync(CCSPlayerController reporter, CCSPlayerController? target, string reason)
    {
        // Get server IP and port
        string serverIp = GetServerIp();

        // Build the payload
        var payload = new Dictionary<string, string?>
        {
            ["player_name"] = reporter.PlayerName,
            ["player_steamid"] = reporter.SteamID.ToString(),
            ["reason"] = reason,
            ["server_name"] = Config.ServerName,
            ["server_ip"] = serverIp,
            ["api_key"] = Config.ApiKey
        };

        // Add target info if present
        if (target != null)
        {
            payload["target_name"] = target.PlayerName;
            payload["target_steamid"] = target.SteamID.ToString();
        }

        // Send async to avoid blocking game thread
        Task.Run(async () =>
        {
            try
            {
                string json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(Config.ApiUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    string responseBody = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[CallAdmin] API Response: {responseBody}");
                }
                else
                {
                    Console.WriteLine($"[CallAdmin] API Error: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CallAdmin] HTTP Error: {ex.Message}");
            }
        });
    }

    private string GetServerIp()
    {
        try
        {
            // Try to get server IP from ConVars
            var ipCvar = ConVar.Find("ip");
            var portCvar = ConVar.Find("hostport");

            string ip = ipCvar?.StringValue ?? "unknown";
            int port = portCvar?.GetPrimitiveValue<int>() ?? 27015;

            // If IP is empty or 0.0.0.0, try alternative
            if (string.IsNullOrEmpty(ip) || ip == "0.0.0.0")
            {
                ip = "unknown";
            }

            return $"{ip}:{port}";
        }
        catch
        {
            return "unknown:27015";
        }
    }
}
