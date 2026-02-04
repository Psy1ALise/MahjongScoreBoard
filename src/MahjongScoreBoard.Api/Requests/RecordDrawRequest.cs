using MahjongScoreBoard.Api.Models;

namespace MahjongScoreBoard.Api.Requests;

public class RecordDrawRequest
{
    public DrawType DrawType { get; set; }
    public List<Guid> TenpaiPlayerIds { get; set; } = new();
}
