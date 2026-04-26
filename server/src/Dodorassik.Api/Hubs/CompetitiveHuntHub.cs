using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Dodorassik.Api.Hubs;

/// <summary>
/// Real-time hub for competitive hunts. Authenticated clients subscribe to a
/// hunt group; the server pushes leaderboard snapshots after each accepted step
/// submission in a competitive hunt.
///
/// Godot WebSocket client connects via:
///   ws://{host}/hubs/competitive?access_token={jwt}
///
/// Protocol: SignalR JSON (record-separator 0x1e delimited).
/// Handshake: {"protocol":"json","version":1}
/// Join group:
///   client → {"type":1,"target":"JoinHunt","arguments":["<huntId>"]}
/// Leaderboard push:
///   server → {"type":1,"target":"LeaderboardUpdated","arguments":[{...}]}
/// </summary>
[Authorize]
public class CompetitiveHuntHub : Hub
{
    /// <summary>
    /// Called by the client to start receiving leaderboard updates for a hunt.
    /// </summary>
    public async Task JoinHunt(string huntId)
    {
        if (!Guid.TryParse(huntId, out _)) return;
        await Groups.AddToGroupAsync(Context.ConnectionId, HuntGroupName(huntId));
    }

    /// <summary>Stop receiving updates for a hunt.</summary>
    public async Task LeaveHunt(string huntId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, HuntGroupName(huntId));
    }

    public static string HuntGroupName(string huntId) => $"hunt:{huntId}";
}
