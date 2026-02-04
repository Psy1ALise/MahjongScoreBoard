namespace MahjongScoreBoard.Api.Models;

public class Round
{
    public int RoundNumber { get; private set; }
    public SeatWind RoundWind { get; private set; }
    public int DealerIndex { get; private set; }
    public int Kyoku => DealerIndex + 1;
    public string KyokuName => $"{RoundWind} {Kyoku}";
    public int Honba { get; private set; }
    public List<HandResult> HandResults { get; private set; }
    public DrawResult? DrawResult { get; private set; }

    public Round(int roundNumber, SeatWind roundWind, int dealerIndex, int honba = 0)
    {
        RoundNumber = roundNumber;
        RoundWind = roundWind;
        DealerIndex = dealerIndex;
        Honba = honba;
        HandResults = new List<HandResult>();
    }

    public void AddHandResult(HandResult result)
    {
        HandResults.Add(result);
    }

    public void SetDrawResult(DrawResult result)
    {
        DrawResult = result;
    }
}
