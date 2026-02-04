namespace MahjongScoreBoard.Api.Models;

public enum DrawType
{
    Exhaustive,     // Ryuukyoku — wall runs out
    FourKan,        // Suukaikan — 4 kans declared by different players
    FourRiichi,     // Suucha riichi — all 4 players declare riichi
    NineTerminals,  // Kyuushu kyuuhai — 9 different terminals/honors in opening hand
    FourWind,       // Suufon renda — all 4 players discard same wind on first turn
    TripleRon       // Sanchahou — 3 players ron on same discard
}
