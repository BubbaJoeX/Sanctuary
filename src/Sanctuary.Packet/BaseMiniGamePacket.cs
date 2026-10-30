using System.Collections.Generic;

using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

/// <summary>
/// Minigame packets (the client's BaseMiniGamePacket family). Sub-ops seen in the client's packet constructors;
/// the layouts follow the sister project for Clone Wars Adventures (same engine), whose sub-ops are one higher
/// (https://github.com/soir20/oxide, src/game_server/packets/minigame.rs).
/// </summary>
public static class BaseMiniGamePacket
{
    public const short OpCode = 0x27;

    public const byte DataLocked = 0x01;
    public const byte DataUnlocked = 0x02;
    public const byte RequestStartGame = 0x05;
    /// <summary>
    /// The client closing a game it started or the start screen (Clone Wars' RequestCancelActiveMinigame).
    /// </summary>
    public const byte RequestCancelGame = 0x06;
    /// <summary>
    /// The client leaving a game (Clone Wars' LeaveInstance).
    /// </summary>
    public const byte RequestLeaveGame = 0x07;
    public const byte Payload = 0x0E;
    public const byte Info = 0x10;
    public const byte StartGame = 0x11;
    public const byte GameOver = 0x12;
    public const byte LeaveGame = 0x13;
    public const byte UpdateGameTimeScalar = 0x14;
    public const byte StarsEarned = 0x15;
    public const byte KnockOut = 0x17;
    public const byte StoreAdvertisement = 0x2C;
    public const byte LootWheelSetItemToLandOn = 0x2D;
    public const byte LootWheelOnRotationStopped = 0x2E;
    public const byte GameEndScore = 0x2F;
    public const byte GroupInfo = 0x32;
    public const byte CreateGameResult = 0x43;
    public const byte UpdateReward = 0x44;

    /// <summary>
    /// Writes the minigame header: opcode, sub-op, then the stage id (the client routes a running game's
    /// packets by it), the sub-op again as an int (always -1) and the stage group id.
    /// </summary>
    public static void WriteHeader(PacketWriter writer, byte subOpCode, int stageId, int stageGroupId)
    {
        writer.Write(OpCode);
        writer.Write(subOpCode);
        writer.Write(stageId);
        writer.Write(-1);
        writer.Write(stageGroupId);
    }

    /// <summary>
    /// Reads the minigame header the client sends.
    /// </summary>
    public static bool TryReadHeader(ref PacketReader reader, out byte subOpCode, out int stageId, out int stageGroupId)
    {
        subOpCode = 0;
        stageId = 0;
        stageGroupId = 0;

        return reader.TryRead(out short opCode) && opCode == OpCode
            && reader.TryRead(out subOpCode)
            && reader.TryRead(out stageId)
            && reader.TryRead(out int _)
            && reader.TryRead(out stageGroupId);
    }
}

/// <summary>
/// A minigame packet with no body beyond the header (start game, leave game).
/// </summary>
public class MiniGameHeaderPacket : ISerializablePacket
{
    public byte SubOpCode;
    public int StageId;
    public int StageGroupId = -1;

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        BaseMiniGamePacket.WriteHeader(writer, SubOpCode, StageId, StageGroupId);

        return writer.Buffer;
    }
}

/// <summary>
/// A message to or from a Flash minigame (MiniGameFlash:OnMessage): tab-separated, the message name first.
/// </summary>
public class MiniGamePayloadPacket : ISerializablePacket
{
    public int StageId;
    public int StageGroupId = -1;
    public string Payload = string.Empty;

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        BaseMiniGamePacket.WriteHeader(writer, BaseMiniGamePacket.Payload, StageId, StageGroupId);

        // The client's payloads count a terminating null in their length, and the movie misreads the last
        // field of a message without one.
        writer.Write(Payload + '\0');

        return writer.Buffer;
    }
}

/// <summary>
/// A minigame's result (MiniGame:OnGameOver). It shows nothing by itself: LeaveGame after it closes the game and,
/// for an in-world game, opens the end screen with these.
/// </summary>
public class MiniGameGameOverPacket : ISerializablePacket
{
    public int StageId;
    public int StageGroupId = -1;
    public bool Won;
    public int StarsEarned;
    public int SecondsLeft;
    public int Score;

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        BaseMiniGamePacket.WriteHeader(writer, BaseMiniGamePacket.GameOver, StageId, StageGroupId);
        writer.Write(Won);
        writer.Write(StarsEarned);
        writer.Write(SecondsLeft);
        writer.Write(Score);

        return writer.Buffer;
    }
}

/// <summary>
/// Starts a minigame on the client: it builds the game's state (keyed by StageId) and shows its start screen, or
/// asks to start at once (RequestStartGame) without one. Layout from the client's Info reader; a new Info destroys
/// the game the client had, so send LeaveGame first.
/// </summary>
public class MiniGameInfoPacket : ISerializablePacket
{
    public int StageId;
    public int StageGroupId = -1;

