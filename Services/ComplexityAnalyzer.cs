using System.Diagnostics;
using InterpolationApp.Interpolators;
using InterpolationApp.Models;

namespace InterpolationApp.Services;

/// <summary>
/// Замір часу побудови коефіцієнтів обох методів на наборах різного розміру.
/// Кожен замір повторюється N разів і повертається середнє значення —
/// для зменшення впливу випадкових коливань.
/// </summary>
public sealed class ComplexityAnalyzer
{
    public sealed record Result(int N, double NewtonMs, double SplineMs);

    public List<Result> Run(int[] sizes, int repeatsPerSize = 5)
    {
        if (sizes is null || sizes.Length == 0)
            throw new ArgumentException("Потрібен хоча б один розмір", nameof(sizes));

        var results = new List<Result>(sizes.Length);
        var rng = new Random(42); // фіксоване seed — для відтворюваності

        foreach (int n in sizes)
        {
            // Точки рівномірно розподілені на [-1, 1] для функції Рунге.
            // Беремо саме її як «складний» випадок, який і є цікавий для аналізу.
            var nodes = FunctionGenerator.Generate(SourceFunction.Runge, -1.0, 1.0, n);

            double newtonTotal = 0;
            double splineTotal = 0;

            // Прогрів — JIT, кеш — щоб перший запуск не псував статистики.
            WarmUp(nodes);

            for (int r = 0; r < repeatsPerSize; r++)
            {
                newtonTotal += MeasureBuild(new NewtonInterpolator(), nodes);
                splineTotal += MeasureBuild(new SplineInterpolator(), nodes);
            }

            results.Add(new Result(
                n,
                newtonTotal / repeatsPerSize,
                splineTotal / repeatsPerSize));
        }

        return results;
    }

    private static double MeasureBuild(Interpolator interpolator, IReadOnlyList<DataPoint> nodes)
    {
        foreach (var p in nodes) interpolator.AddPoint(p);

        var sw = Stopwatch.StartNew();
        interpolator.Build();
        sw.Stop();

        return sw.Elapsed.TotalMilliseconds;
    }

    private static void WarmUp(IReadOnlyList<DataPoint> nodes)
    {
        var n = new NewtonInterpolator();
        foreach (var p in nodes) n.AddPoint(p);
        n.Build();

        var s = new SplineInterpolator();
        foreach (var p in nodes) s.AddPoint(p);
        s.Build();
    }
}
