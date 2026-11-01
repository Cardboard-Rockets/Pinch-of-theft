using System;
using System.Collections.Generic;

namespace Snake
{
    public enum SnakeState { Playing, Lost, Won }

    public sealed class SnakeBoard
    {
        public const int PasswordLength = 3;
        public const int InitialLength = 3;
        private readonly Random random;
        private readonly List<int> body = new List<int>();
        private readonly int[] symbols = new int[PasswordLength];
        private int directionX = 1;
        private int directionY;
        private bool turnedSinceStep;
        public int Width { get; }
        public int Height { get; }
        public int Progress { get; private set; }
        public SnakeState State { get; private set; }
        public IReadOnlyList<int> Body => body;
        public IReadOnlyList<int> Symbols => symbols;
        public int DirectionX => directionX;
        public int DirectionY => directionY;
        public bool LastMoveWasWrong { get; private set; }

        // Создаёт независимую клеточную модель змейки с воспроизводимым генератором случайности.
        public SnakeBoard(int width, int height, int seed)
        {
            if (width < InitialLength + PasswordLength || height < InitialLength)
                throw new ArgumentOutOfRangeException(nameof(width), "Поле слишком маленькое.");
            Width = width;
            Height = height;
            random = new Random(seed);
            Reset();
        }

        // Начинает новую игру с головой и двумя круглыми сегментами тела.
        public void Reset()
        {
            body.Clear();
            int head = (Height / 2) * Width + Width / 2;
            for (int index = 0; index < InitialLength; index++) body.Add(head - index);
            directionX = 1;
            directionY = 0;
            turnedSinceStep = false;
            Progress = 0;
            State = SnakeState.Playing;
            LastMoveWasWrong = false;
            PlaceSymbols();
        }

        // Принимает только один поворот между шагами и запрещает движение в собственную шею.
        public bool Turn(int x, int y)
        {
            if (Math.Abs(x) + Math.Abs(y) != 1 || State != SnakeState.Playing) return false;
            if (turnedSinceStep || (x == directionX && y == directionY)) return false;
            if (x == -directionX && y == -directionY) return false;
            int nextX = body[0] % Width + x;
            int nextY = body[0] / Width + y;
            if (body.Count > 1 && nextX == body[1] % Width && nextY == body[1] / Width) return false;
            directionX = x;
            directionY = y;
            turnedSinceStep = true;
            return true;
        }

        // Выполняет один шаг, проверяет границы, тело и порядок сбора пароля.
        public void Step()
        {
            if (State != SnakeState.Playing) return;
            turnedSinceStep = false;
            LastMoveWasWrong = false;
            int x = body[0] % Width + directionX;
            int y = body[0] / Width + directionY;
            if (x < 0 || x >= Width || y < 0 || y >= Height)
            {
                State = SnakeState.Lost;
                return;
            }
            int next = y * Width + x;
            int eaten = Array.IndexOf(symbols, next);
            bool grows = eaten >= 0 && eaten == Progress;
            int occupiedCount = body.Count - (grows ? 0 : 1);
            for (int index = 0; index < occupiedCount; index++)
            {
                if (body[index] != next) continue;
                State = SnakeState.Lost;
                return;
            }
            body.Insert(0, next);
            if (!grows) body.RemoveAt(body.Count - 1);
            if (eaten < 0) return;
            if (grows)
            {
                symbols[eaten] = -1;
                Progress++;
                if (Progress == PasswordLength) State = SnakeState.Won;
            }
            else
            {
                ResetPassword();
            }
        }

        // Сбрасывает только пароль и разбрасывает символы, сохраняя движение и длину змейки.
        private void ResetPassword()
        {
            Progress = 0;
            LastMoveWasWrong = true;
            PlaceSymbols();
        }

        // Размещает все три символа в разных свободных клетках после начала игры или ошибки.
        private void PlaceSymbols()
        {
            var free = new List<int>();
            var occupied = new HashSet<int>(body);
            for (int cell = 0; cell < Width * Height; cell++)
                if (!occupied.Contains(cell)) free.Add(cell);
            if (free.Count < PasswordLength)
            {
                State = SnakeState.Lost;
                return;
            }
            for (int index = 0; index < symbols.Length; index++)
            {
                int selected = random.Next(free.Count);
                symbols[index] = free[selected];
                free.RemoveAt(selected);
            }
        }
    }
}
