using System;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Packet;

namespace Sanctuary.Game.Tests;

/// <summary>
/// Byte layouts as the client reads them (FreeRealms.exe 0xa30060 and 0xc88940), little-endian.
/// </summary>
[TestClass]
public sealed class ClientLuaPacketTests
{
    [TestMethod]
    public void ExecuteClientLua_IsAbilityHeaderNameAndThreeFloats()
    {
        var bytes = new AbilityPacketExecuteClientLua { FunctionName = "f", X = 1f, Y = 2f, Z = -1f }.Serialize();

        CollectionAssert.AreEqual(new byte[]
        {
            36, 0, 17, 0,                   // Ability opcode, sub-op 17 (shorts)
            1, 0, 0, 0, (byte)'f',          // name
            0, 0, 0x80, 0x3F,               // 1.0
            0, 0, 0, 0x40,                  // 2.0
            0, 0, 0x80, 0xBF                // -1.0
        }, bytes);
    }

    [TestMethod]
    public void FirstTimeEventScript_IsFteHeaderScriptIntsIdAndFlag()
    {
        var bytes = new FirstTimeEventScriptPacket { Script = "A.b", Parameters = [7], FirstTimeEventId = 2, Unknown = true }.Serialize();

        CollectionAssert.AreEqual(new byte[]
        {
            107, 0, 5,                      // first-time event opcode (short), sub-op 5 (byte)
            3, 0, 0, 0, (byte)'A', (byte)'.', (byte)'b',
            1, 0, 0, 0, 7, 0, 0, 0,         // int array
            2, 0, 0, 0,                     // id
            1                               // flag
        }, bytes);
    }

    [TestMethod]
    public void ExecuteClientLua_EmptyNameIsZeroLength()
    {
        var bytes = new AbilityPacketExecuteClientLua().Serialize();

        Assert.AreEqual(4 + 4 + 12, bytes.Length);
        Assert.AreEqual(0, BitConverter.ToInt32(bytes, 4));
    }
}
