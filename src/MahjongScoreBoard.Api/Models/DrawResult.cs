namespace MahjongScoreBoard.Api.Models;

public class DrawResult
{
    public Guid Id { get; private set; }
    public DrawType DrawType { get; private set; }
    public List<Guid> TenpaiPlayerIds { get; private set; }
    public DateTime RecordedAt { get; private set; }

    public DrawResult(DrawType drawType, List<Guid> tenpaiPlayerIds)
    {
        Id = Guid.NewGuid();
        DrawType = drawType;
        TenpaiPlayerIds = tenpaiPlayerIds;
        RecordedAt = DateTime.UtcNow;
    }
}
