using System.Drawing.Drawing2D;
using InterpolationApp.Interpolators;
using InterpolationApp.Models;

namespace InterpolationApp.Forms;

/// <summary>
/// Малювання осей, сітки, вузлів інтерполяції та кривих 
/// інтерполянт на Graphics-контексті.
/// 
/// Координатна система: математична (X росте праворуч, Y росте догори).
/// </summary>
public sealed class ChartRenderer
{
    // Палітра
    private static readonly Color BgColor = Color.White;
    private static readonly Color GridColor = Color.FromArgb(230, 230, 230);
    private static readonly Color AxisColor = Color.Black;
    private static readonly Color NewtonColor = Color.FromArgb(40, 80, 200);   // синій
    private static readonly Color SplineColor = Color.FromArgb(220, 50, 50);   // червоний
    private static readonly Color NodeColor = Color.FromArgb(0, 130, 0);       // зелений

    // Поля графіка (відступи від країв області рендерингу).
    // MarginLeft обчислюється динамічно з фактичної ширини підписів осі Y,
    // щоб коректно працювати при будь-якому масштабуванні DPI.
    private const int MarginLeftBase = 60;
    private const int MarginRight = 20;
    private const int MarginTop = 20;
    private const int MarginBottom = 35;

    public void Render(Graphics g, Rectangle area,
                       NewtonInterpolator newton, SplineInterpolator spline,
                       bool showNewton, bool showSpline)
    {
        ArgumentNullException.ThrowIfNull(g);

        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(BgColor);

        // Якщо обидва порожні — виводимо підказку
        if (newton.Count < 2 && spline.Count < 2)
        {
            using var f = new Font("Segoe UI", 11, FontStyle.Italic);
            using var sf = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            g.DrawString(
                "Немає точок. Введіть точки вручну, завантажте з файлу або згенеруйте з функції.",
                f, Brushes.Gray, area, sf);
            return;
        }

        // Виходимо зі спільного списку точок (вони синхронні в обох інтерполяторах
        // у MainForm; тому беремо з того, який непорожній)
        var points = newton.Count >= 2 ? newton.GetPoints() : spline.GetPoints();

        // === Визначення меж координат ===
        double xMin = points[0].X;
        double xMax = points[^1].X;
        double yMin = points.Min(p => p.Y);
        double yMax = points.Max(p => p.Y);

        // Сплайн і Ньютон можуть мати «викиди» за межі yMin..yMax (особливо Ньютон з ефектом Рунге).
        // Зробимо передвиборку значень, щоб встановити осі коректно.
        if (newton.Count >= 2 || spline.Count >= 2)
        {
            const int probe = 100;
            for (int i = 0; i <= probe; i++)
            {
                double x = xMin + (xMax - xMin) * i / probe;
                if (showNewton && newton.Count >= 2)
                {
                    double y = newton.Evaluate(x);
                    if (!double.IsNaN(y) && !double.IsInfinity(y))
                    {
                        if (y < yMin) yMin = y;
                        if (y > yMax) yMax = y;
                    }
                }
                if (showSpline && spline.Count >= 2)
                {
                    double y = spline.Evaluate(x);
                    if (!double.IsNaN(y) && !double.IsInfinity(y))
                    {
                        if (y < yMin) yMin = y;
                        if (y > yMax) yMax = y;
                    }
                }
            }
        }

        // Невеликий зап для краси
        double xPad = (xMax - xMin) * 0.05;
        double yPad = Math.Max((yMax - yMin) * 0.10, 1e-3);
        xMin -= xPad; xMax += xPad;
        yMin -= yPad; yMax += yPad;

        // === Динамічний MarginLeft за фактичною шириною Y-міток ===
        int marginLeft;
        using (var measureFont = new Font("Segoe UI", 8))
        {
            float maxLabelWidth = 0;
            const int yLines = 6;
            for (int i = 0; i <= yLines; i++)
            {
                double yv = yMin + (yMax - yMin) * i / yLines;
                string label = yv.ToString("G3", System.Globalization.CultureInfo.InvariantCulture);
                float w = g.MeasureString(label, measureFont).Width;
                if (w > maxLabelWidth) maxLabelWidth = w;
            }
            // Беремо максимум з базового margin та фактичної ширини найдовшої мітки + 12px padding
            marginLeft = Math.Max(MarginLeftBase, (int)Math.Ceiling(maxLabelWidth) + 12);
        }

        // === Функції перетворення ===
        int chartW = area.Width - marginLeft - MarginRight;
        int chartH = area.Height - MarginTop - MarginBottom;
        int chartX = area.X + marginLeft;
        int chartY = area.Y + MarginTop;

        if (chartW < 50 || chartH < 50) return; // надто маленька область — нічого не малюємо

        float ToScreenX(double x) =>
            (float)(chartX + (x - xMin) / (xMax - xMin) * chartW);
        float ToScreenY(double y) =>
            (float)(chartY + chartH - (y - yMin) / (yMax - yMin) * chartH);

        // === Сітка та осі ===
        DrawGrid(g, chartX, chartY, chartW, chartH, xMin, xMax, yMin, yMax, ToScreenX, ToScreenY);

        // === Криві ===
        const int curvePoints = 400;
        if (showSpline && spline.Count >= 2)
            DrawCurve(g, spline, xMin, xMax, curvePoints, ToScreenX, ToScreenY,
                      SplineColor, 2.0f, area);

        if (showNewton && newton.Count >= 2)
            DrawCurve(g, newton, xMin, xMax, curvePoints, ToScreenX, ToScreenY,
                      NewtonColor, 2.0f, area);

        // === Вузли (поверх кривих) ===
        DrawNodes(g, points, ToScreenX, ToScreenY);

        // === Легенда ===
        DrawLegend(g, area, showNewton, showSpline);
    }

