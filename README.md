# CS2 CallAdmin Plugin

A CounterStrikeSharp plugin for Counter-Strike 2 that allows players to call admins via an API endpoint (Discord webhook, custom backend, etc.).

## Features

- `!calladmin <reason>` - Report an issue to admins
- `!calladmin @player <reason>` - Report a specific player
- Configurable cooldown to prevent spam
- Multi-language support (Czech/English)
- Customizable messages
- Async HTTP requests (non-blocking)

## Installation

1. Make sure you have [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp) installed
2. Download the latest release
3. Copy the `CallAdmin` folder to `csgo/addons/counterstrikesharp/plugins/`
4. Restart the server or use `css_plugins reload CallAdmin`

## Configuration

After first load, a config file will be created at:
`csgo/addons/counterstrikesharp/configs/plugins/CallAdmin/CallAdmin.json`

```json
{
  "ServerName": "My CS2 Server",
  "ApiUrl": "http://your-api-endpoint/api/calladmin",
  "ApiKey": "your_api_key",
  "CooldownSeconds": 60,
  "ChatPrefix": "[CallAdmin]",
  "Language": "cs",
  "Messages": {
    "ReportSent": "Report byl úspěšně odeslán!",
    "ReportSentEn": "Report has been sent successfully!",
    "OnCooldown": "Musíš počkat {seconds} sekund před dalším reportem.",
    "OnCooldownEn": "You must wait {seconds} seconds before next report.",
    "NoReason": "Musíš zadat důvod! Použití: !calladmin <důvod>",
    "NoReasonEn": "You must provide a reason! Usage: !calladmin <reason>",
    "PlayerNotFound": "Hráč '{name}' nebyl nalezen.",
    "PlayerNotFoundEn": "Player '{name}' not found.",
    "ReportFailed": "Nepodařilo se odeslat report. Zkus to znovu.",
    "ReportFailedEn": "Failed to send report. Please try again.",
    "ReportWithTarget": "Nahlásil jsi hráče {target}: {reason}",
    "ReportWithTargetEn": "You reported player {target}: {reason}"
  },
  "ConfigVersion": 1
}
```

### Configuration Options

| Option | Description | Default |
|--------|-------------|---------|
| `ServerName` | Name of your server (sent in reports) | `CS2 Server` |
| `ApiUrl` | Your API endpoint URL | - |
| `ApiKey` | API authentication key | - |
| `CooldownSeconds` | Cooldown between reports per player | `60` |
| `ChatPrefix` | Prefix for chat messages | `[CallAdmin]` |
| `Language` | Language for messages (`cs` or `en`) | `cs` |
| `Messages` | Customizable message strings | - |

## API Integration

The plugin sends a POST request to your configured `ApiUrl` with the following JSON payload:

```json
{
  "player_name": "Reporter Name",
  "player_steamid": "76561198000000000",
  "reason": "Player's reason for calling admin",
  "server_name": "Your Server Name",
  "server_ip": "192.168.1.1:27015",
  "target_name": "Reported Player Name",
  "target_steamid": "76561198000000001",
  "api_key": "your_api_key"
}
```

**Note:** `target_name` and `target_steamid` are only included when a player uses `@player` syntax.

### Expected Response

```json
{
  "success": true,
  "message_id": "123456789"
}
```

## Usage Examples

```
!calladmin Cheater on server
!calladmin @SuspiciousPlayer Using aimbot
/calladmin Need admin help (silent - only sender sees)
```

## Building from Source

Requirements:
- .NET 8.0 SDK
- CounterStrikeSharp.API NuGet package

```bash
dotnet build -c Release
```

The compiled plugin will be in `bin/Release/net8.0/`

## License

MIT License - Feel free to use, modify, and distribute.

## Credits

- [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp) by roflmuffin
- Plugin by Luky
