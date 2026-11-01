using PinchOfTheft.CCTV;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PinchOfTheft.Interaction
{
    public sealed class KeypadUI : MonoBehaviour
    {
        [SerializeField] private CameraManager cameraManager;
        [SerializeField] private CameraMenuUI menu;
        [SerializeField] private CodePuzzle puzzle;
        [SerializeField] private GameObject panel;
        [SerializeField] private Text display;
        [SerializeField] private GameObject crosshair;
        [SerializeField] private GameObject cameraLabel;
        [SerializeField] private bool maskInput;
        [SerializeField] private bool allowKeyboardInput = true;
        [SerializeField, Min(0f)] private float errorShowTime = 1f;
        private CodeLock activeLock;
        private int InputLength => activeLock != null ? activeLock.CodeLength : puzzle != null ? puzzle.CodeLength : 0;
        private string input = "";
        private float errorUntil;
        public bool IsOpen { get; private set; }
        public void Open() => OpenContext(null);
        public void Open(CodeLock codeLock) { if (codeLock != null && codeLock.CanOpen) OpenContext(codeLock); }
        private void OpenContext(CodeLock codeLock)
        {
            if (PinchOfTheft.Core.GameContext.Instance == null) return;
            if (IsOpen || cameraManager == null || cameraManager.ActiveCamera == null || menu == null || menu.IsOpen || panel == null) return;
            if (codeLock == null && (puzzle == null || !puzzle.CanOpen)) return;
            activeLock = codeLock;
            IsOpen = true;
            var context = PinchOfTheft.Core.GameContext.Instance;
            context.InputLock.Acquire(this);
            context.CursorController.Unlock(this);
            context.Hud.Hide(this);
            input = ""; errorUntil = 0; panel.SetActive(true); Refresh();
        }
        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            activeLock = null;
            input = "";
            errorUntil = 0f;
            if (panel != null) panel.SetActive(false);
            var context = PinchOfTheft.Core.GameContext.Instance;
            if (context != null)
            {
                context.InputLock.Release(this);
                context.CursorController.Lock(this);
                context.Hud.Show(this);
            }
        }
        public void AddDigit(int digit)
        {
            if (!IsOpen || InputLength <= 0 || digit < 0 || digit > 9 || Time.unscaledTime < errorUntil || input.Length >= InputLength) return;
            input += digit.ToString(); Refresh();
        }
        public void Erase()
        {
            if (!IsOpen || input.Length == 0) return;
            input = input.Substring(0, input.Length - 1); Refresh();
        }
        public void Submit()
        {
            if (IsOpen && InputLength > 0 && input.Length == InputLength) { if (activeLock != null) activeLock.CheckCode(input); else puzzle.CheckCode(input); }
        }
        public void ShowError() { input = ""; errorUntil = Time.unscaledTime + errorShowTime; Refresh(); }
        private void Refresh()
        {
            if (display == null || InputLength <= 0) return;
            display.text = Time.unscaledTime < errorUntil ? "ERROR" :
                (maskInput ? new string('*', input.Length) : input).PadRight(InputLength, '_');
        }
        private void Update()
        {
            if (!IsOpen) return;
            if (panel == null || !panel.activeInHierarchy) { Close(); return; }
            if (errorUntil > 0 && Time.unscaledTime >= errorUntil) { errorUntil = 0; Refresh(); }
            var keyboard = Keyboard.current;
            if (!allowKeyboardInput || keyboard == null) return;
            if (keyboard.escapeKey.wasPressedThisFrame) { Close(); return; }
            for (int digit = 0; digit <= 9; digit++)
            {
                Key main = digit == 0 ? Key.Digit0 : (Key)((int)Key.Digit1 + digit - 1);
                Key num = digit == 0 ? Key.Numpad0 : (Key)((int)Key.Numpad1 + digit - 1);
                if (keyboard[main].wasPressedThisFrame || keyboard[num].wasPressedThisFrame) AddDigit(digit);
            }
            if (keyboard.backspaceKey.wasPressedThisFrame) Erase();
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) Submit();
        }
        private void OnDisable() { if (IsOpen) Close(); }
        private void Start() => PinchOfTheft.Core.GameContext.Resolve(this);
    }
}

