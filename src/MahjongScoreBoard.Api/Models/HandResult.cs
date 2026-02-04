namespace MahjongScoreBoard.Api.Models;

public class HandResult
{
    public Guid Id { get; private set; }
    public Guid WinnerId { get; private set; }
    public Guid? LoserId { get; private set; }
    public int Han { get; private set; }
    public int Fu { get; private set; }
    public int PointsWon { get; private set; }
    public int HonbaBonus { get; private set; }
    public List<Yaku> Yaku { get; private set; }
    public bool IsTsumo => LoserId == null;
    public Guid? PaoPlayerId { get; private set; }
    public bool ReceivedKyoutaku { get; private set; }
    public DateTime RecordedAt { get; private set; }

    public HandResult(Guid winnerId, Guid? loserId, int han, int fu, int pointsWon, int honbaBonus, List<Yaku> yaku, bool receivedKyoutaku = false, Guid? paoPlayerId = null)
    {
        Id = Guid.NewGuid();
        WinnerId = winnerId;
        LoserId = loserId;
        Han = han;
        Fu = fu;
        PointsWon = pointsWon;
        HonbaBonus = honbaBonus;
        Yaku = yaku;
        PaoPlayerId = paoPlayerId;
        ReceivedKyoutaku = receivedKyoutaku;
        RecordedAt = DateTime.UtcNow;
    }
}