    public int NameId;
    public int IconId;
    public int DescriptionId;

    /// <summary>
    /// Also sets the start screen's level range.
    /// </summary>
    public int Difficulty;

    /// <summary>
    /// The job the game belongs to (a profile type).
    /// </summary>
    public int ProfileType;

    /// <summary>
    /// The client's minigame type (MiniGameTypeData.txt): 1 runs an in-world game with the minigame HUD, without a
    /// movie. The Flash games have always been sent as 0.
    /// </summary>
    public int Type;

    public bool MembersOnly;

    /// <summary>
    /// What the score screen shows as won.
    /// </summary>
    public RewardBundleBase Reward = new() { Success = false };

    /// <summary>
    /// Unused by the client on this path.
    /// </summary>
    public RewardBundleBase MemberReward = new() { Success = false };

    /// <summary>
    /// The start screen's prizes (up to four).
    /// </summary>
    public RewardBundleBase RewardPreview = new() { Success = false };

    /// <summary>
    /// The game's goals, for the start screen, the "Objective Complete" toasts and the end screen.
    /// </summary>
    public List<QuestObjectiveInfo> Objectives = [];

    public bool ShowStarCounter;
    public bool ShowStatusIcon;
    public bool ShowActionBar;

    /// <summary>
    /// Off, the client skips the start screen and asks to start the game at once.
    /// </summary>
    public bool ShowStartScreen;

    /// <summary>
    /// Whether LeaveGame shows the end (score) screen.
    /// </summary>
    public bool ShowEndScreen;

    /// <summary>
    /// A movie to load first; not used by in-world games.
    /// </summary>
    public string PreloadSwf = string.Empty;

    /// <summary>
    /// Unused by the client.
    /// </summary>
    public int Unknown;
    public bool Unknown2;

    /// <summary>
    /// The MiniGameData.txt id, which also finds the start screen's leaderboard.
    /// </summary>
    public int MiniGameId;

    /// <summary>
    /// Stops drawing and updating the 3D world while the game is up, as for a full-screen game.
    /// </summary>
    public bool HideWorld;

    /// <summary>
    /// Unused by the client.
    /// </summary>
    public bool Unknown3;
    public bool Unknown4;

    /// <summary>
    /// Hides the start screen even when <see cref="ShowStartScreen"/> is set.
    /// </summary>
    public bool SuppressStartScreen;

    public int Unknown6;

    /// <summary>
    /// Read after the body and ignored.
    /// </summary>
    public int Unknown7;

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        BaseMiniGamePacket.WriteHeader(writer, BaseMiniGamePacket.Info, StageId, StageGroupId);

        WriteBody(writer);

        writer.Write(Unknown7);

        return writer.Buffer;
    }

    /// <summary>
    /// The body, which UpdateReward sends too.
    /// </summary>
    public void WriteBody(PacketWriter writer)
    {
        writer.Write(NameId);
        writer.Write(IconId);
        writer.Write(DescriptionId);
        writer.Write(Difficulty);
        writer.Write(ProfileType);
        writer.Write(Type);
        writer.Write(MembersOnly);
        Reward.Serialize(writer);
        MemberReward.Serialize(writer);
        RewardPreview.Serialize(writer);
        writer.Write(Objectives);
        writer.Write(ShowStarCounter);
        writer.Write(ShowStatusIcon);
        writer.Write(ShowActionBar);
        writer.Write(ShowStartScreen);
        writer.Write(ShowEndScreen);
        writer.Write(PreloadSwf);
        writer.Write(Unknown);
        writer.Write(Unknown2);
        writer.Write(MiniGameId);
        writer.Write(HideWorld);
        writer.Write(Unknown3);
        writer.Write(Unknown4);
        writer.Write(SuppressStartScreen);
        writer.Write(Unknown6);
    }
}

/// <summary>
/// Puts what was won on the score screen: the whole Info body again, of which the client uses the rewards.
/// </summary>
public class MiniGameUpdateRewardPacket : ISerializablePacket
{
    public int StageId;
    public int StageGroupId = -1;

    public MiniGameInfoPacket Info = new();

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        BaseMiniGamePacket.WriteHeader(writer, BaseMiniGamePacket.UpdateReward, StageId, StageGroupId);

        Info.WriteBody(writer);

        return writer.Buffer;
    }
}

/// <summary>
/// The game's star tally (the HUD's star counter counts up to it).
/// </summary>
public class MiniGameStarsEarnedPacket : ISerializablePacket
{
    public int StageId;
    public int StageGroupId = -1;

    public int Stars;

    public bool NoAnimation;

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        // The header's middle int, -1 everywhere else, carries the total here.
        writer.Write(BaseMiniGamePacket.OpCode);
        writer.Write(BaseMiniGamePacket.StarsEarned);
        writer.Write(StageId);
        writer.Write(Stars);
        writer.Write(StageGroupId);
        writer.Write(NoAnimation);

        return writer.Buffer;
    }
}
