using System.Numerics;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// The 3D games' packet families (kart racing 33, demolition derby 34, soccer 76, vehicle parts 139): a short opcode
/// and a short sub-op, and the client drops any of them with a byte too few or too many, so each is written exactly.
/// </summary>
public abstract class BaseArenaGamePacket
{
    private readonly short _opCode;
    private readonly ushort _subOpCode;

    protected BaseArenaGamePacket(short opCode, ushort subOpCode)
    {
        _opCode = opCode;
        _subOpCode = subOpCode;
    }

    public void Write(PacketWriter writer)
    {
        writer.Write(_opCode);
        writer.Write(_subOpCode);
    }

    public bool TryRead(ref PacketReader reader)
    {
        if (!reader.TryRead(out short opCode) || opCode != _opCode)
            return false;

        if (!reader.TryRead(out ushort subOpCode) || subOpCode != _subOpCode)
            return false;

        return true;
    }

    /// <summary>
    /// A 4x4 transform, row by row: the rotation rows, then the position with w 1.
    /// </summary>
    protected static void WriteTransform(PacketWriter writer, Matrix4x4 transform)
    {
        writer.Write(transform.M11); writer.Write(transform.M12); writer.Write(transform.M13); writer.Write(transform.M14);
        writer.Write(transform.M21); writer.Write(transform.M22); writer.Write(transform.M23); writer.Write(transform.M24);
        writer.Write(transform.M31); writer.Write(transform.M32); writer.Write(transform.M33); writer.Write(transform.M34);
        writer.Write(transform.M41); writer.Write(transform.M42); writer.Write(transform.M43); writer.Write(transform.M44);
    }

    protected static bool TryReadTransform(ref PacketReader reader, out Matrix4x4 transform)
    {
        transform = Matrix4x4.Identity;

        for (var row = 0; row < 4; row++)
        {
            for (var column = 0; column < 4; column++)
            {
                if (!reader.TryRead(out float value))
                    return false;

                transform[row, column] = value;
            }
        }

        return true;
    }

    /// <summary>
    /// The kart race's transform: the three rotation rows and the position, three floats each.
    /// </summary>
    protected static void WriteCompactTransform(PacketWriter writer, Matrix4x4 transform)
    {
        writer.Write(transform.M11); writer.Write(transform.M12); writer.Write(transform.M13);
        writer.Write(transform.M21); writer.Write(transform.M22); writer.Write(transform.M23);
        writer.Write(transform.M31); writer.Write(transform.M32); writer.Write(transform.M33);
        writer.Write(transform.M41); writer.Write(transform.M42); writer.Write(transform.M43);
    }

    protected static bool TryReadCompactTransform(ref PacketReader reader, out Matrix4x4 transform)
    {
        transform = Matrix4x4.Identity;

        for (var row = 0; row < 4; row++)
        {
            for (var column = 0; column < 3; column++)
            {
                if (!reader.TryRead(out float value))
                    return false;

                transform[row, column] = value;
            }
        }

        return true;
    }
}
