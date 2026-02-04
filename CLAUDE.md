# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build and Run Commands

```bash
# Build the solution
dotnet build

# Run the API (from project root)
dotnet run --project src/MahjongScoreBoard.Api

# Run with hot reload for development
dotnet watch --project src/MahjongScoreBoard.Api

# Clean build artifacts
dotnet clean

# Run all tests (xUnit)
dotnet test

# Run a single test class
dotnet test --filter ClassName=ScoringServiceTests

# Run a single test by name
dotnet test --filter FullyQualifiedName~MethodName
```

Tests are in `tests/MahjongScoreBoard.Tests/` using xUnit: `ScoringServiceTests.cs`, `GameServiceTests.cs`, `ApiIntegrationTests.cs`.

The API runs on `http://localhost:5053` (HTTP) or `https://localhost:7166` (HTTPS). Swagger UI at `/swagger`, OpenAPI spec at `/openapi/v1.json`.

## Architecture Overview

This is a **Riichi Mahjong Scorekeeper** Web API implementing Japanese Mahjong's Han/Fu scoring system.

### Three-Layer Architecture

```
Controllers (API) → Services (Business Logic) → Models (Domain)
```

- **GameController** handles all game endpoints, **YakuController** provides yaku reference data
- **GameService** manages game state and orchestrates scoring; **ScoringService** contains Han/Fu calculation logic
- **Models** are encapsulated domain objects with behavior (e.g., `Player.AddPoints()`, `Game.AdvanceRound()`)

### Key Domain Concepts

- **Game** is the aggregate root containing Players and Rounds
- **HandResult** records a winning hand with winner, loser (null if tsumo), han, fu, and yaku list
- **ScoringService.CalculatePayment()** is the core scoring logic - handles mangan thresholds, dealer/non-dealer multipliers, and ron vs tsumo distribution

### Data Storage

In-memory only via `ConcurrentDictionary<Guid, Game>` in GameService. Data is lost on restart.

## Riichi Scoring Rules (in ScoringService)

The implementation uses **dictionary lookup tables** in `ScoringService` (keyed by `(han, fu)` tuples) for non-limit hands rather than runtime formula calculation. Separate tables exist for dealer/non-dealer ron and tsumo.

Conceptual formula: `fu × 2^(han+2)`, capped at 2000 (mangan threshold).

Limit hands:
- 5+ han = Mangan (base 2000)
- 6-7 han = Haneman (base 3000)
- 8-10 han = Baiman (base 4000)
- 11-12 han = Sanbaiman (base 6000)
- 13+ han = Yakuman (base 8000)

Payment multipliers:
- Ron: dealer ×6, non-dealer ×4
- Tsumo dealer wins: each pays base ×2
- Tsumo non-dealer wins: dealer pays base ×2, others pay base ×1

All payments rounded up to nearest 100.

## Wind Rotation & Renchan Rules (in Game model)

Standard Hanchan (East + South rounds):

Seat winds: Each player starts with a wind (East/South/West/North). East = dealer.

Wind rotation (on non-dealer win):
- DealerIndex advances by 1
- All players' seat winds rotate (East→South→West→North→East)
- Honba resets to 0
- When DealerIndex wraps to 0: RoundWind advances (East→South)
- When RoundWind would advance past South: game ends automatically (Hanchan complete)

Renchan (dealer repeat, on dealer win):
- Dealer stays, seat winds don't rotate
- Honba increments by 1

Honba bonus payments:
- Ron: loser pays extra 300 × honba
- Tsumo: each non-winner pays extra 100 × honba (total 300 × honba to winner)

## Kyōtaku / Riichi Deposit System

Endpoint: `POST /api/game/{id}/riichi` with PlayerId.

When a player declares riichi:
- Player's `RiichiSticks` increments by 1 (can stack across draws)
- Game `Kyoutaku` increments by 1 (riichi stick on the table)
- Player does NOT lose 1000 yet

When a player wins (ron or tsumo):
- Each player loses 1000 × their `RiichiSticks`, sticks cleared
- Winner collects all kyōtaku (Kyoutaku × 1000 points)
- Kyoutaku resets to 0

On draws: riichi sticks and kyōtaku persist to next hand. A player can declare riichi again in the next hand, stacking another stick.

On game end: remaining kyōtaku and unsettled riichi sticks go to the overall winner.

## Configurable Rules (GameRules)

Rule toggles are passed as flat fields in `CreateGameRequest` and stored on the `Game` object.

| Rule | Default | Effect |
|------|---------|--------|
| `Kiriage` | false | 4han30fu / 3han60fu → mangan (not 7700/11600) |
| `Atamahane` | false | Single ron only per discard (flag for caller) |
| `Kazoeyakuman` | true | If false, 13+ han without yakuman yaku caps at sanbaiman |
| `DoubleYakuman` | false | If true, special yakuman variants score double |
| `CompositeYakuman` | true | If true, different yakuman stack together |
| `Bankruptcy` | true | If false, game continues when a player goes below 0 |
| `AbortiveDraws` | true | If false, only exhaustive draws allowed |

Kiriage is enforced in ScoringService. Kazoeyakuman and bankruptcy are enforced in GameService.RecordHand. AbortiveDraws is enforced in GameService.RecordDraw.

## Draw Handling (in GameService)

Endpoint: `POST /api/game/{id}/draw` with DrawType and TenpaiPlayerIds.

Draw types: Exhaustive (ryuukyoku), FourKan, FourRiichi, NineTerminals, FourWind, TripleRon.

Exhaustive draw noten penalty (3000 total):
- 1 tenpai / 3 noten: tenpai gets 3000, each noten pays 1000
- 2 tenpai / 2 noten: each tenpai gets 1500, each noten pays 1500
- 3 tenpai / 1 noten: each tenpai gets 1000, noten pays 3000
- 0 or 4 tenpai: no exchange

Abortive draws: no point exchange, honba increments.

All draws increment honba. Dealer stays if tenpai (tenpai renchan), rotates if noten.

## Postman Collections

API test collections are in `postman/`:
- `MahjongScorekeeper.postman_collection.json` - main API workflows
- `PaoFlowTests.postman_collection.json` - pao (responsibility) flow tests
