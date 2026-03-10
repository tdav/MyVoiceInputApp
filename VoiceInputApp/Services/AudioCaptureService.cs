using System;
using NAudio.Wave;

namespace VoiceInputApp.Services
{
    /// <summary>
    /// Продвинутый сервис захвата аудио с динамической обработкой (AGC, Noise Gate, Limiter).
    /// </summary>
    public class AudioCaptureService : IDisposable
    {
        private WaveInEvent? _waveIn;
        public event Action<byte[]>? AudioDataAvailable;

        // Настройки обработки
        private const float TargetPeak = 0.8f;      // Целевой пик громкости (80% от макс)
        private const float NoiseGateThreshold = 0.01f; // Порог шума (ниже этого - тишина)
        private float _currentGain = 2.0f;          // Начальное усиление

        public void Start()
        {
            if (_waveIn != null) return;

            _waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(16000, 16, 1),
                BufferMilliseconds = 200 // Меньшие буферы для более быстрой реакции
            };

            _waveIn.DataAvailable += OnDataAvailable;
            _waveIn.StartRecording();
        }

        public void Stop()
        {
            if (_waveIn == null) return;
            _waveIn.StopRecording();
            _waveIn.DataAvailable -= OnDataAvailable;
            _waveIn.Dispose();
            _waveIn = null;
        }

        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            if (e.BytesRecorded == 0) return;

            int sampleCount = e.BytesRecorded / 2;
            float[] floatSamples = new float[sampleCount];
            float maxPeak = 0;

            // 1. Конвертируем в float и находим пик для AGC
            for (int i = 0; i < sampleCount; i++)
            {
                short sample = BitConverter.ToInt16(e.Buffer, i * 2);
                float value = sample / 32768f;
                floatSamples[i] = value;
                
                float absValue = Math.Abs(value);
                if (absValue > maxPeak) maxPeak = absValue;
            }

            // 2. Dynamic AGC (Автоматическая регулировка)
            // Если звук есть, плавно подстраиваем усиление
            if (maxPeak > 0.001f)
            {
                float neededGain = TargetPeak / maxPeak;
                // Плавное изменение усиления, чтобы не было "скачков"
                _currentGain = _currentGain * 0.9f + neededGain * 0.1f;
                // Ограничиваем усиление сверху, чтобы не усиливать тишину до бесконечности
                if (_currentGain > 10.0f) _currentGain = 10.0f;
            }

            byte[] processedData = new byte[e.BytesRecorded];

            // 3. Применяем Noise Gate, Усиление и Лимитер
            for (int i = 0; i < sampleCount; i++)
            {
                float processed = floatSamples[i];

                // Noise Gate: если сигнал очень слабый - это фоновый шум
                if (maxPeak < NoiseGateThreshold)
                {
                    processed = 0;
                }
                else
                {
                    // Применяем текущее усиление
                    processed *= _currentGain;

                    // Peak Limiter: мягкое ограничение, чтобы не было клиппинга
                    if (processed > 1.0f) processed = 1.0f;
                    if (processed < -1.0f) processed = -1.0f;
                }

                // Конвертируем обратно в 16-bit PCM
                short outSample = (short)(processed * 32767);
                byte[] bytes = BitConverter.GetBytes(outSample);
                processedData[i * 2] = bytes[0];
                processedData[i * 2 + 1] = bytes[1];
            }

            AudioDataAvailable?.Invoke(processedData);
        }

        public void Dispose() => Stop();
    }
}
