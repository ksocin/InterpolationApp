using System.Globalization;

namespace InterpolationApp.Models;

/// <summary>
/// Точка з координатами X та Y у площині.
/// Незмінна (immutable) — потрібна для гарантії, що набір точок 
/// не зміниться під час обчислень інтерполятора.
/// </summary>
public sealed class DataPoint
{
    public double X { get; }
    public double Y { get; }

    public DataPoint(double x, double y)
    {
        if (double.IsNaN(x) || double.IsInfinity(x))
            throw new ArgumentException("X має бути скінченним числом", nameof(x));
        if (double.IsNaN(y) || double.IsInfinity(y))
            throw new ArgumentException("Y має бути скінченним числом", nameof(y));

        X = x;
        Y = y;
    }

    public override string ToString() =>
        $"({X.ToString("G6", CultureInfo.InvariantCulture)}; " +
        $"{Y.ToString("G6", CultureInfo.InvariantCulture)})";
}
