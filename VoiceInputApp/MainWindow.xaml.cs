using System.IO;
using System.Windows;
using VoiceInputApp.Services;

namespace VoiceInputApp
{
    public partial class MainWindow : Window
    {
        private readonly AudioCaptureService audioService;
        private readonly WhisperService whisperService;
        private readonly KeyboardService keyboardService;
        private readonly VadService vadService;

        private MemoryStream audioBuffer = new MemoryStream();
        private bool isRecording = false;
        private int silenceCounter = 0;
        private const int MaxSilenceChunks = 3;

        public MainWindow()
        {
            InitializeComponent();
            PositionWindowBottomCenter();

            string modelPathж

            try
            {
                whisperService = new WhisperService(modelPath);
                audioService = new AudioCaptureService();
                keyboardService = new KeyboardService();
                vadService = new VadService();

                audioService.AudioDataAvailable += OnAudioDataAvailable;
                StatusText.Text = "Ready";
            }
            catch (Exception ex)
            {
                StatusText.Text = "Error: Model not found";
                MessageBox.Show($"Initialization failed: {ex.Message}");
            }
        }

        private void ControlButton_Click(object sender, RoutedEventArgs e)
        {
            if (!isRecording)
            {
                StartCapture();
            }
            else
            {
                StopCapture();
            }
        }

        private void StartCapture()
        {
            isRecording = true;
            audioBuffer = new MemoryStream();
            silenceCounter = 0;
            audioService.Start();
            
            // Визуальная индикация: меняем фон кнопки или иконки (можно добавить анимацию)
            StatusText.Text = "Listening...";
        }

        private void StopCapture()
        {
            audioService.Stop();
            isRecording = false;
            StatusText.Text = "Processing...";
            Task.Run(ProcessAndTypeAsync);
        }

        private void OnAudioDataAvailable(byte[] data)
        {
            bool isSpeech = vadService.IsSpeech(data);

            if (isSpeech)
            {
                audioBuffer.Write(data, 0, data.Length);
                silenceCounter = 0;
            }
            else
            {
                silenceCounter++;
                if (silenceCounter >= MaxSilenceChunks && audioBuffer.Length > 0)
                {
                    Task.Run(ProcessAndTypeAsync);
                }
            }
        }

        private async Task ProcessAndTypeAsync()
        {
            byte[] audioToProcess;
            lock (audioBuffer)
            {
                if (audioBuffer.Length == 0) return;
                audioToProcess = audioBuffer.ToArray();
                audioBuffer = new MemoryStream();
                silenceCounter = 0;
            }

            try
            {
                string text = await whisperService.TranscribeAsync(audioToProcess);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    keyboardService.TypeText(text);
                }
            }
            catch (Exception) { /* Log error */ }
        }

        private void PositionWindowBottomCenter()
        {
            var workArea = SystemParameters.WorkArea;
            Left = workArea.Left + (workArea.Width - Width) / 2;
            Top = workArea.Bottom - Height - 20;
        }

     

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            audioService?.Dispose();
            whisperService?.Dispose();
            vadService?.Dispose();
            base.OnClosed(e);
        }
    }
}
