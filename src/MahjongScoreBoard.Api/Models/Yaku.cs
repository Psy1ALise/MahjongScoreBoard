namespace MahjongScoreBoard.Api.Models;

public enum Yaku
{
    Riichi = 1,
    Tsumo = 2,
    Tanyao = 3,
    Yakuhai = 4,
    Chankan = 5,
    Rinshankaihou = 6,
    Haitei = 7,
    Houtei = 8,
    Pinfu = 9,
    Ippatsu = 10, 
    Iipeikou = 11,

    Toitoi = 12,
    Sanankou = 13,
    Sanshoku = 14,
    Sanshokkudoukou = 15,
    Sankantsu = 16,
    Honroutou = 17,
    Chantaku = 18,
    Shousangen = 19,
    Ittsuu = 20,

    Ryanpeikou = 21,
    Honitsu = 22,
    Junchan = 23,

    Nagashimangan = 24,
    
    Chinitsu = 25,

    Tenhou = 26,
    Chiihou = 27,
    Daisangen = 28,
    Daisuushii = 29,
    Shousuushii = 30,
    Suuankou = 31,
    Chinroutou = 32,
    Suukantsu = 33,
    Tsuiisou = 34,
    Ryuuiisou = 35,
    Kokushimusou = 36,
    Chuuren = 37,
    Kazoeyakuman = 38,

    SuuankouTanki = 43,
    KokushiJuusanmen = 44,
    JunseiChuurenpoutou = 45,

    Aka = 39,
    Dora = 40,
    Ura = 41,

    _ = 42
}

public static class YakuExtensions
{
    public static int GetHanValue(this Yaku yaku) => yaku switch
    {
        // 1 han
        Yaku.Riichi => 1,
        Yaku.Tsumo => 1,
        Yaku.Tanyao => 1,
        Yaku.Yakuhai => 1,
        Yaku.Chankan => 1,
        Yaku.Rinshankaihou => 1,
        Yaku.Haitei => 1,
        Yaku.Houtei => 1,
        Yaku.Pinfu => 1,
        Yaku.Ippatsu => 1,
        Yaku.Iipeikou => 1,

        // 2 han
        Yaku.Toitoi => 2,
        Yaku.Sanankou => 2,
        Yaku.Sanshoku => 2,
        Yaku.Sanshokkudoukou => 2,
        Yaku.Sankantsu => 2,
        Yaku.Honroutou => 2,
        Yaku.Chantaku => 2,
        Yaku.Shousangen => 2,
        Yaku.Ittsuu => 2,

        // 3 han
        Yaku.Ryanpeikou => 3,
        Yaku.Honitsu => 3,
        Yaku.Junchan => 3,

        // 5 han (Mangan)
        Yaku.Nagashimangan => 5,

        // 6 han
        Yaku.Chinitsu => 6,

        // Yakuman (13 han)
        Yaku.Tenhou => 13,
        Yaku.Chiihou => 13,
        Yaku.Daisangen => 13,
        Yaku.Daisuushii => 13,
        Yaku.Shousuushii => 13,
        Yaku.Suuankou => 13,
        Yaku.Chinroutou => 13,
        Yaku.Suukantsu => 13,
        Yaku.Tsuiisou => 13,
        Yaku.Ryuuiisou => 13,
        Yaku.Kokushimusou => 13,
        Yaku.Chuuren => 13,
        Yaku.Kazoeyakuman => 13,
        Yaku.SuuankouTanki => 13,
        Yaku.KokushiJuusanmen => 13,
        Yaku.JunseiChuurenpoutou => 13,

        // Bonus (1 han each, not valid yaku alone)
        Yaku.Aka => 1,
        Yaku.Dora => 1,
        Yaku.Ura => 1,

        _ => 0
    };

