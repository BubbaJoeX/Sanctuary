using System;
using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// "Report" on a name in the chat log, or /report: the name and the last chat lines (up to ten) the reporter's client
/// got from them. With no chat from that player the client sends nothing. The lines come from the client, so check
/// them against the server's chat log.
/// </summary>
public class ReportPacketReportPlayer : BaseReportPacket, IDeserializable<ReportPacketReportPlayer>
{
    public new const short OpCode = 1;

    public string ReportedName = string.Empty;

    public List<(int Channel, string Message, ulong UnixTime)> Lines = [];

    public ReportPacketReportPlayer() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out ReportPacketReportPlayer value)
    {
        value = new ReportPacketReportPlayer();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader) || !reader.TryRead(out value.ReportedName) || !reader.TryRead(out int count) || count is < 0 or > 10)
            return false;

        for (var i = 0; i < count; i++)
        {
            if (!reader.TryRead(out int channel) || !reader.TryRead(out string message) || !reader.TryRead(out ulong time))
                return false;

            value.Lines.Add((channel, message, time));
        }

        return reader.RemainingLength == 0;
    }
}