    private void DrawGrid(Graphics g, int x, int y, int w, int h,
                          double xMin, double xMax, double yMin, double yMax,
                          Func<double, float> toX, Func<double, float> toY)
    {
        using var gridPen = new Pen(GridColor, 1);
        using var axisPen = new Pen(AxisColor, 1.5f);
        using var font = new Font("Segoe UI", 8);

        // Кадр області
        g.DrawRectangle(axisPen, x, y, w, h);

        // Сітка по X — приблизно 8 ліній
        const int xLines = 8;
        for (int i = 0; i <= xLines; i++)
        {
            double xv = xMin + (xMax - xMin) * i / xLines;
            float sx = toX(xv);
            g.DrawLine(gridPen, sx, y, sx, y + h);
            string label = xv.ToString("G3", System.Globalization.CultureInfo.InvariantCulture);
            var size = g.MeasureString(label, font);
            g.DrawString(label, font, Brushes.Black,
                         sx - size.Width / 2, y + h + 4);
        }

        // Сітка по Y — приблизно 6 ліній
        const int yLines = 6;
        for (int i = 0; i <= yLines; i++)
        {
            double yv = yMin + (yMax - yMin) * i / yLines;
            float sy = toY(yv);
            g.DrawLine(gridPen, x, sy, x + w, sy);
            string label = yv.ToString("G3", System.Globalization.CultureInfo.InvariantCulture);
            var size = g.MeasureString(label, font);
            g.DrawString(label, font, Brushes.Black,
                         x - size.Width - 4, sy - size.Height / 2);
        }

        // Осі X=0 та Y=0 — якщо вони в межах
        using var zeroPen = new Pen(Color.FromArgb(180, 180, 180), 1) { DashStyle = DashStyle.Dash };
        if (yMin <= 0 && yMax >= 0)
        {
            float sy = toY(0);
            g.DrawLine(zeroPen, x, sy, x + w, sy);
        }
        if (xMin <= 0 && xMax >= 0)
        {
            float sx = toX(0);
            g.DrawLine(zeroPen, sx, y, sx, y + h);
        }
    }

    private void DrawCurve(Graphics g, Interpolator interp,
                           double xMin, double xMax, int steps,
                           Func<double, float> toX, Func<double, float> toY,
                           Color color, float width, Rectangle clipArea)
    {
        using var pen = new Pen(color, width);

        var pts = new List<PointF>(steps + 1);
        var nodes = interp.GetPoints();
        double xa = nodes[0].X;
        double xb = nodes[^1].X;

        for (int i = 0; i <= steps; i++)
        {
            double x = xa + (xb - xa) * i / steps;
            double y;
            try { y = interp.Evaluate(x); }
            catch { continue; }

            if (double.IsNaN(y) || double.IsInfinity(y)) continue;

            float sx = toX(x);
            float sy = toY(y);

            // Захист від «вибухів» (Ньютон може видати дуже великі значення —
            // обрізаємо лінію за межами області, щоб уникнути екстремальних координат)
            if (sy < clipArea.Top - 1000 || sy > clipArea.Bottom + 1000)
            {
                if (pts.Count > 1) g.DrawLines(pen, pts.ToArray());
                pts.Clear();
                continue;
            }

            pts.Add(new PointF(sx, sy));
        }

        if (pts.Count > 1) g.DrawLines(pen, pts.ToArray());
    }

    private void DrawNodes(Graphics g, IReadOnlyList<DataPoint> nodes,
                           Func<double, float> toX, Func<double, float> toY)
    {
        using var brush = new SolidBrush(NodeColor);
        using var pen = new Pen(Color.Black, 1);
        const float r = 4f;

        foreach (var p in nodes)
        {
            float sx = toX(p.X);
            float sy = toY(p.Y);
            g.FillEllipse(brush, sx - r, sy - r, 2 * r, 2 * r);
            g.DrawEllipse(pen, sx - r, sy - r, 2 * r, 2 * r);
        }
    }

    private void DrawLegend(Graphics g, Rectangle area, bool showNewton, bool showSpline)
    {
        using var font = new Font("Segoe UI", 9);

        // Вимірюємо ширину найдовшого підпису легенди, щоб коректно
        // позиціонувати її при будь-якому DPI / шрифті
        string[] labels = { "Поліном Ньютона", "Кубічний сплайн", "Вузли" };
        float maxTextWidth = 0;
        foreach (string s in labels)
        {
            float w = g.MeasureString(s, font).Width;
            if (w > maxTextWidth) maxTextWidth = w;
        }
        // Лінія/маркер (25px) + 8px padding + текст + 12px правий запас
        int legendBlockWidth = (int)Math.Ceiling(maxTextWidth) + 25 + 8 + 12;

        int y = area.Top + 10;
        int x = area.Right - legendBlockWidth;
        // Гарантуємо мінімальну відстань від лівого краю
        if (x < area.Left + 100) x = area.Left + 100;

        if (showNewton)
        {
            using var pen = new Pen(NewtonColor, 2);
            g.DrawLine(pen, x, y + 8, x + 25, y + 8);
            g.DrawString("Поліном Ньютона", font, Brushes.Black, x + 33, y);
            y += 20;
        }
        if (showSpline)
        {
            using var pen = new Pen(SplineColor, 2);
            g.DrawLine(pen, x, y + 8, x + 25, y + 8);
            g.DrawString("Кубічний сплайн", font, Brushes.Black, x + 33, y);
            y += 20;
        }
        using var dotBrush = new SolidBrush(NodeColor);
        g.FillEllipse(dotBrush, x + 9, y + 5, 7, 7);
        g.DrawString("Вузли", font, Brushes.Black, x + 33, y);
    }
}
