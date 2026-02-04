# Endpoint Data Flow

This document describes the data flow for each API endpoint in the Mahjong Scoreboard API.

---

## Game Endpoints

### POST /api/game - Create Game

**Request Body: `CreateGameRequest`**
```json
{
  "playerNames": ["Player1", "Player2", "Player3", "Player4"],
  "startingScore": 25000,
  "targetScore": 30000,
  "uma": [30, 10, -10, -30],
  "kiriage": false,
  "atamahane": false,
  "kazoeyakuman": true,
  "doubleYakuman": false,
  "compositeYakuman": true,
  "bankruptcy": true,
  "abortiveDraws": true,
  "pao": true
}
```

**Data Flow:**
```
GameController.CreateGame()
    │
    ├─► Build GameRules from request fields
    │
    └─► GameService.CreateGame(playerNames, startingScore, rules)
            │
            ├─► new Game(playerNames, startingScore, rules)
            │       │
            │       ├─► Creates 4 Player objects with IDs, names, scores
            │       ├─► Assigns seat winds (East, South, West, North)
            │       └─► Initializes round state (East 1, Honba 0, Kyoutaku 0)
            │
            └─► Stores game in ConcurrentDictionary<Guid, Game>
```

**Response: `GameResponse`** (201 Created)

---

### GET /api/game/{id} - Get Game

**Data Flow:**
```
GameController.GetGame(id)
    │
    └─► GameService.GetGame(id)
            │
            └─► Dictionary lookup by Guid
```

**Response: `GameResponse`** (200 OK) or 404 Not Found

---

### GET /api/game - Get All Games

**Data Flow:**
```
GameController.GetAllGames()
    │
    └─► GameService.GetAllGames()
            │
            └─► Returns all values from ConcurrentDictionary
```

**Response: `List<GameSummaryResponse>`** (200 OK)

---

### POST /api/game/{id}/round - Record Round (Win)

**Request Body: `RecordRoundRequest`**
```json
{
  "winners": [
    {
      "winnerId": "guid",
      "han": 3,
      "fu": 30,
      "yaku": ["Riichi", "Tanyao"],
      "paoPlayerId": null
    }
  ],
  "loserId": "guid-or-null"
}
```

**Data Flow:**
```
GameController.RecordRound()
    │
    └─► GameService.RecordRound(gameId, winners, loserId)
            │
            ├─► Validation: game exists, not completed, valid players
            │
            ├─► Determine win type: isTsumo = (loserId == null)
            │
            ├─► Sort winners by atamahane order (seat order from loser)
            │
            ├─► If Atamahane rule: truncate to first winner only
            │
            └─► For each winner:
                    │
                    ├─► Apply Kazoeyakuman rule (cap han if disabled)
                    │
                    ├─► Calculate yakuman multiplier (composite/double)
                    │
                    ├─► ScoringService.CalculatePayment(han, fu, isDealer, isTsumo, ...)
                    │       │
                    │       ├─► Check Kiriage (4han30fu/3han60fu → mangan)
                    │       ├─► Check limit hands (mangan+ thresholds)
                    │       └─► Lookup from scoring tables or calculate limit
                    │
                    ├─► Calculate honba bonus (first winner only: honba × 300)
                    │
                    ├─► Handle Pao rule (liable player pays)
                    │   ├─► Pao Tsumo: liable pays full ron + honba
                    │   └─► Pao Ron: split 50/50, liable pays honba
                    │
                    ├─► OR Apply normal payments:
                    │   ├─► Tsumo: each non-winner pays (dealer ×2, others ×1)
                    │   └─► Ron: loser pays full amount + honba
                    │
                    ├─► Update winner stats (RonWins/TsumoWins)
                    │
                    └─► Create HandResult, add to current Round
            │
            ├─► Update loser DealIn count (if ron)
            │
            ├─► Game.SettleRiichi(firstWinnerId)
            │       │
            │       ├─► Each player loses 1000 × RiichiSticks
            │       ├─► First winner gains Kyoutaku × 1000
            │       └─► Reset Kyoutaku to 0
            │
            ├─► Game.AdvanceRound(dealerWon)
            │       │
            │       ├─► Dealer won: honba++, seat winds stay
            │       └─► Non-dealer won: rotate winds, honba=0, advance round
            │
            └─► Check bankruptcy rule → Game.EndGame() if triggered
```

**Response: `RoundConclusionResponse`** (200 OK)
```json
{
  "results": [{
    "id": "guid",
    "winnerId": "guid",
    "loserId": "guid-or-null",
    "han": 3,
    "fu": 30,
    "pointsWon": 3900,
    "honbaBonus": 0,
    "yaku": ["Riichi", "Tanyao"],
    "isTsumo": false,
    "receivedKyoutaku": true,
    "paoPlayerId": null
  }],
  "gameStatus": "InProgress"
}
```

---

### POST /api/game/{id}/draw - Record Draw

**Request Body: `RecordDrawRequest`**
```json
{
  "drawType": "Exhaustive",
  "tenpaiPlayerIds": ["guid1", "guid2"]
}
```

**Draw Types:** `Exhaustive`, `FourKan`, `FourRiichi`, `NineTerminals`, `FourWind`, `TripleRon`

