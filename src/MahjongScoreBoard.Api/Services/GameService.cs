using System.Collections.Concurrent;
using MahjongScoreBoard.Api.Models;

namespace MahjongScoreBoard.Api.Services;

public class GameService : IGameService
{
    private readonly ConcurrentDictionary<Guid, Game> _games = new();
    private readonly IScoringService _scoringService;

    public GameService(IScoringService scoringService)
    {
        _scoringService = scoringService;
    }

    // For testing: CreateGame(playerNames, 25000)
    public Game CreateGame(List<string> playerNames, int startingScore, GameRules? rules = null)
    {
        var game = new Game(playerNames, startingScore, rules);
        _games[game.Id] = game;
        return game;
    }

    public Game? GetGame(Guid gameId)
    {
        _games.TryGetValue(gameId, out var game);
        return game;
    }

    public IEnumerable<Game> GetAllGames()
    {
        return _games.Values;
    }

    public List<HandResult> RecordRound(Guid gameId, List<(Guid winnerId, int han, int fu, List<Yaku> yaku, Guid? paoPlayerId)> winners, Guid? loserId)
    {
        var game = GetGame(gameId)
            ?? throw new InvalidOperationException("Game not found");

        if (game.Status == GameStatus.Completed)
            throw new InvalidOperationException("Game has already ended");

        if (winners.Count == 0)
            throw new InvalidOperationException("At least one winner is required");

        bool isTsumo = !loserId.HasValue;

        if (isTsumo && winners.Count > 1)
            throw new InvalidOperationException("Tsumo can only have one winner");

        if (!isTsumo && winners.Count > 3)
            throw new InvalidOperationException("At most 3 winners allowed for multi-ron");

        Player? loser = null;
        if (loserId.HasValue)
        {
            loser = game.GetPlayerById(loserId.Value)
                ?? throw new InvalidOperationException("Loser not found");
        }

        // Sort winners by atamahane order (seat order counter-clockwise from loser)
        if (!isTsumo && winners.Count > 1)
        {
            int loserIndex = game.Players.FindIndex(p => p.Id == loserId!.Value);
            winners = winners.OrderBy(w =>
            {
                int winnerIndex = game.Players.FindIndex(p => p.Id == w.winnerId);
                return (winnerIndex - loserIndex + game.Players.Count) % game.Players.Count;
            }).ToList();
        }

        // Atamahane: if enabled, truncate to first winner only (after sorting)
        if (game.Rules.Atamahane && winners.Count > 1)
        {
            winners = new List<(Guid winnerId, int han, int fu, List<Yaku> yaku, Guid? paoPlayerId)> { winners[0] };
        }

        var results = new List<HandResult>();
        int honba = game.Honba;
        bool dealerWon = false;

        for (int i = 0; i < winners.Count; i++)
        {
            var (winnerId, han, fu, yaku, paoPlayerId) = winners[i];
            bool isFirst = i == 0;

            var winner = game.GetPlayerById(winnerId)
                ?? throw new InvalidOperationException("Winner not found");

            bool isDealer = game.GetDealer().Id == winnerId;
            if (isDealer) dealerWon = true;

            int effectiveHan = han;

            // Kazoeyakuman: if disabled, cap han at 12 (sanbaiman) unless hand has actual yakuman yaku
            if (!game.Rules.Kazoeyakuman)
            {
                bool hasActualYakuman = yaku.Any(y => y.IsYakuman() && y != Yaku.Kazoeyakuman);
                if (!hasActualYakuman)
                {
                    effectiveHan = Math.Min(effectiveHan, 12);
                }
            }

            // Calculate yakuman multiplier for composite/double yakuman
            int yakumanMultiplier = 1;
            if (effectiveHan >= 13)
            {
                var yakumanYaku = yaku.Where(y => y.IsYakuman() && y != Yaku.Kazoeyakuman).ToList();

                if (yakumanYaku.Count == 0)
                {
                    yakumanMultiplier = 1;
                }
                else if (game.Rules.CompositeYakuman)
                {
                    yakumanMultiplier = 0;
                    foreach (var y in yakumanYaku)
                    {
                        if (game.Rules.DoubleYakuman && y.IsDoubleYakuman())
                            yakumanMultiplier += 2;
                        else
                            yakumanMultiplier += 1;
                    }
                }
                else
                {
                    if (game.Rules.DoubleYakuman && yakumanYaku.Any(y => y.IsDoubleYakuman()))
                        yakumanMultiplier = 2;
                    else
                        yakumanMultiplier = 1;
                }
            }

            var scoring = _scoringService.CalculatePayment(effectiveHan, fu, isDealer, isTsumo, game.Players.Count, game.Rules.Kiriage, yakumanMultiplier);

            // Only first winner (atamahane) gets honba bonus
            int honbaBonus = isFirst ? honba * 300 : 0;
            bool receivedKyoutaku = isFirst;

            // Resolve pao player
            Player? paoPlayer = null;
            bool usePao = paoPlayerId.HasValue && game.Rules.Pao;
            if (usePao)
            {
                paoPlayer = game.GetPlayerById(paoPlayerId!.Value)
                    ?? throw new InvalidOperationException("Pao player not found");
            }

            var handResult = new HandResult(winnerId, loserId, effectiveHan, fu, scoring.TotalPoints, honbaBonus, yaku, receivedKyoutaku, usePao ? paoPlayerId : null);
            game.CurrentRound.AddHandResult(handResult);

            if (usePao && paoPlayer != null && !(loserId.HasValue && loserId.Value == paoPlayerId!.Value))
            {
                if (isTsumo)
                {
                    // Pao tsumo: liable player pays full ron amount + all honba
                    int ronAmount = scoring.RonPayment ?? scoring.TotalPoints;
                    // For tsumo, we need to compute the equivalent ron payment
                    // Use the scoring service to get ron payment
                    var ronScoring = _scoringService.CalculatePayment(effectiveHan, fu, game.GetDealer().Id == winnerId, false, game.Players.Count, game.Rules.Kiriage, yakumanMultiplier);
                    int paoPayment = ronScoring.RonPayment!.Value + (isFirst ? honba * 300 : 0);
                    paoPlayer.DeductPoints(paoPayment);
                    winner.AddPoints(paoPayment);
                }
                else
                {
                    // Pao ron (liable ≠ ronned): split base 50/50, liable pays honba
                    int halfBase = RoundUpTo100(scoring.RonPayment!.Value / 2);
                    int loserPays = halfBase;
                    int paoPays = halfBase + (isFirst ? honba * 300 : 0);
                    loser!.DeductPoints(loserPays);
                    paoPlayer.DeductPoints(paoPays);
                    winner.AddPoints(loserPays + paoPays);
                }
            }
            else if (isTsumo)
            {
                ApplyTsumoPayments(game, winner, scoring, honba);
            }
            else
            {
                // For multi-ron, only first winner gets honba bonus from loser
                ApplyRonPayment(winner, loser!, scoring, isFirst ? honba : 0);
            }

            if (isTsumo)
                winner.RecordTsumoWin();
            else
                winner.RecordRonWin();

            results.Add(handResult);
        }

        if (!isTsumo && loser != null)
            loser.RecordDealIn();

        // Settle riichi: first winner collects kyōtaku
        game.SettleRiichi(winners[0].winnerId);

        game.AdvanceRound(dealerWon);

        if (game.Rules.Bankruptcy && game.IsPlayerBusted())
        {
            game.EndGame();
        }

        return results;
    }

