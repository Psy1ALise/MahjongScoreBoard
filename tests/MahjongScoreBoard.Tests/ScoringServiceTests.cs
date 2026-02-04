using MahjongScoreBoard.Api.Services;
using Xunit;
using Xunit.Abstractions;

namespace MahjongScoreBoard.Tests;

public class ScoringServiceTests
{
    private readonly IScoringService _scoringService;
    private readonly ITestOutputHelper _output;

    public ScoringServiceTests(ITestOutputHelper output)
    {
        _scoringService = new ScoringService();
        _output = output;
    }

    [Theory]
    [InlineData(1, 30, 240)]   // 30 * 2^3 = 240
    [InlineData(2, 30, 480)]   // 30 * 2^4 = 480
    [InlineData(3, 30, 960)]   // 30 * 2^5 = 960
    [InlineData(4, 30, 1920)]  // 30 * 2^6 = 1920
    [InlineData(4, 40, 2000)]  // 40 * 2^6 = 2560, capped at 2000
    [InlineData(5, 30, 2000)]  // Mangan
    [InlineData(6, 30, 3000)]  // Haneman
    [InlineData(8, 30, 4000)]  // Baiman
    [InlineData(11, 30, 6000)] // Sanbaiman
    [InlineData(13, 30, 8000)] // Yakuman
    public void CalculateBasePoints_ReturnsCorrectValue(int han, int fu, int expected)
    {
        _output.WriteLine($"Testing: {han} han, {fu} fu");

        var result = _scoringService.CalculateBasePoints(han, fu);

        _output.WriteLine($"Result: {result} base points (expected: {expected})");
        Assert.Equal(expected, result);
    }

    [Fact]
    public void CalculatePayment_NonDealerRon_ReturnsCorrectPayment()
    {
        _output.WriteLine("=== Non-Dealer Ron Test ===");

        // 3 han 30 fu = 960 base, non-dealer ron = 960 * 4 = 3840, rounded to 3900
        var result = _scoringService.CalculatePayment(han: 3, fu: 30, isDealer: false, isTsumo: false, playerCount: 4);

        _output.WriteLine($"3 han 30 fu, Non-dealer Ron");
        _output.WriteLine($"Total Points: {result.TotalPoints}");
        _output.WriteLine($"Ron Payment: {result.RonPayment}");

        Assert.Equal(3900, result.TotalPoints);
        Assert.Equal(3900, result.RonPayment);
        Assert.Null(result.TsumoPaymentFromDealer);
        Assert.Null(result.TsumoPaymentFromNonDealer);
    }

    [Fact]
    public void CalculatePayment_DealerRon_ReturnsCorrectPayment()
    {
        _output.WriteLine("=== Dealer Ron Test ===");

        // 3 han 30 fu = 960 base, dealer ron = 960 * 6 = 5760, rounded to 5800
        var result = _scoringService.CalculatePayment(han: 3, fu: 30, isDealer: true, isTsumo: false, playerCount: 4);

        _output.WriteLine($"3 han 30 fu, Dealer Ron");
        _output.WriteLine($"Total Points: {result.TotalPoints}");
        _output.WriteLine($"Ron Payment: {result.RonPayment}");

        Assert.Equal(5800, result.TotalPoints);
        Assert.Equal(5800, result.RonPayment);
    }

    [Fact]
    public void CalculatePayment_NonDealerTsumo_ReturnsCorrectPayment()
    {
        _output.WriteLine("=== Non-Dealer Tsumo Test ===");

        // 3 han 30 fu = 960 base
        // Non-dealer tsumo: dealer pays 960*2=1920->2000, others pay 960->1000
        // Total: 2000 + 1000 + 1000 = 4000
        var result = _scoringService.CalculatePayment(han: 3, fu: 30, isDealer: false, isTsumo: true, playerCount: 4);

        _output.WriteLine($"3 han 30 fu, Non-dealer Tsumo (4 players)");
        _output.WriteLine($"Total Points: {result.TotalPoints}");
        _output.WriteLine($"Payment from Dealer: {result.TsumoPaymentFromDealer}");
        _output.WriteLine($"Payment from Non-Dealer: {result.TsumoPaymentFromNonDealer}");

        Assert.Equal(4000, result.TotalPoints);
        Assert.Equal(2000, result.TsumoPaymentFromDealer);
        Assert.Equal(1000, result.TsumoPaymentFromNonDealer);
    }

    [Fact]
    public void CalculatePayment_DealerTsumo_ReturnsCorrectPayment()
    {
        _output.WriteLine("=== Dealer Tsumo Test ===");

        // 3 han 30 fu = 960 base
        // Dealer tsumo: each pays 960*2=1920->2000
        // Total: 2000 * 3 = 6000
        var result = _scoringService.CalculatePayment(han: 3, fu: 30, isDealer: true, isTsumo: true, playerCount: 4);

        _output.WriteLine($"3 han 30 fu, Dealer Tsumo (4 players)");
        _output.WriteLine($"Total Points: {result.TotalPoints}");
        _output.WriteLine($"Payment from each Non-Dealer: {result.TsumoPaymentFromNonDealer}");

        Assert.Equal(6000, result.TotalPoints);
        Assert.Null(result.TsumoPaymentFromDealer);
        Assert.Equal(2000, result.TsumoPaymentFromNonDealer);
    }

