using PinchOfTheft.CCTV;
using UnityEngine;

namespace MiniGames.Integration
{
    [DefaultExecutionOrder(-500)]
    public sealed class HouseInitialCameraAccess : MonoBehaviour
    {
        // До запуска управления камерами открывает только камеры 01, 02, 03 и 04 этой сцены.
        private void Awake()
        {
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var camera in root.GetComponentsInChildren<CCTVCamera>(true))
                {
                    bool available = camera.Group == 0 && camera.Slot >= 1 && camera.Slot <= 4;
                    camera.isAccessible = available;
                    camera.SetVisibleInMenu(available);
                }
        }
    }
}
