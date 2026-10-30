using System.Diagnostics;

namespace TaskCoordinator;

public sealed class DiagnosticService
{
    public async Task<DiagnosticResult> RunAsync(string name, TimeSpan duration, CancellationToken token = default)
    {
        // TODO 1
        ValidateName(name);
        if (duration < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "Длительность не может быть отрицательной.");

        var sw = Stopwatch.StartNew();
        // TODO 2
        await Task.Delay(duration, token);
        sw.Stop();
        // TODO 3
        return new(name, DiagnosticStatus.Completed, $"Завершено за {sw.ElapsedMilliseconds} мс", sw.Elapsed);
    }

    public async Task<DiagnosticResult> RunCpuBoundAsync(string name, int iterations, CancellationToken token = default)
    {
        ValidateName(name);
        if (iterations < 0)
            throw new ArgumentOutOfRangeException(nameof(iterations), iterations, "Число итераций не может быть отрицательным.");

        // TODO 4
        var sw = Stopwatch.StartNew();
        long sum = await Task.Run(() =>
        {
            long s = 0;
            for (int i = 0; i < iterations; i++)
            {
                if (i % 1000 == 0)
                    token.ThrowIfCancellationRequested();
                s += i % 7;
            }
            return s;
        }, token);
        sw.Stop();
        return new(name, DiagnosticStatus.Completed, $"Сумма: {sum}", sw.Elapsed);
    }

    public async Task<IReadOnlyList<DiagnosticResult>> RunAllAsync(CancellationToken token = default)
    {
        var tasks = new[]
        {
            RunAsync("Сеть", TimeSpan.FromMilliseconds(600), token),
            RunAsync("Диск", TimeSpan.FromMilliseconds(900), token),
            RunAsync("База", TimeSpan.FromMilliseconds(700), token)
        };
        // TODO 5
        return await Task.WhenAll(tasks);
    }

    public async Task<DiagnosticResult> RunWithTimeoutAsync(string name, TimeSpan duration, TimeSpan timeout, CancellationToken token = default)
    {
        if (timeout < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "Тайм-аут не может быть отрицательным.");

        // TODO 6
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token);
        // TODO 7
        linked.CancelAfter(timeout);

        try
        {
            return await RunAsync(name, duration, linked.Token);
        }
        // TODO 8
        catch (OperationCanceledException ex) when (!token.IsCancellationRequested && linked.IsCancellationRequested)
        {
            throw new TimeoutException($"Диагностика «{name}» не уложилась в {timeout.TotalMilliseconds:F0} мс.", ex);
        }
    }

    public async Task<string> FirstFinishedAsync(CancellationToken token = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        var tasks = new[]
        {
            RunAsync("Быстрая", TimeSpan.FromMilliseconds(150), cts.Token),
            RunAsync("Средняя", TimeSpan.FromMilliseconds(350), cts.Token),
            RunAsync("Медленная", TimeSpan.FromMilliseconds(600), cts.Token)
        };
        // TODO 9
        var winner = await Task.WhenAny(tasks);
        var result = await winner;
        cts.Cancel();
        return result.Name;
    }

    // TODO 10
    public async Task<DiagnosticResult> RunSafeAsync(
        string name,
        Func<CancellationToken, Task<DiagnosticResult>> operation,
        IProgress<DiagnosticResult>? progress = null,
        CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        progress?.Report(new(name, DiagnosticStatus.Pending, "Ожидает запуска", TimeSpan.Zero));
        var sw = Stopwatch.StartNew();
        try
        {
            progress?.Report(new(name, DiagnosticStatus.Running, "Выполняется", TimeSpan.Zero));
            var result = await operation(token);
            progress?.Report(result);
            return result;
        }
        catch (OperationCanceledException ex)
        {
            var r = new DiagnosticResult(name, DiagnosticStatus.Cancelled, $"Отменено: {ex.Message}", sw.Elapsed);
            progress?.Report(r);
            return r;
        }
        catch (Exception ex)
        {
            var r = new DiagnosticResult(name, DiagnosticStatus.Failed, $"{ex.GetType().Name}: {ex.Message}", sw.Elapsed);
            progress?.Report(r);
            return r;
        }
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Имя диагностики не может быть пустым.", nameof(name));
    }
}
