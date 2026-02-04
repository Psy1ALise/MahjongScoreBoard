namespace MahjongScoreBoard.Api.Services;

public class ScoringService : IScoringService
{
    // Limit hand thresholds
    private const int Mangan = 5;
    private const int Haneman = 6;
    private const int Baiman = 8;
    private const int Sanbaiman = 11;
    private const int Yakuman = 13;

    // Non-dealer Ron lookup table: (han, fu) -> points
    private static readonly Dictionary<(int han, int fu), int> NonDealerRonTable = new()
    {
        // 1 han
        [(1, 30)] = 1000, [(1, 40)] = 1300, [(1, 50)] = 1600, [(1, 60)] = 2000,
        [(1, 70)] = 2300, [(1, 80)] = 2600, [(1, 90)] = 2900, [(1, 100)] = 3200, [(1, 110)] = 3600,
        // 2 han
        [(2, 20)] = 1300, [(2, 25)] = 1600, [(2, 30)] = 2000, [(2, 40)] = 2600, [(2, 50)] = 3200,
        [(2, 60)] = 3900, [(2, 70)] = 4500, [(2, 80)] = 5200, [(2, 90)] = 5800, [(2, 100)] = 6400, [(2, 110)] = 7100,
        // 3 han (70fu+ = mangan = mangan)
        [(3, 20)] = 2600, [(3, 25)] = 3200, [(3, 30)] = 3900, [(3, 40)] = 5200, [(3, 50)] = 6400,
        [(3, 60)] = 7700,
        // 4 han (40fu+ = mangan = mangan)
        [(4, 20)] = 5200, [(4, 25)] = 6400, [(4, 30)] = 7700,
    };

    // Dealer Ron lookup table: (han, fu) -> points
    private static readonly Dictionary<(int han, int fu), int> DealerRonTable = new()
    {
        // 1 han
        [(1, 30)] = 1500, [(1, 40)] = 2000, [(1, 50)] = 2400, [(1, 60)] = 2900,
        [(1, 70)] = 3400, [(1, 80)] = 3900, [(1, 90)] = 4400, [(1, 100)] = 4800, [(1, 110)] = 5300,
        // 2 han
        [(2, 20)] = 2000, [(2, 25)] = 2400, [(2, 30)] = 2900, [(2, 40)] = 3900, [(2, 50)] = 4800,
        [(2, 60)] = 5800, [(2, 70)] = 6800, [(2, 80)] = 7700, [(2, 90)] = 8700, [(2, 100)] = 9600, [(2, 110)] = 10600,
        // 3 han (70fu+ = mangan = mangan)
        [(3, 20)] = 3900, [(3, 25)] = 4800, [(3, 30)] = 5800, [(3, 40)] = 7700, [(3, 50)] = 9600,
        [(3, 60)] = 11600,
        // 4 han (40fu+ = mangan = mangan)
        [(4, 20)] = 7700, [(4, 25)] = 9600, [(4, 30)] = 11600,
    };

    // Non-dealer Tsumo lookup table: (han, fu) -> (dealerPays, nonDealerPays)
    private static readonly Dictionary<(int han, int fu), (int dealer, int nonDealer)> NonDealerTsumoTable = new()
    {
        // 1 han
        [(1, 30)] = (500, 300), [(1, 40)] = (700, 400), [(1, 50)] = (800, 400), [(1, 60)] = (1000, 500),
        [(1, 70)] = (1200, 600), [(1, 80)] = (1300, 700), [(1, 90)] = (1500, 800), [(1, 100)] = (1600, 800), [(1, 110)] = (1800, 900),
        // 2 han
        [(2, 20)] = (700, 400), [(2, 25)] = (800, 400), [(2, 30)] = (1000, 500), [(2, 40)] = (1300, 700), [(2, 50)] = (1600, 800),
        [(2, 60)] = (2000, 1000), [(2, 70)] = (2300, 1200), [(2, 80)] = (2600, 1300), [(2, 90)] = (2900, 1500), [(2, 100)] = (3200, 1600), [(2, 110)] = (3600, 1800),
        // 3 han (70fu+ = mangan = mangan)
        [(3, 20)] = (1300, 700), [(3, 25)] = (1600, 800), [(3, 30)] = (2000, 1000), [(3, 40)] = (2600, 1300), [(3, 50)] = (3200, 1600),
        [(3, 60)] = (3900, 2000),
        // 4 han (40fu+ = mangan = mangan)
        [(4, 20)] = (2600, 1300), [(4, 25)] = (3200, 1600), [(4, 30)] = (3900, 2000),
    };

    // Dealer Tsumo lookup table: (han, fu) -> eachPays
    private static readonly Dictionary<(int han, int fu), int> DealerTsumoTable = new()
    {
        // 1 han
        [(1, 30)] = 500, [(1, 40)] = 700, [(1, 50)] = 800, [(1, 60)] = 1000,
        [(1, 70)] = 1200, [(1, 80)] = 1300, [(1, 90)] = 1500, [(1, 100)] = 1600, [(1, 110)] = 1800,
        // 2 han
        [(2, 20)] = 700, [(2, 25)] = 800, [(2, 30)] = 1000, [(2, 40)] = 1300, [(2, 50)] = 1600,
        [(2, 60)] = 2000, [(2, 70)] = 2300, [(2, 80)] = 2600, [(2, 90)] = 2900, [(2, 100)] = 3200, [(2, 110)] = 3600,
        // 3 han (70fu+ = mangan = mangan)
        [(3, 20)] = 1300, [(3, 25)] = 1600, [(3, 30)] = 2000, [(3, 40)] = 2600, [(3, 50)] = 3200,
        [(3, 60)] = 3900,
        // 4 han (40fu+ = mangan = mangan)
        [(4, 20)] = 2600, [(4, 25)] = 3200, [(4, 30)] = 3900,
    };

