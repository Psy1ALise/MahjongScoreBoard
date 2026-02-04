# Riichi Mahjong Scorekeeper

A Web API for tracking Japanese Riichi Mahjong game scores, built with .NET 10 and C#.

## Overview

This API implements the complete Han/Fu scoring system for Japanese Riichi Mahjong, including:

- Full scoring calculation with lookup tables for accuracy
- Wind rotation and dealer repeat (renchan) mechanics
- Honba bonus payment tracking
- Riichi declaration and kyoutaku (pot) settlement
- Exhaustive and abortive draw handling with noten penalties
- Multi-ron (double/triple ron) with atamahane ordering
- Pao (responsibility) rule for liable player payments
- Configurable rule variations (kiriage, kazoeyakuman, bankruptcy, etc.)
- Uma/Oka final score adjustments

## Which Game and Why

I chose **Riichi Mahjong** because it's a game I genuinely love and play regularly. I watch professional tournaments frequently and have always been fascinated by how the scoring is presented during broadcasts.

Interestingly, some professional mahjong groups in Japan use **MS Excel Macros** to build their scoring systems and broadcast UI. This inspired me to explore whether I could recreate something similar using modern software development practices.

Since frontend development isn't my primary expertise, I focused on building a clean, well-structured **Web API** that could serve as the backend for any future UI implementation.

## How to Run

### Prerequisites
- .NET 10 SDK

### Build and Run
```bash
# Build the solution
dotnet build

# Run the API
dotnet run --project src/MahjongScoreBoard.Api

# Run with hot reload for development
dotnet watch --project src/MahjongScoreBoard.Api

# Clean build artifacts
dotnet clean
```

The API runs on:
- `http://localhost:5053` (HTTP)
- `https://localhost:7166` (HTTPS)

Swagger UI available at `/swagger`, OpenAPI spec at `/openapi/v1.json`.

### Run Tests
```bash
# Run all tests
dotnet test

# Run a single test class
dotnet test --filter ClassName=ScoringServiceTests

# Run a single test by name
dotnet test --filter FullyQualifiedName~MethodName
```

Tests use xUnit and are located in `tests/MahjongScoreBoard.Tests/`.

### Postman Collections
Two Postman collections are included for testing:

1. **Main API workflows**: `postman/MahjongScorekeeper.postman_collection.json`
   - Import into Postman
   - Run the "Full Game Flow" folder for an end-to-end test
   - Collection auto-saves game and player IDs for subsequent requests

2. **Pao flow tests**: `postman/PaoFlowTests.postman_collection.json`
   - Tests for responsibility (pao) rule scenarios

## API Endpoints

### Game Management

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/game` | Create a new game with 4 players |
| GET | `/api/game` | List all games |
| GET | `/api/game/{id}` | Get game by ID |
| DELETE | `/api/game/{id}` | Delete a game |
| POST | `/api/game/{id}/end` | End game and calculate final scores |

### Gameplay

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/game/{id}/round` | Record a winning hand (ron or tsumo) |
| POST | `/api/game/{id}/draw` | Record a draw (exhaustive or abortive) |
| POST | `/api/game/{id}/riichi` | Declare riichi for a player |
| GET | `/api/game/{id}/history` | Get complete round-by-round history |

### Player Lookups

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/game/player/{name}` | Find all games by player name |
| GET | `/api/game/active/{name}` | Find active (in-progress) game for player |
| GET | `/api/game/search?players=A,B,C,D` | Find game by exact 4 player names |

### Reference Data

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/yaku` | List all yaku with han values and descriptions |

## Key Design Decisions

### Lookup Tables for Scoring
The traditional Han/Fu scoring calculation involves complex formulas with rounding rules that are error-prone. Since the actual score combinations are finite and well-defined, I chose to use **dictionary lookup tables** keyed by `(han, fu)` tuples instead of runtime calculations. This approach is:
- More reliable (no rounding errors)
- Easier to verify against official scoring charts
- Faster at runtime

Separate tables exist for dealer/non-dealer and ron/tsumo combinations.

### Three-Layer Architecture
```
Controllers (API) → Services (Business Logic) → Models (Domain)
```
- **ScoringService** handles point calculations independently
- **GameService** manages game state and orchestrates scoring
- **Models** are encapsulated with behavior (e.g., `Player.AddPoints()`, `Game.AdvanceRound()`)

### 4-Player Standard
While the requirements suggested 2-6 players, I intentionally restricted to exactly 4 players because:
- Riichi Mahjong is standardised as a 4-player game
- The scoring system (dealer pays 2x, non-dealers pay 1x) assumes 4 players
- This reflects how the game is actually played professionally

### Player Name Lookups
Added endpoints to find games by player names since GUIDs aren't practical for a game night scenario.

## Scoring System

### Han/Fu to Points

