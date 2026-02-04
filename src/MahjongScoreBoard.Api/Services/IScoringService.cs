namespace MahjongScoreBoard.Api.Services;

public interface IScoringService
{
    int CalculateBasePoints(int han, int fu);
    ScoringResult CalculatePayment(int han, int fu, bool isDealer, bool isTsumo, int playerCount, bool kiriage = false, int yakumanMultiplier = 1);
}

public record ScoringResult(
    int TotalPoints,
    int? RonPayment,
    int? TsumoPaymentFromDealer,
    int? TsumoPaymentFromNonDealer
);
