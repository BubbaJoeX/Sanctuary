using Sanctuary.Game.Entities;
using Sanctuary.Packet;

namespace Sanctuary.Game.Helpers;

public static class EventTravel
{
    /// <summary>
    /// Sends the player to an event's gathering spot: a teleport within their zone, or a zone change.
    /// </summary>
    public static bool TrySend(Player player, EventJoinBook.JoinPoint point)
    {
        if (player.Zone.DefinitionId == point.ZoneDefinitionId && player.Zone.OwnerId is null)
        {
            player.UpdatePosition(point.Position, point.Rotation);

            player.SendTunneled(new ClientUpdatePacketUpdateLocation
            {
                Position = point.Position,
                Rotation = point.Rotation,
                Teleport = true
            });

            return true;
        }

        return player.TeleportToZone(point.ZoneDefinitionId, null, point.Position, point.Rotation);
    }
}
