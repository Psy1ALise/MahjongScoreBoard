using MahjongScoreBoard.Api.Models;
using MahjongScoreBoard.Api.Services;
using Xunit;
using Xunit.Abstractions;

namespace MahjongScoreBoard.Tests;

public class GameServiceTests
{
    private readonly IGameService _gameService;
    private readonly ITestOutputHelper _output;

    public GameServiceTests(ITestOutputHelper output)
    {
        _output = output;
        var scoringService = new ScoringService();
        _gameService = new GameService(scoringService);
    }

    // Helper to wrap single-winner calls into the new RecordRound signature
    private HandResult RecordSingleRound(Guid gameId, Guid winnerId, Guid? loserId, int han, int fu, List<Yaku> yaku)
    {
        var winners = new List<(Guid winnerId, int han, int fu, List<Yaku> yaku, Guid? paoPlayerId)> { (winnerId, han, fu, yaku, null) };
        return _gameService.RecordRound(gameId, winners, loserId)[0];
    }

    [Fact]
    public void CreateGame_WithValidPlayers_CreatesGame()
    {
        _output.WriteLine("=== Create Game Test ===");

        var playerNames = new List<string> { "Alice", "Bob", "Charlie", "Dave" };
        var game = _gameService.CreateGame(playerNames, 25000);

        _output.WriteLine($"Game ID: {game.Id}");
        _output.WriteLine($"Players: {string.Join(", ", game.Players.Select(p => $"{p.Name} ({p.SeatWind})"))}");
        _output.WriteLine($"Status: {game.Status}");
        _output.WriteLine($"Starting scores: {string.Join(", ", game.Players.Select(p => p.Score))}");

        Assert.NotEqual(Guid.Empty, game.Id);
        Assert.Equal(4, game.Players.Count);
        Assert.Equal(GameStatus.InProgress, game.Status);
        Assert.All(game.Players, p => Assert.Equal(25000, p.Score));
    }

    [Fact]
    public void CreateGame_WithCustomStartingScore_SetsCorrectScore()
    {
        _output.WriteLine("=== Create Game with Custom Score Test ===");

        var playerNames = new List<string> { "Alice", "Bob", "Charlie", "Dave" };
        var game = _gameService.CreateGame(playerNames, startingScore: 30000);

        _output.WriteLine($"Starting scores: {string.Join(", ", game.Players.Select(p => p.Score))}");

        Assert.All(game.Players, p => Assert.Equal(30000, p.Score));
    }

    [Fact]
    public void CreateGame_WithTooFewPlayers_ThrowsException()
    {
        _output.WriteLine("=== Create Game with Too Few Players Test ===");

        var playerNames = new List<string> { "Alice" };

        var ex = Assert.Throws<ArgumentException>(() => _gameService.CreateGame(playerNames, 25000));
        _output.WriteLine($"Exception: {ex.Message}");

        Assert.Contains("4 players", ex.Message);
    }

    [Fact]
    public void CreateGame_WithTooManyPlayers_ThrowsException()
    {
        _output.WriteLine("=== Create Game with Too Many Players Test ===");

        var playerNames = new List<string> { "P1", "P2", "P3", "P4", "P5", "P6", "P7" };

        var ex = Assert.Throws<ArgumentException>(() => _gameService.CreateGame(playerNames, 25000));
        _output.WriteLine($"Exception: {ex.Message}");

        Assert.Contains("4 players", ex.Message);
    }

    [Fact]
    public void RecordHand_Ron_UpdatesScoresCorrectly()
    {
        _output.WriteLine("=== Record Ron Hand Test ===");

        var playerNames = new List<string> { "Alice", "Bob", "Charlie", "Dave" };
        var game = _gameService.CreateGame(playerNames, 25000);

        var winner = game.Players[1]; // Bob
        var loser = game.Players[2];  // Charlie

        _output.WriteLine($"Before: Winner({winner.Name})={winner.Score}, Loser({loser.Name})={loser.Score}");

        var result = RecordSingleRound(
            game.Id,
            winner.Id,
            loser.Id,
            han: 3,
            fu: 30,
            yaku: new List<Yaku> { Yaku.Riichi, Yaku.Tanyao, Yaku.Pinfu }
        );

        game = _gameService.GetGame(game.Id)!;
        winner = game.Players[1];
        loser = game.Players[2];

        _output.WriteLine($"After: Winner({winner.Name})={winner.Score}, Loser({loser.Name})={loser.Score}");
        _output.WriteLine($"Points transferred: {result.PointsWon}");
        _output.WriteLine($"Yaku: {string.Join(", ", result.Yaku)}");

        Assert.Equal(3900, result.PointsWon);
        Assert.Equal(25000 + 3900, winner.Score);
        Assert.Equal(25000 - 3900, loser.Score);
    }

    [Fact]
    public void RecordHand_Tsumo_UpdatesAllScoresCorrectly()
    {
        _output.WriteLine("=== Record Tsumo Hand Test ===");

        var playerNames = new List<string> { "Alice", "Bob", "Charlie", "Dave" };
        var game = _gameService.CreateGame(playerNames, 25000);

        var winner = game.Players[1]; // Bob (non-dealer)

        _output.WriteLine("Before:");
        foreach (var p in game.Players)
            _output.WriteLine($"  {p.Name}: {p.Score}");

        var result = RecordSingleRound(
            game.Id,
            winner.Id,
            loserId: null, // Tsumo
            han: 3,
            fu: 30,
            yaku: new List<Yaku> { Yaku.Riichi, Yaku.Tsumo, Yaku.Tanyao }
        );

        game = _gameService.GetGame(game.Id)!;

        _output.WriteLine("After:");
        foreach (var p in game.Players)
            _output.WriteLine($"  {p.Name}: {p.Score}");

        _output.WriteLine($"Total points won: {result.PointsWon}");
        _output.WriteLine($"Is Tsumo: {result.IsTsumo}");

        Assert.True(result.IsTsumo);
        Assert.Equal(4000, result.PointsWon);

        // Winner should gain points
        Assert.Equal(25000 + 4000, game.Players[1].Score);
    }

    [Fact]
    public void RecordHand_DealerWins_DealerStays()
    {
        _output.WriteLine("=== Dealer Renchan Test ===");

        var playerNames = new List<string> { "Alice", "Bob", "Charlie", "Dave" };
        var game = _gameService.CreateGame(playerNames, 25000);

        var dealer = game.GetDealer();
        var loser = game.Players[1];

        _output.WriteLine($"Dealer before: {dealer.Name} (index {game.DealerIndex})");

        RecordSingleRound(
            game.Id,
            dealer.Id,
            loser.Id,
            han: 2,
            fu: 30,
            yaku: new List<Yaku> { Yaku.Yakuhai, Yaku.Tanyao }
        );

        game = _gameService.GetGame(game.Id)!;
        _output.WriteLine($"Dealer after: {game.GetDealer().Name} (index {game.DealerIndex})");

        Assert.Equal(dealer.Id, game.GetDealer().Id);
    }

