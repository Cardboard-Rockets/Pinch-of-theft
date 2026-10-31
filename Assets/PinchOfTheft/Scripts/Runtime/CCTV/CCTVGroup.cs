using UnityEngine;
namespace PinchOfTheft.CCTV
{
    public sealed class CCTVGroup : MonoBehaviour
    {
        [SerializeField, Range(0,9), Tooltip("Номер группы камер")] private int group;
        [SerializeField, Tooltip("Доступность всей группы")] private bool accessible = true;
        private void OnValidate()
        {
            foreach (var camera in GetComponentsInChildren<CCTVCamera>(true)) camera.ConfigureGroup(group, accessible);
        }
    }
}
