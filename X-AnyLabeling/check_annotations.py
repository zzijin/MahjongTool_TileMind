"""
检查 X-AnyLabeling JSON 标注文件的脚本。
检查项：
  1. label 值不在 classes.txt 中（非法标签）
  2. 同一图片中同 label 出现次数超限（普通牌 ≤4，赤宝牌 0m/0p/0s ≤1）
  3. 同一图片中同 label 的标注框严重重叠（IoU > 0.5，疑似重复标注）
  4. 标注框宽度或高度 < 10px（过小，可能无效）
"""

import json
from pathlib import Path
from collections import Counter

# =========================================================================
# 配置
# =========================================================================
BASE = Path(__file__).parent / "dataset"  # 脚本所在目录下的 dataset 子目录
CHECK_DIRS = ["label-01", "label-02", "label-03"]
VALID_LABELS = set(
    "1m 2m 3m 4m 5m 6m 7m 8m 9m 0m "
    "1p 2p 3p 4p 5p 6p 7p 8p 9p 0p "
    "1s 2s 3s 4s 5s 6s 7s 8s 9s 0s "
    "1z 2z 3z 4z 5z 6z 7z".split()
)
RED_FIVES = {"0m", "0p", "0s"}
MAX_NORMAL = 4
MAX_RED_FIVE = 1
IOU_THRESHOLD = 0.5  # IoU 超过此值视为重复标注


def compute_iou(box1, box2):
    """计算两个矩形框的 IoU。box 格式: [[x1,y1],[x2,y2]]"""
    x1_min, y1_min = box1[0]
    x1_max, y1_max = box1[1]
    x2_min, y2_min = box2[0]
    x2_max, y2_max = box2[1]

    # 确保 min < max
    if x1_min > x1_max:
        x1_min, x1_max = x1_max, x1_min
    if y1_min > y1_max:
        y1_min, y1_max = y1_max, y1_min
    if x2_min > x2_max:
        x2_min, x2_max = x2_max, x2_min
    if y2_min > y2_max:
        y2_min, y2_max = y2_max, y2_min

    # 交集
    inter_x1 = max(x1_min, x2_min)
    inter_y1 = max(y1_min, y2_min)
    inter_x2 = min(x1_max, x2_max)
    inter_y2 = min(y1_max, y2_max)

    inter_w = max(0, inter_x2 - inter_x1)
    inter_h = max(0, inter_y2 - inter_y1)
    inter_area = inter_w * inter_h

    area1 = (x1_max - x1_min) * (y1_max - y1_min)
    area2 = (x2_max - x2_min) * (y2_max - y2_min)
    union = area1 + area2 - inter_area

    return inter_area / union if union > 0 else 0


def main():
    invalid_labels = []
    count_violations = []
    overlap_violations = []
    zero_dim_violations = []
    total_shapes = 0
    total_files = 0

    for d in CHECK_DIRS:
        dir_path = BASE / d
        if not dir_path.exists():
            print(f"[SKIP] {d} not found")
            continue

        for f in sorted(dir_path.glob("*.json")):
            total_files += 1
            data = json.load(open(f, encoding="utf-8"))
            shapes = data.get("shapes", [])
            total_shapes += len(shapes)

            # 收集所有标注
            counter = Counter()
            boxes_by_label = {}  # label -> [([x1,y1],[x2,y2]), ...]
            for s in shapes:
                label = s.get("label", "")
                counter[label] += 1

                if label in VALID_LABELS and len(s.get("points", [])) == 2:
                    boxes_by_label.setdefault(label, []).append(s["points"])

            # --- 检查 1: 非法 label ---
            for s in shapes:
                label = s.get("label", "")
                if label not in VALID_LABELS:
                    invalid_labels.append(f"{d}/{f.name}: \"{label}\"")

            # --- 检查 2: 计数超限 ---
            for label, count in counter.items():
                if label in RED_FIVES and count > MAX_RED_FIVE:
                    count_violations.append(
                        f"{d}/{f.name}: \"{label}\" x{count} (red-five max {MAX_RED_FIVE})"
                    )
                elif label not in RED_FIVES and label in VALID_LABELS and count > MAX_NORMAL:
                    count_violations.append(
                        f"{d}/{f.name}: \"{label}\" x{count} (normal max {MAX_NORMAL})"
                    )

            # --- 检查 3: 过小标注框 ---
            for idx, s in enumerate(shapes):
                label = s.get("label", "")
                if label not in VALID_LABELS:
                    continue
                pts = s.get("points", [])
                if len(pts) == 2:
                    p1, p2 = pts[0], pts[1]
                    w = abs(p2[0] - p1[0])
                    h = abs(p2[1] - p1[1])
                    if w < 10 or h < 10:
                        zero_dim_violations.append(
                            f"{d}/{f.name}: \"{label}\" shape#{idx+1} w={w:.0f} h={h:.0f}"
                        )

            # --- 检查 4: 同 label 重叠框 ---
            for label, boxes in boxes_by_label.items():
                n = len(boxes)
                for i in range(n):
                    for j in range(i + 1, n):
                        iou = compute_iou(boxes[i], boxes[j])
                        if iou > IOU_THRESHOLD:
                            overlap_violations.append(
                                f"{d}/{f.name}: \"{label}\" box#{i+1} & box#{j+1} IoU={iou:.3f}"
                            )

    # =========================================================================
    # 输出结果
    # =========================================================================
    print(f"Scanned: {total_files} JSON files, {total_shapes} shapes")
    print()

    # 1
    print("=" * 60)
    print(f"1. Invalid labels ({len(invalid_labels)})")
    print("=" * 60)
    if invalid_labels:
        for item in invalid_labels:
            print(f"  {item}")
    else:
        print("  [OK] All labels valid")

    # 2
    print()
    print("=" * 60)
    print(f"2. Count violations ({len(count_violations)})")
    print("=" * 60)
    if count_violations:
        for item in count_violations:
            print(f"  {item}")
    else:
        print("  [OK] No count violations")

    # 3
    print()
    print("=" * 60)
    print(f"3. Small boxes (w<10 or h<10, {len(zero_dim_violations)})")
    print("=" * 60)
    if zero_dim_violations:
        for item in zero_dim_violations:
            print(f"  {item}")
    else:
        print("  [OK] No zero-dimension boxes")

    # 4
    print()
    print("=" * 60)
    print(f"4. Overlapping boxes (IoU > {IOU_THRESHOLD}, {len(overlap_violations)})")
    print("=" * 60)
    if overlap_violations:
        for item in overlap_violations:
            print(f"  {item}")
    else:
        print("  [OK] No overlapping boxes")


if __name__ == "__main__":
    main()