    [Fact]
    public void CalculatePayment_Mangan_ReturnsCorrectPayment()
    {
        _output.WriteLine("=== Mangan Test ===");

        // Mangan (5 han) = 2000 base
        // Non-dealer ron = 2000 * 4 = 8000
        var result = _scoringService.CalculatePayment(han: 5, fu: 30, isDealer: false, isTsumo: false, playerCount: 4);

        _output.WriteLine($"Mangan (5 han), Non-dealer Ron");
        _output.WriteLine($"Total Points: {result.TotalPoints}");

        Assert.Equal(8000, result.TotalPoints);
    }

    [Fact]
    public void CalculatePayment_Yakuman_ReturnsCorrectPayment()
    {
        _output.WriteLine("=== Yakuman Test ===");

        // Yakuman (13+ han) = 8000 base
        // Non-dealer ron = 8000 * 4 = 32000
        var result = _scoringService.CalculatePayment(han: 13, fu: 30, isDealer: false, isTsumo: false, playerCount: 4);

        _output.WriteLine($"Yakuman (13 han), Non-dealer Ron");
        _output.WriteLine($"Total Points: {result.TotalPoints}");

        Assert.Equal(32000, result.TotalPoints);
    }

    // === Kiriage Tests ===

    [Fact]
    public void CalculatePayment_Kiriage_4Han30Fu_ReturnsMangan()
    {
        var result = _scoringService.CalculatePayment(han: 4, fu: 30, isDealer: false, isTsumo: false, playerCount: 4, kiriage: true);
        Assert.Equal(8000, result.TotalPoints);
    }

    [Fact]
    public void CalculatePayment_Kiriage_3Han60Fu_ReturnsMangan()
    {
        var result = _scoringService.CalculatePayment(han: 3, fu: 60, isDealer: false, isTsumo: false, playerCount: 4, kiriage: true);
        Assert.Equal(8000, result.TotalPoints);
    }

    [Fact]
    public void CalculatePayment_NoKiriage_4Han30Fu_Returns7700()
    {
        var result = _scoringService.CalculatePayment(han: 4, fu: 30, isDealer: false, isTsumo: false, playerCount: 4, kiriage: false);
        Assert.Equal(7700, result.TotalPoints);
    }

    // === Yakuman Multiplier Tests ===

    [Theory]
    [InlineData(1, 32000)]   // single yakuman
    [InlineData(2, 64000)]   // double
    [InlineData(3, 96000)]   // triple
    public void CalculatePayment_YakumanMultiplier_NonDealerRon(int multiplier, int expected)
    {
        var result = _scoringService.CalculatePayment(han: 13, fu: 0, isDealer: false, isTsumo: false, playerCount: 4, yakumanMultiplier: multiplier);
        Assert.Equal(expected, result.TotalPoints);
    }

    [Theory]
    [InlineData(1, 48000)]
    [InlineData(2, 96000)]
    public void CalculatePayment_YakumanMultiplier_DealerRon(int multiplier, int expected)
    {
        var result = _scoringService.CalculatePayment(han: 13, fu: 0, isDealer: true, isTsumo: false, playerCount: 4, yakumanMultiplier: multiplier);
        Assert.Equal(expected, result.TotalPoints);
    }

    [Fact]
    public void CalculatePayment_YakumanMultiplier_NonDealerTsumo()
    {
        var result = _scoringService.CalculatePayment(han: 13, fu: 0, isDealer: false, isTsumo: true, playerCount: 4, yakumanMultiplier: 2);

        // 2x: dealer pays 32000, others pay 16000 each, total = 64000
        Assert.Equal(64000, result.TotalPoints);
        Assert.Equal(32000, result.TsumoPaymentFromDealer);
        Assert.Equal(16000, result.TsumoPaymentFromNonDealer);
    }

    [Fact]
    public void CalculatePayment_YakumanMultiplier_DealerTsumo()
    {
        var result = _scoringService.CalculatePayment(han: 13, fu: 0, isDealer: true, isTsumo: true, playerCount: 4, yakumanMultiplier: 2);

        // 2x: each non-dealer pays 32000, total = 96000
        Assert.Equal(96000, result.TotalPoints);
        Assert.Equal(32000, result.TsumoPaymentFromNonDealer);
    }

    // === Limit Hand Tests ===

    [Theory]
    [InlineData(6, 12000)]   // Haneman
    [InlineData(8, 16000)]   // Baiman
    [InlineData(11, 24000)]  // Sanbaiman
    public void CalculatePayment_LimitHands_NonDealerRon(int han, int expected)
    {
        var result = _scoringService.CalculatePayment(han: han, fu: 30, isDealer: false, isTsumo: false, playerCount: 4);
        Assert.Equal(expected, result.TotalPoints);
    }

    [Theory]
    [InlineData(6, 18000)]   // Haneman
    [InlineData(8, 24000)]   // Baiman
    [InlineData(11, 36000)]  // Sanbaiman
    public void CalculatePayment_LimitHands_DealerRon(int han, int expected)
    {
        var result = _scoringService.CalculatePayment(han: han, fu: 30, isDealer: true, isTsumo: false, playerCount: 4);
        Assert.Equal(expected, result.TotalPoints);
    }
}
