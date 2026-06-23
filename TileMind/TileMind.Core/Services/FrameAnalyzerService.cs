using Microsoft.Extensions.Options;
using TileMind.Common.Config;
using TileMind.Common.Models;

namespace TileMind.Core.Services;

/// <summary>
/// 单帧静态分析服务：分离手牌/副露、判定副露类型。
/// 不依赖任何跨帧状态，在追踪/非追踪模式下均可使用。
/// </summary>
public class FrameAnalyzerService
{
    private readonly HandMeldSeparator _separator;

    public FrameAnalyzerService(IOptionsSnapshot<GameStateTrackerOptions> options)
    {
        _separator = new HandMeldSeparator(options.Value);
    }

    /// <summary>
    /// 对一帧检测结果执行静态分析，输出 AnalyzedFrame。
    /// </summary>
    public AnalyzedFrame Analyze(FrameDetections input)
    {
        var result = new AnalyzedFrame
        {
            Timestamp = input.Timestamp,
            DoraIndicatorDetections = input.DoraIndicatorDetections,
            DiscardPondDetections = input.DiscardPondDetections
        };

        // 宝牌指示牌 → 实际宝牌映射
        result.DoraTiles = MapDoraTiles(input.DoraIndicatorDetections);

        foreach (SeatPosition seat in Enum.GetValues<SeatPosition>())
        {
            input.HandAndMeldDetections.TryGetValue(seat, out var handMeldDets);
            handMeldDets ??= new();

            var (handDets, meldGroups) = _separator.Separate(handMeldDets, seat);

            // 推断暗杠：2 张同牌 + 两侧大间隙 → 补充为 4 张 Ankan
            InferAnkans(meldGroups, handDets, seat);

            var melds = meldGroups
                .Where(g => g.Count >= 2)
                .Select(g => new MeldAnalysis
                {
                    MeldType = DetermineMeldType(g, seat),
                    Tiles = g
                }).ToList();

            result.DiscardPondDetections.TryGetValue(seat, out var pondDets);
            pondDets ??= new();

            result.Players[seat] = new PlayerFrameAnalysis
            {
                Seat = seat,
                HandTiles = handDets,
                Melds = melds,
                HasRiichiDiscard = DetectRiichi(seat, pondDets, out var riichiTile),
                RiichiDiscardTile = riichiTile
            };
        }

        return result;
    }

