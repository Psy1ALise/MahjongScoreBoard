using MahjongScoreBoard.Api.Models;

namespace MahjongScoreBoard.Api.Services;

public interface IGameService
{
    Game CreateGame(List<string> playerNames, int startingScore, GameRules? rules = null);
    Game? GetGame(Guid gameId);
    IEnumerable<Game> GetAllGames();
    List<HandResult> RecordRound(Guid gameId, List<(Guid winnerId, int han, int fu, List<Yaku> yaku, Guid? paoPlayerId)> winners, Guid? loserId);
    DrawResult RecordDraw(Guid gameId, DrawType drawType, List<Guid> tenpaiPlayerIds);
    void DeclareRiichi(Guid gameId, Guid playerId);
    Game EndGame(Guid gameId);
    bool DeleteGame(Guid gameId);

    // Player name based lookups
    IEnumerable<Game> GetGamesByPlayerName(string playerName);
    Game? GetGameByAllPlayers(List<string> playerNames);
    Game? GetActiveGameByPlayerName(string playerName);
}
