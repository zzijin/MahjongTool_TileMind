using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace TileMind.UI.Views
{
    /// <summary>
    /// QuadrilateralControl.xaml 的交互逻辑
    /// </summary>
    public partial class QuadrilateralControl : UserControl
    {
        // 四个顶点坐标（相对于父级Canvas）
        public static readonly DependencyProperty TopLeftProperty =
            DependencyProperty.Register(nameof(TopLeft), typeof(Point), typeof(QuadrilateralControl),
                new FrameworkPropertyMetadata(new Point(0, 0), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnVertexChanged));

        public static readonly DependencyProperty TopRightProperty =
            DependencyProperty.Register(nameof(TopRight), typeof(Point), typeof(QuadrilateralControl),
                new FrameworkPropertyMetadata(new Point(100, 0), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnVertexChanged));

        public static readonly DependencyProperty BottomLeftProperty =
            DependencyProperty.Register(nameof(BottomLeft), typeof(Point), typeof(QuadrilateralControl),
                new FrameworkPropertyMetadata(new Point(0, 100), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnVertexChanged));

        public static readonly DependencyProperty BottomRightProperty =
            DependencyProperty.Register(nameof(BottomRight), typeof(Point), typeof(QuadrilateralControl),
                new FrameworkPropertyMetadata(new Point(100, 100), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnVertexChanged));

        public Point TopLeft { get => (Point)GetValue(TopLeftProperty); set => SetValue(TopLeftProperty, value); }
        public Point TopRight { get => (Point)GetValue(TopRightProperty); set => SetValue(TopRightProperty, value); }
        public Point BottomLeft { get => (Point)GetValue(BottomLeftProperty); set => SetValue(BottomLeftProperty, value); }
        public Point BottomRight { get => (Point)GetValue(BottomRightProperty); set => SetValue(BottomRightProperty, value); }

        public event Action<QuadrilateralControl> VertexChanged;

        public QuadrilateralControl()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            UpdateVisual();
            PositionThumbs();
        }

        private static void OnVertexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (QuadrilateralControl)d;
            ctrl.UpdateVisual();
            ctrl.PositionThumbs();

            // 仅在控件已加载后才通知外部，避免 InitializeComponent 期间触发
            if (ctrl.IsLoaded)
                ctrl.VertexChanged?.Invoke(ctrl);
        }

        private void UpdateVisual()
        {
            var pathFigure = new PathFigure
            {
                StartPoint = TopLeft,
                Segments = new PathSegmentCollection
                {
                    new LineSegment(TopRight, true),
                    new LineSegment(BottomRight, true),
                    new LineSegment(BottomLeft, true)
                },
                IsClosed = true
            };
            var pathGeometry = new PathGeometry(new[] { pathFigure });
            FillPath.Data = pathGeometry;
        }

        private void PositionThumbs()
        {
            Canvas.SetLeft(TopLeftThumb, TopLeft.X - 6);
            Canvas.SetTop(TopLeftThumb, TopLeft.Y - 6);

            Canvas.SetLeft(TopRightThumb, TopRight.X - 6);
            Canvas.SetTop(TopRightThumb, TopRight.Y - 6);

            Canvas.SetLeft(BottomLeftThumb, BottomLeft.X - 6);
            Canvas.SetTop(BottomLeftThumb, BottomLeft.Y - 6);

            Canvas.SetLeft(BottomRightThumb, BottomRight.X - 6);
            Canvas.SetTop(BottomRightThumb, BottomRight.Y - 6);
        }

        private void OnThumbDragDelta(object sender, DragDeltaEventArgs e)
        {
            Thumb thumb = (Thumb)sender;
            //Point newPos;

            if (thumb == TopLeftThumb)
                TopLeft = new Point(TopLeft.X + e.HorizontalChange, TopLeft.Y + e.VerticalChange);
            else if (thumb == TopRightThumb)
                TopRight = new Point(TopRight.X + e.HorizontalChange, TopRight.Y + e.VerticalChange);
            else if (thumb == BottomLeftThumb)
                BottomLeft = new Point(BottomLeft.X + e.HorizontalChange, BottomLeft.Y + e.VerticalChange);
            else if (thumb == BottomRightThumb)
                BottomRight = new Point(BottomRight.X + e.HorizontalChange, BottomRight.Y + e.VerticalChange);
        }
    }
}
