using CounterStrikeSharp.API.Core;
using System.Text.Json.Serialization;

namespace CallAdmin;

public class MessagesConfig
{
    [JsonPropertyName("ReportSent")]
    public string ReportSent { get; set; } = "Report byl úspěšně odeslán!";

    [JsonPropertyName("ReportSentEn")]
    public string ReportSentEn { get; set; } = "Report has been sent successfully!";

    [JsonPropertyName("OnCooldown")]
    public string OnCooldown { get; set; } = "Musíš počkat {seconds} sekund před dalším reportem.";

    [JsonPropertyName("OnCooldownEn")]
    public string OnCooldownEn { get; set; } = "You must wait {seconds} seconds before next report.";

    [JsonPropertyName("NoReason")]
    public string NoReason { get; set; } = "Použití: /calladmin <důvod> | /calladmin @hráč | /calladmin @hráč <důvod>";

    [JsonPropertyName("NoReasonEn")]
    public string NoReasonEn { get; set; } = "Usage: /calladmin <reason> | /calladmin @player | /calladmin @player <reason>";

    [JsonPropertyName("PlayerNotFound")]
    public string PlayerNotFound { get; set; } = "Hráč '{name}' nebyl nalezen.";

    [JsonPropertyName("PlayerNotFoundEn")]
    public string PlayerNotFoundEn { get; set; } = "Player '{name}' not found.";

    [JsonPropertyName("ReportFailed")]
    public string ReportFailed { get; set; } = "Nepodařilo se odeslat report. Zkus to znovu.";

    [JsonPropertyName("ReportFailedEn")]
    public string ReportFailedEn { get; set; } = "Failed to send report. Please try again.";

    [JsonPropertyName("ReportWithTarget")]
    public string ReportWithTarget { get; set; } = "Nahlásil jsi hráče {target}: {reason}";

    [JsonPropertyName("ReportWithTargetEn")]
    public string ReportWithTargetEn { get; set; } = "You reported player {target}: {reason}";

    [JsonPropertyName("UseSlash")]
    public string UseSlash { get; set; } = "Používej /calladmin nebo /report místo !";

    [JsonPropertyName("UseSlashEn")]
    public string UseSlashEn { get; set; } = "Use /calladmin or /report instead of !";

    [JsonPropertyName("NoLinks")]
    public string NoLinks { get; set; } = "Nelze posílat odkazy! Napiš důvod bez URL.";

    [JsonPropertyName("NoLinksEn")]
    public string NoLinksEn { get; set; } = "Links are not allowed! Write the reason without URL.";
}

public class CallAdminConfig : BasePluginConfig
{
    [JsonPropertyName("ServerName")]
    public string ServerName { get; set; } = "CS2 Server";

    [JsonPropertyName("ApiUrl")]
    public string ApiUrl { get; set; } = "http://your-api-endpoint/api/calladmin";

    [JsonPropertyName("ApiKey")]
    public string ApiKey { get; set; } = "your_api_key_here";

    [JsonPropertyName("CooldownSeconds")]
    public int CooldownSeconds { get; set; } = 60;

    [JsonPropertyName("ChatPrefix")]
    public string ChatPrefix { get; set; } = "[CallAdmin]";

    [JsonPropertyName("Language")]
    public string Language { get; set; } = "cs"; // "cs" or "en"

    [JsonPropertyName("Messages")]
    public MessagesConfig Messages { get; set; } = new();
}
