using System.Collections.Concurrent;
using System.Collections.Generic;

using Sanctuary.Game.Authoring;
using Sanctuary.Game.Zones;

namespace Sanctuary.Gateway.Admin;

/// <summary>
/// A placed object as undo steps see it. Undoing a delete brings the object back under a new guid, so every step
/// that names it goes through one of these, which follows it to the new guid.
/// </summary>
public sealed class ObjectRef
{
    public ulong Guid { get; set; }
}

/// <summary>
/// Each referee's map edits made through the admin panel, newest last, so they can be undone one at a time.
/// In memory only, like the tile editor's undo.
/// </summary>
public sealed class AdminUndoBook
{
    private const int MaxUndo = 50;

    private readonly ConcurrentDictionary<ulong, List<TileUndo>> _books = new();
    private readonly ConcurrentDictionary<ulong, ObjectRef> _refs = new();

    /// <summary>
    /// The shared reference to the object with this guid.
    /// </summary>
    public ObjectRef Ref(ulong guid) => _refs.GetOrAdd(guid, _ => new ObjectRef { Guid = guid });

    /// <summary>
    /// The object came back under a new guid (an undone delete).
    /// </summary>
    public void Follow(ObjectRef objectRef, ulong guid)
    {
        _refs.TryRemove(objectRef.Guid, out _);

        objectRef.Guid = guid;

        _refs[guid] = objectRef;
    }

    public void Push(Referee referee, TileUndo undo)
    {
        var book = _books.GetOrAdd(referee.Id, _ => []);

        lock (book)
        {
            book.Add(undo);

            if (book.Count > MaxUndo)
                book.RemoveAt(0);
        }
    }

    /// <summary>
    /// Takes the referee's latest edit in <paramref name="zone"/>, or null when there's none.
    /// </summary>
    public TileUndo? Pop(Referee referee, IZone zone)
    {
        if (!_books.TryGetValue(referee.Id, out var book))
            return null;

        lock (book)
        {
            var index = book.FindLastIndex(x => ReferenceEquals(x.Zone, zone));

            if (index < 0)
                return null;

            var undo = book[index];
            book.RemoveAt(index);

            return undo;
        }
    }

    public int Count(Referee referee, IZone zone)
    {
        if (!_books.TryGetValue(referee.Id, out var book))
            return 0;

        lock (book)
            return book.FindAll(x => ReferenceEquals(x.Zone, zone)).Count;
    }
}
