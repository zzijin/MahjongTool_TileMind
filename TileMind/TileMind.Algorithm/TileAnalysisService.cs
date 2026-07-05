using RiichiSharp;
using RiichiSharp.Tenpai;
using TileMind.Common.Models;

namespace TileMind.Algorithm;

/// <summary>
/// 牌型分析服务：桥接 TileMind 的 AnalyzedFrame 到 RiichiSharp 麻将算法。
/// 输出向听数、听牌分析、打牌推荐、胡牌选项。
/// </summary>
public class TileAnalysisService
{
    /// <summary>用于输出调试日志的可选回调。</summary>
    public static Action<string>? DebugLog { get; set; }

    /// <summary>
    /// 基于单帧静态分析结果，对当前本家手牌做完整牌型分析。
    /// 场风/自风暂用默认值（东/东），后续接入 InfoArea 解析后修正。
    /// </summary>
    public TileAnalysisResult Analyze(AnalyzedFrame analysis)
    {
        var self = analysis.Players.GetValueOrDefault(SeatPosition.Self);
        if (self == null)
            return new TileAnalysisResult { Shanten = 6 };

        var result = new TileAnalysisResult();

        string handStr = HandStringBuilder.BuildHandString(self.HandTiles);
        if (string.IsNullOrEmpty(handStr))
            return new TileAnalysisResult { Shanten = 6 };

        int calledMelds = HandStringBuilder.CountCalledMelds(self.Melds);
        string visibleStr = HandStringBuilder.BuildVisibleString(analysis);

        int shanten;
        try { shanten = MahjongEngine.Shanten(handStr, calledMelds).Shanten; }
        catch (Exception ex)
        {
            DebugLog?.Invoke($"[TileAnalysis] Shanten failed: {ex.Message}");
            return new TileAnalysisResult { Shanten = 6 };
        }
        result.Shanten = shanten;
        result.RemainingCounts = CalcRemainingCounts(analysis, handStr);

        if (shanten == 0)
        {
            int akaCount = HandStringBuilder.CountAkaDora(self.HandTiles);
            var doraIndicators = BuildDoraIndicators(analysis.DoraIndicatorDetections);
            FillWinOptions(result, self, handStr, calledMelds, visibleStr, akaCount, doraIndicators);
        }
        else if (shanten > 0)
        {
            FillDiscardOptions(result, handStr, calledMelds);
        }
        // shanten == -1: already completed hand, no further analysis needed

        return result;
    }

    private static void FillWinOptions(
        TileAnalysisResult result, PlayerFrameAnalysis self,
        string handStr, int calledMelds, string visibleStr, int akaCount,
        List<RiichiSharp.Tiles.Tile> doraIndicators)
    {
        var tiles = RiichiSharp.Parse.TenhouParser.Parse(handStr);
        if (tiles == null) return;

        var visible = RiichiSharp.Parse.TenhouParser.Parse(visibleStr);
        var tenpai = TenpaiCalculator.Calculate(tiles.Value, calledMelds, visible);

        foreach (var wait in tenpai.Waits)
        {
            var winTile = TileTypeMapper.ToTileMind(wait);
            var rsTile = TileTypeMapper.ToRiichiSharp(winTile);
            if (rsTile == null) continue;
            string scoringHand = HandStringBuilder.BuildScoringHandString(self) +
                                 rsTile.Value.Number + SuitChar(rsTile.Value.Suit);

            // 分别计算自摸和荣和得点
            foreach (var winType in new[] { WinType.Tsumo, WinType.Ron })
            {
                var ctx = new GameContext
                {
                    WinType = winType,
                    WinningTile = rsTile.Value,
                    RoundWind = RiichiSharp.Tiles.Tile.East,
                    SeatWind = RiichiSharp.Tiles.Tile.East,
                    AkaCount = akaCount,
                    DoraIndicators = doraIndicators,
                };

                try
                {
                    var score = MahjongEngine.Score(scoringHand, ctx);
                    int remaining = result.RemainingCounts.GetValueOrDefault(winTile, 0);
                    result.WinOptions.Add(new WinOption
                    {
                        WinTile = winTile,
                        Remaining = remaining,
                        Han = score.Han,
                        Fu = score.FuTotal,
                        Points = score.Points,
                        YakuNames = score.YakuList?.Select(y => y.ToString()).ToList() ?? new()
                    });
                }
                catch (Exception ex)
                {
                    DebugLog?.Invoke($"[TileAnalysis] Score({winType}) failed for {winTile}: {ex.Message}");
                    int remaining = result.RemainingCounts.GetValueOrDefault(winTile, 0);
                    result.WinOptions.Add(new WinOption
                    {
                        WinTile = winTile,
                        Remaining = remaining
                    });
                }
            }
        }
    }

