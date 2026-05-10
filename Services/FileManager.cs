using System.Globalization;
using InterpolationApp.Models;

namespace InterpolationApp.Services;

/// <summary>
/// Завантаження та збереження точок у CSV-форматі.
/// Формат файлу: на кожному рядку «x;y» або «x,y» (підтримуються обидва роздільники
/// для зручності — українські локалі часто конфліктують з крапкою-як-десятковою).
/// Рядки, що починаються з «#» або «x» (англ. літера x як заголовок) ігноруються.
/// </summary>
public sealed class FileManager
{
    private readonly CultureInfo culture = CultureInfo.InvariantCulture;

    /// <summary>Завантаження точок з файлу. Кидає Exception у разі некоректних даних.</summary>
    public List<DataPoint> LoadPoints(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Файл не знайдено: {path}");

        var result = new List<DataPoint>();
        int lineNumber = 0;

        foreach (var raw in File.ReadAllLines(path))
        {
            lineNumber++;
            var line = raw.Trim();

            if (string.IsNullOrEmpty(line)) continue;
            if (line.StartsWith("#")) continue;
            if (line.StartsWith("x", StringComparison.OrdinalIgnoreCase)) continue;

            // Підтримуємо ; , табуляцію, пробіли як роздільники
            var parts = line.Split(new[] { ';', ',', '\t', ' ' },
                StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 2)
                throw new FormatException(
                    $"Рядок {lineNumber}: очікується 2 числа, отримано «{line}»");

            if (!double.TryParse(parts[0], NumberStyles.Float, culture, out double x) ||
                !double.TryParse(parts[1], NumberStyles.Float, culture, out double y))
                throw new FormatException(
                    $"Рядок {lineNumber}: не вдалося розпарсити числа з «{line}»");

            result.Add(new DataPoint(x, y));
        }

        if (result.Count < 2)
            throw new InvalidDataException(
                "У файлі менше 2 точок — недостатньо для побудови інтерполянти.");

        return result;
    }

    /// <summary>
    /// Збереження результатів інтерполяції у файл.
    /// Записує і вузли, і обчислені значення обох інтерполянт у проміжних точках.
    /// </summary>
    public void SaveResults(string path,
                             IReadOnlyList<DataPoint> nodes,
                             IReadOnlyList<(double x, double yNewton, double ySpline)> evaluated)
    {
        using var writer = new StreamWriter(path);

        writer.WriteLine("# Інтерполяція функції — результати");
        writer.WriteLine($"# Кількість вузлів: {nodes.Count}");
        writer.WriteLine($"# Кількість точок інтерполянти: {evaluated.Count}");
        writer.WriteLine();

        writer.WriteLine("# Вузли інтерполяції");
        writer.WriteLine("x;y");
        foreach (var p in nodes)
            writer.WriteLine($"{p.X.ToString(culture)};{p.Y.ToString(culture)}");

        writer.WriteLine();
        writer.WriteLine("# Значення інтерполянт");
        writer.WriteLine("x;y_Newton;y_Spline");
        foreach (var (x, yN, yS) in evaluated)
            writer.WriteLine($"{x.ToString(culture)};{yN.ToString(culture)};{yS.ToString(culture)}");
    }
}
