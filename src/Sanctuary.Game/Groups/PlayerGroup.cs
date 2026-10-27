using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Sanctuary.Game.Groups;

/// <summary>
/// A party of players. The first member leads; the leader leaving hands it to the next one.
/// </summary>
public sealed class PlayerGroup
{
    /// <summary>
    /// The client sizes its group for five.
    /// </summary>
    public const int MaxMembers = 5;

    private static int _lastId;

    public int Id { get; } = Interlocked.Increment(ref _lastId);

    /// <summary>
    /// Replaced on every change rather than edited, so other threads always read a whole roster.
    /// </summary>
    public IReadOnlyList<ulong> Members { get; private set; } = [];

    public ulong LeaderGuid => Members.Count > 0 ? Members[0] : 0;

    public bool IsFull => Members.Count >= MaxMembers;

    public bool Contains(ulong guid) => Members.Contains(guid);

    /// <summary>
    /// A member changed job or rank, so everyone's group window needs their new row.
    /// </summary>
    public event Action<PlayerGroup>? MemberChanged;

    public void NotifyMemberChanged() => MemberChanged?.Invoke(this);

    internal void Add(ulong guid) => Members = [.. Members, guid];

    internal void Remove(ulong guid) => Members = [.. Members.Where(member => member != guid)];
}
