namespace Sanctuary.Game.Resources.Definitions.Rewards;

/// <summary>
/// Stars for a job (a Profiles.json id), or for the active job with none or one the player doesn't have.
/// </summary>
public sealed class ExperienceRewardDropDefinition : RewardDropDefinition
{
    public int Amount { get; set; }

    public int ProfileId { get; set; }
}
