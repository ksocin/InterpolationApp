using InterpolationApp.Models;

namespace InterpolationApp.Interpolators;

/// <summary>
/// Інтерполяція кубічними сплайнами дефекту 1 з природними крайовими умовами:
/// S''(x_0) = S''(x_n) = 0.
/// 
/// На кожному відрізку [x_i, x_{i+1}] сплайн має вигляд:
///   S_i(x) = a_i + b_i·(x - x_i) + c_i·(x - x_i)² + d_i·(x - x_i)³
/// 
/// Коефіцієнти c_i знаходяться методом прогонки (тридіагональна СЛАР, O(n)),
/// далі a_i, b_i, d_i — за явними формулами. Бінарний пошук сегмента → O(log n) на Evaluate.
/// </summary>
public sealed class SplineInterpolator : Interpolator
{
    private double[] a = Array.Empty<double>();
    private double[] b = Array.Empty<double>();
    private double[] c = Array.Empty<double>();
    private double[] d = Array.Empty<double>();
    private double[] h = Array.Empty<double>();   // h_i = x_{i+1} - x_i

    public override string Name => "Кубічний сплайн";

    public override void Build()
    {
        int n = points.Count - 1; // кількість відрізків (вузлів n+1, відрізків n)
        if (n < 1)
            throw new InvalidOperationException(
                "Для побудови сплайна потрібно щонайменше 2 точки.");

        // Вузли — у points[0..n], значення y_i = points[i].Y
        // a_i = y_i для i = 0..n-1 (на початку кожного сегмента сплайн дорівнює y_i)
        a = new double[n + 1];
        b = new double[n];
        c = new double[n + 1];
        d = new double[n];
        h = new double[n];

        for (int i = 0; i <= n; i++)
            a[i] = points[i].Y;

        for (int i = 0; i < n; i++)
        {
            h[i] = points[i + 1].X - points[i].X;
            if (h[i] <= 0)
                throw new InvalidOperationException(
                    $"Вузли мають бути впорядковані за зростанням X (порушено між {i} та {i+1}).");
        }

        // ====== Метод прогонки для коефіцієнтів c[1..n-1] ======
        // Тридіагональна СЛАР:
        //   h[i-1]·c[i-1] + 2(h[i-1] + h[i])·c[i] + h[i]·c[i+1] = 3·((a[i+1]-a[i])/h[i] - (a[i]-a[i-1])/h[i-1])
        // для i = 1..n-1.
        // Природні крайові умови: c[0] = c[n] = 0.

        var alpha = new double[n + 1];   // прогоночні коефіцієнти α_i (масив зміщений)
        var beta = new double[n + 1];    // β_i

        // Крайова умова зліва: c[0] = 0
        c[0] = 0;
        alpha[1] = 0;
        beta[1] = 0;

        // Прямий хід прогонки: i = 1..n-1
        for (int i = 1; i < n; i++)
        {
            double A = h[i - 1];
            double C = 2.0 * (h[i - 1] + h[i]);
            double B = h[i];
            double F = 3.0 * ((a[i + 1] - a[i]) / h[i] - (a[i] - a[i - 1]) / h[i - 1]);

            double denom = C + A * alpha[i];
            if (Math.Abs(denom) < 1e-15)
                throw new InvalidOperationException(
                    "Метод прогонки нестійкий: знаменник близький до нуля.");

            alpha[i + 1] = -B / denom;
            beta[i + 1] = (F - A * beta[i]) / denom;
        }

        // Крайова умова справа: c[n] = 0
        c[n] = 0;

        // Зворотний хід прогонки: i = n-1..1
        for (int i = n - 1; i >= 1; i--)
            c[i] = alpha[i + 1] * c[i + 1] + beta[i + 1];

        // ====== Явні формули для b_i та d_i ======
        for (int i = 0; i < n; i++)
        {
            b[i] = (a[i + 1] - a[i]) / h[i] - h[i] * (2.0 * c[i] + c[i + 1]) / 3.0;
            d[i] = (c[i + 1] - c[i]) / (3.0 * h[i]);
        }

        isBuilt = true;
    }

    /// <summary>
    /// Бінарний пошук індексу сегмента [x_i, x_{i+1}] такого, що x_i ≤ x ≤ x_{i+1}.
    /// O(log n).
    /// </summary>
    private int FindSegment(double x)
    {
        // Якщо x за межами — обрізаємо до крайнього сегмента (екстраполяція).
        if (x <= points[0].X) return 0;
        if (x >= points[^1].X) return points.Count - 2;

        int lo = 0, hi = points.Count - 1;
        while (hi - lo > 1)
        {
            int mid = (lo + hi) / 2;
            if (points[mid].X <= x) lo = mid;
            else hi = mid;
        }
        return lo;
    }

    public override double Evaluate(double x)
    {
        if (!isBuilt) Build();

        int i = FindSegment(x);
        double dx = x - points[i].X;
        return a[i] + dx * (b[i] + dx * (c[i] + dx * d[i]));
    }

    /// <summary>
    /// Доступ до коефіцієнтів сегментів — для виводу їх у текстову форму.
    /// </summary>
    public (double a, double b, double c, double d, double xStart) GetSegment(int i)
    {
        if (!isBuilt) Build();
        if (i < 0 || i >= h.Length)
            throw new ArgumentOutOfRangeException(nameof(i));
        return (a[i], b[i], c[i], d[i], points[i].X);
    }

    public int SegmentCount => h.Length;
}
