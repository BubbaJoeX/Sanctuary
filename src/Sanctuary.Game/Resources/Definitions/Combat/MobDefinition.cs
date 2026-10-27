namespace Sanctuary.Game.Resources.Definitions.Combat;

/// <summary>
/// Combat stats for a hostile world NPC (see Mobs.json). A hostile NPC uses the entry its "Mob" property
/// names (!npc mob), or <see cref="DefaultKey"/>.
/// </summary>
public sealed class MobDefinition
{
    public const string DefaultKey = "default";

    public string Key { get; set; } = string.Empty;

    public string Comment { get; set; } = string.Empty;

    public int MaxHitpoints { get; set; } = 1000;

    public int Damage { get; set; } = 100;

    /// <summary>
    /// Each hit lands within +/- this fraction of <see cref="Damage"/>.
    /// </summary>
    public float DamageVariance { get; set; } = 0.15f;

    public int AttackIntervalMs { get; set; } = 2000;
    public float AttackRange { get; set; } = 3f;

    /// <summary>
    /// Players this close to it are attacked on sight.
    /// </summary>
    public float AggroRadius { get; set; } = 12f;

    /// <summary>
    /// How far from its spawn it chases before giving up and walking home at full health.
    /// </summary>
    public float LeashRadius { get; set; } = 40f;

    public int RespawnSeconds { get; set; } = 30;

    /// <summary>
    /// How long the body stays after it dies, so its death animation can play.
    /// </summary>
    public int CorpseMs { get; set; } = 3000;

    public int Coins { get; set; }
    public int Experience { get; set; }

    /// <summary>
    /// Optional Rewards.json table rolled once per player who helped kill it.
    /// </summary>
    public string? RewardTable { get; set; }

    public int AttackAnimationId { get; set; } = 1099;

    /// <summary>
    /// Extra animation on each hit it takes; the client already flinches it (PlayerUpdatePacketHitPointModification).
    /// </summary>
    public int HitAnimationId { get; set; }

    /// <summary>
    /// Played as it dies, on top of the client's knocked out state.
    /// </summary>
    public int DeathAnimationId { get; set; }

    /// <summary>
    /// Composite effect played on the player it hits.
    /// </summary>
    public int HitEffectId { get; set; } = 7;
}
