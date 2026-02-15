using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace AvaloniaSDR.UI.Diagnostics;

public readonly record struct FrameMetricsSnapshot(
    double CurrentFps,
    double FrameTimeMs,
    double MinFrameTimeMs,
    double MaxFrameTimeMs,
    double AvgFrameTimeMs,
    int FreezeCount);

public sealed class FrameMetrics
{
    private static readonly long s_frequency = Stopwatch.Frequency;
    private const double FreezeThresholdMs = 100.0;
    private const int RollingWindowSize = 128;
    private const double LogIntervalSeconds = 5.0;

    private readonly ILogger<FrameMetrics> _logger;
    private readonly object _syncRoot = new();

    private readonly Queue<long> _frameTicks = new(64);
    private readonly double[] _frameTimes = new double[RollingWindowSize];
    private int _frameTimeHead;
    private int _frameTimeCount;

    private long _lastFrameTick;
    private bool _started;
    private int _freezeCount;
    private long _lastLogTick;

    public FrameMetricsSnapshot Snapshot { get; private set; }

    public FrameMetrics(ILogger<FrameMetrics> logger)
    {
        _logger = logger;
    }

    public void RecordFrame()
    {
        long now = Stopwatch.GetTimestamp();

        lock (_syncRoot)
        {
            if (!_started)
            {
                _started = true;
                _lastFrameTick = now;
                _lastLogTick = now;
                Snapshot = default;
                return;
            }

            double elapsedMs = (now - _lastFrameTick) * 1000.0 / s_frequency;
            _lastFrameTick = now;

            _frameTimes[_frameTimeHead] = elapsedMs;
            _frameTimeHead = (_frameTimeHead + 1) % RollingWindowSize;
            if (_frameTimeCount < RollingWindowSize) _frameTimeCount++;

            double sum = 0, min = double.MaxValue, max = double.MinValue;
            for (int i = 0; i < _frameTimeCount; i++)
            {
                double v = _frameTimes[i];
                if (v < min) min = v;
                if (v > max) max = v;
                sum += v;
            }
            double avg = sum / _frameTimeCount;

            bool isFrozen = elapsedMs >= FreezeThresholdMs;
            if (isFrozen)
            {
                _freezeCount++;
                _logger.LogWarning(
                    "Frame freeze detected: {ElapsedMs:F1} ms (freeze #{Count})",
                    elapsedMs, _freezeCount);
            }

            long oneSecondTicks = s_frequency;
            _frameTicks.Enqueue(now);
            while (_frameTicks.Count > 0 && now - _frameTicks.Peek() > oneSecondTicks)
                _frameTicks.Dequeue();
            double fps = _frameTicks.Count;

            double secondsSinceLog = (now - _lastLogTick) / (double)s_frequency;
            if (secondsSinceLog >= LogIntervalSeconds)
            {
                _lastLogTick = now;
                _logger.LogInformation(
                    "FPS: {Fps:F1} | FrameTime avg/min/max: {Avg:F2}/{Min:F2}/{Max:F2} ms | Freezes: {Freezes}",
                    fps, avg, min, max, _freezeCount);
            }

            Snapshot = new FrameMetricsSnapshot(fps, elapsedMs, min, max, avg, _freezeCount);
        }
    }
}
