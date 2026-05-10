using InterpolationApp.Models;

namespace InterpolationApp.Services;

/// <summary>
/// Генерування набору точок з заданої функції-зразка на відрізку [a, b].
/// Точки рівновіддалені — це найгірший випадок для полінома Ньютона
/// (явище Рунге), і саме тому таке генерування корисне для демонстрації.
/// </summary>
public static class FunctionGenerator
{
    public static List<DataPoint> Generate(SourceFunction f, double a, double b, int count)
    {
        if (count < 2)
            throw new ArgumentException("Потрібно щонайменше 2 точки", nameof(count));
        if (a >= b)
            throw new ArgumentException("Має виконуватись a < b");

        Func<double, double> func = f switch
        {
            SourceFunction.Sin         => Math.Sin,
            SourceFunction.Cos         => Math.Cos,
            SourceFunction.Square      => x => x * x,
            SourceFunction.Runge       => x => 1.0 / (1.0 + 25.0 * x * x),
            SourceFunction.ExpNegative => x => Math.Exp(-x),
            _ => throw new ArgumentOutOfRangeException(nameof(f))
        };

        var result = new List<DataPoint>(count);
        double step = (b - a) / (count - 1);
        for (int i = 0; i < count; i++)
        {
            double x = a + i * step;
            // Захист від float drift в останній точці
            if (i == count - 1) x = b;
            result.Add(new DataPoint(x, func(x)));
        }
        return result;
    }

    public static string GetDisplayName(SourceFunction f) => f switch
    {
        SourceFunction.Sin         => "y = sin(x)",
        SourceFunction.Cos         => "y = cos(x)",
        SourceFunction.Square      => "y = x²",
        SourceFunction.Runge       => "y = 1 / (1 + 25·x²)",
        SourceFunction.ExpNegative => "y = exp(−x)",
        _ => f.ToString()
    };
}