    [Fact]
    public void RecordHand_NonDealerWins_DealerRotates()
    {
        _output.WriteLine("=== Dealer Rotation Test ===");

        var playerNames = new List<string> { "Alice", "Bob", "Charlie", "Dave" };
        var game = _gameService.CreateGame(playerNames, 25000);

        var initialDealerIndex = game.DealerIndex;
        var winner = game.Players[1]; // Non-dealer
        var loser = game.Players[2];

        _output.WriteLine($"Initial dealer index: {initialDealerIndex}");

        RecordSingleRound(
            game.Id,
            winner.Id,
            loser.Id,
            han: 2,
            fu: 30,
            yaku: new List<Yaku> { Yaku.Tanyao, Yaku.Pinfu }
        );

        game = _gameService.GetGame(game.Id)!;
        _output.WriteLine($"New dealer index: {game.DealerIndex}");

        Assert.Equal((initialDealerIndex + 1) % 4, game.DealerIndex);
    }

    [Fact]
    public void EndGame_DeterminesWinner()
    {
        _output.WriteLine("=== End Game Test ===");

        var playerNames = new List<string> { "Alice", "Bob", "Charlie", "Dave" };
        var game = _gameService.CreateGame(playerNames, 25000);

        // Play a hand so Bob has highest score
        var winner = game.Players[1];
        var loser = game.Players[2];
        RecordSingleRound(
            game.Id,
            winner.Id,
            loser.Id,
            han: 5, // Mangan
            fu: 30,
            yaku: new List<Yaku> { Yaku.Honitsu, Yaku.Yakuhai, Yaku.Tanyao }
        );

        game = _gameService.EndGame(game.Id);

        _output.WriteLine($"Game Status: {game.Status}");
        _output.WriteLine("Final Scores:");
        foreach (var p in game.Players.OrderByDescending(p => p.Score))
            _output.WriteLine($"  {p.Name}: {p.Score}");
        _output.WriteLine($"Winner: {game.Winner?.Name}");

        Assert.Equal(GameStatus.Completed, game.Status);
        Assert.NotNull(game.Winner);
        Assert.Equal(winner.Id, game.Winner.Id);
    }

    [Fact]
    public void GetGame_ReturnsCorrectGame()
    {
        _output.WriteLine("=== Get Game Test ===");

        var playerNames = new List<string> { "Alice", "Bob", "Charlie", "Dave" };
        var game = _gameService.CreateGame(playerNames, 25000);

        var retrieved = _gameService.GetGame(game.Id);

        _output.WriteLine($"Created ID: {game.Id}");
        _output.WriteLine($"Retrieved ID: {retrieved?.Id}");

        Assert.NotNull(retrieved);
        Assert.Equal(game.Id, retrieved.Id);
    }

    [Fact]
    public void GetGame_NonExistent_ReturnsNull()
    {
        _output.WriteLine("=== Get Non-Existent Game Test ===");

        var result = _gameService.GetGame(Guid.NewGuid());

        _output.WriteLine($"Result: {(result == null ? "null" : "found")}");

        Assert.Null(result);
    }

    [Fact]
    public void DeleteGame_RemovesGame()
    {
        _output.WriteLine("=== Delete Game Test ===");

        var playerNames = new List<string> { "Alice", "Bob", "Charlie", "Dave" };
        var game = _gameService.CreateGame(playerNames, 25000);

        _output.WriteLine($"Game ID: {game.Id}");

        var deleted = _gameService.DeleteGame(game.Id);
        var retrieved = _gameService.GetGame(game.Id);

        _output.WriteLine($"Deleted: {deleted}");
        _output.WriteLine($"Can still retrieve: {retrieved != null}");

        Assert.True(deleted);
        Assert.Null(retrieved);
    }

    // === Honba Tests ===

    [Fact]
    public void RecordHand_DealerWins_HonbaIncrements()
    {
        var game = _gameService.CreateGame(new List<string> { "A", "B", "C", "D" }, 25000);
        var dealer = game.GetDealer();
        var loser = game.Players[1];

        RecordSingleRound(game.Id, dealer.Id, loser.Id, 2, 30, new List<Yaku> { Yaku.Yakuhai, Yaku.Tanyao });
        game = _gameService.GetGame(game.Id)!;

        Assert.Equal(1, game.Honba);
    }

    [Fact]
    public void RecordHand_NonDealerWins_HonbaResets()
    {
        var game = _gameService.CreateGame(new List<string> { "A", "B", "C", "D" }, 25000);
        var dealer = game.GetDealer();
        var loser = game.Players[1];

        // Dealer wins first (honba -> 1)
        RecordSingleRound(game.Id, dealer.Id, loser.Id, 2, 30, new List<Yaku> { Yaku.Yakuhai, Yaku.Tanyao });
        game = _gameService.GetGame(game.Id)!;
        Assert.Equal(1, game.Honba);

        // Non-dealer wins (honba -> 0)
        var nonDealer = game.Players[1];
        RecordSingleRound(game.Id, nonDealer.Id, game.Players[2].Id, 2, 30, new List<Yaku> { Yaku.Tanyao, Yaku.Pinfu });
        game = _gameService.GetGame(game.Id)!;

        Assert.Equal(0, game.Honba);
    }

    [Fact]
    public void RecordHand_Ron_IncludesHonbaBonus()
    {
        var game = _gameService.CreateGame(new List<string> { "A", "B", "C", "D" }, 25000);
        var dealer = game.GetDealer();

        // Dealer wins to set honba=1
        RecordSingleRound(game.Id, dealer.Id, game.Players[1].Id, 2, 30, new List<Yaku> { Yaku.Yakuhai, Yaku.Tanyao });

        // Next hand with honba=1: Ron bonus = 300
        game = _gameService.GetGame(game.Id)!;
        var result = RecordSingleRound(game.Id, dealer.Id, game.Players[1].Id, 1, 30, new List<Yaku> { Yaku.Yakuhai });
        // Dealer Ron 1han30fu = 1500, honba bonus = 300

        Assert.Equal(300, result.HonbaBonus);
    }

    [Fact]
    public void RecordHand_Tsumo_IncludesHonbaBonusPerPlayer()
    {
        var game = _gameService.CreateGame(new List<string> { "A", "B", "C", "D" }, 25000);
        var dealer = game.GetDealer();

        // Dealer wins to set honba=1
        RecordSingleRound(game.Id, dealer.Id, game.Players[1].Id, 2, 30, new List<Yaku> { Yaku.Yakuhai, Yaku.Tanyao });

        // Tsumo with honba=1: bonus = 100 per non-winner × 3 = 300
        game = _gameService.GetGame(game.Id)!;
        var result = RecordSingleRound(game.Id, dealer.Id, null, 2, 30, new List<Yaku> { Yaku.Tsumo, Yaku.Yakuhai });

        Assert.Equal(300, result.HonbaBonus);
    }

    // === Draw Tests ===

    [Fact]
    public void RecordDraw_Exhaustive_AppliesNotenPenalty()
    {
        var game = _gameService.CreateGame(new List<string> { "A", "B", "C", "D" }, 25000);
        var tenpaiPlayer = game.Players[0];

        _gameService.RecordDraw(game.Id, DrawType.Exhaustive, new List<Guid> { tenpaiPlayer.Id });
        game = _gameService.GetGame(game.Id)!;

        // 1 tenpai, 3 noten: tenpai gets 3000, noten pay 1000 each
        Assert.Equal(25000 + 3000, game.Players[0].Score);
        Assert.Equal(25000 - 1000, game.Players[1].Score);
        Assert.Equal(25000 - 1000, game.Players[2].Score);
        Assert.Equal(25000 - 1000, game.Players[3].Score);
    }

