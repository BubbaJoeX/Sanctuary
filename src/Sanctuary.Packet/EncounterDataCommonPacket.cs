using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// The combat rules of the zone the player just entered: combat stance, damage, abilities, the HP bar, the party
/// frames and the combat camera, and the end screen's score values. Live sent it on every zone load:
/// <see cref="Battle"/> in a battle's instance, <see cref="Minigame"/> in a kart race's, demolition derby's or soccer
/// match's, <see cref="Default"/> everywhere else.
/// </summary>
public class EncounterDataCommonPacket : ISerializablePacket
{
    public const short OpCode = 62;

    public bool EncounterCombatStance;
    public bool CanTakeDamage;
    public bool CanUseAbilities;
    public bool CanUseConsumables;
    public bool CanMelee;
    public bool CanUseTraits;
    public bool CanChangeMaxMoveSpeed;
    public bool ShowHpBar;
    public bool ShowCombatUi;
    public bool UseCombatCamera;
    public bool IsCombatEncounter;

    public int GameTimeOverride = -1;

    public float ObjectiveMultiplier = 1f;

    public int QuickCompletionTime = 3600;
    public int QuickCompletionValue;
    public int QuickCompletionLoss;

    /// <summary>
    /// Points per enemy defeated on the end screen.
    /// </summary>
    public int NpcKOValue = 1;

    public int KnockoutRewardValue;
    public int KnockoutPenalty;

    public static EncounterDataCommonPacket Default => new();

    public static EncounterDataCommonPacket Battle => new()
    {
        EncounterCombatStance = true,
        CanTakeDamage = true,
        CanUseAbilities = true,
        CanUseConsumables = true,
        CanMelee = true,
        CanUseTraits = true,
        CanChangeMaxMoveSpeed = true,
        ShowHpBar = true,
        ShowCombatUi = true,
        UseCombatCamera = true,
        IsCombatEncounter = true,
        ObjectiveMultiplier = 500f,
        QuickCompletionTime = 1,
        QuickCompletionValue = 100000,
        QuickCompletionLoss = 500,
        NpcKOValue = 300,
        KnockoutRewardValue = 25000,
        KnockoutPenalty = 5000
    };

    /// <summary>
    /// The 3D games' rules: nothing of combat, and the minigame end screen's score values.
    /// </summary>
    public static EncounterDataCommonPacket Minigame => new()
    {
        QuickCompletionTime = 1,
        QuickCompletionLoss = 100,
        NpcKOValue = 3000,
        KnockoutRewardValue = 250000,
        KnockoutPenalty = 50000
    };

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        writer.Write(OpCode);
        writer.Write(EncounterCombatStance);
        writer.Write(CanTakeDamage);
        writer.Write(CanUseAbilities);
        writer.Write(CanUseConsumables);
        writer.Write(CanMelee);
        writer.Write(CanUseTraits);
        writer.Write(CanChangeMaxMoveSpeed);
        writer.Write(ShowHpBar);
        writer.Write(ShowCombatUi);
        writer.Write(UseCombatCamera);
        writer.Write(IsCombatEncounter);
        writer.Write(GameTimeOverride);
        writer.Write(ObjectiveMultiplier);
        writer.Write(QuickCompletionTime);
        writer.Write(QuickCompletionValue);
        writer.Write(QuickCompletionLoss);
        writer.Write(NpcKOValue);
        writer.Write(KnockoutRewardValue);
        writer.Write(KnockoutPenalty);

        return writer.Buffer;
    }
}
