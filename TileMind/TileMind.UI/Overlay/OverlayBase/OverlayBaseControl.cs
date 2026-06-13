using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Text;
using System.Windows;
using System.Windows.Media;
using TileMind.UI.Overlay.OverlayBase.DrawingCommand;

namespace TileMind.UI.Overlay.OverlayBase
{
    public abstract class OverlayBaseControl : UIElement
    {
        private readonly VisualCollection _visuals;
        private readonly Dictionary<DrawingInfo, DrawingVisual> _visualMap = new();
        private MatrixTransform _renderTransform = new(Matrix.Identity);

        // 依赖属性：数据源集合
        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(nameof(ItemsSource), typeof(ObservableCollection<DrawingInfo>), typeof(OverlayBaseControl),
                new FrameworkPropertyMetadata(null, OnItemsSourceChanged));

        public ObservableCollection<DrawingInfo> ItemsSource
        {
            get => (ObservableCollection<DrawingInfo>)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        // 统一控制覆盖层中所有填充画刷的不透明度
        public static readonly DependencyProperty FillOpacityProperty =
            DependencyProperty.Register(nameof(FillOpacity), typeof(double), typeof(OverlayBaseControl),
                new FrameworkPropertyMetadata(0.3, OnRedrawPropertyChanged));

        public double FillOpacity
        {
            get => (double)GetValue(FillOpacityProperty);
            set => SetValue(FillOpacityProperty, value);
        }

        public static readonly DependencyProperty StrokeThicknessProperty =
            DependencyProperty.Register(nameof(StrokeThickness), typeof(double), typeof(OverlayBaseControl),
                new FrameworkPropertyMetadata(2.0, OnRedrawPropertyChanged));

        public double StrokeThickness
        {
            get => (double)GetValue(StrokeThicknessProperty);
            set => SetValue(StrokeThicknessProperty, value);
        }

        private static void OnRedrawPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((OverlayBaseControl)d).RefreshAll();
        }

        protected OverlayBaseControl()
        {
            _visuals = new VisualCollection(this);
            //Background = Brushes.Transparent;
        }

        // 必须将内部 VisualCollection 暴露给 WPF 的视觉树遍历机制，否则绘制的 DrawingVisual 将不可见
        protected override int VisualChildrenCount => _visuals.Count;

        protected override Visual GetVisualChild(int index)
        {
            if (index < 0 || index >= _visuals.Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _visuals[index];
        }

        // 使覆盖层对鼠标事件透明。
        // 返回自身命中结果 → WPF 检查到 IsHitTestVisible=False（XAML 设置）后穿透到下层。
        protected override HitTestResult HitTestCore(PointHitTestParameters hitTestParameters)
        {
            return new PointHitTestResult(this, hitTestParameters.HitPoint);
        }

        protected override GeometryHitTestResult HitTestCore(GeometryHitTestParameters hitTestParameters)
        {
            return new GeometryHitTestResult(this, IntersectionDetail.FullyContains);
        }

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var overlay = (OverlayBaseControl)d;
            if (e.OldValue is ObservableCollection<DrawingInfo> oldCol)
            {
                oldCol.CollectionChanged -= overlay.OnCollectionChanged;
                foreach (var item in oldCol)
                    overlay.RemoveVisualForItem(item);
            }
            if (e.NewValue is ObservableCollection<DrawingInfo> newCol)
            {
                newCol.CollectionChanged += overlay.OnCollectionChanged;
                foreach (var item in newCol)
                    overlay.AddVisualForItem(item);
            }
        }

        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Reset:
                    // Clear() 触发的全量重置：先清旧 visual，再重新添加所有项
                    foreach (var item in _visualMap.Keys.ToList())
                        RemoveVisualForItem(item);
                    foreach (DrawingInfo item in (ObservableCollection<DrawingInfo>)sender!)
                        AddVisualForItem(item);
                    break;

                case NotifyCollectionChangedAction.Remove:
                case NotifyCollectionChangedAction.Replace:
                    if (e.OldItems != null)
                        foreach (DrawingInfo item in e.OldItems)
                            RemoveVisualForItem(item);
                    goto case NotifyCollectionChangedAction.Add;

                case NotifyCollectionChangedAction.Add:
                    if (e.NewItems != null)
                        foreach (DrawingInfo item in e.NewItems)
                            AddVisualForItem(item);
                    break;
            }
        }

        private void AddVisualForItem(DrawingInfo item)
        {
            var visual = new DrawingVisual();
            _visualMap[item] = visual;
            _visuals.Add(visual);
            RenderDrawingInfo(item);
        }

        private void RemoveVisualForItem(DrawingInfo item)
        {
            if (_visualMap.TryGetValue(item, out var visual))
            {
                _visuals.Remove(visual);
                _visualMap.Remove(item);
            }
        }

        /// <summary>
        /// 局部更新指定数据项（性能优化的核心入口）
        /// </summary>
        public void UpdateItemVisual(DrawingInfo item)
        {
            if (_visualMap.TryGetValue(item, out _))
                RenderDrawingInfo(item);
        }

        /// <summary>
        /// 刷新所有数据项的绘制
        /// </summary>
        public void RefreshAll()
        {
            foreach (var item in _visualMap.Keys)
                RenderDrawingInfo(item);
        }

        /// <summary>
        /// 设置从原始坐标系到控件坐标系的变换矩阵
        /// </summary>
        public void SetRenderTransform(Matrix matrix)
        {
            _renderTransform = new MatrixTransform(matrix);
            RefreshAll();
        }

        /// <summary>
        /// 抽象方法：由子类提供绘制时使用的填充画刷和描边画笔（可根据数据项动态决定）
        /// </summary>
        protected abstract (Brush fillBrush, Pen strokePen) GetDrawingStyles(DrawingInfo item);

        private void RenderDrawingInfo(DrawingInfo info)
        {
            if (!_visualMap.TryGetValue(info, out var visual))
                return;

            var (fillBrush, strokePen) = GetDrawingStyles(info);

            using (DrawingContext dc = visual.RenderOpen())
            {
                // 始终应用坐标系变换（屏幕像素 → WPF DIP）
                dc.PushTransform(_renderTransform);

                foreach (var cmd in info.DrawingCommands)
                {
                    cmd.Draw(dc, fillBrush, strokePen);
                }

                dc.Pop();
            }
        }
    }
}