    [Fact]
    public void RecordDraw_Exhaustive_AllTenpai_NoExchange()
    {
        var game = _gameService.CreateGame(new List<string> { "A", "B", "C", "D" }, 25000);
        var allIds = game.Players.Select(p => p.Id).ToList();

        _gameService.RecordDraw(game.Id, DrawType.Exhaustive, allIds);
        game = _gameService.GetGame(game.Id)!;

        Assert.All(game.Players, p => Assert.Equal(25000, p.Score));
    }

    [Fact]
    public void RecordDraw_AlwaysIncrementsHonba()
    {
        var game = _gameService.CreateGame(new List<string> { "A", "B", "C", "D" }, 25000);

        _gameService.RecordDraw(game.Id, DrawType.Exhaustive, new List<Guid> { game.Players[0].Id });
        game = _gameService.GetGame(game.Id)!;

        Assert.Equal(1, game.Honba);
    }

    [Fact]
    public void RecordDraw_DealerTenpai_DealerStays()
    {
        var game = _gameService.CreateGame(new List<string> { "A", "B", "C", "D" }, 25000);
        var dealerId = game.GetDealer().Id;

        _gameService.RecordDraw(game.Id, DrawType.Exhaustive, new List<Guid> { dealerId });
        game = _gameService.GetGame(game.Id)!;

        Assert.Equal(dealerId, game.GetDealer().Id);
    }

    [Fact]
    public void RecordDraw_DealerNoten_DealerRotates()
    {
        var game = _gameService.CreateGame(new List<string> { "A", "B", "C", "D" }, 25000);
        var initialDealerIndex = game.DealerIndex;

        // Dealer is noten (not in tenpai list)
        _gameService.RecordDraw(game.Id, DrawType.Exhaustive, new List<Guid> { game.Players[1].Id });
        game = _gameService.GetGame(game.Id)!;

        Assert.Equal((initialDealerIndex + 1) % 4, game.DealerIndex);
    }

    [Fact]
    public void RecordDraw_Abortive_NoPointExchange()
    {
        var game = _gameService.CreateGame(new List<string> { "A", "B", "C", "D" }, 25000);

        _gameService.RecordDraw(game.Id, DrawType.FourKan, new List<Guid>());
        game = _gameService.GetGame(game.Id)!;

        Assert.All(game.Players, p => Assert.Equal(25000, p.Score));
        Assert.Equal(1, game.Honba);
    }

    // === Riichi / Kyoutaku Tests ===

    [Fact]
    public void DeclareRiichi_IncrementsKyoutaku()
    {
        var game = _gameService.CreateGame(new List<string> { "A", "B", "C", "D" }, 25000);

        _gameService.DeclareRiichi(game.Id, game.Players[0].Id);
        game = _gameService.GetGame(game.Id)!;

        Assert.Equal(1, game.Kyoutaku);
        Assert.Equal(1, game.Players[0].RiichiSticks);
    }

    [Fact]
    public void SettleRiichi_WinnerCollectsKyoutaku()
    {
        var game = _gameService.CreateGame(new List<string> { "A", "B", "C", "D" }, 25000);

        // Player 0 declares riichi
        _gameService.DeclareRiichi(game.Id, game.Players[0].Id);
        // Player 1 wins by ron from player 2
        var result = RecordSingleRound(game.Id, game.Players[1].Id, game.Players[2].Id, 2, 30, new List<Yaku> { Yaku.Tanyao, Yaku.Pinfu });

        game = _gameService.GetGame(game.Id)!;

        // Kyoutaku should be 0 after settlement
        Assert.Equal(0, game.Kyoutaku);
        // Player 0: 25000 - 1000 (riichi stick deduction)
        Assert.Equal(24000, game.Players[0].Score);
        // Player 1: 25000 + 2000 (ron) + 1000 (kyoutaku)
        Assert.Equal(25000 + 2000 + 1000, game.Players[1].Score);
    }

    [Fact]
    public void RiichiSticks_StackAcrossDraws()
    {
        var game = _gameService.CreateGame(new List<string> { "A", "B", "C", "D" }, 25000);

        // Player 0 riichis, then draw
        _gameService.DeclareRiichi(game.Id, game.Players[0].Id);
        _gameService.RecordDraw(game.Id, DrawType.Exhaustive, new List<Guid> { game.Players[0].Id });

        // Player 0 riichis again
        game = _gameService.GetGame(game.Id)!;
        _gameService.DeclareRiichi(game.Id, game.Players[0].Id);
        game = _gameService.GetGame(game.Id)!;

        Assert.Equal(2, game.Players[0].RiichiSticks);
        Assert.Equal(2, game.Kyoutaku);
    }

    // === Kyoku Naming Tests ===

    [Fact]
    public void KyokuName_StartsAsEast1()
    {
        var game = _gameService.CreateGame(new List<string> { "A", "B", "C", "D" }, 25000);

        Assert.Equal("East 1", game.KyokuName);
        Assert.Equal(1, game.Kyoku);
    }

    [Fact]
    public void KyokuName_AdvancesCorrectly()
    {
        var game = _gameService.CreateGame(new List<string> { "A", "B", "C", "D" }, 25000);

        // Non-dealer wins → dealer rotates to index 1 → East 2
        RecordSingleRound(game.Id, game.Players[1].Id, game.Players[2].Id, 2, 30, new List<Yaku> { Yaku.Tanyao, Yaku.Pinfu });
        game = _gameService.GetGame(game.Id)!;

        Assert.Equal("East 2", game.KyokuName);
    }

    // === Configurable Rules Tests ===

    [Fact]
    public void CreateGame_WithCustomRules_StoresRules()
    {
        var rules = new GameRules
        {
            Kiriage = true,
            Bankruptcy = false,
            AbortiveDraws = false,
            Kazoeyakuman = false
        };

        var game = _gameService.CreateGame(new List<string> { "A", "B", "C", "D" }, 25000, rules);

        Assert.True(game.Rules.Kiriage);
        Assert.False(game.Rules.Bankruptcy);
        Assert.False(game.Rules.AbortiveDraws);
        Assert.False(game.Rules.Kazoeyakuman);
    }

    [Fact]
    public void Kiriage_4Han30Fu_RoundsToMangan()
    {
        var rules = new GameRules { Kiriage = true };
        var game = _gameService.CreateGame(new List<string> { "A", "B", "C", "D" }, 25000, rules);

        var result = RecordSingleRound(game.Id, game.Players[1].Id, game.Players[2].Id, 4, 30,
            new List<Yaku> { Yaku.Riichi, Yaku.Tanyao, Yaku.Pinfu, Yaku.Dora });

        // 4han30fu non-dealer ron: normally 7700, kiriage → mangan 8000
        Assert.Equal(8000, result.PointsWon);
    }

