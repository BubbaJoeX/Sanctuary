using Sanctuary.Game.Entities;
using Sanctuary.Packet.Common.Chat;

namespace Sanctuary.Game.Helpers;

/// <summary>
/// Copies what a snooped player says, on every channel including tells, to the referees snooping them (!snoop).
/// </summary>
public static class SnoopHelper
{
    public static void Relay(IZoneManager zoneManager, Player speaker, ChatChannel channel, string? toName, string message)
    {
        if (speaker.Snoopers.IsEmpty)
            return;

        var line = channel == ChatChannel.Tell
            ? $"[Snoop] {speaker.Name.FullName} -> {toName}: {message}"
            : $"[Snoop] {speaker.Name.FullName} ({channel}): {message}";

        foreach (var snooperGuid in speaker.Snoopers)
        {
            // Referees who logged off stop snooping.
            if (!zoneManager.TryGetPlayer(snooperGuid, out var snooper) || !snooper.IsMod && !snooper.IsAdmin)
            {
                speaker.Snoopers.TryRemove(snooperGuid);
                continue;
            }

            ChatHelper.SendSystemMessage(snooper, line);
        }
    }
}
