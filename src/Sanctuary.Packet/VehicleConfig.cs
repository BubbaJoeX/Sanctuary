using System;
using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// A kart's or derby car's whole make-up (the client's VehicleConfig, reader 0x9d7b50): its body, wheels and part
/// models, paint, effects, sounds, handling and physics, AI speed zones and damage states, 191 fields in all. The
/// race's and the derby's CreateProxied packets and the garage's VehicleConfig carry one.
/// <para>
/// The client has no names for its fields and nothing to build one from: the server sends them. They are kept as the
/// live game's bytes (one per chassis), walked here only to find where one ends and to repaint the body.
/// </para>
/// </summary>
public sealed class VehicleConfig
{
    private enum Field : byte
    {
        String,
        U32,
        F32,
        Bool,
        Vec3,
        StringList,
        U32List,
        F32List,
        Vec3List,
        SurfaceMap,
        AttachPointList,
        AiZoneList,
        DamageStateList,
        PartTextureList,
        PhysicsBlock,
        SoundBlock
    }

    // The client's read order. The first eight fields are the body: collision mesh, mass, the box's offset and half
    // extents, then the tint and the body, second decal and decal texture aliases.
    private static readonly Field[] Layout = BuildLayout();

    private const int TintField = 4;
    private const int BodyTextureField = 5;
    private const int Decal2TextureField = 6;
    private const int DecalTextureField = 7;

    public byte[] Data { get; }

    public VehicleConfig(byte[] data)
    {
        Data = data;
    }

    public static VehicleConfig FromHex(string hex) => new(Convert.FromHexString(hex));

    public int Length => Data.Length;

    public string CollisionModel => ReadPrefix().Strings[0];

    public uint BodyTintId => ReadPrefix().Tint;

    public string BodyTextureAlias => ReadPrefix().Strings[1];

    public string DecalTextureAlias => ReadPrefix().Strings[3];

    public void Write(PacketWriter writer) => writer.Write(Data);

    /// <summary>
    /// Reads one off the stream, as the client does: false if it is cut short or a list claims more entries than
    /// there are bytes left.
    /// </summary>
    public static bool TryRead(ref PacketReader reader, out VehicleConfig config)
    {
        config = null!;

        var start = reader.Span.Length - reader.RemainingLength;

        foreach (var field in Layout)
        {
            if (!TrySkip(ref reader, field))
                return false;
        }

        var end = reader.Span.Length - reader.RemainingLength;

        config = new VehicleConfig(reader.Span[start..end].ToArray());

        return true;
    }

    /// <summary>
    /// The same vehicle repainted: a body tint, body texture alias or decal alias given replaces the old one.
    /// </summary>
    public VehicleConfig Repaint(uint? tintId = null, string? bodyTextureAlias = null, string? decalTextureAlias = null)
    {
        var prefix = ReadPrefix();

        using var writer = new PacketWriter();

        var reader = new PacketReader(Data);

        for (var i = 0; i <= DecalTextureField; i++)
        {
            var before = reader.RemainingLength;

            TrySkip(ref reader, Layout[i]);

            switch (i)
            {
                case TintField:
                    writer.Write(tintId ?? prefix.Tint);
                    break;

                case BodyTextureField:
                    writer.Write(bodyTextureAlias ?? prefix.Strings[1]);
                    break;

                case DecalTextureField:
                    writer.Write(decalTextureAlias ?? prefix.Strings[3]);
                    break;

                default:
                    writer.Write(Data.AsSpan(Data.Length - before, before - reader.RemainingLength));
                    break;
            }
        }

        writer.Write(Data.AsSpan(Data.Length - reader.RemainingLength));

        return new VehicleConfig(writer.Buffer);
    }

    private (uint Tint, string[] Strings) ReadPrefix()
    {
        var reader = new PacketReader(Data);
        var strings = new List<string>();
        uint tint = 0;

        for (var i = 0; i <= DecalTextureField; i++)
        {
            switch (Layout[i])
            {
                case Field.String:
                    reader.TryRead(out string value);
                    strings.Add(value);
                    break;

                case Field.U32 when i == TintField:
                    reader.TryRead(out tint);
                    break;

                default:
                    TrySkip(ref reader, Layout[i]);
                    break;
            }
        }

        // Wire order: collision mesh, body, second decal, decal.
        return (tint, strings.ToArray());
    }