    [Fact]
    public void Kiriage_3Han60Fu_RoundsToMangan()
    {
        var rules = new GameRules { Kiriage = true };
        var game = _gameService.CreateGame(new List<string> { "A", "B", "C", "D" }, 25000, rules);

        var result = RecordSingleRound(game.Id, game.Players[1].Id, game.Players[2].Id, 3, 60,
            new List<Yaku> { Yaku.Toitoi, Yaku.Yakuhai });

        // 3han60fu non-dealer ron: normally 7700, kiriage → mangan 8000
        Assert.Equal(8000, result.PointsWon);
    }

    [Fact]
    public void Kiriage_Disabled_NoRounding()
    {
        var rules = new GameRules { Kiriage = false };
        var game = _gameService.CreateGame(new List<string> { "A", "B", "C", "D" }, 25000, rules);

        var result = RecordSingleRound(game.Id, game.Players[1].Id, game.Players[2].Id, 4, 30,
            new List<Yaku> { Yaku.Riichi, Yaku.Tanyao, Yaku.Pinfu, Yaku.Dora });

        Assert.Equal(7700, result.PointsWon);
    }

    [Fact]
    public void Kazoeyakuman_Disabled_CapsAtSanbaiman()
    {
        var rules = new GameRules { Kazoeyakuman = false };
        var game = _gameService.CreateGame(new List<string> { "A", "B", "C", "D" }, 25000, rules);

        // 13 han with no actual yakuman yaku → should cap at sanbaiman (24000)
        var result = RecordSingleRound(game.Id, game.Players[1].Id, game.Players[2].Id, 13, 30,
            new List<Yaku> { Yaku.Chinitsu, Yaku.Riichi, Yaku.Ippatsu, Yaku.Tanyao, Yaku.Pinfu, Yaku.Iipeikou, Yaku.Dora });

        Assert.Equal(24000, result.PointsWon);
    }

    [Fact]
    public void Kazoeyakuman_Disabled_ActualYakuman_StillYakuman()
    {
        var rules = new GameRules { Kazoeyakuman = false };
        var game = _gameService.CreateGame(new List<string> { "A", "B", "C", "D" }, 25000, rules);

        // Actual yakuman yaku → should still be yakuman (32000)
        var result = RecordSingleRound(game.Id, game.Players[1].Id, game.Players[2].Id, 13, 0,
            new List<Yaku> { Yaku.Kokushimusou });

        Assert.Equal(32000, result.PointsWon);
    }

    [Fact]
    public void AbortiveDraws_Disabled_RejectsAbortiveDraw()
    {
        var rules = new GameRules { AbortiveDraws = false };
        var game = _gameService.CreateGame(new List<string> { "A", "B", "C", "D" }, 25000, rules);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            _gameService.RecordDraw(game.Id, DrawType.FourKan, new List<Guid>()));

