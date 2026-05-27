using SoundFlow.Abstracts;
using SoundFlow.Abstracts.Devices;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Components;
using SoundFlow.Enums;
using SoundFlow.Structs;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace PassthroughApp.ViewModels;

public class MainWindowViewModel : INotifyPropertyChanged, IDisposable
{
    private MiniAudioEngine? _engine;
    private AudioCaptureDevice? _capture;
    private AudioPlaybackDevice? _playback;
    private PassthroughComponent? _component;

    private bool _isRunning;
    private string _latencyText = "-";
    private string _statusText = "Waiting...";

    public bool IsRunning
    {
        get => _isRunning;
        set { _isRunning = value; OnPropertyChanged(); OnPropertyChanged(nameof(ButtonLabel)); }
    }
    public string LatencyText
    {
        get => _latencyText;
        set { _latencyText = value; OnPropertyChanged(); }
    }
    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }
    public string ButtonLabel => IsRunning ? "⏹  Stop" : "▶  Start";

    public ICommand ToggleCommand { get; }

    public MainWindowViewModel()
    {
        ToggleCommand = new RelayCommand(Toggle);
    }

    private void Toggle()
    {
        if (IsRunning) Stop();
        else Start();
    }

    private void Start()
    {
        try
        {
            _engine = new MiniAudioEngine();
            _engine.UpdateAudioDevicesInfo();

            var capInfo = _engine.CaptureDevices.FirstOrDefault(x => x.IsDefault);
            var pbInfo = _engine.PlaybackDevices.FirstOrDefault(x => x.IsDefault);

            _capture = _engine.InitializeCaptureDevice(capInfo, AudioFormat.DvdHq);
            _playback = _engine.InitializePlaybackDevice(pbInfo, AudioFormat.DvdHq);

            _component = new PassthroughComponent(_capture, AudioFormat.DvdHq);
            _component.LatencyMeasured += ms =>
                // Avalonia: Dispatcher.UIThread.Post(...)
                // WPF:      Application.Current.Dispatcher.Invoke(...)
                Application.Current.Dispatcher.Invoke(() => LatencyText = $"{ms} ms");

            _playback.MasterMixer.AddComponent(_component);
            _capture.Start();
            _playback.Start();

            IsRunning = true;
            StatusText = "Running — Mic → Headphone";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
    }

    private void Stop()
    {
        _capture?.Stop();
        _playback?.Stop();
        if (_playback != null && _component != null)
            _playback.MasterMixer.RemoveComponent(_component);

        _capture?.Dispose();
        _playback?.Dispose();
        _engine?.Dispose();

        _capture = null;
        _playback = null;
        _component = null;
        _engine = null;

        IsRunning = false;
        StatusText = "Paused";
        LatencyText = "-";
    }

    public void Dispose() => Stop();

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public class RelayCommand(Action execute) : ICommand
{
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? _) => true;
    public void Execute(object? _) => execute();
}

// ── PassthroughComponent ─────────────────────────────────────────
class PassthroughComponent : SoundComponent
{
    private readonly Queue<float> _queue = new();
    private readonly object _lock = new();

    private long _lastCapturedAt;

    // 버퍼 상한: 5ms (48000Hz × 0.005 = 240 samples)
    private const int MaxSamples = 240;

    private long _lastReportTick;
    private const long ReportIntervalMs = 200;

    public event Action<long>? LatencyMeasured;

    public PassthroughComponent(AudioCaptureDevice captureDevice, AudioFormat format)
        : base(captureDevice.Engine, format)
    {
        captureDevice.OnAudioProcessed += OnCaptured;
    }

    private void OnCaptured(Span<float> data, Capability capability)
    {
        long now = Stopwatch.GetTimestamp();
        lock (_lock)
        {
            if (_queue.Count + data.Length > MaxSamples)
                _queue.Clear();

            _lastCapturedAt = now;
            foreach (var s in data)
                _queue.Enqueue(s);
        }
    }

    protected override void GenerateAudio(Span<float> buffer, int channels)
    {
        lock (_lock)
        {
            for (int i = 0; i < buffer.Length; i++)
                buffer[i] = _queue.Count > 0 ? _queue.Dequeue() : 0f;

            if (_lastCapturedAt > 0)
            {
                long nowMs = Stopwatch.GetTimestamp() * 1000 / Stopwatch.Frequency;
                if (nowMs - _lastReportTick >= ReportIntervalMs)
                {
                    _lastReportTick = nowMs;
                    long capMs = _lastCapturedAt * 1000 / Stopwatch.Frequency;
                    LatencyMeasured?.Invoke(nowMs - capMs);
                }
            }
        }
    }
}