    private void ApplyTsumoPayments(Game game, Player winner, ScoringResult scoring, int honba)
    {
        int honbaPerPlayer = honba * 100;
        foreach (var player in game.Players.Where(p => p.Id != winner.Id))
        {
            int payment;
            if (player.Id == game.GetDealer().Id)
            {
                payment = (scoring.TsumoPaymentFromDealer ?? scoring.TsumoPaymentFromNonDealer!.Value) + honbaPerPlayer;
            }
            else
            {
                payment = scoring.TsumoPaymentFromNonDealer!.Value + honbaPerPlayer;
            }

            player.DeductPoints(payment);
            winner.AddPoints(payment);
        }
    }

    private static int RoundUpTo100(int amount)
    {
        return (amount + 99) / 100 * 100;
    }

    private void ApplyRonPayment(Player winner, Player loser, ScoringResult scoring, int honba)
    {
        int honbaBonus = honba * 300;
        loser.DeductPoints(scoring.RonPayment!.Value + honbaBonus);
        winner.AddPoints(scoring.RonPayment!.Value + honbaBonus);
    }

    public DrawResult RecordDraw(Guid gameId, DrawType drawType, List<Guid> tenpaiPlayerIds)
    {
        var game = GetGame(gameId)
            ?? throw new InvalidOperationException("Game not found");

        if (game.Status == GameStatus.Completed)
            throw new InvalidOperationException("Game has already ended");

        // Check if abortive draws are allowed
        if (!game.Rules.AbortiveDraws && drawType != DrawType.Exhaustive)
            throw new InvalidOperationException("Abortive draws are disabled in this game's rules");

        // Validate tenpai player IDs
        foreach (var pid in tenpaiPlayerIds)
        {
            if (game.GetPlayerById(pid) == null)
                throw new InvalidOperationException($"Player {pid} not found");
        }

        var drawResult = new DrawResult(drawType, tenpaiPlayerIds);
        game.CurrentRound.SetDrawResult(drawResult);

        // Exhaustive draw: noten penalty (3000 total from noten to tenpai)
        if (drawType == DrawType.Exhaustive)
        {
            ApplyNotenPenalty(game, tenpaiPlayerIds);
        }
        // Abortive draws: no point exchange

        bool dealerTenpai = tenpaiPlayerIds.Contains(game.GetDealer().Id);
        game.AdvanceAfterDraw(dealerTenpai);

        if (game.Rules.Bankruptcy && game.IsPlayerBusted())
        {
            game.EndGame();
        }

        return drawResult;
    }

