namespace MahjongScoreBoard.Api.Models;

public class Player
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public int Score { get; private set; }
    public SeatWind SeatWind { get; private set; }
    public int RiichiSticks { get; private set; }
    public int RonWins { get; private set; }
    public int TsumoWins { get; private set; }
    public int DealInCount { get; private set; }
    public int RiichiCount { get; private set; }

    public Player(string name, SeatWind seatWind, int startingScore = 25000)
    {
        Id = Guid.NewGuid();
        Name = name;
        SeatWind = seatWind;
        Score = startingScore;
        RiichiSticks = 0;
    }

    public void DeclareRiichi()
    {
        RiichiSticks++;
        RiichiCount++;
    }

    public void RecordRonWin() => RonWins++;
    public void RecordTsumoWin() => TsumoWins++;
    public void RecordDealIn() => DealInCount++;

    public void ClearRiichi()
    {
        RiichiSticks = 0;
    }

    public void AddPoints(int points)
    {
        Score += points;
    }

    public void DeductPoints(int points)
    {
        Score -= points;
    }

    public void RotateSeat()
    {
        SeatWind = (SeatWind)(((int)SeatWind + 1) % 4);
    }
}