    private static void FillDiscardOptions(
        TileAnalysisResult result, string handStr, int calledMelds)
    {
        try
        {
            var discards = MahjongEngine.EffectiveDiscards(handStr, calledMelds);
            foreach (var d in discards)
            {
                result.DiscardOptions.Add(new DiscardOption
                {
                    DiscardTile = TileTypeMapper.ToTileMind(d.Discarded),
                    ShantenAfter = d.ShantenAfter,
                    UniqueUkeire = d.UniqueUkeire,
                    TotalUkeire = d.TotalUkeire,
                    UkeireTiles = d.UkeireTiles?
                        .Select(u => new UkeireTile
                        {
                            Tile = TileTypeMapper.ToTileMind(u.Tile),
                            Available = u.Available
                        }).ToList() ?? new()
                });
            }
        }
        catch (Exception ex)
        {
            DebugLog?.Invoke($"[TileAnalysis] EffectiveDiscards failed: {ex.Message}");
        }
    }

    private static Dictionary<TileType, int> CalcRemainingCounts(AnalyzedFrame analysis, string selfHandStr)
    {
        var known = new int[34];
        CountTiles(known, selfHandStr);

        // 本家副露
        if (analysis.Players.TryGetValue(SeatPosition.Self, out var self))
            foreach (var det in self.Melds.SelectMany(m => m.Tiles))
                AddKnown(known, det);

        // 所有弃牌
        foreach (var det in analysis.DiscardPondDetections.Values.SelectMany(d => d))
            AddKnown(known, det);

        // 其他玩家副露
        foreach (var (seat, player) in analysis.Players)
        {
            if (seat == SeatPosition.Self) continue;
            foreach (var det in player.Melds.SelectMany(m => m.Tiles))
                AddKnown(known, det);
        }

        // 宝牌指示牌（指示牌和宝牌本身都在王牌区，都应从剩余牌中扣除）
        foreach (var det in analysis.DoraIndicatorDetections)
            AddKnown(known, det);
        foreach (var dora in analysis.DoraTiles)
            AddKnown(known, dora);

        var result = new Dictionary<TileType, int>();
        for (int i = 0; i < 34; i++)
            result[TileTypeMapper.ToTileMind(new RiichiSharp.Tiles.Tile((byte)i))] = Math.Max(0, 4 - known[i]);

        return result;
    }

    /// <summary>将宝牌指示牌检测结果转为 RiichiSharp Tile 数组。</summary>
    private static List<RiichiSharp.Tiles.Tile> BuildDoraIndicators(List<Common.Models.DetectionResult> indicatorDets)
    {
        return indicatorDets
            .Select(d => TileTypeMapper.ToRiichiSharp(d.TileType))
            .Where(t => t != null)
            .Select(t => t!.Value)
            .ToList();
    }

    private static void AddKnown(int[] known, Common.Models.DetectionResult det)
    {
        int id = TileTypeMapper.GetTileId(det);
        if (id >= 0) known[id]++;
    }

    private static void AddKnown(int[] known, TileType tile)
    {
        int id = TileTypeMapper.GetTileId(tile);
        if (id >= 0) known[id]++;
    }

    private static void CountTiles(int[] known, string handStr)
    {
        var tiles = RiichiSharp.Parse.TenhouParser.Parse(handStr);
        if (tiles == null) return;
        for (int i = 0; i < 34; i++)
            known[i] += tiles.Value[i];
    }

    private static char SuitChar(RiichiSharp.Tiles.Suit suit) => suit switch
    {
        RiichiSharp.Tiles.Suit.Manzu => 'm',
        RiichiSharp.Tiles.Suit.Pinzu => 'p',
        RiichiSharp.Tiles.Suit.Souzu => 's',
        _ => 'z'
    };
}
