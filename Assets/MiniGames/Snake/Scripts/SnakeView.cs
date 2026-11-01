using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Snake
{
    public sealed class SnakeView : MonoBehaviour
    {
        [SerializeField] private Vector2 fieldTopLeft = new Vector2(-15.6f, 5.4f);
        [SerializeField, Min(0.01f)] private float cellSize = 0.8f;
        private const float HeadFrameDuration = 0.12f;
        private readonly List<SpriteRenderer> segments = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> food = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> slots = new List<SpriteRenderer>();
        private Sprite[] headFrames;
        private float[] headDurations;
        private Sprite bodySprite;
        private Sprite cover;
        private Sprite[] symbols;
        private Material material;
        private Text message;
        private Text status;
        private SnakeBoard board;
        private float animationTime;
        private int animationFrame;

        // Назначает локальные спрайты, материал и текстовые поля сцены.
        public void Configure(Sprite[] heads, float[] durations, Sprite body, Sprite square, Sprite[] letters,
            Material spriteMaterial, Text result, Text progress)
        {
            headFrames = heads;
            headDurations = durations;
            bodySprite = body;
            cover = square;
            symbols = letters;
            material = spriteMaterial;
            message = result;
            status = progress;
            for (int index = 0; index < SnakeBoard.PasswordLength; index++)
            {
                food.Add(CreateSprite("Symbol " + (index + 1), symbols[index], 3));
                SpriteRenderer slot = CreateSprite("Password slot " + (index + 1), cover, 4);
                slot.transform.localPosition = new Vector3(9.5f + index * 2.6f, 6f, 0f);
                Fit(slot, 2.2f);
                slots.Add(slot);
            }
        }

        // Обновляет сегменты, еду и открытые ячейки после шага клеточной модели.
        public void Render(SnakeBoard model)
        {
            board = model;
            while (segments.Count < board.Body.Count)
                segments.Add(CreateSprite("Segment " + segments.Count, bodySprite, 5));
            for (int index = 0; index < segments.Count; index++)
            {
                SpriteRenderer segment = segments[index];
                segment.gameObject.SetActive(index < board.Body.Count);
                if (index >= board.Body.Count) continue;
                segment.sprite = index == 0 ? headFrames[animationFrame] : bodySprite;
                segment.transform.localPosition = CellPosition(board.Body[index]);
                segment.transform.localRotation = index == 0
                    ? Quaternion.Euler(0f, 0f, Mathf.Atan2(-board.DirectionY, board.DirectionX) * Mathf.Rad2Deg + 90f)
                    : Quaternion.identity;
                Fit(segment, cellSize);
            }
            for (int index = 0; index < food.Count; index++)
            {
                food[index].gameObject.SetActive(board.Symbols[index] >= 0);
                if (board.Symbols[index] >= 0) food[index].transform.localPosition = CellPosition(board.Symbols[index]);
                Fit(food[index], cellSize);
                slots[index].sprite = index < board.Progress ? symbols[index] : cover;
                Fit(slots[index], 2.2f);
            }
            status.text = $"{board.Progress} / 3\nWASD to move";
            if (board.LastMoveWasWrong) status.text += "\nPassword reset";
            ShowMessage(board.State == SnakeState.Lost ? "Game over\nSpace or click to retry"
                : "");
            if (board.State == SnakeState.Won) status.text = "3 / 3";
        }

        // Воспроизводит исходные кадры головы без изменения её квадратных пропорций.
        private void Update()
        {
            if (board == null || board.State != SnakeState.Playing || segments.Count == 0) return;
            animationTime += Time.deltaTime;
            float duration = headDurations != null && animationFrame < headDurations.Length
                ? Mathf.Max(0.01f, headDurations[animationFrame]) : HeadFrameDuration;
            if (animationTime < duration) return;
            animationTime %= duration;
            animationFrame = (animationFrame + 1) % headFrames.Length;
            segments[0].sprite = headFrames[animationFrame];
        }

        // Показывает сообщение внутри поля без вмешательства в остальной проект.
        public void ShowMessage(string text)
        {
            message.text = text;
        }

        // Преобразует номер клетки в центр соответствующего квадрата игрового поля.
        private Vector3 CellPosition(int cell)
        {
            return new Vector3(fieldTopLeft.x + (cell % board.Width + 0.5f) * cellSize,
                fieldTopLeft.y - (cell / board.Width + 0.5f) * cellSize, 0f);
        }

        // Создаёт спрайт с локальным материалом и явным порядком отрисовки.
        private SpriteRenderer CreateSprite(string name, Sprite sprite, int order)
        {
            var item = new GameObject(name);
            item.transform.SetParent(transform, false);
            var renderer = item.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = material;
            renderer.sortingOrder = order;
            return renderer;
        }

        // Вписывает спрайт в квадрат равномерным масштабом с сохранением соотношения сторон.
        private static void Fit(SpriteRenderer renderer, float size)
        {
            Vector3 bounds = renderer.sprite.bounds.size;
            float scale = size / Mathf.Max(bounds.x, bounds.y);
            renderer.transform.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
