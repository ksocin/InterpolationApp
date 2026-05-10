using InterpolationApp.Models;

namespace InterpolationApp.Interpolators;

/// <summary>
/// Абстрактний базовий клас для інтерполяторів.
/// Інкапсулює спільну поведінку: зберігання точок, відсортованих за X,
/// та відстеження стану побудови коефіцієнтів.
/// </summary>
public abstract class Interpolator
{
    protected readonly List<DataPoint> points = new();
    protected bool isBuilt;

    /// <summary>Кількість вузлів інтерполяції.</summary>
    public int Count => points.Count;

    /// <summary>Чи побудовані коефіцієнти інтерполянти (готові до Evaluate).</summary>
    public bool IsBuilt => isBuilt;

    /// <summary>Назва методу для відображення в UI.</summary>
    public abstract string Name { get; }

    /// <summary>Додавання точки. Точки зберігаються відсортованими за X.</summary>
    public void AddPoint(DataPoint p)
    {
        ArgumentNullException.ThrowIfNull(p);

        // Перевірка унікальності X — без неї всі методи інтерполяції розваляться
        // (поділені різниці будуть ділити на нуль; сплайн зведе h_i до нуля).
        foreach (var existing in points)
        {
            if (Math.Abs(existing.X - p.X) < 1e-12)
                throw new InvalidOperationException(
                    $"Точка з X={p.X} вже існує. Координата X має бути унікальною.");
        }

        // Вставка зі збереженням сортування за X
        int index = points.FindIndex(q => q.X > p.X);
        if (index < 0) points.Add(p);
        else points.Insert(index, p);

        isBuilt = false;
    }

    /// <summary>Видалення точки за індексом.</summary>
    public void RemovePoint(int index)
    {
        if (index < 0 || index >= points.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        points.RemoveAt(index);
        isBuilt = false;
    }

    /// <summary>Очищення всіх точок.</summary>
    public void Clear()
    {
        points.Clear();
        isBuilt = false;
    }

    /// <summary>Копія списку точок (зовнішній код не може змінити внутрішній стан).</summary>
    public IReadOnlyList<DataPoint> GetPoints() => points.AsReadOnly();

    /// <summary>
    /// Побудова коефіцієнтів інтерполянти. 
    /// Реалізується конкретним методом у кожному нащадку.
    /// </summary>
    public abstract void Build();

    /// <summary>
    /// Обчислення значення інтерполянти у точці x.
    /// Якщо коефіцієнти ще не побудовані — будує їх автоматично.
    /// </summary>
    public abstract double Evaluate(double x);

    /// <summary>
    /// Перевірка, що x знаходиться в межах вузлів інтерполяції.
    /// </summary>
    public bool IsInRange(double x)
    {
        if (points.Count == 0) return false;
        return x >= points[0].X && x <= points[^1].X;
    }
}
