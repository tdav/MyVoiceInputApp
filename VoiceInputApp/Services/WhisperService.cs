using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Whisper.net;

namespace VoiceInputApp.Services
{
    public class WhisperService : IDisposable
    {
        private WhisperFactory? _factory;
        private string _lastContext = string.Empty;
        
        public string CurrentLanguage { get; set; } = "ru";

        public WhisperService(string modelPath)
        {
            if (!File.Exists(modelPath))
                throw new FileNotFoundException("Whisper model file not found", modelPath);

            InitializeFactory(modelPath);
        }

        private void InitializeFactory(string modelPath)
        {
            try
            {
                // Попытка инициализировать фабрику (Clblast рантайм подгрузится автоматически если доступен)
                _factory = WhisperFactory.FromPath(modelPath);
            }
            catch (Exception)
            {
                _factory = WhisperFactory.FromPath(modelPath);
            }
        }

        public void ResetContext()
        {
            _lastContext = string.Empty;
        }

        public async Task<string> TranscribeAsync(byte[] audioPcmData)
        {
            if (_factory == null || audioPcmData == null || audioPcmData.Length == 0) return string.Empty;

            var samples = ConvertPcmToFloat(audioPcmData);

            using var processor = _factory.CreateBuilder()
                .WithLanguage(CurrentLanguage)
                .WithThreads(Math.Max(1, Environment.ProcessorCount))
                .WithPrompt(_lastContext)
                .Build();

            var result = new List<string>();
            
            await foreach (var segment in processor.ProcessAsync(samples))
            {
                if (!string.IsNullOrWhiteSpace(segment.Text) && segment.Text.Trim().Length > 1)
                {
                    result.Add(segment.Text);
                }
            }

            string transcribedText = string.Join(" ", result).Trim();
            
            if (!string.IsNullOrEmpty(transcribedText))
            {
                _lastContext = transcribedText.Length > 100 
                    ? transcribedText.Substring(transcribedText.Length - 100) 
                    : transcribedText;
            }

            return transcribedText;
        }

        private float[] ConvertPcmToFloat(byte[] bytes)
        {
            var samples = new float[bytes.Length / 2];
            for (int i = 0; i < samples.Length; i++)
            {
                short sample = BitConverter.ToInt16(bytes, i * 2);
                samples[i] = sample / 32768f;
            }
            return samples;
        }

        public void Dispose()
        {
            _factory?.Dispose();
        }
    }
}
