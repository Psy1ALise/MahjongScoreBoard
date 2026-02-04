using System.ComponentModel.DataAnnotations;

namespace MahjongScoreBoard.Api.Requests;

public class CreateGameRequest
{
    [Required]
    [MinLength(4, ErrorMessage = "Exactly 4 players are required")]
    [MaxLength(4, ErrorMessage = "Exactly 4 players are required")]
    public List<string> PlayerNames { get; set; } = new();

    [Range(1000, 100000, ErrorMessage = "Starting score must be between 1000 and 100000")]
    public int StartingScore { get; set; } = 25000;

    [Range(1000, 100000)]
    public int TargetScore { get; set; } = 30000;

    public List<int> Uma { get; set; } = new() { 30, 10, -10, -30 };

    // Rule toggles
    public bool Kiriage { get; set; } = false;
    public bool Atamahane { get; set; } = false;
    public bool Kazoeyakuman { get; set; } = true;
    public bool DoubleYakuman { get; set; } = false;
    public bool CompositeYakuman { get; set; } = true;
    public bool Bankruptcy { get; set; } = true;
    public bool AbortiveDraws { get; set; } = true;
    public bool Pao { get; set; } = true;

    
}
