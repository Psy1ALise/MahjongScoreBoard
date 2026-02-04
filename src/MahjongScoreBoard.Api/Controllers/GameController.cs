using Microsoft.AspNetCore.Mvc;
using MahjongScoreBoard.Api.Models;
using MahjongScoreBoard.Api.Requests;
using MahjongScoreBoard.Api.Services;

namespace MahjongScoreBoard.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GameController : ControllerBase
{
    private readonly IGameService _gameService;

    public GameController(IGameService gameService)
    {
        _gameService = gameService;
    }

    [HttpPost]
    public ActionResult<GameResponse> CreateGame([FromBody] CreateGameRequest request)
    {
        var rules = new GameRules
        {
            Kiriage = request.Kiriage,
            Atamahane = request.Atamahane,
            Kazoeyakuman = request.Kazoeyakuman,
            DoubleYakuman = request.DoubleYakuman,
            CompositeYakuman = request.CompositeYakuman,
            Bankruptcy = request.Bankruptcy,
            AbortiveDraws = request.AbortiveDraws,
            Pao = request.Pao,
            TargetScore = request.TargetScore,
            Uma = request.Uma
        };
        var game = _gameService.CreateGame(request.PlayerNames, request.StartingScore, rules);
        return CreatedAtAction(nameof(GetGame), new { id = game.Id }, ToGameResponse(game));
    }

    [HttpGet("{id:guid}")]
    public ActionResult<GameResponse> GetGame(Guid id)
    {
        var game = _gameService.GetGame(id);
        if (game == null)
            return NotFound();

        return Ok(ToGameResponse(game));
    }

    [HttpGet]
    public ActionResult<IEnumerable<GameSummaryResponse>> GetAllGames()
    {
        var games = _gameService.GetAllGames()
            .Select(g => new GameSummaryResponse(
                g.Id,
                g.Players.Select(p => new PlayerSummary(p.Name, p.Score, p.RonWins, p.TsumoWins, p.DealInCount, p.RiichiCount)).ToList(),
                g.Status.ToString(),
                g.CreatedAt,
                g.CompletedAt
            ));

        return Ok(games);
    }

