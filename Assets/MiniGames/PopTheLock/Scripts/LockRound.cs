using System;

namespace PopTheLock
{
    public enum KeyPhase { Orbit, Pulling, Holding, Returning, Missing, Finished, Lost }

    public sealed class LockRound
    {
        private const float FullCircle = 360f;
        private readonly bool[] opened;
        private float speed;
        private readonly float tolerance;
        private readonly float firstLockAngle;
        private readonly float pullDuration;
        private readonly float holdDuration;
        private readonly float returnDuration;
        private readonly float missDuration;
        private readonly float missDepth;
        private float elapsed;
        private float attackAngle;
        private int target = -1;
        public KeyPhase Phase { get; private set; } = KeyPhase.Orbit;
        public float KeyAngle { get; private set; }
        public float InwardAmount { get; private set; }
        public int OpenedCount { get; private set; }
        public int Count => opened.Length;
        public bool Won => Phase == KeyPhase.Finished;
        public float AngularSpeed => speed;
        public int Lives { get; private set; }
        public bool Lost => Phase == KeyPhase.Lost;
        public bool IsOver => Won || Lost;

        // Создаёт раунд с независимыми замками и настраиваемыми окнами попадания и анимациями.
        public LockRound(int count, float angularSpeed, float hitTolerance, float startAngle, float lockAngle,
            float pullTime, float holdTime, float returnTime, float missTime, float missFraction, int startingLives = 3)
        {
            if (count < 1 || pullTime <= 0f || holdTime <= 0f || returnTime <= 0f || missTime <= 0f)
                throw new ArgumentOutOfRangeException(nameof(count), "Число замков и длительности должны быть положительными.");
            opened = new bool[count];
            Lives = Math.Max(1, startingLives);
            speed = angularSpeed;
            tolerance = Math.Max(0f, Math.Min(hitTolerance, FullCircle / count * 0.49f));
            firstLockAngle = lockAngle;
            KeyAngle = Normalize(startAngle);
            pullDuration = pullTime;
            holdDuration = holdTime;
            returnDuration = returnTime;
            missDuration = missTime;
            missDepth = Math.Max(0f, Math.Min(1f, missFraction));
        }

        // Возвращает угол замка на внутренней окружности.
        public float LockAngle(int index)
        {
            return Normalize(firstLockAngle - index * FullCircle / Count);
        }

        // Сообщает, открыт ли замок с заданным индексом.
        public bool IsOpen(int index)
        {
            return opened[index];
        }

        // Проверяет попадание и меняет направление вращения при каждой принятой попытке.
        public bool Press()
        {
            if (Phase != KeyPhase.Orbit) return false;
            speed = -speed;
            target = -1;
            float nearest = float.PositiveInfinity;
            for (int index = 0; index < Count; index++)
            {
                if (opened[index]) continue;
                float distance = Math.Abs(Delta(KeyAngle, LockAngle(index)));
                if (distance > tolerance || distance >= nearest) continue;
                nearest = distance;
                target = index;
            }
            elapsed = 0f;
            attackAngle = KeyAngle;
            if (target < 0) Lives--;
            Phase = target >= 0 ? KeyPhase.Pulling : KeyPhase.Missing;
            return target >= 0;
        }

        // Продвигает вращение и анимации с сохранением оставшегося времени при смене фаз.
        public void Advance(float deltaTime)
        {
            if (deltaTime <= 0f || IsOver) return;
            while (deltaTime > 0f && !IsOver)
            {
                if (Phase == KeyPhase.Orbit)
                {
                    KeyAngle = Normalize(KeyAngle + speed * deltaTime);
                    return;
                }
                float duration = Duration();
                float consumed = Math.Min(deltaTime, duration - elapsed);
                elapsed += consumed;
                deltaTime -= consumed;
                float progress = Math.Min(1f, elapsed / duration);
                float smooth = progress * progress * (3f - 2f * progress);
                if (Phase == KeyPhase.Missing)
                {
                    KeyAngle = Normalize(KeyAngle + speed * consumed);
                    InwardAmount = missDepth * (float)Math.Sin(Math.PI * progress);
                }
                else if (Phase == KeyPhase.Pulling)
                {
                    KeyAngle = Normalize(attackAngle + Delta(attackAngle, LockAngle(target)) * smooth);
                    InwardAmount = smooth;
                }
                else if (Phase == KeyPhase.Returning) InwardAmount = 1f - smooth;
                if (elapsed >= duration) FinishPhase();
            }
        }

        // Возвращает продолжительность текущего этапа анимации.
        private float Duration()
        {
            switch (Phase)
            {
                case KeyPhase.Pulling: return pullDuration;
                case KeyPhase.Holding: return holdDuration;
                case KeyPhase.Returning: return returnDuration;
                default: return missDuration;
            }
        }

        // Открывает достигнутый замок и переводит ключ к следующему этапу или завершению игры.
        private void FinishPhase()
        {
            elapsed = 0f;
            switch (Phase)
            {
                case KeyPhase.Pulling:
                    opened[target] = true;
                    OpenedCount++;
                    InwardAmount = 1f;
                    Phase = KeyPhase.Holding;
                    break;
                case KeyPhase.Holding:
                    Phase = KeyPhase.Returning;
                    break;
                default:
                    InwardAmount = 0f;
                    Phase = Lives == 0 ? KeyPhase.Lost
                        : OpenedCount == Count ? KeyPhase.Finished : KeyPhase.Orbit;
                    break;
            }
        }

        // Приводит угол к диапазону от нуля до полного оборота.
        private static float Normalize(float angle)
        {
            return (angle % FullCircle + FullCircle) % FullCircle;
        }

        // Находит кратчайшую угловую разницу с корректным переходом через ноль.
        private static float Delta(float from, float to)
        {
            return Normalize(to - from + FullCircle * 0.5f) - FullCircle * 0.5f;
        }
    }
}