    private static bool TrySkip(ref PacketReader reader, Field field)
    {
        switch (field)
        {
            case Field.String:
                return TrySkipString(ref reader);

            case Field.U32:
            case Field.F32:
                return TrySkipBytes(ref reader, 4);

            case Field.Bool:
                return TrySkipBytes(ref reader, 1);

            case Field.Vec3:
                return TrySkipBytes(ref reader, 12);

            case Field.StringList:
                {
                    if (!TryReadCount(ref reader, out var count))
                        return false;

                    for (var i = 0; i < count; i++)
                    {
                        if (!TrySkipString(ref reader))
                            return false;
                    }

                    return true;
                }

            case Field.U32List:
            case Field.F32List:
                return TryReadCount(ref reader, out var words) && TrySkipBytes(ref reader, words * 4);

            case Field.Vec3List:
                return TryReadCount(ref reader, out var vectors) && TrySkipBytes(ref reader, vectors * 12);

            case Field.SurfaceMap:
                // u32 key, then four floats each.
                return TryReadCount(ref reader, out var surfaces) && TrySkipBytes(ref reader, surfaces * 20);

            case Field.AttachPointList:
                // u32 id, u32, a 4x4 matrix.
                return TryReadCount(ref reader, out var attachPoints) && TrySkipBytes(ref reader, attachPoints * 72);

            case Field.AiZoneList:
                {
                    if (!TryReadCount(ref reader, out var zones))
                        return false;

                    for (var i = 0; i < zones; i++)
                    {
                        // From, To, a float, a float list, two floats, then a surface map.
                        if (!TrySkipBytes(ref reader, 12) || !TrySkip(ref reader, Field.F32List) || !TrySkipBytes(ref reader, 8)
                            || !TrySkip(ref reader, Field.SurfaceMap))
                            return false;
                    }

                    return true;
                }

            case Field.DamageStateList:
                {
                    if (!TryReadCount(ref reader, out var states))
                        return false;

                    for (var i = 0; i < states; i++)
                    {
                        // Minimum health, u32, two floats, then two lists of (effect id, 4x4 matrix).
                        if (!TrySkipBytes(ref reader, 16)
                            || !TryReadCount(ref reader, out var effects) || !TrySkipBytes(ref reader, effects * 68)
                            || !TryReadCount(ref reader, out var effects2) || !TrySkipBytes(ref reader, effects2 * 68))
                            return false;
                    }

                    return true;
                }

            case Field.PartTextureList:
                {
                    if (!TryReadCount(ref reader, out var textures))
                        return false;

                    for (var i = 0; i < textures; i++)
                    {
                        // Tint, texture alias, u32.
                        if (!TrySkipBytes(ref reader, 4) || !TrySkipString(ref reader) || !TrySkipBytes(ref reader, 4))
                            return false;
                    }

                    return true;
                }

            case Field.PhysicsBlock:
                return TrySkipBytes(ref reader, 46 * 4);

            case Field.SoundBlock:
                {
                    // Four strings, nine floats, three strings.
                    for (var i = 0; i < 4; i++)
                    {
                        if (!TrySkipString(ref reader))
                            return false;
                    }

                    if (!TrySkipBytes(ref reader, 36))
                        return false;

                    for (var i = 0; i < 3; i++)
                    {
                        if (!TrySkipString(ref reader))
                            return false;
                    }

                    return true;
                }
        }

        return false;
    }

    private static bool TryReadCount(ref PacketReader reader, out int count)
    {
        count = 0;

        // The client refuses a list longer than the bytes left.
        if (!reader.TryRead(out uint value) || value > reader.RemainingLength)
            return false;

        count = (int)value;

        return true;
    }

    private static bool TrySkipString(ref PacketReader reader) =>
        reader.TryRead(out int length) && length >= 0 && TrySkipBytes(ref reader, length);

    private static bool TrySkipBytes(ref PacketReader reader, int count)
    {
        if (count < 0 || count > reader.RemainingLength)
            return false;

        reader.Advance(count);

        return true;
    }

    private static Field[] BuildLayout()
    {
        var layout = new List<Field>
        {
            Field.String, Field.F32, Field.Vec3, Field.Vec3, Field.U32, Field.String, Field.String, Field.String,
            // Body, tires, wheels, axles, front panels and their damaged models.
            Field.StringList, Field.StringList, Field.StringList, Field.StringList, Field.StringList, Field.StringList
        };

        // Part models (exhaust, spoiler, engine, bottle, crystal, steering wheel, gears), then the AI and handling
        // profiles and the audio config.
        Repeat(layout, Field.String, 13);

        Repeat(layout, Field.U32, 10);
        layout.Add(Field.F32);
        Repeat(layout, Field.U32, 4);
        layout.AddRange([Field.U32List, Field.String]);
        Repeat(layout, Field.U32, 6);
        layout.AddRange([Field.U32List, Field.String, Field.String, Field.String]);

        // Tire splash effects and their speeds.
        Repeat(layout, Field.U32, 12);
        Repeat(layout, Field.F32, 3);

        Repeat(layout, Field.U32, 9);
        layout.AddRange([Field.AttachPointList, Field.String]);
        Repeat(layout, Field.F32, 9);
        layout.AddRange([Field.SurfaceMap, Field.U32, Field.Vec3List, Field.Vec3List, Field.F32List, Field.F32List, Field.Vec3List, Field.F32]);
        Repeat(layout, Field.Vec3, 3);
        layout.Add(Field.F32);
        Repeat(layout, Field.Vec3, 8);
        layout.Add(Field.Bool);
        Repeat(layout, Field.F32, 20);
        layout.Add(Field.U32);
        Repeat(layout, Field.F32, 18);

        // The torque curve, then the AI speed zones and the damage states.
        layout.AddRange([Field.Vec3List, Field.F32, Field.F32, Field.U32, Field.F32, Field.AiZoneList, Field.DamageStateList]);
        Repeat(layout, Field.F32, 3);
        Repeat(layout, Field.F32List, 6);
        Repeat(layout, Field.F32, 12);
        layout.Add(Field.Bool);
        Repeat(layout, Field.F32, 8);
        layout.AddRange([Field.U32, Field.F32, Field.PhysicsBlock, Field.SoundBlock, Field.PartTextureList]);

        return layout.ToArray();
    }

    private static void Repeat(List<Field> layout, Field field, int count)
    {
        for (var i = 0; i < count; i++)
            layout.Add(field);
    }

    public override string ToString() => $"{CollisionModel} ({Length} bytes)";
}
