using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// A grouped client opening (true) or closing (false) the job browser on a start screen.
/// </summary>
public class GroupPacketChangingJob : BaseGroupPacket, IDeserializable<GroupPacketChangingJob>
{
    public new const short OpCode = 14;

    public bool IsChangingJob;

    public GroupPacketChangingJob() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out GroupPacketChangingJob value)
    {
        value = new GroupPacketChangingJob();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader) || !reader.TryRead(out value.IsChangingJob))
            return false;

        return reader.RemainingLength == 0;
    }
}