    private void ApplyNotenPenalty(Game game, List<Guid> tenpaiPlayerIds)
    {
        int tenpaiCount = tenpaiPlayerIds.Count;
        int notenCount = game.Players.Count - tenpaiCount;

        if (tenpaiCount == 0 || tenpaiCount == 4)
            return; // No exchange if all tenpai or all noten

        // 3000 total split: noten players pay, tenpai players receive
        int payPerNoten = 3000 / notenCount;
        int receivePerTenpai = 3000 / tenpaiCount;

        foreach (var player in game.Players)
        {
            if (tenpaiPlayerIds.Contains(player.Id))
            {
                player.AddPoints(receivePerTenpai);
            }
            else
            {
                player.DeductPoints(payPerNoten);
            }
        }
    }

    public void DeclareRiichi(Guid gameId, Guid playerId)
    {
        var game = GetGame(gameId)
            ?? throw new InvalidOperationException("Game not found");

        if (game.Status == GameStatus.Completed)
            throw new InvalidOperationException("Game has already ended");

        game.DeclareRiichi(playerId);
    }

    public Game EndGame(Guid gameId)
    {
        var game = GetGame(gameId)
            ?? throw new InvalidOperationException("Game not found");

        game.EndGame();
        return game;
    }

    public bool DeleteGame(Guid gameId)
    {
        return _games.TryRemove(gameId, out _);
    }

    public IEnumerable<Game> GetGamesByPlayerName(string playerName)
    {
        return _games.Values
            .Where(g => g.Players.Any(p =>
                p.Name.Equals(playerName, StringComparison.OrdinalIgnoreCase)));
    }

    public Game? GetGameByAllPlayers(List<string> playerNames)
    {
        if (playerNames.Count != 4)
            return null;

        var sortedNames = playerNames.Select(n => n.ToLowerInvariant()).OrderBy(n => n).ToList();

        return _games.Values.FirstOrDefault(g =>
        {
            var gameNames = g.Players.Select(p => p.Name.ToLowerInvariant()).OrderBy(n => n).ToList();
            return gameNames.SequenceEqual(sortedNames);
        });
    }

    public Game? GetActiveGameByPlayerName(string playerName)
    {
        return _games.Values
            .Where(g => g.Status == GameStatus.InProgress)
            .FirstOrDefault(g => g.Players.Any(p =>
                p.Name.Equals(playerName, StringComparison.OrdinalIgnoreCase)));
    }
}
