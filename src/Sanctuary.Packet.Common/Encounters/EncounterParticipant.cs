using System.Numerics;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

/// <summary>
/// A player in a battle, as its details list them.
/// </summary>
public class EncounterParticipant : ISerializableType
{
    /// <summary>
    /// The hash key in front of each one; the same for a character every time.
    /// </summary>
    public int Key;

    public ulong Guid;

    public bool Unknown;

    public int Unknown2 = 1;

    public bool Joined = true;

    /// <summary>
    /// In the battle's instance (false at the offer).
    /// </summary>
    public bool Entered;

    public bool Unknown3;
    public bool Unknown4;
    public bool Unknown5;
    public bool Unknown6;

    /// <summary>
    /// Where the player was when the battle was offered.
    /// </summary>
    public Vector4 Position;

    public string Name = string.Empty;

    public bool Unknown7;

    public string Unknown8 = string.Empty;

    /// <summary>
    /// Live: the server id, the same as the header's ReplyId.
    /// </summary>
    public int ServerId;

    public int Unknown9;
    public int Unknown10;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(Key);
        writer.Write(Guid);
        writer.Write(Unknown);
        writer.Write(Unknown2);
        writer.Write(Joined);
        writer.Write(Entered);
        writer.Write(Unknown3);
        writer.Write(Unknown4);
        writer.Write(Unknown5);
        writer.Write(Unknown6);
        writer.Write(Position);
        writer.Write(Name);
        writer.Write(Unknown7);
        writer.Write(Unknown8);
        writer.Write(ServerId);
        writer.Write(Unknown9);
        writer.Write(Unknown10);
    }
}
