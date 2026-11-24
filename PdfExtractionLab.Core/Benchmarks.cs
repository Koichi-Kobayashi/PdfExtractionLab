using System.Diagnostics;

namespace PdfExtractionLab.Core;

public enum Stage
{
    Open,
    Text,
    Layout,
    Images,
    Total,
}

public enum InputSource
{
    Memory,
    File,
}

public enum Lifetime
{
    Reopen,
    Reuse,
}

public record Conditions(
    InputSource Source,
    Lifetime Lifetime,
    int Repeats = 100,
    int Workers = 1,
    bool Warmup = true
);

public record Measurement(
    string Engine,
    Stage Stage,
    int Success,
    int Failure,
    double? MeanMs,
    double? MedianMs,
    double? MinMs,
    double? MaxMs,
    double? P95Ms,
    double? StdDevMs,
    double? AllocatedBytes,
    long Gen0,
    long Gen1,
    long Gen2,
    string? Error
);

public static class BenchmarkWork
{
    public static IPdfSession Open(
        IPdfExtractionEngine engine,
        string path,
        byte[] data,
        InputSource source
    ) => source == InputSource.File ? engine.Open(path) : engine.Open(data);

    public static int Consume(
        IPdfSession doc,
        Stage stage,
        int start,
        int step,
        CancellationToken token
    )
    {
        int count = 0;
        for (int n = start; n <= doc.PageCount; n += step)
        {
            token.ThrowIfCancellationRequested();
            if (stage is Stage.Text or Stage.Total)
                count += doc.Text(n, token).Length;
            if (stage is Stage.Layout or Stage.Total)
                count += doc.Layout(n, token).Elements.Count;
            if (stage is Stage.Images or Stage.Total)
                count += doc.Images(n, token).Count;
        }
        return count;
    }

    public static IReadOnlyList<Measurement> Measure(
        IPdfExtractionEngine engine,
        string path,
        byte[] data,
        Conditions options,
        CancellationToken token,
        Action<Stage, int>? progress = null
    )
    {
        if (options.Repeats < 1 || options.Workers < 1)
            throw new ArgumentOutOfRangeException(nameof(options));
        List<Measurement> results = [];
        foreach (var stage in Enum.GetValues<Stage>())
        {
            var times = new List<double>();
            long allocated = 0;
            long[] collections = new long[3];
            string? error = null;
            int failures = 0;
            int workers = stage == Stage.Open ? 1 : options.Workers;
            IPdfSession[] sessions = new IPdfSession[workers];
            IPdfSession OpenOne() => Open(engine, path, data, options.Source);
            try
            {
                for (int i = options.Warmup ? -1 : 0; i < options.Repeats; i++)
                {
                    token.ThrowIfCancellationRequested();
                    bool timed = i >= 0;
                    bool includeOpen =
                        stage == Stage.Open
                        || (stage == Stage.Total && options.Lifetime == Lifetime.Reopen);
                    try
                    {
                        if (!includeOpen)
                            for (int j = 0; j < workers; j++)
                                sessions[j] ??= OpenOne();
                        long before =
                            workers == 1
                                ? GC.GetAllocatedBytesForCurrentThread()
                                : GC.GetTotalAllocatedBytes(true);
                        int g0 = GC.CollectionCount(0),
                            g1 = GC.CollectionCount(1),
                            g2 = GC.CollectionCount(2);
                        long start = Stopwatch.GetTimestamp();
                        if (includeOpen)
                            for (int j = 0; j < workers; j++)
                                sessions[j] = OpenOne();
                        if (stage == Stage.Open)
                        {
                            _ = sessions[0].PageCount;
                        }
                        else if (workers == 1)
                        {
                            Consume(sessions[0], stage, 1, 1, token);
                        }
                        else
                            Parallel.For(
                                0,
                                workers,
                                new ParallelOptions
                                {
                                    CancellationToken = token,
                                    MaxDegreeOfParallelism = workers,
                                },
                                j => Consume(sessions[j], stage, j + 1, workers, token)
                            );
                        var elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                        long bytes =
                            (
                                workers == 1
                                    ? GC.GetAllocatedBytesForCurrentThread()
                                    : GC.GetTotalAllocatedBytes(true)
                            ) - before;
                        if (timed)
                        {
                            collections[0] += GC.CollectionCount(0) - g0;
                            collections[1] += GC.CollectionCount(1) - g1;
                            collections[2] += GC.CollectionCount(2) - g2;
                            allocated += bytes;
                            times.Add(elapsed);
                        }
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        error = ex.GetType().Name;
                        if (timed)
                            failures++;
                        Close();
                    }
                    finally
                    {
                        if (options.Lifetime == Lifetime.Reopen || stage == Stage.Open)
                            Close();
                    }
                    if (timed)
                        progress?.Invoke(stage, i + 1);
                }
            }
            finally
            {
                Close();
            }
            times.Sort();
            int n = times.Count;
            double? mean = n == 0 ? null : times.Average();
            results.Add(
                new(
                    engine.Name,
                    stage,
                    n,
                    failures,
                    mean,
                    n == 0 ? null : (times[(n - 1) / 2] + times[n / 2]) / 2,
                    n == 0 ? null : times[0],
                    n == 0 ? null : times[^1],
                    n == 0 ? null : times[(int)Math.Ceiling(n * .95) - 1],
                    n <= 1
                        ? null
                        : Math.Sqrt(times.Sum(t => Math.Pow(t - mean!.Value, 2)) / (n - 1)),
                    n == 0 ? null : (double)allocated / n,
                    collections[0],
                    collections[1],
                    collections[2],
                    error
                )
            );
            void Close()
            {
                for (int j = 0; j < sessions.Length; j++)
                {
                    sessions[j]?.Dispose();
                    sessions[j] = null!;
                }
            }
        }
        return results;
    }
}