The API uses lookup tables for non-limit hands. For limit hands:

| Han | Limit Name | Non-dealer Ron | Dealer Ron |
|-----|------------|----------------|------------|
| 5 | Mangan | 8,000 | 12,000 |
| 6-7 | Haneman | 12,000 | 18,000 |
| 8-10 | Baiman | 16,000 | 24,000 |
| 11-12 | Sanbaiman | 24,000 | 36,000 |
| 13+ | Yakuman | 32,000 | 48,000 |

Tsumo payments split between players (dealer pays 2x, non-dealers pay 1x).

### Honba Bonus
- Ron: loser pays extra 300 × honba
- Tsumo: each non-winner pays extra 100 × honba

### Riichi and Kyoutaku
- Declaring riichi increments player's riichi stick count and game kyoutaku
- Points (1000 per stick) are deducted when a hand is won
- First winner collects all kyoutaku
- On draws, riichi sticks persist to next hand

## Configurable Rules

Pass these as fields in the create game request:

| Rule | Default | Effect |
|------|---------|--------|
| `kiriage` | false | 4han30fu / 3han60fu rounds up to mangan |
| `atamahane` | false | Only first winner gets points in multi-ron |
| `kazoeyakuman` | true | 13+ han counts as yakuman (false caps at sanbaiman) |
| `doubleYakuman` | false | Special yakuman variants score double |
| `compositeYakuman` | true | Multiple yakuman stack together |
| `bankruptcy` | true | Game ends when a player goes below 0 |
| `abortiveDraws` | true | Allow non-exhaustive draws (FourKan, etc.) |
| `pao` | true | Liable player rule for certain yakuman |
| `targetScore` | 30000 | Target score for uma calculation |
| `uma` | [30,10,-10,-30] | Uma adjustments for 1st-4th place |

## Project Structure
```
MahjongScoreBoard/
├── src/MahjongScoreBoard.Api/
│   ├── Controllers/        # API endpoints (GameController, YakuController)
│   ├── Models/             # Domain entities (Game, Player, Round, HandResult, etc.)
│   ├── Services/           # Business logic (ScoringService, GameService)
│   └── Requests/           # API request DTOs
├── tests/MahjongScoreBoard.Tests/
│   ├── ScoringServiceTests.cs
│   ├── GameServiceTests.cs
│   └── ApiIntegrationTests.cs
├── postman/
│   ├── MahjongScorekeeper.postman_collection.json
│   └── PaoFlowTests.postman_collection.json
└── docs/
    └── ENDPOINT_DATA_FLOW.md   # Detailed endpoint data flow documentation
```

## What I'd Improve With More Time

### Data Persistence
- Currently in-memory only (data lost on restart)
- Would add SQLite or PostgreSQL for game history
- Player statistics across sessions
- Leaderboards and win rate tracking

### Broadcasting UI
- Real-time score display for streaming
- Visual representation of seat positions and winds
- Hand history timeline with point movements
- Integration with OBS or similar streaming software
- WebSocket support for live updates

### Additional Features
- 3-player (sanma) variant support
- Tournament mode with bracket management
- Chombo (penalty) handling
- Red dora configuration
- Session-based scoring (multiple hanchan)

## Example Usage

### Create a Game
```bash
curl -X POST http://localhost:5053/api/game \
  -H "Content-Type: application/json" \
  -d '{
    "playerNames": ["Alice", "Bob", "Charlie", "Diana"],
    "startingScore": 25000,
    "kiriage": false,
    "bankruptcy": true
  }'
```

### Record a Ron
```bash
curl -X POST http://localhost:5053/api/game/{gameId}/round \
  -H "Content-Type: application/json" \
  -d '{
    "winners": [{
      "winnerId": "{winnerId}",
      "han": 3,
      "fu": 30,
      "yaku": ["Riichi", "Tanyao", "Pinfu"]
    }],
    "loserId": "{loserId}"
  }'
```

### Record a Tsumo
```bash
curl -X POST http://localhost:5053/api/game/{gameId}/round \
  -H "Content-Type: application/json" \
  -d '{
    "winners": [{
      "winnerId": "{winnerId}",
      "han": 2,
      "fu": 30,
      "yaku": ["MenzenTsumo", "Tanyao"]
    }]
  }'
```

### Declare Riichi
```bash
curl -X POST http://localhost:5053/api/game/{gameId}/riichi \
  -H "Content-Type: application/json" \
  -d '{"playerId": "{playerId}"}'
```

### Record an Exhaustive Draw
```bash
curl -X POST http://localhost:5053/api/game/{gameId}/draw \
  -H "Content-Type: application/json" \
  -d '{
    "drawType": "Exhaustive",
    "tenpaiPlayerIds": ["{player1Id}", "{player2Id}"]
  }'
```
