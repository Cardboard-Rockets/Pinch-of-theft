using PinchOfTheft.Core;
using UnityEngine;
using UnityEngine.Events;
namespace PinchOfTheft.Interaction
{
    public sealed class CodeLock : MonoBehaviour
    {
        [SerializeField] private CodePuzzle source;
        [SerializeField] private KeypadUI keypad;
        [SerializeField] private InteractableSphere lockInteractable;
        [SerializeField] private bool requirePower;
        [SerializeField] private bool powered;
        [SerializeField] private string successMessage = "Замок 2: код принят";
        [SerializeField] private bool logWrongAttempts;
        [SerializeField] private string wrongMessage = "Замок 2: неверный код";
        [SerializeField] private UnityEvent onSolved = new();
        [SerializeField] private UnityEvent onWrong = new();
        public bool IsSolved { get; private set; }
        public int CodeLength => source != null && source.HasCode ? source.Code.Length : 0;
        public bool HasPower => !requirePower || powered;
        public bool CanOpen => HasPower && source != null && source.HasCode && source.CodeVisible && !IsSolved;
        public void SetPowered(bool value){powered=value;RefreshAvailability();}
        private void OnEnable(){if(source!=null)source.CodeStateChanged+=RefreshAvailability;}
        private void OnDisable(){if(source!=null)source.CodeStateChanged-=RefreshAvailability;}
        private void RefreshAvailability(){if(lockInteractable!=null)lockInteractable.SetAvailable(CanOpen);}
        private void Start()
        {
            if(source==null){var sources=Object.FindObjectsByType<CodePuzzle>();if(sources.Length==1)source=sources[0];else{Debug.LogWarning("CodeLock: назначь единственный источник кода",this);enabled=false;return;}}
            if(lockInteractable==null)lockInteractable=GetComponent<InteractableSphere>();
            source.CodeStateChanged-=RefreshAvailability;source.CodeStateChanged+=RefreshAvailability;
            RefreshAvailability();
            if(onSolved.GetPersistentEventCount()==0)Debug.LogWarning("CodeLock: дверь не назначена в onSolved",this);
        }
        public void Open(){if(CanOpen&&keypad!=null&&GameContext.Instance!=null)keypad.Open(this);}
        public bool CheckCode(string input)
        {
            if(!CanOpen)return false;
            if(input!=source.Code){onWrong.Invoke();if(logWrongAttempts)Debug.Log(wrongMessage,this);if(keypad!=null)keypad.ShowError();return false;}
            IsSolved=true;Debug.Log(successMessage,this);onSolved.Invoke();if(keypad!=null)keypad.Close();if(lockInteractable!=null)lockInteractable.SetAvailable(false);return true;
        }
    }
}