    public ScoringResult CalculatePayment(int han, int fu, bool isDealer, bool isTsumo, int playerCount, bool kiriage = false, int yakumanMultiplier = 1)
    {
        // Kiriage: 4han30fu and 3han60fu round up to mangan
        if (kiriage && ((han == 4 && fu == 30) || (han == 3 && fu == 60)))
        {
            han = Mangan;
        }
        // 3 han 70fu+ or 4 han 40fu+ is mangan by base points
        else if ((han == 3 && fu >= 70) || (han == 4 && fu >= 40))
        {
            han = Mangan;
        }

        if (isTsumo)
        {
            return CalculateTsumoPayment(han, fu, isDealer, playerCount, yakumanMultiplier);
        }
        else
        {
            return CalculateRonPayment(han, fu, isDealer, yakumanMultiplier);
        }
    }

    private ScoringResult CalculateRonPayment(int han, int fu, bool isDealer, int yakumanMultiplier = 1)
    {
        int ronPayment = GetRonPoints(han, fu, isDealer, yakumanMultiplier);

        return new ScoringResult(
            TotalPoints: ronPayment,
            RonPayment: ronPayment,
            TsumoPaymentFromDealer: null,
            TsumoPaymentFromNonDealer: null
        );
    }

    private ScoringResult CalculateTsumoPayment(int han, int fu, bool isDealer, int playerCount, int yakumanMultiplier = 1)
    {
        if (isDealer)
        {
            int eachPays = GetDealerTsumoPayment(han, fu, yakumanMultiplier);
            int total = eachPays * (playerCount - 1);

            return new ScoringResult(
                TotalPoints: total,
                RonPayment: null,
                TsumoPaymentFromDealer: null,
                TsumoPaymentFromNonDealer: eachPays
            );
        }
        else
        {
            var (dealerPays, nonDealerPays) = GetNonDealerTsumoPayment(han, fu, yakumanMultiplier);
            int total = dealerPays + (nonDealerPays * (playerCount - 2));

            return new ScoringResult(
                TotalPoints: total,
                RonPayment: null,
                TsumoPaymentFromDealer: dealerPays,
                TsumoPaymentFromNonDealer: nonDealerPays
            );
        }
    }

    private int GetRonPoints(int han, int fu, bool isDealer, int yakumanMultiplier = 1)
    {
        // Check for limit hands first
        if (han >= Yakuman)
            return (isDealer ? 48000 : 32000) * yakumanMultiplier;
        if (han >= Sanbaiman)
            return isDealer ? 36000 : 24000;
        if (han >= Baiman)
            return isDealer ? 24000 : 16000;
        if (han >= Haneman)
            return isDealer ? 18000 : 12000;
        if (han >= Mangan)
            return isDealer ? 12000 : 8000;

        // Lookup from table
        var table = isDealer ? DealerRonTable : NonDealerRonTable;
        if (table.TryGetValue((han, fu), out int points))
        {
            return points;
        }

        throw new ArgumentException($"No scoring entry for {han} han {fu} fu");
    }

    private int GetDealerTsumoPayment(int han, int fu, int yakumanMultiplier = 1)
    {
        // Check for limit hands first
        if (han >= Yakuman)
            return 16000 * yakumanMultiplier;
        if (han >= Sanbaiman)
            return 12000;
        if (han >= Baiman)
            return 8000;
        if (han >= Haneman)
            return 6000;
        if (han >= Mangan)
            return 4000;

        // Lookup from table
        if (DealerTsumoTable.TryGetValue((han, fu), out int payment))
        {
            return payment;
        }

        throw new ArgumentException($"No scoring entry for dealer tsumo {han} han {fu} fu");
    }

    private (int dealer, int nonDealer) GetNonDealerTsumoPayment(int han, int fu, int yakumanMultiplier = 1)
    {
        // Check for limit hands first
        if (han >= Yakuman)
            return (16000 * yakumanMultiplier, 8000 * yakumanMultiplier);
        if (han >= Sanbaiman)
            return (12000, 6000);
        if (han >= Baiman)
            return (8000, 4000);
        if (han >= Haneman)
            return (6000, 3000);
        if (han >= Mangan)
            return (4000, 2000);

        // Lookup from table
        if (NonDealerTsumoTable.TryGetValue((han, fu), out var payment))
        {
            return payment;
        }

        throw new ArgumentException($"No scoring entry for non-dealer tsumo {han} han {fu} fu");
    }

    public int CalculateBasePoints(int han, int fu)
    {
        if (han >= Yakuman) return 8000;
        if (han >= Sanbaiman) return 6000;
        if (han >= Baiman) return 4000;
        if (han >= Haneman) return 3000;
        if (han >= Mangan) return 2000;

        int basePoints = fu * (int)Math.Pow(2, han + 2);
        return Math.Min(basePoints, 2000);
    }
}
