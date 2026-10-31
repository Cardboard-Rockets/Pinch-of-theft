using PinchOfTheft.CCTV;
using PinchOfTheft.Core;
using UnityEngine;
using UnityEngine.UI;
namespace PinchOfTheft.Interaction
{
    public sealed class KeyPrompt : MonoBehaviour
    {
        [SerializeField] private CameraManager cameraManager;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image fill;
        private Billboard billboard;
        private void Awake(){billboard=GetComponent<Billboard>();if(billboard==null)billboard=gameObject.AddComponent<Billboard>();SetHighlighted(false);}
        public void SetHighlighted(bool value){if(canvasGroup!=null)canvasGroup.alpha=value?1:0;if(billboard!=null)billboard.enabled=value;enabled=value;}
        public void SetProgress(float value){if(fill!=null)fill.fillAmount=Mathf.Clamp01(value);}
        public void Hide(){SetHighlighted(false);gameObject.SetActive(false);}
    }
}
