namespace InterpolationApp.Models;

/// <summary>
/// Перелік функцій-зразків для генерування точок.
/// </summary>
public enum SourceFunction
{
    Sin,           // y = sin(x)
    Cos,           // y = cos(x)
    Square,        // y = x^2
    Runge,         // y = 1 / (1 + 25*x^2)  — функція Рунге
    ExpNegative,   // y = exp(-x)
}
