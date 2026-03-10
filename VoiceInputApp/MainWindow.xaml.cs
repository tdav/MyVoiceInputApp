using NAudio.Wave;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using VoiceInputApp.Services;

namespace VoiceInputApp
{
    public partial class MainWindow : Window
    {
        private AudioCaptureService? _audioService;
        private WhisperService? _whisperService;
        private KeyboardService? _keyboardService;
        private VadService? _vadService;

        private MemoryStream _chunkBuffer = new MemoryStream();
        private MemoryStream _sessionBuffer = new MemoryStream();
        private bool _isRecording = false;
        private int _silenceCounter = 0;
        private bool _isProcessing = false;
        
        private const int ChunkProcessingSilence = 3; 
        private const int FullStopSilence = 8;        

        private const int HOTKEY_ID = 9000;
        private const uint MOD_CONTROL = 0x0002;
        private const uint VK_SPACE = 0x20;

        [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")] public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        public MainWindow()
        {
            InitializeComponent();
            PositionWindow();

            string modelPath = @"C:\Works\Voice input in Russian\src\VoiceInputApp\model\ggml-large-v3-turbo-q8_0.bin";
            if (File.Exists(AppDomain.CurrentDomain.BaseDirectory + "model\\ggml-large-v3-turbo-q8_0.bin"))
                modelPath = AppDomain.CurrentDomain.BaseDirectory + "model\\ggml-large-v3-turbo-q8_0.bin";

            InitializeServices(modelPath);

            Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Recordings"));
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var helper = new WindowInteropHelper(this);
            var hwnd = helper.Handle;
            SetWindowLong(hwnd, -20, GetWindowLong(hwnd, -20) | 0x08000000);
            RegisterHotKey(hwnd, HOTKEY_ID, MOD_CONTROL, VK_SPACE);
            HwndSource.FromHwnd(hwnd).AddHook(HwndHook);
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == 0x0312 && wParam.ToInt32() == HOTKEY_ID) { ToggleCapture(); handled = true; }
            return IntPtr.Zero;
        }

        private void PositionWindow()
        {
            this.Left = (SystemParameters.WorkArea.Width - this.Width) / 2;
            this.Top = SystemParameters.WorkArea.Height - this.Height - 20;
        }

        private void InitializeServices(string modelPath)
        {
            try
            {
                _whisperService = new WhisperService(modelPath);
                _audioService = new AudioCaptureService();
                _keyboardService = new KeyboardService();
                _vadService = new VadService();
                _audioService.AudioDataAvailable += OnAudioDataAvailable;
            }
            catch (Exception) { StatusText.Text = "Error"; }
        }

        private void LanguageSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_whisperService != null && LanguageSelector.SelectedItem is ComboBoxItem item)
            {
                _whisperService.CurrentLanguage = item.Tag?.ToString() ?? "ru";

                _whisperService.ResetContext();
            }

        }

        private void ControlButton_Click(object sender, RoutedEventArgs e) => ToggleCapture();

        private void ToggleCapture()
        {
            if (!_isRecording) StartCapture();
            else StopCapture();
        }

        private void StartCapture()
        {
            if (_isRecording) return;
            _isRecording = true;
            _chunkBuffer = new MemoryStream();
            _sessionBuffer = new MemoryStream();
            _silenceCounter = 0;
            _audioService?.Start();
            
            Dispatcher.Invoke(() => {
                ControlButton.Tag = "Recording";
                StatusText.Text = "Listening...";
            });
        }

        private void StopCapture()
        {
            if (!_isRecording) return;
            _audioService?.Stop();
            _isRecording = false;
            
            Task.Run(async () => {
                SaveSessionToWav();
                await ProcessAndTypeAsync();
                Dispatcher.Invoke(() => {
                    StatusText.Text = "Ready";
                    ControlButton.Tag = "Idle";
                });
            });
        }

        private void SaveSessionToWav()
        {
            if (_sessionBuffer.Length == 0) return;
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Recordings", $"Recording_{timestamp}.wav");
            try
            {
                _sessionBuffer.Position = 0;
                using (var writer = new WaveFileWriter(fileName, new WaveFormat(16000, 16, 1)))
                {
                    _sessionBuffer.CopyTo(writer);
                }
            }
            catch (Exception) { }
        }

        private void OnAudioDataAvailable(byte[] data)
        {
            bool isSpeech = _vadService?.IsSpeech(data) ?? false;
            if (isSpeech)
            {
                _chunkBuffer.Write(data, 0, data.Length);
                _sessionBuffer.Write(data, 0, data.Length);
                _silenceCounter = 0;
                Dispatcher.Invoke(() => {
                    if (!_isProcessing) StatusText.Text = "Speaking...";
                });
            }
            else
            {
                _silenceCounter++;
                if (_silenceCounter == ChunkProcessingSilence && _chunkBuffer.Length > 0)
                {
                    Task.Run(ProcessAndTypeAsync);
                }
                
                if (_silenceCounter >= FullStopSilence) 
                {
                    Dispatcher.Invoke(StopCapture);
                }
                else if (!_isProcessing)
                {
                    Dispatcher.Invoke(() => StatusText.Text = "Silence...");
                }
            }
        }

        private async Task ProcessAndTypeAsync()
        {
            byte[] audioToProcess;
            lock (_chunkBuffer)
            {
                if (_chunkBuffer.Length == 0) return;
                audioToProcess = _chunkBuffer.ToArray();
                _chunkBuffer = new MemoryStream();
            }

            _isProcessing = true;
            Dispatcher.Invoke(() => {
                ControlButton.Tag = "Processing";
                StatusText.Text = "Typing...";
            });

            try
            {
                string? text = await _whisperService?.TranscribeAsync(audioToProcess)!;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    _keyboardService?.TypeText(text);
                }
            }
            catch (Exception) { }
            finally
            {
                _isProcessing = false;
                Dispatcher.Invoke(() => {
                    if (_isRecording)
                    {
                        ControlButton.Tag = "Recording";
                        StatusText.Text = "Listening...";
                    }
                    else
                    {
                        ControlButton.Tag = "Idle";
                        StatusText.Text = "Ready";
                    }
                });
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) try { this.DragMove(); } catch { }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => this.Close();

        protected override void OnClosed(EventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            UnregisterHotKey(hwnd, 9000);
            _audioService?.Dispose();
            _whisperService?.Dispose();
            _vadService?.Dispose();
            base.OnClosed(e);
        }
    }
}
