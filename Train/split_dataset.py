"""
将 X-AnyLabeling 导出的两个数据集合并，随机划分为 train/val/test (80/10/10)。
- 源数据 label-01-export / label-02-export 中有重名文件，加前缀防冲突
- 标签已经是 0-indexed，直接复制
"""

import shutil
import random
from pathlib import Path

# =========================================================================
# 配置
# =========================================================================
SRC_DIRS = [
    Path(r"E:\Code\mahjong_tool\X-AnyLabeling\dataset\label-01-export"),
    Path(r"E:\Code\mahjong_tool\X-AnyLabeling\dataset\label-02-export"),
    Path(r"E:\Code\mahjong_tool\X-AnyLabeling\dataset\label-03-export"),
]
PREFIXES = ["01", "02", "03"]  # 与 SRC_DIRS 一一对应

DST_BASE = Path(r"E:\Code\mahjong_tool\Train\mahjong_dataset")

SPLIT_RATIO = (0.8, 0.1, 0.1)  # train / val / test
RANDOM_SEED = 42

# =========================================================================
# 1. 收集所有文件对
# =========================================================================
pairs = []  # [(src_img, src_label, dst_stem), ...]

for src_dir, prefix in zip(SRC_DIRS, PREFIXES):
    if not src_dir.exists():
        print(f"⚠️ 跳过不存在的目录: {src_dir}")
        continue

    for img_file in sorted(list(src_dir.glob("*.png")) + list(src_dir.glob("*.jpg"))):
        label_file = src_dir / f"{img_file.stem}.txt"
        if not label_file.exists():
            print(f"  skip (no label): {img_file.name}")
            continue

        # 加前缀防重名，如 01_0001
        new_stem = f"{prefix}_{img_file.stem}"
        pairs.append((img_file, label_file, new_stem))

print(f"收集到 {len(pairs)} 个图片-标注对")

# =========================================================================
# 2. 随机打乱
# =========================================================================
random.seed(RANDOM_SEED)
random.shuffle(pairs)

# =========================================================================
# 3. 按比例划分
# =========================================================================
n = len(pairs)
n_train = int(n * SPLIT_RATIO[0])
n_val = int(n * SPLIT_RATIO[1])
# test 取剩余

splits = {
    "train": pairs[:n_train],
    "val": pairs[n_train : n_train + n_val],
    "test": pairs[n_train + n_val :],
}

# =========================================================================
# 4. 清理目标目录并写入
# =========================================================================
for split_name in ["train", "val", "test"]:
    img_dir = DST_BASE / "images" / split_name
    lbl_dir = DST_BASE / "labels" / split_name
    img_dir.mkdir(parents=True, exist_ok=True)
    lbl_dir.mkdir(parents=True, exist_ok=True)

    # 清空旧文件
    for f in img_dir.iterdir():
        f.unlink()
    for f in lbl_dir.iterdir():
        f.unlink()

    for src_img, src_label, stem in splits[split_name]:
        # 复制图片（保留原始扩展名）
        dst_img = img_dir / f"{stem}{src_img.suffix}"
        shutil.copy2(src_img, dst_img)

        # 标签已经 0-indexed，直接复制
        dst_label = lbl_dir / f"{stem}.txt"
        with open(src_label, "r") as f_in, open(dst_label, "w") as f_out:
            for line in f_in:
                line = line.strip()
                if not line:
                    continue
                parts = line.split()
                cls_id = int(parts[0])
                if 0 <= cls_id <= 36:
                    f_out.write(" ".join(parts) + "\n")

    print(f"  {split_name}: {len(splits[split_name])} 对")

print(f"\nDone! Output: {DST_BASE}")
print(f"   Total {n} -> train:{n_train} / val:{n_val} / test:{n - n_train - n_val}")
