using System;
using System.Collections.Generic;
using System.Linq;

using Sanctuary.Game.Resources.Definitions;

namespace Sanctuary.Game.Housing;

/// <summary>
/// Which house teleporter sends to which. Pads only link to pads of their own kind, in the order they were
/// placed: two-way and one-way pads pair up (first with second, third with fourth, ...), and a multi-way pad
/// sends to any other multi-way pad.
/// </summary>
public static class TeleporterLinks
{
    public readonly record struct Pad(ulong Guid, HousingTeleporterMode Mode);

    /// <param name="pads">The house's teleporters in the order they were placed.</param>
    public static ulong? GetDestination(IReadOnlyList<Pad> pads, ulong from, Random random)
    {
        var index = IndexOf(pads, from);

        if (index < 0)
            return null;

        var mode = pads[index].Mode;
        var sameKind = pads.Where(pad => pad.Mode == mode).ToList();
        var position = IndexOf(sameKind, from);

        switch (mode)
        {
            case HousingTeleporterMode.TwoWay:
            {
                var partner = position % 2 == 0 ? position + 1 : position - 1;
                return partner < sameKind.Count ? sameKind[partner].Guid : null;
            }

            case HousingTeleporterMode.OneWay:
                return position % 2 == 0 && position + 1 < sameKind.Count ? sameKind[position + 1].Guid : null;

            case HousingTeleporterMode.MultiWay:
            {
                if (sameKind.Count < 2)
                    return null;

                var pick = random.Next(sameKind.Count - 1);
                return sameKind[pick >= position ? pick + 1 : pick].Guid;
            }
        }

        return null;
    }

    /// <summary>
    /// The beams that show which pads are linked: one per pair, from the sending pad, and a ring through the
    /// multi-way pads.
    /// </summary>
    public static List<(ulong From, ulong To)> GetLinks(IReadOnlyList<Pad> pads)
    {
        var links = new List<(ulong, ulong)>();

        foreach (var group in pads.GroupBy(pad => pad.Mode))
        {
            var sameKind = group.ToList();

            if (group.Key == HousingTeleporterMode.MultiWay)
            {
                if (sameKind.Count == 2)
                    links.Add((sameKind[0].Guid, sameKind[1].Guid));
                else if (sameKind.Count > 2)
                {
                    for (var i = 0; i < sameKind.Count; i++)
                        links.Add((sameKind[i].Guid, sameKind[(i + 1) % sameKind.Count].Guid));
                }

                continue;
            }

            for (var i = 0; i + 1 < sameKind.Count; i += 2)
                links.Add((sameKind[i].Guid, sameKind[i + 1].Guid));
        }

        return links;
    }

    public static bool IsLinked(IReadOnlyList<Pad> pads, ulong guid) =>
        GetLinks(pads).Any(link => link.From == guid || link.To == guid);

    private static int IndexOf(IReadOnlyList<Pad> pads, ulong guid)
    {
        for (var i = 0; i < pads.Count; i++)
        {
            if (pads[i].Guid == guid)
                return i;
        }

        return -1;
    }
}
