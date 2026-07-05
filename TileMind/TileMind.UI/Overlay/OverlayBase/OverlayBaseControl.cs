using System.Windows;
using System.Windows.Media;
using TileMind.UI.Overlay.OverlayBase.DrawingCommand;

namespace TileMind.UI.Overlay.OverlayBase;

public abstract class OverlayBaseControl : UIElement
{
    private readonly VisualCollection _visuals;
    private readonly DrawingVisual _visual = new();
    private MatrixTransform _renderTransform = new(Matrix.Identity);
    private IReadOnlyList<IDrawingCommand>? _commands;

    private Brush _fillBrush = Brushes.Transparent;
    private Pen _strokePen = new(Brushes.White, 2);

    public static readonly DependencyProperty FillOpacityProperty =
        DependencyProperty.Register(nameof(FillOpacity), typeof(double), typeof(OverlayBaseControl),
            new FrameworkPropertyMetadata(0.3, (d, _) => ((OverlayBaseControl)d).Render()));

    public double FillOpacity
    {
        get => (double)GetValue(FillOpacityProperty);
        set => SetValue(FillOpacityProperty, value);
    }

    public static readonly DependencyProperty StrokeThicknessProperty =
        DependencyProperty.Register(nameof(StrokeThickness), typeof(double), typeof(OverlayBaseControl),
            new FrameworkPropertyMetadata(2.0, (d, _) => ((OverlayBaseControl)d).Render()));

    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    protected OverlayBaseControl()
    {
        _visuals = new VisualCollection(this);
        _visuals.Add(_visual);
    }

    protected override int VisualChildrenCount => _visuals.Count;
    protected override Visual GetVisualChild(int index) => _visuals[index];

    protected override HitTestResult HitTestCore(PointHitTestParameters hitTestParameters)
        => new PointHitTestResult(this, hitTestParameters.HitPoint);

    protected override GeometryHitTestResult HitTestCore(GeometryHitTestParameters hitTestParameters)
        => new GeometryHitTestResult(this, IntersectionDetail.FullyContains);

    /// <summary>设置从截取坐标系到控件坐标系的变换矩阵。</summary>
    public void SetRenderTransform(Matrix matrix)
    {
        _renderTransform = new MatrixTransform(matrix);
        Render();
    }

    /// <summary>提交一帧绘制命令，全量重绘到单个 DrawingVisual。</summary>
    public void SubmitFrame(IReadOnlyList<IDrawingCommand> commands, Brush? defaultFill = null, Pen? defaultStroke = null)
    {
        _fillBrush = defaultFill ?? Brushes.Transparent;
        _strokePen = defaultStroke ?? new Pen(Brushes.White, 1);
        _commands = commands;
        Render();
    }

    private void Render()
    {
        using var dc = _visual.RenderOpen();
        dc.PushTransform(_renderTransform);

        if (_commands != null)
        {
            foreach (var cmd in _commands)
                cmd.Draw(dc, _fillBrush, _strokePen);
        }

        dc.Pop();
    }
}