    public static string GetDescription(this Yaku yaku) => yaku switch
    {
        // 1 han
        Yaku.Riichi => "Declared ready (closed hand) before winning",
        Yaku.Tsumo => "Self-draw win (closed hand)",
        Yaku.Tanyao => "All simples (no terminals or honors)",
        Yaku.Yakuhai => "Value tiles (dragons, seat wind, or round wind)",
        Yaku.Chankan => "Robbing a kan",
        Yaku.Rinshankaihou => "Win on dead wall draw after kan",
        Yaku.Haitei => "Win on last tile draw",
        Yaku.Houtei => "Win on last discard",
        Yaku.Pinfu => "No-points hand (all sequences, valueless pair)",
        Yaku.Ippatsu => "Win within one turn of riichi",
        Yaku.Iipeikou => "Two identical sequences in same suit",

        // 2 han
        Yaku.Toitoi => "All triplets and a pair",
        Yaku.Sanankou => "Three concealed triplets",
        Yaku.Sanshoku => "Same sequence in all three suits",
        Yaku.Sanshokkudoukou => "Same triplet in all three suits",
        Yaku.Sankantsu => "Three kans",
        Yaku.Honroutou => "All terminals and honors",
        Yaku.Chantaku => "All sets contain terminals or honors",
        Yaku.Shousangen => "Little three dragons (two triplets, one pair)",
        Yaku.Ittsuu => "Pure straight (1-9 in one suit)",

        // 3 han
        Yaku.Ryanpeikou => "Two sets of identical sequences",
        Yaku.Honitsu => "Half flush (one suit plus honors)",
        Yaku.Junchan => "All sets contain terminals (no honors)",

        // 5 han
        Yaku.Nagashimangan => "All discards are terminals/honors (no calls)",

        // 6 han
        Yaku.Chinitsu => "Full flush (one suit only)",

        // Yakuman
        Yaku.Tenhou => "Dealer wins on initial draw",
        Yaku.Chiihou => "Non-dealer wins on first draw",
        Yaku.Daisangen => "Big three dragons (three dragon triplets)",
        Yaku.Daisuushii => "Big four winds (four wind triplets)",
        Yaku.Shousuushii => "Little four winds (three triplets, one pair)",
        Yaku.Suuankou => "Four concealed triplets",
        Yaku.Chinroutou => "All terminals",
        Yaku.Suukantsu => "Four kans",
        Yaku.Tsuiisou => "All honors",
        Yaku.Ryuuiisou => "All green (2,3,4,6,8 sou + green dragon)",
        Yaku.Kokushimusou => "Thirteen orphans",
        Yaku.Chuuren => "Nine gates",
        Yaku.Kazoeyakuman => "Greater or equal 13 han is counted as a Yakuman",
        Yaku.SuuankouTanki => "Four concealed triplets (pair wait)",
        Yaku.KokushiJuusanmen => "Thirteen orphans (13-sided wait)",
        Yaku.JunseiChuurenpoutou => "Pure nine gates (9-sided wait)",

        // Bonus
        Yaku.Aka => "Red dora tile",
        Yaku.Dora => "Dora indicator bonus",
        Yaku.Ura => "Ura dora (under riichi)",

        _ => "Unknown yaku"
    };

    public static bool IsBonus(this Yaku yaku) => yaku switch
    {
        Yaku.Aka => true,
        Yaku.Dora => true,
        Yaku.Ura => true,
        _ => false
    };

    public static bool IsYakuman(this Yaku yaku) => yaku switch
    {
        Yaku.Tenhou => true,
        Yaku.Chiihou => true,
        Yaku.Daisangen => true,
        Yaku.Daisuushii => true,
        Yaku.Shousuushii => true,
        Yaku.Suuankou => true,
        Yaku.Chinroutou => true,
        Yaku.Suukantsu => true,
        Yaku.Tsuiisou => true,
        Yaku.Ryuuiisou => true,
        Yaku.Kokushimusou => true,
        Yaku.Chuuren => true,
        Yaku.Kazoeyakuman => true,
        Yaku.SuuankouTanki => true,
        Yaku.KokushiJuusanmen => true,
        Yaku.JunseiChuurenpoutou => true,
        _ => false
    };

    public static bool IsDoubleYakuman(this Yaku yaku) => yaku switch
    {
        Yaku.SuuankouTanki => true,
        Yaku.KokushiJuusanmen => true,
        Yaku.JunseiChuurenpoutou => true,
        Yaku.Daisuushii => true,
        _ => false
    };
}
