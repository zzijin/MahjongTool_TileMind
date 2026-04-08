using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using OpenCvSharp;
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using Device = SharpDX.Direct3D11.Device;
using MapFlags = SharpDX.Direct3D11.MapFlags;
using Resource = SharpDX.DXGI.Resource;

namespace TileMind.Vision.ScreenCapture
{

    /// <summary>
    /// 基于 SharpDX 和 DXGI 桌面复制 API 的高性能屏幕捕获服务。
    /// </summary>
    public class DxgiScreenCaptureService : IScreenCaptureService,IDisposable
    {
        private Factory1? _factory;
        private Adapter1? _adapter;
        private Device? _device;
        private Output? _output;
        private Output1? _output1;
        private OutputDuplication? _duplicatedOutput;
        private Texture2D? _stagingTexture;
        private bool _disposed;

        // 捕获参数
        private readonly int _adapterIndex;
        private readonly int _outputIndex;

        public DxgiScreenCaptureService(int adapterIndex = 0, int outputIndex = 0)
        {
            _adapterIndex = adapterIndex;
            _outputIndex = outputIndex;
            InitializeDxgi();
        }

        private void InitializeDxgi()
        {
            try
            {
                // 1. 创建 DXGI 工厂
                _factory = new Factory1();

                // 2. 获取指定的显卡适配器 (通常是独立显卡)
                _adapter = _factory.GetAdapter1(_adapterIndex);

                // 3. 创建设备 (Device)
                _device = new Device(_adapter);

                // 4. 获取显示器输出
                _output = _adapter.GetOutput(_outputIndex);
                _output1 = _output.QueryInterface<Output1>();

                // 5. 获取输出描述，获取尺寸
                var outputDesc = _output.Description;
                int width = outputDesc.DesktopBounds.Right - outputDesc.DesktopBounds.Left;
                int height = outputDesc.DesktopBounds.Bottom - outputDesc.DesktopBounds.Top;

                // 6. 创建用于 CPU 访问的暂存纹理 (Staging Texture)
                var textureDesc = new Texture2DDescription
                {
                    CpuAccessFlags = CpuAccessFlags.Read,
                    BindFlags = BindFlags.None,
                    Format = Format.B8G8R8A8_UNorm,
                    Width = width,
                    Height = height,
                    OptionFlags = ResourceOptionFlags.None,
                    MipLevels = 1,
                    ArraySize = 1,
                    SampleDescription = new SampleDescription(1, 0),
                    Usage = ResourceUsage.Staging
                };
                _stagingTexture = new Texture2D(_device, textureDesc);

                // 7. 创建桌面复制对象
                _duplicatedOutput = _output1.DuplicateOutput(_device);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("DXGI 初始化失败。请确保显卡驱动支持 DXGI 1.2+。", ex);
            }
        }

        public Mat? CaptureWindow()
        {
            return CaptureFrame();
        }

        /// <summary>
        /// 捕获一帧屏幕图像，返回 OpenCV Mat 对象。
        /// </summary>
        /// <param name="timeoutMs">超时时间(毫秒)</param>
        /// <returns>捕获到的图像 Mat，如果失败则返回 null。</returns>
        public Mat? CaptureFrame(int timeoutMs = 10)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(DxgiScreenCaptureService));
            if (_duplicatedOutput == null || _device == null || _stagingTexture == null)
                return null;

            try
            {
                OutputDuplicateFrameInformation frameInfo;
                Resource? screenResource = null;

                // 1. 尝试获取下一帧
                var result = _duplicatedOutput.TryAcquireNextFrame(timeoutMs, out frameInfo, out screenResource);

                if (result.Failure || screenResource == null)
                    return null;

                // 2. 将捕获的 GPU 资源转换为 Texture2D 并复制到暂存纹理
                using (var screenTexture = screenResource.QueryInterface<Texture2D>())
                {
                    _device.ImmediateContext.CopyResource(screenTexture, _stagingTexture);
                }

                // 3. 从暂存纹理映射数据到 CPU 内存
                var dataBox = _device.ImmediateContext.MapSubresource(
                    _stagingTexture, 0, MapMode.Read, MapFlags.None);

                // 4. 创建 OpenCV Mat 对象
                var width = _stagingTexture.Description.Width;
                var height = _stagingTexture.Description.Height;
                var mat = Mat.FromPixelData(height, width, MatType.CV_8UC4, dataBox.DataPointer);

                // 5. BGRA 格式转为 BGR (OpenCV 常用格式)
                var matBgr = new Mat();
                Cv2.CvtColor(mat, matBgr, ColorConversionCodes.BGRA2BGR);

                // 6. 解除映射并释放帧
                _device.ImmediateContext.UnmapSubresource(_stagingTexture, 0);
                _duplicatedOutput.ReleaseFrame();

                screenResource.Dispose();
                return matBgr;
            }
            catch (SharpDXException ex) when (ex.ResultCode == SharpDX.DXGI.ResultCode.WaitTimeout.Result)
            {
                // 超时，无新帧可用，正常情况
                return null;
            }
            catch
            {
                // 其他错误
                return null;
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _duplicatedOutput?.Dispose();
                _stagingTexture?.Dispose();
                _output1?.Dispose();
                _output?.Dispose();
                _device?.Dispose();
                _adapter?.Dispose();
                _factory?.Dispose();

                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }
    }
}