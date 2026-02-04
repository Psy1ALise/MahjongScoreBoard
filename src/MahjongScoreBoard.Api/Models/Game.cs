namespace MahjongScoreBoard.Api.Models;

public class Game
{
    public Guid Id { get; private set; }
    public List<Player> Players { get; private set; }
    public List<Round> Rounds { get; private set; }
    public int CurrentRoundNumber { get; private set; }
    public SeatWind RoundWind { get; private set; }
    public int DealerIndex { get; private set; }
    public int Honba { get; private set; }
    public int Kyoutaku { get; private set; }
    public GameStatus Status { get; private set; }
    public Player? Winner { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public GameRules Rules { get; private set; }
    public int StartingScore { get; private set; }
    public List<FinalScoreEntry>? FinalScores { get; private set; }

    public Game(List<string> playerNames, int startingScore, GameRules? rules = null)
    {
        if (playerNames.Count != 4)
        {
            throw new ArgumentException("Game requires exactly 4 players");
        }

        Id = Guid.NewGuid();
        Players = new List<Player>();

        for (int i = 0; i < playerNames.Count; i++)
        {
            var seatWind = (SeatWind)(i % 4);
            Players.Add(new Player(playerNames[i], seatWind, startingScore));
        }

        Rounds = new List<Round>();
        CurrentRoundNumber = 1;
        RoundWind = SeatWind.East;
        DealerIndex = 0;
        Honba = 0;
        Kyoutaku = 0;
        StartingScore = startingScore;
        Rules = rules ?? new GameRules();
        Status = GameStatus.InProgress;
        CreatedAt = DateTime.UtcNow;

        StartNewRound();
    }

    public Round CurrentRound => Rounds.LastOrDefault()
        ?? throw new InvalidOperationException("No rounds exist");

    public int Kyoku => DealerIndex + 1;
    public string KyokuName => $"{RoundWind} {Kyoku}";

    public Player GetDealer() => Players[DealerIndex];

    public void DeclareRiichi(Guid playerId)
    {
        var player = GetPlayerById(playerId)
            ?? throw new InvalidOperationException("Player not found");

        player.DeclareRiichi();
        Kyoutaku++;
    }

    public int SettleRiichi(Guid winnerId)
    {
        // Each player with riichi sticks loses 1000 per stick
        foreach (var player in Players.Where(p => p.RiichiSticks > 0))
        {
            player.DeductPoints(1000 * player.RiichiSticks);
            player.ClearRiichi();
        }

        // Winner collects all kyōtaku
        int kyoutakuPoints = Kyoutaku * 1000;
        if (kyoutakuPoints > 0)
        {
            var winner = GetPlayerById(winnerId)!;
            winner.AddPoints(kyoutakuPoints);
            Kyoutaku = 0;
        }

        return kyoutakuPoints;
    }

    public Player? GetPlayerById(Guid playerId) =>
        Players.FirstOrDefault(p => p.Id == playerId);

    public void StartNewRound()
    {
        var round = new Round(CurrentRoundNumber, RoundWind, DealerIndex, Honba);
        Rounds.Add(round);
    }

    public void AdvanceRound(bool dealerWon)
    {
        if (dealerWon)
        {
            // Renchan: dealer stays, honba increments
            Honba++;
        }
        else
        {
            // Dealer rotates, all players rotate seat winds
            DealerIndex = (DealerIndex + 1) % Players.Count;
            foreach (var player in Players)
            {
                player.RotateSeat();
            }
            Honba = 0;

            if (DealerIndex == 0)
            {
                // All players have been dealer — advance round wind
                RoundWind = (SeatWind)(((int)RoundWind + 1) % 4);

                // Hanchan ends after South round completes
                if (RoundWind == SeatWind.West)
                {
                    EndGame();
                    return;
                }
            }
        }

        CurrentRoundNumber++;
        StartNewRound();
    }

    public void AdvanceAfterDraw(bool dealerTenpai)
    {
        // Draws always increment honba
        Honba++;

        if (!dealerTenpai)
        {
            // Dealer was noten — dealer rotates
            DealerIndex = (DealerIndex + 1) % Players.Count;
            foreach (var player in Players)
            {
                player.RotateSeat();
            }

            if (DealerIndex == 0)
            {
                RoundWind = (SeatWind)(((int)RoundWind + 1) % 4);

                if (RoundWind == SeatWind.West)
                {
                    EndGame();
                    return;
                }
            }
        }
        // If dealer is tenpai: dealer stays (renchan), honba already incremented

        CurrentRoundNumber++;
        StartNewRound();
    }

    public void EndGame()
    {
        Status = GameStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        Winner = Players.OrderByDescending(p => p.Score).First();

        // Remaining kyōtaku and riichi sticks go to the overall winner
        if (Kyoutaku > 0)
        {
            Winner.AddPoints(Kyoutaku * 1000);
            Kyoutaku = 0;
        }
        foreach (var player in Players.Where(p => p.RiichiSticks > 0))
        {
            player.DeductPoints(1000 * player.RiichiSticks);
            player.ClearRiichi();
        }

        CalculateFinalScores();
    }

    private void CalculateFinalScores()
    {
        int targetScore = Rules.TargetScore;
        int oka = (targetScore - StartingScore) * 4;

        // Sort by score descending, tiebreak by initial seat order
        var ranked = Players
            .OrderByDescending(p => p.Score)
            .ThenBy(p => Players.IndexOf(p))
            .ToList();

        var entries = new List<FinalScoreEntry>();
        var adjustedValues = new double[4];

        for (int i = 0; i < 4; i++)
        {
            int adjusted = ranked[i].Score - targetScore;
            if (i == 0) adjusted += oka;

            // Divide by 1000, round with 0.5 rounding down
            // This means: floor for positive .5, ceiling for negative .5
            double divided = adjusted / 1000.0;
            double rounded = Math.Floor(divided + 0.5 - 0.0001);
            // Simpler: use truncation approach — for .5 rounding down:
            // positive 4.5 → 4, negative -4.5 → -5
            rounded = Math.Ceiling(divided - 0.5);

            adjustedValues[i] = rounded + Rules.Uma[i];
        }

        // Adjust 1st place so sum = 0
        double sum = adjustedValues.Skip(1).Sum();
        adjustedValues[0] = -sum;

        for (int i = 0; i < 4; i++)
        {
            entries.Add(new FinalScoreEntry(
                ranked[i].Id,
                ranked[i].Name,
                ranked[i].Score,
                ranked[i].Score - targetScore + (i == 0 ? oka : 0),
                adjustedValues[i],
                i + 1
            ));
        }

        FinalScores = entries;
    }

    public bool IsPlayerBusted() => Players.Any(p => p.Score < 0);
}

public record FinalScoreEntry(
    Guid PlayerId,
    string PlayerName,
    int RawScore,
    int AdjustedScore,
    double FinalScore,
    int Place
);
