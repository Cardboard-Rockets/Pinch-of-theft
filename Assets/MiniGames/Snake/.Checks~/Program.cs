using System;
using System.Collections.Generic;
using System.Reflection;
using Snake;

internal static class Program
{
    // Выполняет проверки правил змейки без запуска основного Unity-проекта.
    private static void Main()
    {
        var board = new SnakeBoard(28, 16, 123);
        Check(board.Body.Count == 3 && board.Progress == 0, "initial length");
        Check(new HashSet<int>(board.Symbols).Count == 3, "distinct symbols");
        foreach (int cell in board.Symbols) Check(!Contains(board.Body, cell), "food outside body");
        Check(!board.Turn(-1, 0), "reverse blocked");
        Check(!board.Turn(1, 0), "same direction does not consume turn");
        Check(board.Turn(0, -1), "perpendicular turn accepted");
        Check(!board.Turn(-1, 0), "rapid double turn cannot reverse into neck");
        board.Step();
        Check(board.State == SnakeState.Playing, "rapid input does not kill snake");
        Check(!board.Turn(0, 1), "reverse after step blocked");
        Check(board.Turn(-1, 0), "next perpendicular turn accepted after step");
        board.Reset();
        for (int symbol = 0; symbol < 3; symbol++)
        {
            int[] food = Food(board);
            food[symbol] = board.Body[0] + 1;
            board.Step();
            Check(board.Progress == symbol + 1, "ordered progress");
            Check(board.Body.Count == 4 + symbol, "growth");
        }
        Check(board.State == SnakeState.Won, "win");
        int winningHead = board.Body[0];
        board.Step();
        Check(board.Body[0] == winningHead, "freeze after win");
        board.Reset();
        Food(board)[0] = board.Body[0] + 1;
        board.Step();
        int length = board.Body.Count;
        Food(board)[2] = board.Body[0] + 1;
        board.Step();
        Check(board.Progress == 0 && board.LastMoveWasWrong, "wrong resets password");
        Check(board.Body.Count == length, "wrong preserves length");
        Check(board.State == SnakeState.Playing, "wrong does not stop game");
        Check(new HashSet<int>(board.Symbols).Count == 3, "all food respawned");
        foreach (int cell in board.Symbols) Check(cell >= 0 && !Contains(board.Body, cell), "respawn avoids snake");
        int headAfterError = board.Body[0];
        board.Step();
        Check(board.Body[0] == headAfterError + 1, "movement continues after wrong symbol");
        board.Reset();
        int[] symbols = Food(board);
        for (int index = 0; index < symbols.Length; index++) symbols[index] = index;
        for (int step = 0; step < board.Width; step++) board.Step();
        Check(board.State == SnakeState.Lost, "boundary loss");
        board.Reset();
        var body = (List<int>)typeof(SnakeBoard).GetField("body", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(board);
        body.Clear();
        body.AddRange(new[] { 100, 101, 129, 128, 127 });
        board.Step();
        Check(board.State == SnakeState.Lost, "self collision");
        board.Reset();
        body.Clear();
        body.AddRange(new[] {100, 128, 129, 101});
        foreach (int index in new[] {0, 1, 2}) Food(board)[index] = index;
        board.Step();
        Check(board.State == SnakeState.Playing && board.Body[0] == 101, "vacated tail allowed");
        Console.WriteLine("PASS: initial state, food placement, reverse, ordered collection, growth, victory, freeze, wrong symbol, length retention, full respawn, borders, self collision, vacated tail.");
    }

    // Получает позиции еды для детерминированной проверки конкретного сценария.
    private static int[] Food(SnakeBoard board)
    {
        return (int[])typeof(SnakeBoard).GetField("symbols", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(board);
    }

    // Проверяет наличие клетки в теле змейки.
    private static bool Contains(IReadOnlyList<int> cells, int cell)
    {
        for (int index = 0; index < cells.Count; index++) if (cells[index] == cell) return true;
        return false;
    }

    // Останавливает проверку при нарушении ожидаемого игрового правила.
    private static void Check(bool condition, string description)
    {
        if (!condition) throw new Exception("FAIL: " + description);
    }
}
