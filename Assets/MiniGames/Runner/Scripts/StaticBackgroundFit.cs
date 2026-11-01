using UnityEngine;

namespace Runner
{
    public sealed class StaticBackgroundFit : MonoBehaviour
    {
        [SerializeField] private Camera worldCamera;
        [SerializeField] private SpriteRenderer background;

        // Назначает камеру и неподвижное изображение заднего плана.
        public void Configure(Camera camera)
        {
            worldCamera = camera;
            background = GetComponentInChildren<SpriteRenderer>();
        }

        // Подгоняет статичный фон под соотношение сторон окна без прокрутки и параллакса.
        private void LateUpdate()
        {
            float height = worldCamera.orthographicSize * 2f;
            Vector3 spriteSize = background.sprite.bounds.size;
            background.transform.localScale = new Vector3(height * worldCamera.aspect / spriteSize.x, height / spriteSize.y, 1f);
        }
    }
}