    [HttpPost("{id:guid}/round")]
    public ActionResult<RoundConclusionResponse> RecordRound(Guid id, [FromBody] RecordRoundRequest request)
    {
        try
        {
            if (!request.LoserId.HasValue && request.Winners.Count > 1)
                return BadRequest(new { error = "Tsumo can only have one winner" });

            var winners = request.Winners.Select(w => (w.WinnerId, w.Han, w.Fu, w.Yaku, w.PaoPlayerId)).ToList();
            var results = _gameService.RecordRound(id, winners, request.LoserId);

            var game = _gameService.GetGame(id)!;
            var response = new RoundConclusionResponse(
                results.Select(r => new HandResultResponse(
                    r.Id,
                    r.WinnerId,
                    r.LoserId,
                    r.Han,
                    r.Fu,
                    r.PointsWon,
                    r.HonbaBonus,
                    r.Yaku.Select(y => y.ToString()).ToList(),
                    r.IsTsumo,
                    r.ReceivedKyoutaku,
                    r.PaoPlayerId
                )).ToList(),
                game.Status.ToString()
            );

            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/draw")]
    public ActionResult<DrawResultResponse> RecordDraw(Guid id, [FromBody] RecordDrawRequest request)
    {
        try
        {
            var result = _gameService.RecordDraw(id, request.DrawType, request.TenpaiPlayerIds);
            var game = _gameService.GetGame(id)!;
            return Ok(new DrawResultResponse(
                result.Id,
                result.DrawType.ToString(),
                result.TenpaiPlayerIds,
                game.Honba,
                game.Status.ToString()
            ));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/riichi")]
    public ActionResult<GameResponse> DeclareRiichi(Guid id, [FromBody] DeclareRiichiRequest request)
    {
        try
        {
            _gameService.DeclareRiichi(id, request.PlayerId);
            var game = _gameService.GetGame(id)!;
            return Ok(ToGameResponse(game));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{id:guid}/history")]
    public ActionResult<GameHistoryResponse> GetHistory(Guid id)
    {
        var game = _gameService.GetGame(id);
        if (game == null)
            return NotFound();

        var rounds = game.Rounds.Select(r => new RoundResponse(
            r.RoundNumber,
            r.RoundWind.ToString(),
            r.DealerIndex,
            r.Kyoku,
            r.KyokuName,
            r.Honba,
            r.HandResults.Select(h => new HandResultResponse(
                h.Id,
                h.WinnerId,
                h.LoserId,
                h.Han,
                h.Fu,
                h.PointsWon,
                h.HonbaBonus,
                h.Yaku.Select(y => y.ToString()).ToList(),
                h.IsTsumo,
                h.ReceivedKyoutaku,
                h.PaoPlayerId
            )).ToList(),
            r.DrawResult != null ? new DrawResultResponse(
                r.DrawResult.Id,
                r.DrawResult.DrawType.ToString(),
                r.DrawResult.TenpaiPlayerIds,
                r.Honba,
                null
            ) : null
        )).ToList();

        return Ok(new GameHistoryResponse(game.Id, rounds));
    }

    [HttpPost("{id:guid}/end")]
    public ActionResult<GameResponse> EndGame(Guid id)
    {
        try
        {
            var game = _gameService.EndGame(id);
            return Ok(ToGameResponse(game));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public ActionResult DeleteGame(Guid id)
    {
        if (_gameService.DeleteGame(id))
            return NoContent();

        return NotFound();
    }

    [HttpGet("player/{playerName}")]
    public ActionResult<IEnumerable<GameSummaryResponse>> GetGamesByPlayer(string playerName)
    {
        var games = _gameService.GetGamesByPlayerName(playerName)
            .Select(g => new GameSummaryResponse(
                g.Id,
                g.Players.Select(p => new PlayerSummary(p.Name, p.Score, p.RonWins, p.TsumoWins, p.DealInCount, p.RiichiCount)).ToList(),
                g.Status.ToString(),
                g.CreatedAt,
                g.CompletedAt
            ));

        return Ok(games);
    }

    [HttpGet("search")]
    public ActionResult<GameResponse> GetGameByPlayers([FromQuery] string players)
    {
        if (string.IsNullOrWhiteSpace(players))
            return BadRequest(new { error = "Players query parameter is required" });

        var playerNames = players.Split(',').Select(p => p.Trim()).ToList();

        if (playerNames.Count != 4)
            return BadRequest(new { error = "Exactly 4 player names are required" });

        var game = _gameService.GetGameByAllPlayers(playerNames);
        if (game == null)
            return NotFound();

        return Ok(ToGameResponse(game));
    }

    [HttpGet("active/{playerName}")]
    public ActionResult<GameResponse> GetActiveGameByPlayer(string playerName)
    {
        var game = _gameService.GetActiveGameByPlayerName(playerName);
        if (game == null)
            return NotFound();

        return Ok(ToGameResponse(game));
    }

    private static GameResponse ToGameResponse(Game game)
    {
        return new GameResponse(
            game.Id,
            game.Players.Select(p => new PlayerResponse(
                p.Id,
                p.Name,
                p.Score,
                p.SeatWind.ToString(),
                p.RiichiSticks,
                p.RonWins,
                p.TsumoWins,
                p.DealInCount,
                p.RiichiCount
            )).ToList(),
            game.CurrentRoundNumber,
            game.RoundWind.ToString(),
            game.DealerIndex,
            game.Kyoku,
            game.KyokuName,
            game.Honba,
            game.Kyoutaku,
            game.Status.ToString(),
            new GameRulesResponse(
                game.Rules.Kiriage,
                game.Rules.Atamahane,
                game.Rules.Kazoeyakuman,
                game.Rules.DoubleYakuman,
                game.Rules.CompositeYakuman,
                game.Rules.Bankruptcy,
                game.Rules.AbortiveDraws,
                game.Rules.Pao,
                game.Rules.TargetScore,
                game.Rules.Uma
            ),
            game.Status == GameStatus.Completed && game.FinalScores != null
                ? game.FinalScores.Select(fs =>
                {
                    var p = game.GetPlayerById(fs.PlayerId)!;
                    return new RankingEntry(
                        fs.Place,
                        new PlayerResponse(p.Id, p.Name, p.Score, p.SeatWind.ToString(), p.RiichiSticks, p.RonWins, p.TsumoWins, p.DealInCount, p.RiichiCount),
                        fs.AdjustedScore,
                        fs.FinalScore
                    );
                }).ToList()
                : null,
            game.CreatedAt,
            game.CompletedAt
        );
    }
}

public record GameResponse(
    Guid Id,
    List<PlayerResponse> Players,
    int CurrentRound,
    string RoundWind,
    int DealerIndex,
    int Kyoku,
    string KyokuName,
    int Honba,
    int Kyoutaku,
    string Status,
    GameRulesResponse Rules,
    List<RankingEntry>? Ranking,
    DateTime CreatedAt,
    DateTime? CompletedAt
);

public record RankingEntry(
    int Place,
    PlayerResponse Player,
    int AdjustedScore,
    double FinalScore
);

public record GameRulesResponse(
    bool Kiriage,
    bool Atamahane,
    bool Kazoeyakuman,
    bool DoubleYakuman,
    bool CompositeYakuman,
    bool Bankruptcy,
    bool AbortiveDraws,
    bool Pao,
    int TargetScore,
    List<int> Uma
);

public record GameSummaryResponse(
    Guid Id,
    List<PlayerSummary> Players,
    string Status,
    DateTime CreatedAt,
    DateTime? CompletedAt
);

public record PlayerSummary(
    string Name,
    int Score,
    int RonWins,
    int TsumoWins,
    int DealInCount,
    int RiichiCount
);

public record PlayerResponse(
    Guid Id,
    string Name,
    int Score,
    string SeatWind,
    int RiichiSticks,
    int RonWins,
    int TsumoWins,
    int DealInCount,
    int RiichiCount
);

public record RoundConclusionResponse(
    List<HandResultResponse> Results,
    string GameStatus
);

public record HandResultResponse(
    Guid Id,
    Guid WinnerId,
    Guid? LoserId,
    int Han,
    int Fu,
    int PointsWon,
    int HonbaBonus,
    List<string> Yaku,
    bool IsTsumo,
    bool ReceivedKyoutaku,
    Guid? PaoPlayerId
);

public record DrawResultResponse(
    Guid Id,
    string DrawType,
    List<Guid> TenpaiPlayerIds,
    int Honba,
    string? GameStatus
);

public record RoundResponse(
    int RoundNumber,
    string RoundWind,
    int DealerIndex,
    int Kyoku,
    string KyokuName,
    int Honba,
    List<HandResultResponse> HandResults,
    DrawResultResponse? DrawResult = null
);

public record GameHistoryResponse(
    Guid GameId,
    List<RoundResponse> Rounds
);
