using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Runs a first-time event script on the client. Layout from the client's reader (FreeRealms.exe 0xc88940):
/// a string, an int array, an int and a byte. The handler (0xc893f0) turns the string into a Lua method name
/// ("Module.Method" is called as "Module:Method") and calls it through the same lookup as
/// <see cref="AbilityPacketExecuteClientLua"/>, with <see cref="Parameters"/> as numbers. The int and byte go
/// to the first-time event manager afterwards (0xc89270); presumably the event id and whether to mark it seen,
/// which is unconfirmed.
/// </summary>
public class FirstTimeEventScriptPacket : BaseFirstTimeEventPacket, ISerializablePacket
{
    public new const byte OpCode = 5;

    public string? Script;

    public List<int> Parameters = [];

    public int FirstTimeEventId;

    public bool Unknown;

    public FirstTimeEventScriptPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Script);
        writer.Write(Parameters);
        writer.Write(FirstTimeEventId);
        writer.Write(Unknown);

        return writer.Buffer;
    }
}
