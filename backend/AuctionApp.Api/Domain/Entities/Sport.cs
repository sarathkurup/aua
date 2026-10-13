namespace AuctionApp.Api.Domain.Entities;

/// <summary>A sport that tournaments are played in, e.g. Cricket.</summary>
public class Sport
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Emoji shown on cards and filters in the UI.</summary>
    public string? Icon { get; set; }

    /// <summary>
    /// Attribute names this sport's players are described by, as a JSON array — for cricket
    /// ["Batting","Bowling"], for badminton ["Grip","Discipline"]. The admin form renders a
    /// field per entry and the viewer shows whichever are filled in.
    /// </summary>
    public string? SkillFieldsJson { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Tournament> Tournaments { get; set; } = new List<Tournament>();
}
