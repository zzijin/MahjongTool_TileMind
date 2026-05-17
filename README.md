# Mahjong Tool — 日麻 AI 辅助工具集

基于 YOLOv8 + ONNX Runtime 的实时日麻对局分析与 AI 辅助项目，包含模型训练、数据标注、屏幕识别与对局状态追踪。

## 项目目录

| 目录 | 说明 |
|------|------|
| **[TileMind](./TileMind/README.md)** | 主程序 — 屏幕捕获、YOLOv8 推理、对局状态追踪、WPF 叠加层显示 |
| **mahjong_dataset/** | YOLOv8 识别模型训练数据集（图片 + 标注） |
| **mahjong_model/** | 训练实验输出与导出的 ONNX 模型（exp144-m, exp145-s, exp146-n 等） |
| **mahjong_env/** | Python 训练环境（venv） |
| **X-AnyLabeling/** | [标注工具](https://github.com/CVHub520/X-AnyLabeling)，用于标注麻将牌数据集 |
| **runs/** | YOLO 训练运行日志与权重 |

## 模型训练

### 识别类型

共 37 种牌型类别：

| 类别 | 编号 |
|------|------|
| 万子 | 1m–9m, 0m（赤五万） |
| 索子 | 1s–9s, 0s（赤五索） |
| 筒子 | 1p–9p, 0p（赤五筒） |
| 字牌 | 1z–7z（东南西北白发中） |

### 基础模型

YOLOv8（n / s / m），使用 [ultralytics](https://github.com/ultralytics/ultralytics) 框架训练。

### 训练脚本

| 文件 | 用途 |
|------|------|
| `train.py` | 模型训练入口 |
| `export_csharp.py` | 导出 ONNX 模型（供 TileMind 推理使用） |
| `export_label.py` | 导出/转换标注数据 |

### 标注工具

使用 [X-AnyLabeling](https://github.com/CVHub520/X-AnyLabeling) 进行 YOLO 格式的麻将牌标注。

## TileMind 主程序

详见 **[TileMind/README.md](./TileMind/README.md)**，包含：
- 架构概览与模块说明
- 屏幕捕获 → YOLOv8 推理 → 区域路由 → 对局状态追踪 → UI 叠加层的完整流程
- 构建与运行指南
- 配置文件说明
