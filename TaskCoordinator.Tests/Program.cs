using TaskCoordinator;
var pass = 0;
var fail = 0;
var chet = 0;
void Check(bool ok, string n)
{
    if (ok)
    {
        pass++;
        chet++;
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(chet + ": PASS " + n);
    }
    else
    {
        fail++;
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("FAIL " + n);
    }
}
var s = new DiagnosticService();
var one = await s.RunAsync("Сеть", TimeSpan.FromMilliseconds(20));
Check(one.Status == DiagnosticStatus.Completed, "одна диагностика завершается");
var all = await s.RunAllAsync();
Check(
    all.Count == 3 && all.All(x => x.Status == DiagnosticStatus.Completed),
    "WhenAll ждёт три диагностики"
);
var first = await s.FirstFinishedAsync();
Check(first == "Быстрая", "WhenAny возвращает первую завершившуюся");

// Неверное имя
try
{
    await s.RunAsync("", TimeSpan.FromMilliseconds(10));
    Check(false, "пустое имя отклоняется");
}
catch (ArgumentException)
{
    Check(true, "пустое имя отклоняется");
}

// Отрицательное время
try
{
    await s.RunAsync("Сеть", TimeSpan.FromMilliseconds(-1));
    Check(false, "отрицательная длительность отклоняется");
}
catch (ArgumentOutOfRangeException)
{
    Check(true, "отрицательная длительность отклоняется");
}

// Кооперативная отмена
using (var cts = new CancellationTokenSource())
{
    var task = s.RunAsync("Долгая", TimeSpan.FromSeconds(5), cts.Token);
    await Task.Delay(50);
    cts.Cancel();
    try
    {
        await task;
        Check(false, "отмена прерывает диагностику");
    }
    catch (OperationCanceledException)
    {
        Check(task.IsCanceled, "отмена прерывает диагностику");
    }
}

// Отмена CPU-bound вычисления
using (var cts = new CancellationTokenSource())
{
    cts.Cancel();
    try
    {
        await s.RunCpuBoundAsync("CPU", 100_000_000, cts.Token);
        Check(false, "отмена прерывает CPU-bound вычисление");
    }
    catch (OperationCanceledException)
    {
        Check(true, "отмена прерывает CPU-bound вычисление");
    }
}

// Тайм-аут
try
{
    await s.RunWithTimeoutAsync("Тайм-аут", TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(100));
    Check(false, "тайм-аут даёт TimeoutException");
}
catch (TimeoutException)
{
    Check(true, "тайм-аут даёт TimeoutException");
}

// Внешняя отмена не должна превращаться в TimeoutException
using (var cts = new CancellationTokenSource())
{
    var task = s.RunWithTimeoutAsync("Внешняя", TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(3), cts.Token);
    await Task.Delay(50);
    cts.Cancel();
    try
    {
        await task;
        Check(false, "внешняя отмена отличается от тайм-аута");
    }
    catch (TimeoutException)
    {
        Check(false, "внешняя отмена отличается от тайм-аута");
    }
    catch (OperationCanceledException)
    {
        Check(true, "внешняя отмена отличается от тайм-аута");
    }
}
Console.ForegroundColor = ConsoleColor.DarkBlue;
Console.WriteLine($"Итого: {pass} пройдено, {fail} ошибок.");
Console.ResetColor();
return fail == 0 ? 0 : 1;
