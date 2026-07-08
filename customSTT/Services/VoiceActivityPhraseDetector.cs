using System;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace customSTT.Services;

/// <summary>
/// Детектор фраз по паузам в речи (энергия RMS).
/// </summary>
public class VoiceActivityPhraseDetector
{
    private const int SampleRate = 16000;
    private const int BytesPerSample = 2;
    private const int BytesPerMs = SampleRate * BytesPerSample / 1000;

    private readonly int _silenceEndMs;
    private readonly int _minSpeechMs;
    private readonly double _speechThreshold;

    private readonly List<byte> _phraseBuffer = new();
    private bool _inSpeech;
    private int _speechMs;
    private int _silenceMs;

    public VoiceActivityPhraseDetector(
        int silenceEndMs = 900,
        int minSpeechMs = 400,
        double speechThreshold = 900)
    {
        _silenceEndMs = silenceEndMs;
        _minSpeechMs = minSpeechMs;
        _speechThreshold = speechThreshold;
    }

    public event Action<byte[]>? PhraseCompleted;

    public void Reset()
    {
        _phraseBuffer.Clear();
        _inSpeech = false;
        _speechMs = 0;
        _silenceMs = 0;
    }

    public void ProcessChunk(ReadOnlySpan<byte> chunk)
    {
        if (chunk.Length == 0)
            return;

        var chunkMs = chunk.Length / BytesPerMs;
        var rms = ComputeRms(chunk);
        var isSpeech = rms >= _speechThreshold;

        if (isSpeech)
        {
            if (!_inSpeech)
            {
                _inSpeech = true;
                _phraseBuffer.Clear();
                _silenceMs = 0;
            }

            AppendChunk(chunk);
            _speechMs += chunkMs;
            _silenceMs = 0;
            return;
        }

        if (!_inSpeech)
            return;

        AppendChunk(chunk);
        _silenceMs += chunkMs;

        if (_silenceMs < _silenceEndMs)
            return;

        CompletePhraseIfValid();
    }

    public byte[] Flush()
    {
        if (!_inSpeech || _speechMs < _minSpeechMs)
        {
            Reset();
            return Array.Empty<byte>();
        }

        var phrase = _phraseBuffer.ToArray();
        Reset();
        return phrase;
    }

    private void CompletePhraseIfValid()
    {
        if (_speechMs < _minSpeechMs)
        {
            Reset();
            return;
        }

        var phrase = _phraseBuffer.ToArray();
        Reset();
        PhraseCompleted?.Invoke(phrase);
    }

    private void AppendChunk(ReadOnlySpan<byte> chunk)
    {
        var evenLength = chunk.Length - chunk.Length % BytesPerSample;
        if (evenLength <= 0)
            return;

        _phraseBuffer.AddRange(chunk[..evenLength]);
    }

    private static double ComputeRms(ReadOnlySpan<byte> pcm)
    {
        if (pcm.Length < BytesPerSample)
            return 0;

        long sum = 0;
        var count = pcm.Length / BytesPerSample;
        for (var i = 0; i < count; i++)
        {
            var sample = BinaryPrimitives.ReadInt16LittleEndian(pcm.Slice(i * BytesPerSample, BytesPerSample));
            sum += (long)sample * sample;
        }

        return Math.Sqrt((double)sum / count);
    }
}