    /// <summary>
    /// 立直检测：根据弃牌区检测框宽高比按座位方向判断。
    /// 自家/对家：正常 h>w（竖），立直 w>h（横）。
    /// 上家/下家：正常 w>h（横），立直 h>w（竖）。
    /// </summary>
    private static bool DetectRiichi(SeatPosition seat, List<DetectionResult> pondDets, out DetectionResult? riichiTile)
    {
        riichiTile = null;
        foreach (var det in pondDets)
        {
            float ratio = (float)det.BoundingBox.Width / det.BoundingBox.Height;
            bool isRotated = seat is SeatPosition.Self or SeatPosition.Opposite
                ? ratio > 1.0f   // 横置：宽 > 高
                : ratio < 1.0f;  // 竖置：高 > 宽

            if (isRotated)
            {
                riichiTile = det;
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 推断暗杠：副露区中仅有两张同牌 + 两侧大间隙 → 补充为 4 张 Ankan 组。
    /// 暗杠中间两张朝上可识别，两端背牌不可识别，通过间隙特征推断。
    /// </summary>
    private static void InferAnkans(
        List<List<DetectionResult>> meldGroups, List<DetectionResult> handDets, SeatPosition seat)
    {
        bool isHorizontal = seat is SeatPosition.Self or SeatPosition.Opposite;

        foreach (var group in meldGroups.Where(g => g.Count == 2).ToList())
        {
            // 两张牌必须同类型
            if (ActionClassifier.NormalizeTileType(group[0].TileType)
                != ActionClassifier.NormalizeTileType(group[1].TileType))
                continue;

            // 计算组内间距和平均 tile 尺寸
            double pos1 = isHorizontal ? group[0].BoundingBox.X + group[0].BoundingBox.Width / 2.0
                                       : group[0].BoundingBox.Y + group[0].BoundingBox.Height / 2.0;
            double pos2 = isHorizontal ? group[1].BoundingBox.X + group[1].BoundingBox.Width / 2.0
                                       : group[1].BoundingBox.Y + group[1].BoundingBox.Height / 2.0;
            double avgTileWidth = isHorizontal
                ? (group[0].BoundingBox.Width + group[1].BoundingBox.Width) / 2.0
                : (group[0].BoundingBox.Height + group[1].BoundingBox.Height) / 2.0;

            double gapBetween = Math.Abs(pos2 - pos1);
            if (gapBetween > avgTileWidth * 2.5) continue; // 间距过大 → 不是紧邻的暗杠面牌

            // 检查与相邻牌 / 手牌边界的间隙
            double gapLeft = double.MaxValue, gapRight = double.MaxValue;
            foreach (var det in handDets)
            {
                double detPos = isHorizontal ? det.BoundingBox.X + det.BoundingBox.Width / 2.0
                                             : det.BoundingBox.Y + det.BoundingBox.Height / 2.0;
                if (detPos < pos1) gapLeft = Math.Min(gapLeft, pos1 - detPos);
                if (detPos > pos2) gapRight = Math.Min(gapRight, detPos - pos2);
            }
            foreach (var other in meldGroups.Where(g => g != group))
            {
                foreach (var det in other)
                {
                    double detPos = isHorizontal ? det.BoundingBox.X + det.BoundingBox.Width / 2.0
                                                 : det.BoundingBox.Y + det.BoundingBox.Height / 2.0;
                    if (detPos < pos1) gapLeft = Math.Min(gapLeft, pos1 - detPos);
                    if (detPos > pos2) gapRight = Math.Min(gapRight, detPos - pos2);
                }
            }

            // 两侧各有足够间隙 → 推断存在背牌
            if (gapLeft > avgTileWidth * 1.5 && gapRight > avgTileWidth * 1.5)
            {
                // 补充为 4 张：复制两张面牌作为背牌的占位
                var faceDown = group.Select(d =>
                {
                    var clone = new DetectionResult
                    {
                        TileType = d.TileType, // 类型继承面牌
                        BoundingBox = d.BoundingBox, // 近似位置
                        Confidence = d.Confidence,
                    };
                    return clone;
                }).ToList();
                group.AddRange(faceDown);
            }
        }
    }

    /// <summary>
    /// 根据牌数和花色判定副露类型。
    /// </summary>
    internal static MeldType DetermineMeldType(List<DetectionResult> detections, SeatPosition seat)
    {
        if (detections.Count == 4) return MeldType.Kan;
        if (detections.Count != 3) return MeldType.Pon;

        var types = detections
            .Select(d => ActionClassifier.NormalizeTileType(d.TileType))
            .ToList();

        return ActionClassifier.IsChi(types) ? MeldType.Chi : MeldType.Pon;
    }

    /// <summary>
    /// 将宝牌指示牌检测结果映射为实际宝牌 TileType（去重）。
    /// </summary>
    private static List<TileType> MapDoraTiles(List<DetectionResult> doraIndicatorDets)
    {
        var doraTiles = new HashSet<TileType>();
        foreach (var det in doraIndicatorDets)
        {
            var dora = GetDoraTileType(det.TileType);
            if (dora != TileType.Unknown)
                doraTiles.Add(dora);
        }
        return doraTiles.ToList();
    }

    /// <summary>
    /// 日麻规则：从指示牌推算宝牌。
    /// 数牌 1→2→…→8→9→1，风牌 东→南→西→北→东，三元牌 白→发→中→白。
    /// 赤牌（0）视为对应数字 5 处理。
    /// </summary>
    internal static TileType GetDoraTileType(TileType indicator)
    {
        // 赤牌归一化为数字 5
        int normalized = ActionClassifier.NormalizeTileType(indicator);
        int suit = normalized / 10;
        int num = normalized % 10;

        if (suit <= 2) // 万/筒/索
        {
            int doraNum = num == 9 ? 1 : num + 1;
            return (TileType)(suit * 10 + doraNum);
        }
        else if (suit == 3) // 字牌
        {
            int doraNum;
            if (num <= 4) // 风牌 1-4
                doraNum = (num % 4) + 1;
            else // 三元牌 5-7
                doraNum = ((num - 5 + 1) % 3) + 5;
            return (TileType)(suit * 10 + doraNum);
        }

        return TileType.Unknown;
    }
}
