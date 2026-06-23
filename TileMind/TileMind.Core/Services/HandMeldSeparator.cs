using TileMind.Common.Config;
using TileMind.Common.Models;

namespace TileMind.Core.Services;

/// <summary>
/// 手牌/副露分离器。
/// 本家：HandAndMeldArea 含有手牌+副露（面牌均可识别），按主轴排序，最大 gap 处切开。
/// 其他玩家：HandAndMeldArea 只有副露面牌（手牌为背牌不可识别），无需分离，全部归入副露。
/// </summary>
public class HandMeldSeparator
{
    private readonly GameStateTrackerOptions _options;

    public HandMeldSeparator(GameStateTrackerOptions options)
    {
        _options = options;
    }

    public HandMeldSeparator() : this(new GameStateTrackerOptions()) { }

    /// <summary>
    /// 分离指定玩家的手牌和副露组。
    /// </summary>
    public (List<DetectionResult> HandTiles, List<List<DetectionResult>> MeldGroups) Separate(
        List<DetectionResult> detections, SeatPosition seat)
    {
        if (detections.Count == 0)
            return (new(), new());

        var isSelf = seat == SeatPosition.Self;
        var isHorizontal = isSelf || seat == SeatPosition.Opposite;

        // 主轴排序
        var sorted = detections.OrderBy(d =>
            isHorizontal ? d.BoundingBox.X + d.BoundingBox.Width / 2.0
                         : d.BoundingBox.Y + d.BoundingBox.Height / 2.0).ToList();

        if (!isSelf)
        {
            // 其他玩家：全部是副露面牌，直接聚类
            var groups = ClusterMeldCandidates(sorted, isHorizontal);
            return (new(), groups);
        }

        // 本家：找最大 gap 切开手牌/副露
        double avgTileSize = isHorizontal
            ? sorted.Average(d => (double)d.BoundingBox.Width)
            : sorted.Average(d => (double)d.BoundingBox.Height);

        int splitIndex = FindMaxGap(sorted, isHorizontal, _options.HandMeldGapMultiplier);

        List<DetectionResult> handTiles;
        List<DetectionResult> meldCandidates;

        if (splitIndex < 0)
        {
            // 无显著 gap → 全部归手牌
            handTiles = sorted;
            meldCandidates = new();
        }
        else
        {
            // 本家手牌居左 → 左边多的是手牌，右边少的是副露
            int handCount = splitIndex + 1;
            int meldCount = sorted.Count - handCount;
            if (handCount >= meldCount)
            {
                handTiles = sorted.Take(splitIndex + 1).ToList();
                meldCandidates = sorted.Skip(splitIndex + 1).ToList();
            }
            else
            {
                handTiles = sorted.Skip(splitIndex + 1).ToList();
                meldCandidates = sorted.Take(splitIndex + 1).ToList();
            }
        }

        var meldGroups = ClusterMeldCandidates(meldCandidates, isHorizontal);

        return (handTiles, meldGroups);
    }

    /// <summary>在手牌行内找最大 gap 的索引（gap 在 index 和 index+1 之间）。无显著 gap 返回 -1。</summary>
    private static int FindMaxGap(List<DetectionResult> sorted, bool isHorizontal, double gapMultiplier)
    {
        if (sorted.Count < 2) return -1;

        var gaps = new List<double>();
        for (int i = 0; i < sorted.Count - 1; i++)
        {
            double p1 = isHorizontal ? sorted[i].BoundingBox.X + sorted[i].BoundingBox.Width / 2.0
                                     : sorted[i].BoundingBox.Y + sorted[i].BoundingBox.Height / 2.0;
            double p2 = isHorizontal ? sorted[i + 1].BoundingBox.X + sorted[i + 1].BoundingBox.Width / 2.0
                                     : sorted[i + 1].BoundingBox.Y + sorted[i + 1].BoundingBox.Height / 2.0;
            gaps.Add(p2 - p1);
        }

        double avgGap = gaps.Average();
        double maxGap = gaps.Max();

        // 最大 gap 超过平均值 × 倍数 → 至少是正常间距的 2~3 倍，判定为分界
        if (maxGap > avgGap * gapMultiplier && maxGap > avgGap * 1.5 + 10)
            return gaps.IndexOf(maxGap);

        return -1;
    }

    /// <summary>将检测结果沿主轴聚类为 2~4 张一组的副露组。</summary>
    private List<List<DetectionResult>> ClusterMeldCandidates(List<DetectionResult> candidates, bool isHorizontal)
    {
        if (candidates.Count == 0) return new();

        candidates = candidates.OrderBy(d =>
            isHorizontal ? d.BoundingBox.X + d.BoundingBox.Width / 2.0
                         : d.BoundingBox.Y + d.BoundingBox.Height / 2.0).ToList();

        var clusters = ClusterByCoordinate(candidates, d =>
            isHorizontal ? d.BoundingBox.X + d.BoundingBox.Width / 2.0
                         : d.BoundingBox.Y + d.BoundingBox.Height / 2.0,
            _options.MeldProximityThreshold);

        var groups = new List<List<DetectionResult>>();
        foreach (var cluster in clusters)
        {
            if (cluster.Count is >= 2 and <= 4)
                groups.Add(cluster);
        }

        return groups;
    }

    private static List<List<T>> ClusterByCoordinate<T>(
        List<T> items, Func<T, double> coordSelector, double tolerance)
    {
        if (items.Count == 0) return new();

        var sorted = items.OrderBy(coordSelector).ToList();
        var clusters = new List<List<T>>();
        var current = new List<T> { sorted[0] };

        for (int i = 1; i < sorted.Count; i++)
        {
            if (coordSelector(sorted[i]) - coordSelector(sorted[i - 1]) > tolerance)
            {
                clusters.Add(current);
                current = new List<T>();
            }
            current.Add(sorted[i]);
        }
        clusters.Add(current);

        return clusters;
    }
}
