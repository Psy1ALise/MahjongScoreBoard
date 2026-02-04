using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using Xunit.Abstractions;

namespace MahjongScoreBoard.Tests;

public class ApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ApiIntegrationTests(WebApplicationFactory<Program> factory, ITestOutputHelper output)
    {
        _client = factory.CreateClient();
        _output = output;
    }

    [Fact]
    public async Task GetYaku_ReturnsAllYaku()
    {
        _output.WriteLine("=== GET /api/yaku Test ===");

        var response = await _client.GetAsync("/api/yaku");
        var content = await response.Content.ReadAsStringAsync();

        _output.WriteLine($"Status: {response.StatusCode}");
        _output.WriteLine($"Response: {content}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var yakuList = JsonSerializer.Deserialize<List<YakuDto>>(content, _jsonOptions);
        Assert.NotNull(yakuList);
        Assert.True(yakuList.Count > 0);

        _output.WriteLine($"Found {yakuList.Count} yaku definitions");
    }

    [Fact]
    public async Task CreateGame_ReturnsCreatedGame()
    {
        _output.WriteLine("=== POST /api/game Test ===");

        var request = new { playerNames = new[] { "Alice", "Bob", "Charlie", "Dave" }, startingScore = 25000 };

        var response = await _client.PostAsJsonAsync("/api/game", request);
        var content = await response.Content.ReadAsStringAsync();

        _output.WriteLine($"Status: {response.StatusCode}");
        _output.WriteLine($"Response: {content}");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var game = JsonSerializer.Deserialize<GameDto>(content, _jsonOptions);
        Assert.NotNull(game);
        Assert.Equal(4, game.Players?.Count);
        Assert.Equal("InProgress", game.Status);

        _output.WriteLine($"Game created with ID: {game.Id}");
    }

    [Fact]
    public async Task CreateGame_InvalidPlayerCount_ReturnsBadRequest()
    {
        _output.WriteLine("=== POST /api/game (Invalid) Test ===");

        var request = new { playerNames = new[] { "Solo" }, startingScore = 25000 };

        var response = await _client.PostAsJsonAsync("/api/game", request);
        var content = await response.Content.ReadAsStringAsync();

        _output.WriteLine($"Status: {response.StatusCode}");
        _output.WriteLine($"Response: {content}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetGame_ReturnsGame()
    {
        _output.WriteLine("=== GET /api/game/{id} Test ===");

        // Create a game first
        var createRequest = new { playerNames = new[] { "Alice", "Bob", "Charlie", "Dave" } };
        var createResponse = await _client.PostAsJsonAsync("/api/game", createRequest);
        var createContent = await createResponse.Content.ReadAsStringAsync();
        var createdGame = JsonSerializer.Deserialize<GameDto>(createContent, _jsonOptions);

        _output.WriteLine($"Created game: {createdGame?.Id}");

        // Get the game
        var response = await _client.GetAsync($"/api/game/{createdGame?.Id}");
        var content = await response.Content.ReadAsStringAsync();

        _output.WriteLine($"Status: {response.StatusCode}");
        _output.WriteLine($"Response: {content}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var game = JsonSerializer.Deserialize<GameDto>(content, _jsonOptions);
        Assert.NotNull(game);
        Assert.Equal(createdGame?.Id, game.Id);
    }

    [Fact]
    public async Task GetGame_NotFound_Returns404()
    {
        _output.WriteLine("=== GET /api/game/{id} (Not Found) Test ===");

        var response = await _client.GetAsync($"/api/game/{Guid.NewGuid()}");

        _output.WriteLine($"Status: {response.StatusCode}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RecordRound_UpdatesScores()
    {
        _output.WriteLine("=== POST /api/game/{id}/round Test ===");

        // Create a game
        var createRequest = new { playerNames = new[] { "Alice", "Bob", "Charlie", "Dave" } };
        var createResponse = await _client.PostAsJsonAsync("/api/game", createRequest);
        var createContent = await createResponse.Content.ReadAsStringAsync();
        var game = JsonSerializer.Deserialize<GameDto>(createContent, _jsonOptions);

        _output.WriteLine($"Game ID: {game?.Id}");
        _output.WriteLine($"Players: {string.Join(", ", game?.Players?.Select(p => $"{p.Name}({p.Id})") ?? Array.Empty<string>())}");

        var winnerId = game?.Players?[1].Id;
        var loserId = game?.Players?[2].Id;

        // Record a round (Ron)
        var roundRequest = new
        {
            winners = new[]
            {
                new { winnerId = winnerId, han = 3, fu = 30, yaku = new[] { "Riichi", "Tanyao", "Pinfu" } }
            },
            loserId = loserId
        };

        var response = await _client.PostAsJsonAsync($"/api/game/{game?.Id}/round", roundRequest);
        var content = await response.Content.ReadAsStringAsync();

        _output.WriteLine($"Status: {response.StatusCode}");
        _output.WriteLine($"Response: {content}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = JsonSerializer.Deserialize<RoundConclusionDto>(content, _jsonOptions);
        Assert.NotNull(result);
        Assert.Single(result.Results!);
        Assert.Equal(3900, result.Results![0].PointsWon);

        _output.WriteLine($"Points won: {result.Results![0].PointsWon}");

        // Verify scores updated
        var getResponse = await _client.GetAsync($"/api/game/{game?.Id}");
        var getContent = await getResponse.Content.ReadAsStringAsync();
        var updatedGame = JsonSerializer.Deserialize<GameDto>(getContent, _jsonOptions);

        _output.WriteLine("Updated scores:");
        foreach (var p in updatedGame?.Players ?? new List<PlayerDto>())
            _output.WriteLine($"  {p.Name}: {p.Score}");

        var winner = updatedGame?.Players?.FirstOrDefault(p => p.Id == winnerId);
        var loser = updatedGame?.Players?.FirstOrDefault(p => p.Id == loserId);

        Assert.Equal(25000 + 3900, winner?.Score);
        Assert.Equal(25000 - 3900, loser?.Score);
    }

    [Fact]
    public async Task RecordRound_Tsumo_DistributesPayments()
    {
        _output.WriteLine("=== POST /api/game/{id}/round (Tsumo) Test ===");

        // Create a game
        var createRequest = new { playerNames = new[] { "Alice", "Bob", "Charlie", "Dave" } };
        var createResponse = await _client.PostAsJsonAsync("/api/game", createRequest);
        var game = JsonSerializer.Deserialize<GameDto>(await createResponse.Content.ReadAsStringAsync(), _jsonOptions);

        var winnerId = game?.Players?[1].Id; // Non-dealer

        // Record a tsumo (no loserId)
        var roundRequest = new
        {
            winners = new[]
            {
                new { winnerId = winnerId, han = 3, fu = 30, yaku = new[] { "Riichi", "Tsumo", "Tanyao" } }
            }
        };

        var response = await _client.PostAsJsonAsync($"/api/game/{game?.Id}/round", roundRequest);
        var content = await response.Content.ReadAsStringAsync();

        _output.WriteLine($"Status: {response.StatusCode}");
        _output.WriteLine($"Response: {content}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = JsonSerializer.Deserialize<RoundConclusionDto>(content, _jsonOptions);
        Assert.True(result?.Results?[0].IsTsumo);
        Assert.Equal(4000, result?.Results?[0].PointsWon);

        _output.WriteLine($"Tsumo points: {result?.Results?[0].PointsWon}");
    }

    [Fact]
    public async Task GetHistory_ReturnsRounds()
    {
        _output.WriteLine("=== GET /api/game/{id}/history Test ===");

        // Create and play a game
        var createRequest = new { playerNames = new[] { "Alice", "Bob", "Charlie", "Dave" } };
        var createResponse = await _client.PostAsJsonAsync("/api/game", createRequest);
        var game = JsonSerializer.Deserialize<GameDto>(await createResponse.Content.ReadAsStringAsync(), _jsonOptions);

        // Record a round
        var roundRequest = new
        {
            winners = new[]
            {
                new { winnerId = game?.Players?[1].Id, han = 2, fu = 30, yaku = new[] { "Tanyao", "Pinfu" } }
            },
            loserId = game?.Players?[2].Id
        };
        await _client.PostAsJsonAsync($"/api/game/{game?.Id}/round", roundRequest);

        // Get history
        var response = await _client.GetAsync($"/api/game/{game?.Id}/history");
        var content = await response.Content.ReadAsStringAsync();

        _output.WriteLine($"Status: {response.StatusCode}");
        _output.WriteLine($"Response: {content}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task EndGame_SetsWinner()
    {
        _output.WriteLine("=== POST /api/game/{id}/end Test ===");

        // Create a game
        var createRequest = new { playerNames = new[] { "Alice", "Bob", "Charlie", "Dave" } };
        var createResponse = await _client.PostAsJsonAsync("/api/game", createRequest);
        var game = JsonSerializer.Deserialize<GameDto>(await createResponse.Content.ReadAsStringAsync(), _jsonOptions);

        // Record a round so someone has higher score
        var roundRequest = new
        {
            winners = new[]
            {
                new { winnerId = game?.Players?[1].Id, han = 5, fu = 30, yaku = new[] { "Honitsu", "Yakuhai", "Tanyao" } }
            },
            loserId = game?.Players?[2].Id
        };
        await _client.PostAsJsonAsync($"/api/game/{game?.Id}/round", roundRequest);

        // End game
        var response = await _client.PostAsync($"/api/game/{game?.Id}/end", null);
        var content = await response.Content.ReadAsStringAsync();

        _output.WriteLine($"Status: {response.StatusCode}");
        _output.WriteLine($"Response: {content}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var endedGame = JsonSerializer.Deserialize<GameDto>(content, _jsonOptions);
        Assert.Equal("Completed", endedGame?.Status);
        Assert.NotNull(endedGame?.Ranking);
        Assert.Equal(4, endedGame?.Ranking?.Count);

        _output.WriteLine($"1st place: {endedGame?.Ranking?[0].Player?.Name} with {endedGame?.Ranking?[0].Player?.Score} points");
    }

    [Fact]
    public async Task DeleteGame_RemovesGame()
    {
        _output.WriteLine("=== DELETE /api/game/{id} Test ===");

        // Create a game
        var createRequest = new { playerNames = new[] { "Alice", "Bob", "Charlie", "Dave" } };
        var createResponse = await _client.PostAsJsonAsync("/api/game", createRequest);
        var game = JsonSerializer.Deserialize<GameDto>(await createResponse.Content.ReadAsStringAsync(), _jsonOptions);

        _output.WriteLine($"Created game: {game?.Id}");

        // Delete it
        var deleteResponse = await _client.DeleteAsync($"/api/game/{game?.Id}");
        _output.WriteLine($"Delete status: {deleteResponse.StatusCode}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Verify it's gone
        var getResponse = await _client.GetAsync($"/api/game/{game?.Id}");
        _output.WriteLine($"Get after delete status: {getResponse.StatusCode}");

        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task FullGameFlow_EndToEnd()
    {
        _output.WriteLine("=== Full Game Flow Test ===");
        _output.WriteLine("");

        // 1. Create game
        _output.WriteLine("1. Creating game...");
        var createRequest = new { playerNames = new[] { "Alice", "Bob", "Charlie", "Dave" } };
        var createResponse = await _client.PostAsJsonAsync("/api/game", createRequest);
        var game = JsonSerializer.Deserialize<GameDto>(await createResponse.Content.ReadAsStringAsync(), _jsonOptions);
        _output.WriteLine($"   Game created: {game?.Id}");
        _output.WriteLine($"   Players: {string.Join(", ", game?.Players?.Select(p => p.Name) ?? Array.Empty<string>())}");

        // 2. Record several rounds
        _output.WriteLine("");
        _output.WriteLine("2. Playing rounds...");

        // Round 1: Bob wins from Charlie (Ron, 3 han)
        var round1 = new
        {
            winners = new[] { new { winnerId = game?.Players?[1].Id, han = 3, fu = 30, yaku = new[] { "Riichi", "Tanyao", "Pinfu" } } },
            loserId = game?.Players?[2].Id
        };
        await _client.PostAsJsonAsync($"/api/game/{game?.Id}/round", round1);
        _output.WriteLine("   Round 1: Bob ron from Charlie (3 han) - 3900 pts");

        // Round 2: Alice wins tsumo (2 han)
        var round2 = new
        {
            winners = new[] { new { winnerId = game?.Players?[0].Id, han = 2, fu = 30, yaku = new[] { "Tanyao", "Tsumo" } } }
        };
        await _client.PostAsJsonAsync($"/api/game/{game?.Id}/round", round2);
        _output.WriteLine("   Round 2: Alice tsumo (2 han) - 2000 pts");

        // Round 3: Dave wins mangan from Bob
        var round3 = new
        {
            winners = new[] { new { winnerId = game?.Players?[3].Id, han = 5, fu = 30, yaku = new[] { "Honitsu", "Yakuhai", "Tanyao" } } },
            loserId = game?.Players?[1].Id
        };
        await _client.PostAsJsonAsync($"/api/game/{game?.Id}/round", round3);
        _output.WriteLine("   Round 3: Dave ron from Bob (Mangan) - 8000 pts");

        // 3. Check current scores
        _output.WriteLine("");
        _output.WriteLine("3. Current standings:");
        var currentGame = JsonSerializer.Deserialize<GameDto>(await (await _client.GetAsync($"/api/game/{game?.Id}")).Content.ReadAsStringAsync(), _jsonOptions);
        foreach (var p in currentGame?.Players?.OrderByDescending(p => p.Score).ToList() ?? new List<PlayerDto>())
            _output.WriteLine($"   {p.Name}: {p.Score}");

        // 4. End game
        _output.WriteLine("");
        _output.WriteLine("4. Ending game...");
        var endResponse = await _client.PostAsync($"/api/game/{game?.Id}/end", null);
        var endedGame = JsonSerializer.Deserialize<GameDto>(await endResponse.Content.ReadAsStringAsync(), _jsonOptions);

        _output.WriteLine($"   Status: {endedGame?.Status}");
        _output.WriteLine($"   1st: {endedGame?.Ranking?[0].Player?.Name} ({endedGame?.Ranking?[0].Player?.Score} pts)");

        // 5. Get history
        _output.WriteLine("");
        _output.WriteLine("5. Game history:");
        var historyResponse = await _client.GetAsync($"/api/game/{game?.Id}/history");
        var historyContent = await historyResponse.Content.ReadAsStringAsync();
        _output.WriteLine($"   {historyContent}");

        Assert.Equal("Completed", endedGame?.Status);
        Assert.NotNull(endedGame?.Ranking);
    }
}

// DTOs for deserialization
public class GameDto
{
    public Guid Id { get; set; }
    public List<PlayerDto>? Players { get; set; }
    public int CurrentRound { get; set; }
    public string? RoundWind { get; set; }
    public int DealerIndex { get; set; }
    public string? Status { get; set; }
    public List<RankingEntryDto>? Ranking { get; set; }
}

public class RankingEntryDto
{
    public int Place { get; set; }
    public PlayerDto? Player { get; set; }
}

public class PlayerDto
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public int Score { get; set; }
    public string? SeatWind { get; set; }
}

public class RoundConclusionDto
{
    public List<HandResultDto>? Results { get; set; }
    public string? GameStatus { get; set; }
}

public class HandResultDto
{
    public Guid Id { get; set; }
    public Guid WinnerId { get; set; }
    public Guid? LoserId { get; set; }
    public int Han { get; set; }
    public int Fu { get; set; }
    public int PointsWon { get; set; }
    public List<string>? Yaku { get; set; }
    public bool IsTsumo { get; set; }
    public bool ReceivedKyoutaku { get; set; }
}

public class YakuDto
{
    public string? Name { get; set; }
    public int HanValue { get; set; }
    public string? Description { get; set; }
}
