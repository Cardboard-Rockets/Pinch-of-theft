using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PopTheLock
{
    public sealed class LivesView : MonoBehaviour
    {
        private const float IconSize = 80f;
        private const float IconGap = 8f;
        private readonly List<Image> icons = new List<Image>();
        private Sprite keySprite;

        // Назначает локальный спрайт ключа для отображения оставшихся жизней.
        public void Configure(Sprite sprite)
        {
            keySprite = sprite;
        }

        // Показывает справа сверху столько ключей, сколько жизней осталось у игрока.
        public void Render(int lives)
        {
            while (icons.Count < lives)
            {
                var item = new GameObject("Life Key " + (icons.Count + 1), typeof(RectTransform));
                item.transform.SetParent(transform, false);
                Image image = item.AddComponent<Image>();
                image.sprite = keySprite;
                image.preserveAspect = true;
                image.raycastTarget = false;
                RectTransform rect = image.rectTransform;
                rect.anchorMin = rect.anchorMax = Vector2.one;
                rect.pivot = Vector2.one;
                rect.sizeDelta = Vector2.one * IconSize;
                rect.anchoredPosition = new Vector2(-icons.Count * (IconSize + IconGap), 0f);
                icons.Add(image);
            }
            for (int index = 0; index < icons.Count; index++) icons[index].gameObject.SetActive(index < lives);
        }
    }
}
