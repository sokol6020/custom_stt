using System;
using System.Collections.Generic;
using System.Linq;
using NAudio.Wave;

namespace customSTT.Services;

/// <summary>
/// Сервис для захвата аудио с микрофона через NAudio
/// </summary>
public class AudioCaptureService : IDisposable
{
    private WaveInEvent? _waveIn;
    private readonly List<byte> _audioBuffer = new();
    private readonly object _lock = new();
    private bool _isCapturing;

    /// <summary>
    /// Список доступных аудиоустройств
    /// </summary>
    public List<AudioDeviceInfo> AvailableAudioDevices { get; } = new();

    /// <summary>
    /// Текущее захваченное аудио (PCM 16-bit, 16kHz, mono)
    /// </summary>
    private readonly List<byte> _currentAudioData = new();

    public AudioCaptureService()
    {
        InitializeAudioDevices();
    }

    private void InitializeAudioDevices()
    {
        int deviceCount = WaveIn.DeviceCount;
        for (int i = 0; i < deviceCount; i++)
        {
            var caps = WaveIn.GetCapabilities(i);
            AvailableAudioDevices.Add(new AudioDeviceInfo
            {
                Name = caps.ProductName,
                Guid = i.ToString(),
                IsDefault = (i == 0)
            });
        }

        if (AvailableAudioDevices.Count == 0)
        {
            AvailableAudioDevices.Add(new AudioDeviceInfo
            {
                Name = "Default Microphone",
                Guid = "0",
                IsDefault = true
            });
        }
    }

    public List<string> GetAvailableAudioDevices()
    {
        return AvailableAudioDevices.Select(d => d.Name).ToList();
    }

    public int? FindDeviceIndexByName(string? deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            return null;

        var exact = AvailableAudioDevices.FindIndex(d =>
            d.Name.Equals(deviceName, StringComparison.OrdinalIgnoreCase));
        if (exact >= 0)
            return exact;

        var partial = AvailableAudioDevices.FindIndex(d =>
            d.Name.Contains(deviceName, StringComparison.OrdinalIgnoreCase) ||
            deviceName.Contains(d.Name, StringComparison.OrdinalIgnoreCase));
        return partial >= 0 ? partial : null;
    }

    public string? GetDeviceNameByIndex(int index)
    {
        if (index < 0 || index >= AvailableAudioDevices.Count)
            return null;

        return AvailableAudioDevices[index].Name;
    }

    public async Task StartCaptureAsync(string? deviceName = null, int? deviceIndex = null)
    {
        lock (_lock)
        {
            if (_isCapturing)
                StopCaptureSync();

            _currentAudioData.Clear();
            _audioBuffer.Clear();
        }

        int resolvedIndex = 0;
        if (deviceIndex is >= 0 and var idx && idx < WaveIn.DeviceCount)
        {
            resolvedIndex = idx;
        }
        else if (!string.IsNullOrEmpty(deviceName))
        {
            var found = FindDeviceIndexByName(deviceName);
            if (found is >= 0)
                resolvedIndex = found.Value;
        }

        if (resolvedIndex >= WaveIn.DeviceCount)
            throw new ArgumentException($"Устройство '{deviceName}' не найдено");

        _waveIn = new WaveInEvent
        {
            DeviceNumber = resolvedIndex,
            WaveFormat = new WaveFormat(16000, 16, 1),
            BufferMilliseconds = 100
        };

        _waveIn.DataAvailable += OnDataAvailable;
        _waveIn.StartRecording();
        _isCapturing = true;
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        lock (_lock)
        {
            _currentAudioData.AddRange(e.Buffer.AsSpan(0, e.BytesRecorded));
        }
    }

    public byte[] GetCapturedData()
    {
        lock (_lock)
        {
            return _currentAudioData.ToArray();
        }
    }

    public async Task<string> GetNextAudioChunkAsync()
    {
        byte[] data;
        lock (_lock)
        {
            data = _currentAudioData.ToArray();
            _currentAudioData.Clear();
        }

        if (data.Length == 0)
            return string.Empty;

        return ConvertPCMToString(data);
    }

    private string ConvertPCMToString(byte[] pcmData)
    {
        return $"PCM data: {pcmData.Length} bytes";
    }

    public async Task StopCaptureAsync()
    {
        StopCaptureSync();
        await Task.CompletedTask;
    }

    private void StopCaptureSync()
    {
        lock (_lock)
        {
            if (_waveIn != null)
            {
                _waveIn.DataAvailable -= OnDataAvailable;
                _waveIn.StopRecording();
                _waveIn.Dispose();
                _waveIn = null;
            }
            _isCapturing = false;
        }
    }

    public byte[] GetAllCapturedData()
    {
        lock (_lock)
        {
            return _currentAudioData.ToArray();
        }
    }

    public void ClearBuffer()
    {
        lock (_lock)
        {
            _currentAudioData.Clear();
        }
    }

    public void Dispose()
    {
        StopCaptureSync();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Информация об аудиоустройстве
/// </summary>
public class AudioDeviceInfo
{
    public string Name { get; set; } = string.Empty;
    public string Guid { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
}
