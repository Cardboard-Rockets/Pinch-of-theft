# Актуальный код головоломки House
## Assets/PinchOfTheft/Scripts/Runtime/Interaction/DoorOpener.cs
```csharp
using UnityEngine;
using UnityEngine.Events;

namespace PinchOfTheft.Interaction
{
    public sealed class DoorOpener : MonoBehaviour
    {
        [SerializeField, Tooltip("Объект закрытой двери")] private GameObject closedDoor;
        [SerializeField, Tooltip("Объект открытой двери")] private GameObject openedDoor;
        [SerializeField] private UnityEvent onOpened = new();

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            if (closedDoor == null) closedDoor = gameObject;
            enabled = false;
        }

        public void Open()
        {
            if (IsOpen || closedDoor == null || openedDoor == null) return;
            IsOpen = true;
            openedDoor.SetActive(true);
            closedDoor.SetActive(false);
            enabled = false;
            onOpened.Invoke();
        }
    }
}

```
## Assets/PinchOfTheft/Scripts/Runtime/Puzzle/CodePuzzle.cs
```csharp
using PinchOfTheft.CCTV;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PinchOfTheft.Interaction
{
    public sealed class CodePuzzle : MonoBehaviour
    {
        [Header("Части головоломки")]
        [SerializeField, Tooltip("Панель становится доступной после щитка")] private InteractableSphere panelInteractable;
        [SerializeField] private KeypadUI keypad;
        [SerializeField] private Text tvText;
        [SerializeField, Tooltip("ТВ включается отдельной кнопкой после подачи энергии")] private bool separateTvActivation;
        [SerializeField] private InteractableSphere tvInteractable;
        [Header("Код и тексты")]
        [SerializeField, Range(1, 8), Tooltip("Число цифр в пароле")] private int codeLength = 4;
        [SerializeField] private bool useFixedCode;
        [SerializeField] private string fixedCode = "1234";
        [SerializeField] private string tvIdleText = "";
        [SerializeField] private string tvSolvedText = "OK";
        [SerializeField] private string successMessage = "Код принят: доступ разрешён";
        [SerializeField] private bool logWrongAttempts;
        [SerializeField] private string wrongMessage = "Неверный код";
        [SerializeField] private UnityEvent onSolved = new();
        [SerializeField] private UnityEvent onWrong = new();
        private string code;
        private bool solved;
        private bool tvOn;
        public bool HasPower { get; private set; }
        public int CodeLength => Mathf.Clamp(codeLength, 1, 8);
        public bool CanOpen => code != null && !solved;
        private void Start()
        {
            if (panelInteractable != null) panelInteractable.SetAvailable(false);
            if (tvInteractable != null) tvInteractable.SetAvailable(false);
            if (tvText != null) tvText.text = tvIdleText;
            bool hasDoor = false;
            for (int i = 0; i < onSolved.GetPersistentEventCount(); i++)
                if (onSolved.GetPersistentTarget(i) is DoorOpener || onSolved.GetPersistentTarget(i) is DoorController) hasDoor = true;
            if (!hasDoor) Debug.LogWarning("CodePuzzle: дверь не назначена в onSolved", this);
        }
        public void OnShieldActivated()
        {
            if (code != null) return;
            HasPower = true;
            bool valid = fixedCode != null && fixedCode.Length == CodeLength;
            if (valid) foreach (char c in fixedCode) if (c < '0' || c > '9') valid = false;
            code = useFixedCode && valid ? fixedCode : "";
            if (code.Length == 0) for (int i = 0; i < CodeLength; i++) code += Random.Range(0, 10).ToString();
            if (panelInteractable != null) panelInteractable.SetAvailable(true);
            if (separateTvActivation)
            {
                if (tvInteractable != null) tvInteractable.SetAvailable(true);
            }
            else ActivateTV();
        }
        public void ActivateTV()
        {
            if (!HasPower || tvOn) return;
            tvOn = true;
            if (tvText != null) tvText.text = solved ? tvSolvedText : code;
        }
        public bool CheckCode(string input)
        {
            if (!CanOpen) return false;
            if (input != code)
            {
                onWrong.Invoke();
                if (logWrongAttempts) Debug.Log(wrongMessage);
                if (keypad != null) keypad.ShowError();
                return false;
            }
            solved = true;
            Debug.Log(successMessage);
            onSolved.Invoke();
            if (tvText != null && tvOn) tvText.text = tvSolvedText;
            if (keypad != null) keypad.Close();
            if (panelInteractable != null) panelInteractable.SetAvailable(false);
            return true;
        }
    }
}

```
## Assets/PinchOfTheft/Scripts/Runtime/Puzzle/KeypadUI.cs
```csharp
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
        private string input = "";
        private float errorUntil;
        public bool IsOpen { get; private set; }
        public void Open()
        {
            if (PinchOfTheft.Core.GameContext.Instance == null) return;
            if (cameraManager == null || menu == null || menu.IsOpen || puzzle == null || !puzzle.CanOpen || panel == null) return;
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
            if (!IsOpen || puzzle == null || digit < 0 || digit > 9 || Time.unscaledTime < errorUntil || input.Length >= puzzle.CodeLength) return;
            input += digit.ToString(); Refresh();
        }
        public void Erase()
        {
            if (!IsOpen || input.Length == 0) return;
            input = input.Substring(0, input.Length - 1); Refresh();
        }
        public void Submit()
        {
            if (IsOpen && puzzle != null && input.Length == puzzle.CodeLength) puzzle.CheckCode(input);
        }
        public void ShowError() { input = ""; errorUntil = Time.unscaledTime + errorShowTime; Refresh(); }
        private void Refresh()
        {
            if (display == null || puzzle == null) return;
            display.text = Time.unscaledTime < errorUntil ? "ERROR" :
                (maskInput ? new string('*', input.Length) : input).PadRight(puzzle.CodeLength, '_');
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

```
## Assets/PinchOfTheft/Scripts/Runtime/Interaction/NoPowerPrompt.cs
```csharp
using PinchOfTheft.Core;
using UnityEngine;
namespace PinchOfTheft.Interaction
{
    public sealed class NoPowerPrompt : MonoBehaviour
    {
        [SerializeField] private CodePuzzle puzzle;
        [SerializeField] private GameObject popup;
        [SerializeField, Min(0f)] private float maxDistance = 15f;
        private GameContext context;
        private void Start()
        {
            context = GameContext.Instance;
            if (popup != null) popup.SetActive(false);
        }
        private void Update()
        {
            if (puzzle == null || popup == null) return;
            if (puzzle.HasPower)
            {
                popup.SetActive(false);
                enabled = false;
                return;
            }
            var probe = context != null ? context.AimProbe : null;
            bool aimed = probe != null && probe.HasHit && probe.Hit.distance <= maxDistance &&
                probe.Collider.GetComponentInParent<NoPowerPrompt>() == this;
            if (popup.activeSelf != aimed) popup.SetActive(aimed);
        }
        private void OnDisable() { if (popup != null) popup.SetActive(false); }
    }
}

```
