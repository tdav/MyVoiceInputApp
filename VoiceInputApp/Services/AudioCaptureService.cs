using System;
using NAudio.Wave;

namespace VoiceInputApp.Services
{
    /// <summary>
    /// Сервис для захвата аудио с микрофона (16 кГц, 16 бит, Моно).
    /// </summary>
    public class AudioCaptureService : IDisposable
    {
        private WaveInEvent? _waveIn;
        public event Action<byte[]>? AudioDataAvailable;

        public void Start()
        {
            if (_waveIn != null) return;

            _waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(16000, 16, 1),
                BufferMilliseconds = 500 // Чанки по 500 мс для VAD
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
            if (e.BytesRecorded > 0)
            {
                // Создаем копию буфера для безопасной передачи в другие потоки
                byte[] data = new byte[e.BytesRecorded];
                Array.Copy(e.Buffer, data, e.BytesRecorded);
                AudioDataAvailable?.Invoke(data);
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
