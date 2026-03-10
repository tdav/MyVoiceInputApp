using System;
using Microsoft.ML.OnnxRuntime;

namespace VoiceInputApp.Services
{
    public class VadService : IDisposable
    {
        private InferenceSession? _session;

        public VadService(string? modelPath = null)
        {
            if (!string.IsNullOrEmpty(modelPath) && System.IO.File.Exists(modelPath))
            {
                _session = new InferenceSession(modelPath);
            }
        }

        public bool IsSpeech(byte[] audioPcmData)
        {
            if (_session == null)
            {
                return IsSpeechSimple(audioPcmData);
            }
            return IsSpeechSimple(audioPcmData);
        }

        private bool IsSpeechSimple(byte[] audioPcmData)
        {
            double sum = 0;
            for (int i = 0; i < audioPcmData.Length; i += 2)
            {
                short sample = BitConverter.ToInt16(audioPcmData, i);
                sum += (double)sample * sample;
            }
            double rms = Math.Sqrt(sum / (audioPcmData.Length / 2.0));
            
            // Сниженный порог для лучшей чувствительности
            return rms > 150; 
        }

        public void Dispose()
        {
            _session?.Dispose();
        }
    }
}
