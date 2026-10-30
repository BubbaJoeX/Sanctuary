using Sanctuary.Game.Zones;

namespace Sanctuary.Game.Entities;

/// <summary>
/// Something an in-world challenge put out for one player: a pickup, a race flag, an animal to catch. Only they
/// see it.
/// </summary>
public sealed class ChallengeObject : Npc
{
    public ChallengeObject(IZone zone, ulong ownerGuid) : base(zone)
    {
        OwnerGuid = ownerGuid;
    }

    public ulong OwnerGuid { get; }

    public override bool IsVisibleTo(Player player) => player.Guid == OwnerGuid;

    public override bool HasInteraction => InteractAction is not null;
}
