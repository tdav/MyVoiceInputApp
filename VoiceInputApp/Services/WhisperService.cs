using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Whisper.net;
using Whisper.net.Ggml;

namespace VoiceInputApp.Services
{
    public class WhisperService : IDisposable
    {
        private readonly WhisperFactory _factory;
        private readonly WhisperProcessor _processor;

        public WhisperService(string modelPath)
        {
            if (!File.Exists(modelPath))
                throw new FileNotFoundException("Whisper model file not found", modelPath);

            _factory = WhisperFactory.FromPath(modelPath);
            _processor = _factory.CreateBuilder()
                .WithLanguage("ru") // Принудительно русский язык
                .Build();
        }

        public async Task<string> TranscribeAsync(byte[] audioPcmData)
        {
            // Конвертируем 16-bit PCM (byte[]) в float[]
            var samples = ConvertPcmToFloat(audioPcmData);

            var result = new List<string>();
            await foreach (var segment in _processor.ProcessAsync(samples))
            {
                result.Add(segment.Text);
            }

            return string.Join(" ", result).Trim();
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
            _processor.Dispose();
            _factory.Dispose();
        }
    }
}
