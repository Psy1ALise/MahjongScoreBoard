using System.ComponentModel.DataAnnotations;
using MahjongScoreBoard.Api.Models;

namespace MahjongScoreBoard.Api.Requests;

public class RecordRoundRequest
{
    [Required]
    [MinLength(1, ErrorMessage = "At least one winner is required")]
    [MaxLength(3, ErrorMessage = "At most 3 winners allowed")]
    public List<WinnerEntry> Winners { get; set; } = new();

    public Guid? LoserId { get; set; }
}

public class WinnerEntry
{
    [Required]
    public Guid WinnerId { get; set; }

    [Range(1, 13, ErrorMessage = "Han must be between 1 and 13")]
    public int Han { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Fu must be 0 or greater")]
    public int Fu { get; set; } = 30;

    [Required]
    [MinLength(1, ErrorMessage = "At least one yaku is required")]
    public List<Yaku> Yaku { get; set; } = new();

    public Guid? PaoPlayerId { get; set; }
}
