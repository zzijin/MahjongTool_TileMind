namespace TileMind.Common.Config;

/// <summary>
/// OCR 模型与字符字典的路径配置（可持久化到 JSON）。
/// </summary>
public class OcrOptions
{
    public const string SettingFilePath = @".\settings\ocrsettings.json";

    /// <summary>文字检测 ONNX 模型路径。</summary>
    public string DetModelPath { get; set; }
        = @"E:\Code\NumberRecognizer\ocr_service\models\det\PP-OCRv5_server_det\model.onnx";

    /// <summary>文字识别 ONNX 模型路径。</summary>
    public string RecModelPath { get; set; }
        = @"E:\Code\NumberRecognizer\ocr_service\models\rec\PP-OCRv5_server_rec\model.onnx";

    /// <summary>字符字典 JSON 路径。</summary>
    public string RecCharDictPath { get; set; }
        = @"E:\Code\NumberRecognizer\ocr_service\models\rec\PP-OCRv5_server_rec\char_dict.json";

    /// <summary>GPU 设备 ID（-1=CPU）。</summary>
    public int GpuDeviceId { get; set; } = 0;
}