        Assert.Contains("Abortive draws are disabled", ex.Message);
    }

    [Fact]
    public void AbortiveDraws_Disabled_AllowsExhaustiveDraw()
    {
        var rules = new GameRules { AbortiveDraws = false };
        var game = _gameService.CreateGame(new List<string> { "A", "B", "C", "D" }, 25000, rules);

        // Should not throw
        _gameService.RecordDraw(game.Id, DrawType.Exhaustive, new List<Guid> { game.Players[0].Id });
        game = _gameService.GetGame(game.Id)!;

        Assert.Equal(1, game.Honba);
    }

    [Fact]
    public void Bankruptcy_Enabled_EndsGameOnNegativeScore()
    {
        var rules = new GameRules { Bankruptcy = true };
        var game = _gameService.CreateGame(new List<string> { "A", "B", "C", "D" }, 25000, rules);

        // Yakuman from non-dealer ron: 32000 → loser goes to -7000
        RecordSingleRound(game.Id, game.Players[1].Id, game.Players[2].Id, 13, 0,
            new List<Yaku> { Yaku.Kokushimusou });

        game = _gameService.GetGame(game.Id)!;

        Assert.Equal(GameStatus.Completed, game.Status);
        Assert.True(game.Players[2].Score < 0);
    }

    [Fact]
    public void Bankruptcy_Disabled_GameContinuesOnNegativeScore()
    {
        var rules = new GameRules { Bankruptcy = false };
        var game = _gameService.CreateGame(new List<string> { "A", "B", "C", "D" }, 25000, rules);

        // Yakuman: loser goes negative
        RecordSingleRound(game.Id, game.Players[1].Id, game.Players[2].Id, 13, 0,
            new List<Yaku> { Yaku.Kokushimusou });

        game = _gameService.GetGame(game.Id)!;

        Assert.Equal(GameStatus.InProgress, game.Status);
        Assert.True(game.Players[2].Score < 0);
    }

    // === Composite & Double Yakuman Tests ===

    [Fact]
    public void CompositeYakuman_TwoYakuman_DoublePayment()
    {
        var rules = new GameRules { CompositeYakuman = true };
        var game = _gameService.CreateGame(new List<string> { "A", "B", "C", "D" }, 100000, rules);

        var result = RecordSingleRound(game.Id, game.Players[1].Id, game.Players[2].Id, 13, 0,
            new List<Yaku> { Yaku.Tsuiisou, Yaku.Suuankou });

        // 2 yakuman × 32000 = 64000 non-dealer ron
        Assert.Equal(64000, result.PointsWon);
    }

    [Fact]
    public void CompositeYakuman_Disabled_SinglePayment()
    {
        var rules = new GameRules { CompositeYakuman = false };
        var game = _gameService.CreateGame(new List<string> { "A", "B", "C", "D" }, 100000, rules);

        var result = RecordSingleRound(game.Id, game.Players[1].Id, game.Players[2].Id, 13, 0,
            new List<Yaku> { Yaku.Tsuiisou, Yaku.Suuankou });

        // Composite disabled → single yakuman = 32000
        Assert.Equal(32000, result.PointsWon);
    }

    [Fact]
    public void DoubleYakuman_SuuankouTanki_DoublePayment()
    {
        var rules = new GameRules { DoubleYakuman = true, CompositeYakuman = true };
        var game = _gameService.CreateGame(new List<string> { "A", "B", "C", "D" }, 100000, rules);

        var result = RecordSingleRound(game.Id, game.Players[1].Id, game.Players[2].Id, 13, 0,
            new List<Yaku> { Yaku.SuuankouTanki });

        // SuuankouTanki is a double yakuman variant → 2 × 32000 = 64000
        Assert.Equal(64000, result.PointsWon);
    }

    [Fact]
    public void DoubleYakuman_Disabled_SinglePayment()
    {
        var rules = new GameRules { DoubleYakuman = false, CompositeYakuman = true };
        var game = _gameService.CreateGame(new List<string> { "A", "B", "C", "D" }, 100000, rules);

        var result = RecordSingleRound(game.Id, game.Players[1].Id, game.Players[2].Id, 13, 0,
            new List<Yaku> { Yaku.SuuankouTanki });

        // DoubleYakuman disabled → single yakuman = 32000
        Assert.Equal(32000, result.PointsWon);
    }

    [Fact]
    public void CompositeAndDouble_TripleYakuman()
    {
        var rules = new GameRules { CompositeYakuman = true, DoubleYakuman = true };
        var game = _gameService.CreateGame(new List<string> { "A", "B", "C", "D" }, 200000, rules);

        var result = RecordSingleRound(game.Id, game.Players[1].Id, game.Players[2].Id, 13, 0,
            new List<Yaku> { Yaku.Tsuiisou, Yaku.SuuankouTanki });

        // Tsuiisou(1x) + SuuankouTanki(2x) = 3x × 32000 = 96000
        Assert.Equal(96000, result.PointsWon);
    }

    [Fact]
    public void CompositeYakuman_DealerRon_CorrectPayment()
    {
        var rules = new GameRules { CompositeYakuman = true };
        var game = _gameService.CreateGame(new List<string> { "A", "B", "C", "D" }, 100000, rules);
        var dealer = game.GetDealer();

        var result = RecordSingleRound(game.Id, dealer.Id, game.Players[1].Id, 13, 0,
            new List<Yaku> { Yaku.Tsuiisou, Yaku.Suuankou });

        // 2 yakuman × 48000 (dealer) = 96000
        Assert.Equal(96000, result.PointsWon);
    }

    [Fact]
    public void CompositeYakuman_Tsumo_CorrectDistribution()
    {
        var rules = new GameRules { CompositeYakuman = true };
        var game = _gameService.CreateGame(new List<string> { "A", "B", "C", "D" }, 100000, rules);

        var result = RecordSingleRound(game.Id, game.Players[1].Id, null, 13, 0,
            new List<Yaku> { Yaku.Tsuiisou, Yaku.Suuankou });

        // Non-dealer tsumo 2x yakuman: dealer pays 16000×2=32000, others pay 8000×2=16000
        // Total: 32000 + 16000 + 16000 = 64000
        Assert.Equal(64000, result.PointsWon);

        game = _gameService.GetGame(game.Id)!;
        // Dealer (index 0) paid 32000
        Assert.Equal(100000 - 32000, game.Players[0].Score);
        // Other non-dealers paid 16000 each
        Assert.Equal(100000 - 16000, game.Players[2].Score);
        Assert.Equal(100000 - 16000, game.Players[3].Score);
        // Winner collected 64000
        Assert.Equal(100000 + 64000, game.Players[1].Score);
    }

    // === Completed Game Guard Tests ===

    [Fact]
    public void RecordHand_CompletedGame_Throws()
    {
        var game = _gameService.CreateGame(new List<string> { "A", "B", "C", "D" }, 25000);
        _gameService.EndGame(game.Id);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            RecordSingleRound(game.Id, game.Players[0].Id, game.Players[1].Id, 2, 30, new List<Yaku> { Yaku.Tanyao, Yaku.Pinfu }));

        Assert.Contains("already ended", ex.Message);
    }

    [Fact]
    public void RecordDraw_CompletedGame_Throws()
    {
        var game = _gameService.CreateGame(new List<string> { "A", "B", "C", "D" }, 25000);
        _gameService.EndGame(game.Id);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            _gameService.RecordDraw(game.Id, DrawType.Exhaustive, new List<Guid>()));

        Assert.Contains("already ended", ex.Message);
    }

    [Fact]
    public void DeclareRiichi_CompletedGame_Throws()
    {
        var game = _gameService.CreateGame(new List<string> { "A", "B", "C", "D" }, 25000);
        _gameService.EndGame(game.Id);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            _gameService.DeclareRiichi(game.Id, game.Players[0].Id));

        Assert.Contains("already ended", ex.Message);
    }

    // === Player Lookup Tests ===

    [Fact]
    public void GetGamesByPlayerName_FindsGames()
    {
        _gameService.CreateGame(new List<string> { "Alice", "Bob", "Charlie", "Dave" }, 25000);
        _gameService.CreateGame(new List<string> { "Alice", "Eve", "Frank", "Grace" }, 25000);

        var games = _gameService.GetGamesByPlayerName("Alice").ToList();

        Assert.Equal(2, games.Count);
    }

    [Fact]
    public void GetActiveGameByPlayerName_FindsOnlyActive()
    {
        var game1 = _gameService.CreateGame(new List<string> { "Alice", "Bob", "Charlie", "Dave" }, 25000);
        _gameService.EndGame(game1.Id);
        var game2 = _gameService.CreateGame(new List<string> { "Alice", "Eve", "Frank", "Grace" }, 25000);

        var active = _gameService.GetActiveGameByPlayerName("Alice");

        Assert.NotNull(active);
        Assert.Equal(game2.Id, active.Id);
    }

    // === Multi-Ron Tests ===

    [Fact]
    public void RecordRound_DoubleRon_BothWinnersScored()
    {
        var game = _gameService.CreateGame(new List<string> { "A", "B", "C", "D" }, 25000);
        var loserId = game.Players[2].Id; // C is loser

        // Turn order from loser (index 2): 3→0→1, so player 3 sorted first
        var winners = new List<(Guid winnerId, int han, int fu, List<Yaku> yaku, Guid? paoPlayerId)>
        {
            (game.Players[1].Id, 3, 30, new List<Yaku> { Yaku.Riichi, Yaku.Tanyao, Yaku.Pinfu }, null),
            (game.Players[3].Id, 2, 30, new List<Yaku> { Yaku.Tanyao, Yaku.Pinfu }, null)
        };

        var results = _gameService.RecordRound(game.Id, winners, loserId);
        game = _gameService.GetGame(game.Id)!;

        Assert.Equal(2, results.Count);
        // Player 3 (atamahane) sorted first: 2000 ron
        Assert.Equal(game.Players[3].Id, results[0].WinnerId);
        Assert.Equal(2000, results[0].PointsWon);
        // Player 1 sorted second: 3900 ron
        Assert.Equal(game.Players[1].Id, results[1].WinnerId);
        Assert.Equal(3900, results[1].PointsWon);
        // Loser paid both
        Assert.Equal(25000 - 3900 - 2000, game.Players[2].Score);
    }

    [Fact]
    public void RecordRound_DoubleRon_FirstWinnerGetsHonbaAndKyoutaku()
    {
        var game = _gameService.CreateGame(new List<string> { "A", "B", "C", "D" }, 25000);

        // Set honba to 1 by dealer winning first
        RecordSingleRound(game.Id, game.Players[0].Id, game.Players[1].Id, 2, 30, new List<Yaku> { Yaku.Yakuhai, Yaku.Tanyao });
        game = _gameService.GetGame(game.Id)!;
        Assert.Equal(1, game.Honba);

        // Declare riichi for player 0
        _gameService.DeclareRiichi(game.Id, game.Players[0].Id);
        game = _gameService.GetGame(game.Id)!;
        Assert.Equal(1, game.Kyoutaku);

        // Double ron from player 2 (loser). Seat order from loser(2): 3→0→1
        // Pass in "wrong" order — server should auto-sort by atamahane
        var winners = new List<(Guid winnerId, int han, int fu, List<Yaku> yaku, Guid? paoPlayerId)>
        {
            (game.Players[1].Id, 2, 30, new List<Yaku> { Yaku.Tanyao, Yaku.Pinfu }, null),
            (game.Players[3].Id, 1, 30, new List<Yaku> { Yaku.Yakuhai }, null)
        };

        var results = _gameService.RecordRound(game.Id, winners, game.Players[2].Id);

        // Player 3 is closer to loser (index 2) in turn order, so sorted first
        Assert.Equal(game.Players[3].Id, results[0].WinnerId);
        Assert.Equal(game.Players[1].Id, results[1].WinnerId);

        // First (atamahane) winner gets honba bonus (300) and kyoutaku
        Assert.Equal(300, results[0].HonbaBonus);
        Assert.True(results[0].ReceivedKyoutaku);

        // Second winner gets no honba bonus and no kyoutaku
        Assert.Equal(0, results[1].HonbaBonus);
        Assert.False(results[1].ReceivedKyoutaku);

        game = _gameService.GetGame(game.Id)!;
        Assert.Equal(0, game.Kyoutaku);
    }

    [Fact]
    public void RecordRound_Atamahane_MultiRonTruncatedToFirst()
    {
        var rules = new GameRules { Atamahane = true };
        var game = _gameService.CreateGame(new List<string> { "A", "B", "C", "D" }, 25000, rules);

        // Loser is player 2. Turn order from loser: 3→0→1.
        // Player 3 is closest, so atamahane keeps player 3.
        var winners = new List<(Guid winnerId, int han, int fu, List<Yaku> yaku, Guid? paoPlayerId)>
        {
            (game.Players[1].Id, 3, 30, new List<Yaku> { Yaku.Riichi, Yaku.Tanyao, Yaku.Pinfu }, null),
            (game.Players[3].Id, 2, 30, new List<Yaku> { Yaku.Tanyao, Yaku.Pinfu }, null)
        };

        var results = _gameService.RecordRound(game.Id, winners, game.Players[2].Id);

        // Only the atamahane winner (player 3, closest to loser) is processed
        Assert.Single(results);
        Assert.Equal(game.Players[3].Id, results[0].WinnerId);
        Assert.Equal(2000, results[0].PointsWon);

        // Loser only paid atamahane winner
        game = _gameService.GetGame(game.Id)!;
        Assert.Equal(25000 - 2000, game.Players[2].Score);
        // Player 1 was not processed
        Assert.Equal(25000, game.Players[1].Score);
    }

    [Fact]
    public void RecordRound_DoubleRon_DealerWins_Renchan()
    {
        var game = _gameService.CreateGame(new List<string> { "A", "B", "C", "D" }, 25000);
        var dealerId = game.GetDealer().Id; // Player 0

        // Double ron where dealer is one of the winners
        var winners = new List<(Guid winnerId, int han, int fu, List<Yaku> yaku, Guid? paoPlayerId)>
        {
            (game.Players[1].Id, 2, 30, new List<Yaku> { Yaku.Tanyao, Yaku.Pinfu }, null),
            (dealerId, 2, 30, new List<Yaku> { Yaku.Yakuhai, Yaku.Tanyao }, null)
        };

        _gameService.RecordRound(game.Id, winners, game.Players[2].Id);
        game = _gameService.GetGame(game.Id)!;

        // Dealer won → renchan
        Assert.Equal(dealerId, game.GetDealer().Id);
        Assert.Equal(1, game.Honba);
    }

    [Fact]
    public void RecordRound_DoubleRon_AutoSortsByAtamahaneOrder()
    {
        // Players: 0(A), 1(B), 2(C), 3(D)
        // Loser is player 0. Turn order from loser: 1→2→3
        // Pass winners in reverse order (3 then 1) — server should reorder to 1 then 3
        var game = _gameService.CreateGame(new List<string> { "A", "B", "C", "D" }, 25000);

        var winners = new List<(Guid winnerId, int han, int fu, List<Yaku> yaku, Guid? paoPlayerId)>
        {
            (game.Players[3].Id, 1, 30, new List<Yaku> { Yaku.Yakuhai }, null),
            (game.Players[1].Id, 2, 30, new List<Yaku> { Yaku.Tanyao, Yaku.Pinfu }, null)
        };

        var results = _gameService.RecordRound(game.Id, winners, game.Players[0].Id);

        // Player 1 is closer to loser (index 0) in turn order, so sorted first
        Assert.Equal(game.Players[1].Id, results[0].WinnerId);
        Assert.Equal(game.Players[3].Id, results[1].WinnerId);

        // Only the atamahane winner (player 1) gets honba and kyoutaku
        Assert.True(results[0].ReceivedKyoutaku);
        Assert.False(results[1].ReceivedKyoutaku);
    }

    [Fact]
    public void RecordRound_DoubleRon_PostmanFlow_CorrectScores()
    {
        // Mirrors "Full Game Flow 4 (Multi-Ron)" in Postman
        // Players: 0=Dealer(East), 1=South, 2=West, 3=North
        var game = _gameService.CreateGame(new List<string> { "Dealer", "South", "West", "North" }, 25000);

        // Step 2: Dealer ron from South (2han30fu dealer ron = 2900). Renchan → honba=1
        RecordSingleRound(game.Id, game.Players[0].Id, game.Players[1].Id, 2, 30,
            new List<Yaku> { Yaku.Yakuhai, Yaku.Tanyao });
        game = _gameService.GetGame(game.Id)!;
        Assert.Equal(1, game.Honba);
        Assert.Equal(27900, game.Players[0].Score); // Dealer
        Assert.Equal(22100, game.Players[1].Score); // South

        // Steps 4-5: South and North declare riichi
        _gameService.DeclareRiichi(game.Id, game.Players[1].Id);
        _gameService.DeclareRiichi(game.Id, game.Players[3].Id);
        game = _gameService.GetGame(game.Id)!;
        Assert.Equal(2, game.Kyoutaku);

        // Step 6: Double ron from West (player 2). South(3han30fu) and North(1han30fu).
        // Atamahane: West(2)→North(3)→Dealer(0)→South(1). North is first.
        var winners = new List<(Guid winnerId, int han, int fu, List<Yaku> yaku, Guid? paoPlayerId)>
        {
            (game.Players[1].Id, 3, 30, new List<Yaku> { Yaku.Riichi, Yaku.Tanyao, Yaku.Pinfu }, null),
            (game.Players[3].Id, 1, 30, new List<Yaku> { Yaku.Riichi }, null)
        };
        var results = _gameService.RecordRound(game.Id, winners, game.Players[2].Id);
        game = _gameService.GetGame(game.Id)!;

        // North sorted first (atamahane)
        Assert.Equal(game.Players[3].Id, results[0].WinnerId);
        Assert.Equal(game.Players[1].Id, results[1].WinnerId);

        // Kyoutaku cleared, honba reset
        Assert.Equal(0, game.Kyoutaku);
        Assert.Equal(0, game.Honba);

        // Score verification:
        // Dealer: 27900 (unchanged)
        Assert.Equal(27900, game.Players[0].Score);
        // South: 22100 + 3900 (ron) - 1000 (riichi) = 24000 (note: no kyoutaku or honba for South)
        // Actually South gets kyoutaku? No — North is atamahane, North gets kyoutaku.
        // Wait: SettleRiichi gives kyoutaku to first winner (North). But both South and North
        // lose 1000 each for their riichi sticks.
        // South: 22100 + 3900 - 1000 = 25000... let me recalc.
        // Hmm, 22100 - 1000 (riichi stick) + 3900 (ron) = 25000
        // North: 25000 - 1000 (riichi stick) + 1000 (ron) + 300 (honba) + 2000 (kyoutaku) = 27300
        // West: 25000 - 1300 (ron+honba to North) - 3900 (ron to South) = 19800

        Assert.Equal(25000, game.Players[1].Score); // South
        Assert.Equal(19800, game.Players[2].Score); // West
        Assert.Equal(27300, game.Players[3].Score); // North
    }

    // === Player Statistics Tests ===

    [Fact]
    public void PlayerStats_RonWin_IncrementsWinnerAndLoser()
    {
        var game = _gameService.CreateGame(new List<string> { "A", "B", "C", "D" }, 25000);

        RecordSingleRound(game.Id, game.Players[1].Id, game.Players[2].Id, 2, 30,
            new List<Yaku> { Yaku.Tanyao, Yaku.Pinfu });
        game = _gameService.GetGame(game.Id)!;

        Assert.Equal(1, game.Players[1].RonWins);
        Assert.Equal(0, game.Players[1].TsumoWins);
        Assert.Equal(1, game.Players[2].DealInCount);
        Assert.Equal(0, game.Players[0].RonWins);
        Assert.Equal(0, game.Players[0].DealInCount);
    }

    [Fact]
    public void PlayerStats_TsumoWin_IncrementsWinner()
    {
        var game = _gameService.CreateGame(new List<string> { "A", "B", "C", "D" }, 25000);

        RecordSingleRound(game.Id, game.Players[0].Id, null, 2, 30,
            new List<Yaku> { Yaku.Tanyao, Yaku.Tsumo });
        game = _gameService.GetGame(game.Id)!;

        Assert.Equal(1, game.Players[0].TsumoWins);
        Assert.Equal(0, game.Players[0].RonWins);
        // No deal-in for tsumo
        Assert.All(game.Players, p => Assert.Equal(0, p.DealInCount));
    }

    [Fact]
    public void PlayerStats_Riichi_IncrementsRiichiCount()
    {
        var game = _gameService.CreateGame(new List<string> { "A", "B", "C", "D" }, 25000);

        _gameService.DeclareRiichi(game.Id, game.Players[0].Id);
        _gameService.DeclareRiichi(game.Id, game.Players[0].Id);
        game = _gameService.GetGame(game.Id)!;

        Assert.Equal(2, game.Players[0].RiichiCount);
        Assert.Equal(0, game.Players[1].RiichiCount);
    }

    [Fact]
    public void PlayerStats_MultipleRounds_Accumulate()
    {
        var game = _gameService.CreateGame(new List<string> { "A", "B", "C", "D" }, 25000);

        // Round 1: Player 1 rons from Player 2
        RecordSingleRound(game.Id, game.Players[1].Id, game.Players[2].Id, 2, 30,
            new List<Yaku> { Yaku.Tanyao, Yaku.Pinfu });

        // Round 2: Player 1 rons from Player 3
        RecordSingleRound(game.Id, game.Players[1].Id, game.Players[3].Id, 2, 30,
            new List<Yaku> { Yaku.Tanyao, Yaku.Pinfu });

        // Round 3: Player 0 tsumo
        RecordSingleRound(game.Id, game.Players[0].Id, null, 2, 30,
            new List<Yaku> { Yaku.Tanyao, Yaku.Tsumo });

        game = _gameService.GetGame(game.Id)!;

        Assert.Equal(2, game.Players[1].RonWins);
        Assert.Equal(0, game.Players[1].TsumoWins);
        Assert.Equal(1, game.Players[0].TsumoWins);
        Assert.Equal(1, game.Players[2].DealInCount);
        Assert.Equal(1, game.Players[3].DealInCount);
    }

    // === Oka/Uma Tests ===

    [Fact]
    public void OkaUma_DefaultRules_CalculatesCorrectly()
    {
        // Default: startingScore=25000, targetScore=30000, uma=[20,10,-10,-20]
        // Oka = (30000 - 25000) * 4 = 20000 → awarded to 1st
        var game = _gameService.CreateGame(new List<string> { "A", "B", "C", "D" }, 25000);

        // Give player 1 a win so scores differ
        RecordSingleRound(game.Id, game.Players[1].Id, game.Players[2].Id, 3, 30,
            new List<Yaku> { Yaku.Riichi, Yaku.Tanyao, Yaku.Pinfu });

        _gameService.EndGame(game.Id);
        game = _gameService.GetGame(game.Id)!;

        Assert.NotNull(game.FinalScores);
        Assert.Equal(4, game.FinalScores!.Count);

        // Player 1 won 3900 → 28900. Others: A=25000, C=21100, D=25000
        // Sorted: B(28900), A(25000), D(25000), C(21100)
        // Tiebreak A vs D: A is index 0, D is index 3 → A ranks higher
        Assert.Equal("B", game.FinalScores[0].PlayerName);
        Assert.Equal("A", game.FinalScores[1].PlayerName);
        Assert.Equal("D", game.FinalScores[2].PlayerName);
        Assert.Equal("C", game.FinalScores[3].PlayerName);

        // B: (28900 - 30000 + 20000) = 18900 → 18900/1000 = 18.9 → ceil(18.4) = 19 → +uma(20) = 39
        // A: (25000 - 30000) = -5000 → -5.0 → ceil(-5.5) = -5 → +uma(10) = 5
        // D: (25000 - 30000) = -5000 → -5.0 → ceil(-5.5) = -5 → +uma(-10) = -15
        // C: (21100 - 30000) = -8900 → -8.9 → ceil(-9.4) = -9 → +uma(-20) = -29
        // Sum of 2nd,3rd,4th: 5 + (-15) + (-29) = -39 → 1st = 39 ✓

        Assert.Equal(39, game.FinalScores[0].FinalScore);
        Assert.Equal(5, game.FinalScores[1].FinalScore);
        Assert.Equal(-15, game.FinalScores[2].FinalScore);
        Assert.Equal(-29, game.FinalScores[3].FinalScore);

        // Zero-sum check
        Assert.Equal(0, game.FinalScores.Sum(f => f.FinalScore));
    }

    [Fact]
    public void OkaUma_CustomUma_AppliesCorrectly()
    {
        var rules = new GameRules { Uma = new List<int> { 30, 10, -10, -30 } };
        var game = _gameService.CreateGame(new List<string> { "A", "B", "C", "D" }, 25000, rules);

        _gameService.EndGame(game.Id);
        game = _gameService.GetGame(game.Id)!;

        // All equal 25000. Tiebreak by seat: A(1st), B(2nd), C(3rd), D(4th)
        // A: (25000-30000+20000)=15000 → 15 → +30 = 45
        // B: -5000 → -5 → +10 = 5
        // C: -5000 → -5 → -10 = -15
        // D: -5000 → -5 → -30 = -35
        // Sum 2+3+4: 5-15-35 = -45 → 1st = 45 ✓

        Assert.Equal(0, game.FinalScores!.Sum(f => f.FinalScore));
        Assert.Equal(45, game.FinalScores[0].FinalScore);
        Assert.Equal(-35, game.FinalScores[3].FinalScore);
    }

    [Fact]
    public void OkaUma_NoOka_WhenTargetEqualsStarting()
    {
        var rules = new GameRules { TargetScore = 25000 };
        var game = _gameService.CreateGame(new List<string> { "A", "B", "C", "D" }, 25000, rules);

        _gameService.EndGame(game.Id);
        game = _gameService.GetGame(game.Id)!;

        // Oka = (25000-25000)*4 = 0. All at 25000.
        // A: (25000-25000)=0 → 0 → +20 = 20
        // B: 0 → +10 = 10
        // C: 0 → -10 = -10
        // D: 0 → -20 = -20
        // Sum 2+3+4 = 10-10-20 = -20 → 1st = 20 ✓

        Assert.Equal(20, game.FinalScores![0].FinalScore);
        Assert.Equal(0, game.FinalScores.Sum(f => f.FinalScore));
    }

    [Fact]
    public void PlayerStats_DoubleRon_BothWinnersAndLoserTracked()
    {
        var game = _gameService.CreateGame(new List<string> { "A", "B", "C", "D" }, 25000);

        var winners = new List<(Guid winnerId, int han, int fu, List<Yaku> yaku, Guid? paoPlayerId)>
        {
            (game.Players[1].Id, 2, 30, new List<Yaku> { Yaku.Tanyao, Yaku.Pinfu }, null),
            (game.Players[3].Id, 1, 30, new List<Yaku> { Yaku.Yakuhai }, null)
        };
        _gameService.RecordRound(game.Id, winners, game.Players[2].Id);
        game = _gameService.GetGame(game.Id)!;

        Assert.Equal(1, game.Players[1].RonWins);
        Assert.Equal(1, game.Players[3].RonWins);
        Assert.Equal(1, game.Players[2].DealInCount); // Only counted once
    }

    // === Pao (Sekinin Barai) Tests ===

    [Fact]
    public void Pao_Tsumo_LiablePaysFull_OthersPayNothing()
    {
        var rules = new GameRules { Pao = true };
        var game = _gameService.CreateGame(new List<string> { "A", "B", "C", "D" }, 100000, rules);

        // Player 1 tsumo yakuman, player 2 is liable (pao)
        var winners = new List<(Guid winnerId, int han, int fu, List<Yaku> yaku, Guid? paoPlayerId)>
        {
            (game.Players[1].Id, 13, 0, new List<Yaku> { Yaku.Daisangen }, game.Players[2].Id)
        };
        _gameService.RecordRound(game.Id, winners, null);
        game = _gameService.GetGame(game.Id)!;

        // Non-dealer tsumo yakuman: normal would be dealer 16000 + 8000×2 = 32000
        // With pao: liable pays equivalent ron amount = 32000
        // Dealer and player 3 pay nothing
        Assert.Equal(100000, game.Players[0].Score); // Dealer unchanged
        Assert.Equal(100000 + 32000, game.Players[1].Score); // Winner
        Assert.Equal(100000 - 32000, game.Players[2].Score); // Liable
        Assert.Equal(100000, game.Players[3].Score); // Unchanged
    }

    [Fact]
    public void Pao_Ron_LiableNotRonned_SplitPayment()
    {
        var rules = new GameRules { Pao = true };
        var game = _gameService.CreateGame(new List<string> { "A", "B", "C", "D" }, 100000, rules);

        // Player 1 rons from player 3, player 2 is liable (pao)
        var winners = new List<(Guid winnerId, int han, int fu, List<Yaku> yaku, Guid? paoPlayerId)>
        {
            (game.Players[1].Id, 13, 0, new List<Yaku> { Yaku.Daisangen }, game.Players[2].Id)
        };
        _gameService.RecordRound(game.Id, winners, game.Players[3].Id);
        game = _gameService.GetGame(game.Id)!;

        // Non-dealer ron yakuman = 32000
        // Split: ronned player pays half (16000), liable pays half + honba (16000 + 0)
        Assert.Equal(100000 + 32000, game.Players[1].Score); // Winner gets full
        Assert.Equal(100000 - 16000, game.Players[2].Score); // Liable pays half
        Assert.Equal(100000 - 16000, game.Players[3].Score); // Ronned pays half
    }

    [Fact]
    public void Pao_Ron_LiableIsRonned_NormalRon()
    {
        var rules = new GameRules { Pao = true };
        var game = _gameService.CreateGame(new List<string> { "A", "B", "C", "D" }, 100000, rules);

        // Player 1 rons from player 2, player 2 is also liable (pao)
        var winners = new List<(Guid winnerId, int han, int fu, List<Yaku> yaku, Guid? paoPlayerId)>
        {
            (game.Players[1].Id, 13, 0, new List<Yaku> { Yaku.Daisangen }, game.Players[2].Id)
        };
        _gameService.RecordRound(game.Id, winners, game.Players[2].Id);
        game = _gameService.GetGame(game.Id)!;

        // Liable = ronned → normal ron payment
        Assert.Equal(100000 + 32000, game.Players[1].Score);
        Assert.Equal(100000 - 32000, game.Players[2].Score);
    }

    [Fact]
    public void Pao_Disabled_PaoPlayerIdIgnored()
    {
        // Pao rule is OFF (default)
        var game = _gameService.CreateGame(new List<string> { "A", "B", "C", "D" }, 100000);

        // Pass paoPlayerId anyway — should be ignored
        var winners = new List<(Guid winnerId, int han, int fu, List<Yaku> yaku, Guid? paoPlayerId)>
        {
            (game.Players[1].Id, 13, 0, new List<Yaku> { Yaku.Daisangen }, game.Players[2].Id)
        };
        _gameService.RecordRound(game.Id, winners, null);
        game = _gameService.GetGame(game.Id)!;

        // Normal tsumo: dealer pays 16000, others pay 8000 each
        Assert.Equal(100000 - 16000, game.Players[0].Score); // Dealer
        Assert.Equal(100000 + 32000, game.Players[1].Score); // Winner
        Assert.Equal(100000 - 8000, game.Players[2].Score);
        Assert.Equal(100000 - 8000, game.Players[3].Score);
    }

    [Fact]
    public void Pao_Enabled_NoPaoPlayerId_NormalPayment()
    {
        var rules = new GameRules { Pao = true };
        var game = _gameService.CreateGame(new List<string> { "A", "B", "C", "D" }, 100000, rules);

        // No paoPlayerId
        var winners = new List<(Guid winnerId, int han, int fu, List<Yaku> yaku, Guid? paoPlayerId)>
        {
            (game.Players[1].Id, 13, 0, new List<Yaku> { Yaku.Daisangen }, null)
        };
        _gameService.RecordRound(game.Id, winners, null);
        game = _gameService.GetGame(game.Id)!;

        // Normal tsumo
        Assert.Equal(100000 - 16000, game.Players[0].Score);
        Assert.Equal(100000 + 32000, game.Players[1].Score);
        Assert.Equal(100000 - 8000, game.Players[2].Score);
        Assert.Equal(100000 - 8000, game.Players[3].Score);
    }
}
