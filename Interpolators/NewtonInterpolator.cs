using InterpolationApp.Models;

namespace InterpolationApp.Interpolators;

/// <summary>
/// Інтерполяційний поліном Ньютона у формі поділених різниць.
/// Складність побудови: O(n²). Складність обчислення значення: O(n) (схема Горнера).
/// </summary>
public sealed class NewtonInterpolator : Interpolator
{
    /// <summary>Поділені різниці першого «ребра»: f[x0], f[x0,x1], f[x0,x1,x2], ...</summary>
    private double[] coefficients = Array.Empty<double>();

    public override string Name => "Поліном Ньютона";

    /// <summary>
    /// Будує таблицю поділених різниць та зберігає коефіцієнти полінома.
    /// </summary>
    public override void Build()
    {
        int n = points.Count;
        if (n < 2)
            throw new InvalidOperationException(
                "Для побудови полінома Ньютона потрібно щонайменше 2 точки.");

        // Таблиця поділених різниць: table[i] на старті = y_i,
        // на кожному кроці перетворюється in-place.
        // Коефіцієнти перші ребра:  f[x0], f[x0,x1], f[x0,x1,x2], ...
        var table = new double[n];
        for (int i = 0; i < n; i++)
            table[i] = points[i].Y;

        coefficients = new double[n];
        coefficients[0] = table[0];

        // Прохід по стовпчиках поділених різниць
        for (int j = 1; j < n; j++)
        {
            // Обчислення стовпчика j: знизу вгору, щоб не затерти попередні значення
            for (int i = n - 1; i >= j; i--)
            {
                double dx = points[i].X - points[i - j].X;
                if (Math.Abs(dx) < 1e-15)
                    throw new InvalidOperationException(
                        $"Збіг X-координат у вузлах {i - j} та {i}; поділена різниця не визначена.");
                table[i] = (table[i] - table[i - 1]) / dx;
            }
            coefficients[j] = table[j];
        }

        isBuilt = true;
    }

    /// <summary>
    /// Обчислення значення P(x) за схемою Горнера.
    /// P(x) = c0 + (x-x0)(c1 + (x-x1)(c2 + ... + (x-x_{n-2}) c_{n-1} ...))
    /// </summary>
    public override double Evaluate(double x)
    {
        if (!isBuilt) Build();

        int n = coefficients.Length;
        double result = coefficients[n - 1];
        for (int i = n - 2; i >= 0; i--)
            result = result * (x - points[i].X) + coefficients[i];

        return result;
    }

    /// <summary>
    /// Доступ до побудованих коефіцієнтів (для відображення формули в UI).
    /// </summary>
    public IReadOnlyList<double> GetCoefficients()
    {
        if (!isBuilt) Build();
        return coefficients;
    }

    /// <summary>
    /// Текстова форма полінома: "c0 + c1·(x − x0) + c2·(x − x0)(x − x1) + ..."
    /// </summary>
    public string GetPolynomialString()
    {
        if (!isBuilt) Build();

        var sb = new System.Text.StringBuilder();
        sb.Append(coefficients[0].ToString("F4", System.Globalization.CultureInfo.InvariantCulture));

        for (int i = 1; i < coefficients.Length; i++)
        {
            double c = coefficients[i];
            sb.Append(c >= 0 ? " + " : " − ");
            sb.Append(Math.Abs(c).ToString("F4", System.Globalization.CultureInfo.InvariantCulture));
            for (int k = 0; k < i; k++)
            {
                double xk = points[k].X;
                string sign = xk >= 0 ? "−" : "+";
                sb.Append($"·(x {sign} {Math.Abs(xk):F4})");
            }
        }

        return sb.ToString();
    }
}
