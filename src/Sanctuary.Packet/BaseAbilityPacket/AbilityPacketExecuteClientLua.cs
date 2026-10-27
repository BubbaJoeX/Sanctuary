using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Calls a Lua function on the client with three numbers. Layout from the client's reader (FreeRealms.exe 0xa30060):
/// a string, then three floats. The handler (0xa367ea) looks the name up like the client's own Lua calls (0x9797a0):
/// a global ("Foo"), a table field ("Foo.Bar") or a method with self ("Foo:Bar"), then pcalls it with
/// (<see cref="X"/>, <see cref="Y"/>, <see cref="Z"/>). Unknown names are ignored; errors go to the client's LuaErrors.log.
/// </summary>
public class AbilityPacketExecuteClientLua : BaseAbilityPacket, ISerializablePacket
{
    public new const short OpCode = 17;

    public string? FunctionName;

    public float X;
    public float Y;
    public float Z;

    public AbilityPacketExecuteClientLua() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(FunctionName);
        writer.Write(X);
        writer.Write(Y);
        writer.Write(Z);

        return writer.Buffer;
    }
}
