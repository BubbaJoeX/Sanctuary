using System.Collections.Generic;

namespace Sanctuary.WebAPI.Models;

public class RefereePickModel
{
    public string Title { get; set; } = "";
    public string Text { get; set; } = "";
    public string Action { get; set; } = "";
    public int Param1 { get; set; }
    public string Icon { get; set; } = "";
    public string Button { get; set; } = "";
}

public class RefereePicksSaveModel
{
    /// <summary>
    /// The <see cref="RefereePicksModel.Version"/> the edit started from, so one referee can't overwrite another's save.
    /// </summary>
    public string Version { get; set; } = "";

    public List<RefereePickModel> Picks { get; set; } = [];
}

public class RefereePicksModel
{
    public required string Version { get; set; }

    public required List<RefereePickModel> Picks { get; set; }

    public int MaxPicks { get; set; }
    public int MaxTitleLength { get; set; }
    public int MaxTextLength { get; set; }
    public int MaxButtonLength { get; set; }

    /// <summary>
    /// Seconds until players online see a save.
    /// </summary>
    public int RefreshSeconds { get; set; }

    public required List<RefereePickKindModel> Kinds { get; set; }

    public required IReadOnlyList<string> Icons { get; set; }

    public required List<PointOfInterestModel> PointsOfInterest { get; set; }
}

public record RefereePickKindModel(string Name, string Action, string Icon, string Button);

public record PointOfInterestModel(int Id, string Name);

public record AdminUserModel(string Username, string Role);
