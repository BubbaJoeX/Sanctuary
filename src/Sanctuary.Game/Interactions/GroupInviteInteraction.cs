using Sanctuary.Game.Entities;
using Sanctuary.Game.Groups;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Interactions;

/// <summary>
/// "Invite to Group" on another player's menu, beside Add Friend.
/// </summary>
public class GroupInviteInteraction : IInteraction
{
    private readonly IGroupManager _groupManager;

    public GroupInviteInteraction(IGroupManager groupManager)
    {
        _groupManager = groupManager;
    }

    public int Id => Data.Id;

    public static InteractionData Data = new()
    {
        Id = IInteraction.UniqueId++,
        IconId = 136,
        ButtonText = 3372
    };

    /// <summary>
    /// Shown while <paramref name="player"/> could bring <paramref name="other"/> into their group.
    /// </summary>
    public static bool CanInvite(Player player, Player other) =>
        other.Group is null && (player.Group is null || player.Group.LeaderGuid == player.Guid && !player.Group.IsFull);

    public void OnInteract(Player player, IEntity other)
    {
        if (other is Player otherPlayer)
            _groupManager.Invite(player, otherPlayer.Guid, otherPlayer.Name.FullName);
    }
}
