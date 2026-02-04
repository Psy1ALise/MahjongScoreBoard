namespace MahjongScoreBoard.Api.Models;

public class GameRules
{
    public bool Kiriage { get; set; } = false;
    public bool Atamahane { get; set; } = false;
    public bool Kazoeyakuman { get; set; } = true;
    public bool DoubleYakuman { get; set; } = false;
    public bool CompositeYakuman { get; set; } = true;
    public bool Bankruptcy { get; set; } = true;
    public bool AbortiveDraws { get; set; } = true;
    public bool Pao { get; set; } = false;
    public int TargetScore { get; set; } = 30000;
    public List<int> Uma { get; set; } = new() { 30, 10, -10, -30 };
}
