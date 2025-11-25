using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Cvars;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CallAdmin;

public class CallAdmin : BasePlugin, IPluginConfig<CallAdminConfig>
{
    public override string ModuleName => "CallAdmin";
    public override string ModuleVersion => "1.0.5";
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

        // Listen to chat messages to detect !calladmin vs /calladmin
        AddCommandListener("say", OnPlayerChat);
        AddCommandListener("say_team", OnPlayerChat);

        // Console commands (without css_ prefix)
        AddCommand("calladmin", "Call an admin for help", OnConsoleCommand);
        AddCommand("report", "Report a player (alias for calladmin)", OnConsoleCommand);

        Console.WriteLine($"[CallAdmin] Server: {Config.ServerName}, Language: {Config.Language}");
    }

    public override void Unload(bool hotReload)
    {
        Console.WriteLine("[CallAdmin] Plugin unloaded.");
    }

    private HookResult OnPlayerChat(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid)
            return HookResult.Continue;

        string message = command.GetArg(1).Trim();

        // Check for !calladmin or !report (wrong usage)
        if (message.StartsWith("!calladmin", StringComparison.OrdinalIgnoreCase) ||
            message.StartsWith("!report", StringComparison.OrdinalIgnoreCase))
        {
            string useSlashMsg = GetMessage("UseSlash");
            player.PrintToChat($" {ChatColors.Yellow}{Config.ChatPrefix}{ChatColors.Default} {useSlashMsg}");
            return HookResult.Continue;
        }

        // Check for /calladmin (correct usage)
        if (message.StartsWith("/calladmin", StringComparison.OrdinalIgnoreCase))
        {
            string args = message.Length > 10 ? message.Substring(10).Trim() : "";
            ProcessCallAdmin(player, args);
            return HookResult.Handled;
        }

        // Check for /report (alias)
        if (message.StartsWith("/report", StringComparison.OrdinalIgnoreCase))
        {
            string args = message.Length > 7 ? message.Substring(7).Trim() : "";
            ProcessCallAdmin(player, args);
            return HookResult.Handled;
        }

        return HookResult.Continue;
    }

    private void OnConsoleCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid)
            return;

        // Get all arguments after the command
        string args = "";
        for (int i = 1; i < command.ArgCount; i++)
        {
            args += command.GetArg(i) + " ";
        }
        args = args.Trim();

        ProcessCallAdmin(player, args);
    }

    private void ProcessCallAdmin(CCSPlayerController player, string args)
    {
        // Check cooldown
        if (IsOnCooldown(player.SteamID, out int remainingSeconds))
        {
            string cooldownMsg = GetMessage("OnCooldown").Replace("{seconds}", remainingSeconds.ToString());
            player.PrintToChat($" {ChatColors.Red}{Config.ChatPrefix}{ChatColors.Default} {cooldownMsg}");
            return;
        }

        // Check if reason provided
        if (string.IsNullOrWhiteSpace(args))
        {
            string noReasonMsg = GetMessage("NoReason");
            player.PrintToChat($" {ChatColors.Red}{Config.ChatPrefix}{ChatColors.Default} {noReasonMsg}");
            return;
        }

        // Check for URLs/links in the message
        if (ContainsUrl(args))
        {
            string noLinksMsg = GetMessage("NoLinks");
            player.PrintToChat($" {ChatColors.Red}{Config.ChatPrefix}{ChatColors.Default} {noLinksMsg}");
            Console.WriteLine($"[CallAdmin] Blocked link attempt from {player.PlayerName}: {args}");
            return;
        }

        // Check if first argument starts with @ (target player)
        CCSPlayerController? targetPlayer = null;
        string reason = args;

        if (args.StartsWith("@"))
        {
            // Parse @player mention
            string[] parts = args.Split(' ', 2);
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
            reason = parts.Length > 1 ? parts[1].Trim() : "";

            if (string.IsNullOrWhiteSpace(reason))
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

    private bool ContainsUrl(string text)
    {
        string lowerText = text.ToLower();

        Console.WriteLine($"[CallAdmin] URL Check: '{text}'");

        // Simple check for // followed by anything
        if (lowerText.Contains("//"))
        {
            Console.WriteLine("[CallAdmin] Blocked: contains //");
            return true;
        }

        // Check for common URL patterns
        string[] urlPatterns = {
            "http:", "https:", "www.", "ftp:",
            ".com", ".cz", ".net", ".org", ".eu", ".sk", ".de", ".ru", ".io", ".gg", ".tv", ".me",
            ".info", ".biz", ".xyz", ".online", ".site", ".website", ".link", ".click", ".gy", ".ly"
        };

        foreach (var pattern in urlPatterns)
        {
            if (lowerText.Contains(pattern))
            {
                Console.WriteLine($"[CallAdmin] Blocked: contains {pattern}");
                return true;
            }
        }

        // Detect URL shorteners pattern: word.2-4chars/something (rb.gy/xxx, bit.ly/xxx)
        if (Regex.IsMatch(lowerText, @"[a-z0-9]+\.[a-z]{2,4}/"))
        {
            Console.WriteLine("[CallAdmin] Blocked: shortener pattern");
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
            "UseSlash" => isEnglish ? Config.Messages.UseSlashEn : Config.Messages.UseSlash,
            "NoLinks" => isEnglish ? Config.Messages.NoLinksEn : Config.Messages.NoLinks,
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
