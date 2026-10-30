using PinchOfTheft.CCTV;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PinchOfTheft.Interaction
{
    public sealed class CodePuzzle : MonoBehaviour
    {
        [SerializeField] private InteractableSphere panelInteractable;
        [SerializeField] private KeypadUI keypad;
        [SerializeField] private Text tvText;
        [SerializeField, Range(1, 8)] private int codeLength = 4;
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
        public int CodeLength => Mathf.Clamp(codeLength, 1, 8);
        public bool CanOpen => code != null && !solved;
        private void Start()
        {
            if (panelInteractable != null) panelInteractable.SetAvailable(false);
            if (tvText != null) tvText.text = tvIdleText;
        }
        public void OnShieldActivated()
        {
            if (code != null) return;
            bool valid = fixedCode != null && fixedCode.Length == CodeLength;
            if (valid) foreach (char c in fixedCode) if (c < '0' || c > '9') valid = false;
            code = useFixedCode && valid ? fixedCode : "";
            if (code.Length == 0) for (int i = 0; i < CodeLength; i++) code += Random.Range(0, 10).ToString();
            if (tvText != null) tvText.text = code;
            if (panelInteractable != null) panelInteractable.SetAvailable(true);
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
            if (tvText != null) tvText.text = tvSolvedText;
            if (keypad != null) keypad.Close();
            if (panelInteractable != null) panelInteractable.SetAvailable(false);
            return true;
        }
    }
}
