using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Sanctuary.Game.Minigames.Mimic;

/// <summary>
/// A row of one of the Mimic movies' data tables (MimicMiniGamePhaseData.txt, MimicSwordData.txt): numbers
/// separated by carets, in the layout the movie's own GameServer parses. The server never shipped these tables to
/// the client; it sent the movie the rows of its game one message each, which is what <see cref="ToString"/> gives.
/// </summary>
public sealed class MimicRow
{
    /// <summary>
    /// The columns every table starts with: the row's id and the game (recipe) it belongs to.
    /// </summary>
    public const int IdColumn = 0;
    public const int GameColumn = 1;

    /// <summary>
    /// A phase row's columns (the movie's LD_* constants): the round's number from 1, its timer in seconds, its
    /// stage (round type) and, from <see cref="FirstDataColumn"/>, the stage's data.
    /// </summary>
    public const int SequenceColumn = 2;
    public const int DurationColumn = 3;
    public const int StageTypeColumn = 7;
    public const int FirstDataColumn = 8;

    private readonly string[] _fields;

    private MimicRow(string[] fields)
    {
        _fields = fields;
    }

    public int Count => _fields.Length;

    public int Id => Int(IdColumn);
    public int Game => Int(GameColumn);

    public int Sequence => Int(SequenceColumn);
    public int Duration => Int(DurationColumn);
    public int StageType => Int(StageTypeColumn);

    /// <summary>
    /// A phase row's DATA1, DATA2, ... (from 1, as the tables name them); 0 for a column the row doesn't have.
    /// </summary>
    public int Data(int number) => Int(FirstDataColumn + number - 1);

    /// <summary>
    /// The column as a number, or 0. The movie reads them with parseInt, which makes a blank NaN.
    /// </summary>
    public int Int(int index)
    {
        return index < _fields.Length && int.TryParse(_fields[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;
    }

    /// <summary>
    /// True when every column is a whole number, which is all the movie's parseInt reads correctly.
    /// </summary>
    public bool IsNumeric => _fields.All(x => int.TryParse(x, NumberStyles.Integer, CultureInfo.InvariantCulture, out _));

    public override string ToString() => string.Join('^', _fields);

    /// <summary>
    /// Parses one line of a table. Blank lines and lines starting with # (notes for people) aren't rows.
    /// </summary>
    public static bool TryParse(string line, out MimicRow row)
    {
        row = null!;

        var text = line.Trim();

        if (text.Length == 0 || text.StartsWith('#'))
            return false;

        var fields = text.Split('^').Select(x => x.Trim()).ToList();

        // The client's tables end their rows with a caret; the movie ignores what follows the last column.
        while (fields.Count > 0 && fields[^1].Length == 0)
            fields.RemoveAt(fields.Count - 1);

        if (fields.Count <= GameColumn)
            return false;

        row = new MimicRow([.. fields]);
        return true;
    }

    /// <summary>
    /// Every row of a table file; none when the file is missing.
    /// </summary>
    public static List<MimicRow> ReadTable(string path)
    {
        var rows = new List<MimicRow>();

        if (!File.Exists(path))
            return rows;

        foreach (var line in File.ReadLines(path))
        {
            if (TryParse(line, out var row))
                rows.Add(row);
        }

        return rows;
    }
}
