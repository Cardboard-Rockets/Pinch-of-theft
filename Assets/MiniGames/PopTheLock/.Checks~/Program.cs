using System;
using PopTheLock;
internal static class Program
{
// Проверяет правила, жизни и смену направления без запуска Unity.
public static void Main()
        {
            var round = new LockRound(8, 90f, 10f, 180f, 90f, 0.1f, 0.1f, 0.1f, 0.4f, 0.25f);
            int[] order = { 6, 2, 7, 0, 4, 1, 5, 3 };
            foreach (int index in order)
            {
                float distance = round.AngularSpeed > 0f
                    ? (round.LockAngle(index) - round.KeyAngle + 360f) % 360f
                    : (round.KeyAngle - round.LockAngle(index) + 360f) % 360f;
                round.Advance(distance / 90f);
                float previousSpeed = round.AngularSpeed;
                Require(round.Press(), "Не принят замок в произвольном порядке.");
                Require(round.AngularSpeed == -previousSpeed, "Попадание не меняет направление.");
                Require(!round.Press(), "Повторное нажатие во время притяжения принято.");
                Require(round.AngularSpeed == -previousSpeed, "Отклонённое нажатие меняет направление.");
                round.Advance(0.1f);
                Require(round.IsOpen(index) && round.InwardAmount == 1f, "Замок не открыт при достижении ключом.");
                round.Advance(0.21f);
            }
            Require(round.Won && round.OpenedCount == 8 && round.InwardAmount == 0f, "Победа наступила неверно.");
            Require(round.Lives == 3, "Правильные попадания отнимают жизни.");
            float finalAngle = round.KeyAngle;
            round.Advance(100f);
            Require(!round.Press() && round.KeyAngle == finalAngle, "Игра движется после победы.");
            var miss = new LockRound(8, 100f, 10f, 22.5f, 90f, 0.1f, 0.1f, 0.1f, 0.4f, 0.25f);
            Require(!miss.Press(), "Промах засчитан как попадание.");
            miss.Advance(0.2f);
            Require(Math.Abs(miss.InwardAmount - 0.25f) < 0.001f && Math.Abs(miss.KeyAngle - 2.5f) < 0.001f,
                "При промахе ключ не приближается или не вращается в обратную сторону.");
            miss.Advance(0.2f);
            Require(miss.Phase == KeyPhase.Orbit && miss.OpenedCount == 0 && miss.InwardAmount == 0f,
                "Промах остановил игру или открыл замок.");
            var wrap = new LockRound(8, 0f, 4f, 359f, 0f, 0.1f, 0.1f, 0.1f, 0.4f, 0.25f);
            Require(wrap.Press(), "Не работает попадание через границу углов 0/360.");
            wrap.Advance(1f);
            Require(wrap.OpenedCount == 1 && !wrap.Press(), "Открытый замок можно открыть повторно.");
            var lives = new LockRound(8, 0f, 10f, 22.5f, 90f, 0.1f, 0.1f, 0.1f, 0.4f, 0.25f);
            for (int expected = 2; expected >= 0; expected--)
            {
                Require(!lives.Press() && lives.Lives == expected, "Промах не отнимает ровно одну жизнь.");
                lives.Press();
                Require(lives.Lives == expected, "Нажатие во время анимации отнимает жизнь.");
                lives.Advance(0.4f);
                Require(lives.Lost == (expected == 0), "Поражение наступает не на последней жизни.");
            }
            float lostAngle = lives.KeyAngle;
            lives.Press();
            lives.Advance(5f);
            Require(lives.Lives == 0 && lives.KeyAngle == lostAngle, "Игра продолжается после поражения.");
            var customLives = new LockRound(8, 0f, 10f, 22.5f, 90f, 0.1f, 0.1f, 0.1f, 0.4f, 0.25f, 5);
            Require(customLives.Lives == 5, "Настраиваемое число жизней не применяется.");
            Console.WriteLine("PopTheLock lives PASS: default 3, custom 5, miss decrement, ignored input, loss, freeze, hits preserve lives.");
            Console.WriteLine("PopTheLock rules PASS: reverse on hit and miss, ignored input guard, arbitrary order, wraparound, already-open lock, victory freeze.");
        }


// Останавливает проверку при нарушении условия.
private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}