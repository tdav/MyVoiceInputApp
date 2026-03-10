using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace VoiceInputApp.Services
{
    /// <summary>
    /// Сервис для обнаружения голоса (VAD).
    /// Пока модель ONNX не загружена, использует простой RMS-детектор.
    /// </summary>
    public class VadService : IDisposable
    {
        private InferenceSession? _session;
        private readonly float _threshold = 0.5f; // Порог вероятности голоса

        public VadService(string? modelPath = null)
        {
            if (!string.IsNullOrEmpty(modelPath) && System.IO.File.Exists(modelPath))
            {
                _session = new InferenceSession(modelPath);
            }
        }

        public bool IsSpeech(byte[] audioPcmData)
        {
            // Если ONNX сессия не инициализирована, используем RMS как заглушку
            if (_session == null)
            {
                return IsSpeechSimple(audioPcmData);
            }

            // TODO: Реализовать полноценный инференс Silero VAD (требует подготовки тензоров)
            return IsSpeechSimple(audioPcmData);
        }

        private bool IsSpeechSimple(byte[] audioPcmData)
        {
            // Простой расчет громкости (RMS)
            double sum = 0;
            for (int i = 0; i < audioPcmData.Length; i += 2)
            {
                short sample = BitConverter.ToInt16(audioPcmData, i);
                sum += (double)sample * sample;
            }
            double rms = Math.Sqrt(sum / (audioPcmData.Length / 2.0));
            
            // Порог громкости (эмпирически: 500-1000 для обычного микрофона)
            return rms > 500; 
        }

        public void Dispose()
        {
            _session?.Dispose();
        }
    }
}
