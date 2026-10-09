using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Rustychops;

public sealed record AudioDevice(string Id, string Name) { public override string ToString() => Name; }
public sealed record InputReading(double Decibels, bool Clipping, double? Frequency);

/// <summary>Native WASAPI capture. No network, browser, or live speaker monitoring.</summary>
public sealed class AudioInput : IDisposable
{
    private readonly object _gate = new();
    private readonly float[] _ring = new float[4096];
    private int _position, _count;
    private WasapiCapture? _capture;
    private MMDevice? _device;
    public int SessionVersion { get; private set; }
    public int Channel { get; set; }
    public int ChannelCount => _capture?.WaveFormat.Channels ?? 1;
    public bool Running => _capture != null;
    public event Action<string>? Stopped;

    public static IReadOnlyList<AudioDevice> Devices(DataFlow flow)
    {
        using var enumerator = new MMDeviceEnumerator();
        var devices = enumerator.EnumerateAudioEndPoints(flow, DeviceState.Active);
        var result = new List<AudioDevice>();
        foreach (var device in devices) { result.Add(new AudioDevice(device.ID, device.FriendlyName)); device.Dispose(); }
        return result;
    }

    public void Start(string? id)
    {
        Stop();
        using var enumerator = new MMDeviceEnumerator();
        _device = string.IsNullOrEmpty(id) ? enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia) : enumerator.GetDevice(id);
        try
        {
            var capture = new WasapiCapture(_device);
            _capture = capture;
            capture.DataAvailable += CaptureData;
            capture.RecordingStopped += (_, args) =>
            {
                if (ReferenceEquals(_capture, capture)) Stopped?.Invoke(args.Exception?.Message ?? "Input stopped or disconnected.");
            };
            capture.StartRecording();
        }
        catch { Stop(); throw; }
    }

    private void CaptureData(object? sender, WaveInEventArgs args)
    {
        if (sender is not WasapiCapture capture) return;
        var format = capture.WaveFormat;
        int bytes = format.BitsPerSample / 8;
        int channel = Math.Clamp(Channel, 0, format.Channels - 1);
        bool floating = format.Encoding == WaveFormatEncoding.IeeeFloat || format is WaveFormatExtensible extended && extended.SubFormat == new Guid("00000003-0000-0010-8000-00aa00389b71");
        lock (_gate)
        {
            for (int offset = channel * bytes; offset + bytes <= args.BytesRecorded; offset += format.BlockAlign)
            {
                float value = floating && bytes == 4 ? BitConverter.ToSingle(args.Buffer, offset) : bytes switch
                {
                    2 => BitConverter.ToInt16(args.Buffer, offset) / 32768f,
                    3 => ((args.Buffer[offset] | args.Buffer[offset + 1] << 8 | args.Buffer[offset + 2] << 16) << 8 >> 8) / 8388608f,
                    4 => BitConverter.ToInt32(args.Buffer, offset) / 2147483648f,
                    1 => (args.Buffer[offset] - 128) / 128f,
                    _ => 0
                };
                _ring[_position] = value;
                _position = (_position + 1) % _ring.Length;
                _count = Math.Min(_count + 1, _ring.Length);
            }
        }
    }

    public InputReading Read()
    {
        var samples = new float[4096];
        int count;
        lock (_gate)
        {
            count = _count;
            for (int i = 0; i < samples.Length; i++) samples[i] = _ring[(_position + i) % samples.Length];
        }
        double rms = Math.Sqrt(samples.Sum(value => (double)value * value) / samples.Length);
        return new InputReading(Math.Max(-60, 20 * Math.Log10(Math.Max(rms, .000001))), samples.Any(value => Math.Abs(value) >= .98), count == samples.Length && _capture != null ? PitchDetector.Detect(samples, _capture.WaveFormat.SampleRate) : null);
    }

    public void ResetChannel(int channel)
    {
        lock (_gate) { Channel = channel; _position = _count = 0; Array.Clear(_ring); }
    }

    public void Stop()
    {
        SessionVersion++;
        var capture = _capture; _capture = null;
        if (capture != null) { capture.DataAvailable -= CaptureData; capture.StopRecording(); capture.Dispose(); }
        _device?.Dispose(); _device = null;
        lock (_gate) { _position = _count = 0; Array.Clear(_ring); }
    }
    public void Dispose() { Stop(); }
}
