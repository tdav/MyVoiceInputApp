using WindowsInput;

namespace VoiceInputApp.Services
{
    public class KeyboardService
    {
        private readonly InputSimulator _inputSimulator;

        public KeyboardService()
        {
            _inputSimulator = new InputSimulator();
        }

        public void TypeText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            // Добавляем пробел в конце для разделения фраз, как в ТЗ
            _inputSimulator.Keyboard.TextEntry(text + " ");
        }
    }
}
