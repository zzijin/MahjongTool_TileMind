using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TileMind.Common.Models;
using TileMind.Core.Services;
using TileMind.UI.Overlay;
using TileMind.UI.Overlay.OverlayBase;

namespace TileMind.UI.ViewModels;

public partial class OverlayWindowViewModel : ViewModel
{
    private readonly IServiceProvider _serviceProvider;
    private readonly MahjongTileCommandGenerator _commandGenerator = new();
    private readonly ILogger<OverlayWindowViewModel> _logger;

    private CancellationTokenSource? _cts;
    private IServiceScope? _pipelineScope;
    private Task? _pipelineTask;

    public ObservableCollection<DrawingInfo> OverlayItems { get; } = new();

    public OverlayWindowViewModel(FrameStateHub hub, IServiceProvider serviceProvider, ILogger<OverlayWindowViewModel> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        hub.FrameProcessed += OnFrameProcessed;
    }

    [RelayCommand]
    private void StartPipeline()
    {
        if (_pipelineTask != null) return;

        _cts = new CancellationTokenSource();
        _pipelineScope = _serviceProvider.CreateScope();
        var pipeline = _pipelineScope.ServiceProvider.GetRequiredService<GamePipelineService>();

        _pipelineTask = Task.Run(async () =>
        {
            _logger.LogInformation("流水线开始。");
            try
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    pipeline.ProcessFrame();
                    await Task.Delay(500, _cts.Token);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "流水线异常。");
            }
            finally
            {
                _logger.LogInformation("流水线停止。");
            }
        }, _cts.Token);
    }

    [RelayCommand]
    private void StopPipeline()
    {
        _cts?.Cancel();
        _pipelineTask?.Wait(TimeSpan.FromSeconds(5));
        _pipelineTask = null;
        _pipelineScope?.Dispose();
        _pipelineScope = null;
    }

    private void OnFrameProcessed(FrameDetections detections, List<MahjongAction> actions)
    {
        Application.Current.Dispatcher.BeginInvoke(() => RefreshOverlay(detections));
    }

    private void RefreshOverlay(FrameDetections detections)
    {
        OverlayItems.Clear();

        foreach (SeatPosition seat in Enum.GetValues<SeatPosition>())
        {
            if (detections.HandAndMeldDetections.TryGetValue(seat, out var handDets) && handDets.Count > 0)
            {
                var commands = handDets.SelectMany(d => _commandGenerator.GenerateCommands(d)).ToList();
                OverlayItems.Add(new PlayerTileDrawingInfo(seat, handDets, commands));
            }
        }

        foreach (SeatPosition seat in Enum.GetValues<SeatPosition>())
        {
            if (detections.DiscardPondDetections.TryGetValue(seat, out var pondDets) && pondDets.Count > 0)
            {
                var commands = pondDets.SelectMany(d => _commandGenerator.GenerateCommands(d)).ToList();
                OverlayItems.Add(new PlayerTileDrawingInfo(seat, pondDets, commands));
            }
        }
    }
}
