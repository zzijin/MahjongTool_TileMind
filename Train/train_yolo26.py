"""
train_yolo26.py — 基于 YOLO26 的麻将牌检测模型训练脚本

YOLO26 相比 YOLOv8 的关键改进：
  - 端到端检测头（end2end）：推理时无需 NMS 后处理，CPU 推理快 43%
  - DFL 替换为 L1 归一化距离回归损失
  - MuSGD 优化器（SGD + Muon 正交化更新），收敛更快
  - 支持 predict/val/export 时通过 end2end=True/False 切换检测头

参考文档：
  - Predict API: https://docs.ultralytics.com/zh/modes/predict
  - 训练配方:   https://docs.ultralytics.com/guides/yolo26-training-recipe

环境要求：pip install ultralytics>=8.3.0
"""

from ultralytics import YOLO

if __name__ == '__main__':
    # =========================================================================
    # 模型选择（首次运行自动下载预训练权重）
    #   YOLO26n  2.4M 参数, 40.9 mAP — 边缘设备 / 实时推理首选
    #   YOLO26s  9.5M 参数, 48.6 mAP — 速度与精度平衡
    #   YOLO26m  20.4M 参数, 53.1 mAP — 服务器端精度优先
    #   YOLO26l  24.8M 参数, 55.0 mAP — 复杂场景高精度
    #   YOLO26x  55.7M 参数, 57.5 mAP — 极致精度
    # =========================================================================
    model = YOLO('yolo26n.pt')

    # =========================================================================
    # 训练 — 参数对齐 YOLOv8 exp146（SGD + lr0=0.01 + cls=1.0）
    # 仅 close_mosaic=10 保留 YOLO26 配方
    # =========================================================================
    model.train(
        # --- 核心参数 ---
        data='mahjong_dataset/dataset.yaml',
        imgsz=1280,     # 高分辨率：麻将牌在截图中较小，1280 提升小目标检测精度
        batch=8,        # 梯度稳定性介于 batch=12(YOLOv8) 和 batch=4(之前) 之间

        # --- 优化器（对齐 YOLOv8 SGD 配置）---
        optimizer='SGD',      # 不用 auto：小数据集下 auto 会选 AdamW(lr=0.0002)，太慢
        lr0=0.01,             # SGD 标准学习率
        weight_decay=0.0005,  # SGD 标准权重衰减
        momentum=0.937,       # SGD 动量

        # --- 损失权重（对齐 YOLOv8，37 类需更高分类权重）---
        cls=1.0,              # 从 0.5 提高到 1.0，强调分类学习

        # --- 显存优化 ---
        amp=True,       # FP16 混合精度，加速训练并节省显存
        workers=8,      # 87 张图用 8 进程足够，过多反而容易卡住

        # --- 数据增强 ---
        close_mosaic=10,               # YOLO26 配方
        mixup=0.0,                     # 关闭 mixup
        copy_paste=0.0,                # 关闭 copy_paste

        # --- 训练控制 ---
        device=0,                       # GPU 编号
        epochs=300,                     # 对齐 YOLOv8 训练轮数
        patience=50,                    # 早停
        save=True,                      # 保存 checkpoints 和 best.pt
        project='mahjong_model',        # 输出根目录
        name='yolo26-exp4'              # SGD+lr0.01+cls1.0 对比实验
    )

    # =========================================================================
    # 验证（end2end=True 使用 NMS-Free 端到端检测头）
    # =========================================================================
    model.val(split='test', end2end=True)