**Data Flow:**
```
GameController.RecordDraw()
    │
    └─► GameService.RecordDraw(gameId, drawType, tenpaiPlayerIds)
            │
            ├─► Validation: game exists, not completed
            │
            ├─► Check AbortiveDraws rule (non-Exhaustive draws)
            │
            ├─► Create DrawResult, add to current Round
            │
            ├─► If Exhaustive: ApplyNotenPenalty()
            │       │
            │       ├─► 1 tenpai / 3 noten: +3000 / -1000 each
            │       ├─► 2 tenpai / 2 noten: +1500 each / -1500 each
            │       ├─► 3 tenpai / 1 noten: +1000 each / -3000
            │       └─► 0 or 4 tenpai: no exchange
            │
            ├─► Game.AdvanceAfterDraw(dealerTenpai)
            │       │
            │       ├─► Honba increments (always)
            │       ├─► Dealer tenpai: renchan (dealer stays)
            │       └─► Dealer noten: rotate winds
            │
            └─► Check bankruptcy rule → Game.EndGame() if triggered
```

**Response: `DrawResultResponse`** (200 OK)

---

### POST /api/game/{id}/riichi - Declare Riichi

**Request Body: `DeclareRiichiRequest`**
```json
{
  "playerId": "guid"
}
```

**Data Flow:**
```
GameController.DeclareRiichi()
    │
    └─► GameService.DeclareRiichi(gameId, playerId)
            │
            └─► Game.DeclareRiichi(playerId)
                    │
                    ├─► Player.RiichiSticks++ (can stack across draws)
                    ├─► Player.RiichiCount++ (stat tracking)
                    └─► Game.Kyoutaku++ (stick on table)

                    Note: 1000 points NOT deducted yet
                          (settled when someone wins)
```

**Response: `GameResponse`** (200 OK)

---

### GET /api/game/{id}/history - Get Game History

**Data Flow:**
```
GameController.GetHistory()
    │
    └─► GameService.GetGame(id)
            │
            └─► Maps all Rounds to RoundResponse
                    │
                    ├─► Round metadata (wind, kyoku, honba, dealer)
                    ├─► HandResults (wins in that round)
                    └─► DrawResult (if round ended in draw)
```

**Response: `GameHistoryResponse`** (200 OK)

---

### POST /api/game/{id}/end - End Game

**Data Flow:**
```
GameController.EndGame()
    │
    └─► GameService.EndGame(gameId)
            │
            └─► Game.EndGame()
                    │
                    ├─► Set Status = Completed
                    ├─► Set CompletedAt timestamp
                    ├─► Settle remaining riichi (to winner)
                    └─► Calculate FinalScores with Uma
```

**Response: `GameResponse`** (200 OK) with Ranking populated

---

### DELETE /api/game/{id} - Delete Game

**Data Flow:**
```
GameController.DeleteGame()
    │
    └─► GameService.DeleteGame(id)
            │
            └─► ConcurrentDictionary.TryRemove(id)
```

**Response:** 204 No Content or 404 Not Found

---

### GET /api/game/player/{playerName} - Get Games by Player

**Data Flow:**
```
GameController.GetGamesByPlayer()
    │
    └─► GameService.GetGamesByPlayerName(playerName)
            │
            └─► Filter games where any player name matches (case-insensitive)
```

**Response: `List<GameSummaryResponse>`** (200 OK)

---

### GET /api/game/search?players=A,B,C,D - Search Game by Players

**Data Flow:**
```
GameController.GetGameByPlayers()
    │
    └─► GameService.GetGameByAllPlayers(playerNames)
            │
            └─► Find first game where all 4 player names match (sorted, case-insensitive)
```

**Response: `GameResponse`** (200 OK) or 404 Not Found

---

### GET /api/game/active/{playerName} - Get Active Game by Player

**Data Flow:**
```
GameController.GetActiveGameByPlayer()
    │
    └─► GameService.GetActiveGameByPlayerName(playerName)
            │
            └─► Filter games: Status == InProgress AND player name matches
```

**Response: `GameResponse`** (200 OK) or 404 Not Found

---

## Yaku Endpoints

### GET /api/yaku - Get All Yaku

**Data Flow:**
```
YakuController.GetAllYaku()
    │
    └─► Enum.GetValues<Yaku>()
            │
            └─► For each Yaku enum value:
                    ├─► GetHanValue() - extension method
                    └─► GetDescription() - extension method
```

**Response: `List<YakuResponse>`** (200 OK)
```json
[
  {
    "name": "Riichi",
    "hanValue": 1,
    "description": "Declared ready hand"
  },
  ...
]
```

---

## Core Type Summary

### Request Types
| Type | Used By |
|------|---------|
| `CreateGameRequest` | POST /api/game |
| `RecordRoundRequest` | POST /api/game/{id}/round |
| `RecordDrawRequest` | POST /api/game/{id}/draw |
| `DeclareRiichiRequest` | POST /api/game/{id}/riichi |

### Response Types
| Type | Used By |
|------|---------|
| `GameResponse` | Full game state with all details |
| `GameSummaryResponse` | Condensed game list view |
| `RoundConclusionResponse` | Result of recording a win |
| `DrawResultResponse` | Result of recording a draw |
| `GameHistoryResponse` | Full round-by-round history |
| `YakuResponse` | Yaku reference data |

### Domain Models
| Model | Description |
|-------|-------------|
| `Game` | Aggregate root: players, rounds, rules, state |
| `Player` | ID, name, score, seat wind, stats |
| `Round` | Round number, wind, kyoku, results |
| `HandResult` | Single winning hand record |
| `DrawResult` | Draw type and tenpai players |
| `GameRules` | Configurable rule toggles |

### Services
| Service | Responsibility |
|---------|----------------|
| `GameService` | Game CRUD, round recording, state management |
| `ScoringService` | Han/Fu → point calculation via lookup tables |
