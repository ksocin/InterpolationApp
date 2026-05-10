using InterpolationApp.Services;

namespace InterpolationApp.Forms;

/// <summary>
/// Форма аналізу обчислювальної складності — гістограма часу побудови
/// коефіцієнтів обох методів на наборах різного розміру.
/// </summary>
public sealed class ComplexityForm : Form
{
    private readonly Button runBtn = new()
    {
        Text = "Запустити аналіз",
        AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
        MinimumSize = new Size(200, 36),
    };
    private readonly Panel chartPanel = new() { Dock = DockStyle.Fill, BackColor = Color.White };
    private readonly DataGridView resultsGrid = new()
    {
        Dock = DockStyle.Fill,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        ReadOnly = true,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        RowHeadersVisible = false,
    };

    private List<ComplexityAnalyzer.Result>? results;

    public ComplexityForm()
    {
        Text = "Аналіз обчислювальної складності";
        ClientSize = new Size(900, 600);
        MinimumSize = new Size(700, 450);
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Dpi;

        BuildUI();
        runBtn.Click += OnRun;
        chartPanel.Paint += OnPaint;
        chartPanel.Resize += (_, _) => chartPanel.Invalidate();
    }

    private void BuildUI()
    {
        resultsGrid.Columns.Add("N", "n (точок)");
        resultsGrid.Columns.Add("Newton", "Ньютон, мс");
        resultsGrid.Columns.Add("Spline", "Сплайн, мс");

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
        };
        split.Panel1.Controls.Add(chartPanel);
        split.Panel2.Controls.Add(resultsGrid);

        var topPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(8),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
        };
        topPanel.Controls.Add(runBtn);

        // Порядок Add важливий: верхня панель ПЕРШОЮ, потім — заповнювач
        Controls.Add(split);
        Controls.Add(topPanel);

        // Розмір сплітера встановлюємо ПІСЛЯ Add, щоб уникнути виключення MinSize
        Load += (_, _) =>
        {
            split.SplitterDistance = (int)(split.Width * 0.65);
        };
    }

    private async void OnRun(object? sender, EventArgs e)
    {
        runBtn.Enabled = false;
        runBtn.Text = "Виконується…";

        try
        {
            // Запускаємо в окремому потоці, щоб не блокувати UI
            results = await Task.Run(() =>
            {
                var analyzer = new ComplexityAnalyzer();
                int[] sizes = { 10, 25, 50, 100, 250, 500, 1000 };
                return analyzer.Run(sizes, repeatsPerSize: 5);
            });

            // Заповнюємо таблицю
            resultsGrid.Rows.Clear();
            foreach (var r in results)
                resultsGrid.Rows.Add(r.N, r.NewtonMs.ToString("F3"), r.SplineMs.ToString("F3"));

            chartPanel.Invalidate();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Помилка",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            runBtn.Enabled = true;
            runBtn.Text = "Запустити аналіз";
        }
    }

    private void OnPaint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Color.White);

        if (results is null || results.Count == 0)
        {
            using var f = new Font("Segoe UI", 11, FontStyle.Italic);
            using var sf = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            g.DrawString("Натисніть «Запустити аналіз», щоб виміряти час",
                f, Brushes.Gray, chartPanel.ClientRectangle, sf);
            return;
        }

        const int marginLeft = 60;
        const int marginRight = 20;
        const int marginTop = 40;
        const int marginBottom = 50;

        int w = chartPanel.Width - marginLeft - marginRight;
        int h = chartPanel.Height - marginTop - marginBottom;
        int x0 = marginLeft;
        int y0 = marginTop;

        // Знаходимо максимум часу (для масштабу)
        double tMax = 0;
        foreach (var r in results)
        {
            tMax = Math.Max(tMax, r.NewtonMs);
            tMax = Math.Max(tMax, r.SplineMs);
        }
        if (tMax <= 0) tMax = 1;

        // Округлюємо вгору для красивих сіткових ліній
        tMax *= 1.15;

        // Рамка
        using var framePen = new Pen(Color.Black, 1.5f);
        g.DrawRectangle(framePen, x0, y0, w, h);

        // Сітка по Y
        using var gridPen = new Pen(Color.FromArgb(230, 230, 230), 1);
        using var labelFont = new Font("Segoe UI", 8);
        for (int i = 0; i <= 5; i++)
        {
            float y = y0 + h - h * i / 5f;
            g.DrawLine(gridPen, x0, y, x0 + w, y);
            string label = (tMax * i / 5).ToString("F2") + " мс";
            var sz = g.MeasureString(label, labelFont);
            g.DrawString(label, labelFont, Brushes.Black, x0 - sz.Width - 4, y - sz.Height / 2);
        }

        // Гістограма: 2 стовпчики (Ньютон, Сплайн) на кожен n
        int groupCount = results.Count;
        float groupWidth = (float)w / groupCount;
        float barWidth = groupWidth * 0.35f;

        using var newtonBrush = new SolidBrush(Color.FromArgb(40, 80, 200));
        using var splineBrush = new SolidBrush(Color.FromArgb(220, 50, 50));

        for (int i = 0; i < groupCount; i++)
        {
            var r = results[i];
            float gx = x0 + i * groupWidth + groupWidth / 2;

            float hN = (float)(r.NewtonMs / tMax * h);
            float hS = (float)(r.SplineMs / tMax * h);

            g.FillRectangle(newtonBrush, gx - barWidth - 2, y0 + h - hN, barWidth, hN);
            g.FillRectangle(splineBrush, gx + 2, y0 + h - hS, barWidth, hS);
            g.DrawRectangle(Pens.Black, gx - barWidth - 2, y0 + h - hN, barWidth, hN);
            g.DrawRectangle(Pens.Black, gx + 2, y0 + h - hS, barWidth, hS);

            // Підпис під столбіком — n
            string xLabel = r.N.ToString();
            var xLabelSize = g.MeasureString(xLabel, labelFont);
            g.DrawString(xLabel, labelFont, Brushes.Black,
                gx - xLabelSize.Width / 2, y0 + h + 6);
        }

        // Лейбли осей
        using var axisFont = new Font("Segoe UI", 9, FontStyle.Bold);
        g.DrawString("Кількість вузлів n", axisFont, Brushes.Black,
            x0 + w / 2 - 60, y0 + h + 24);

        // Легенда
        int legendX = x0 + 12;
        int legendY = y0 + 8;
        g.FillRectangle(newtonBrush, legendX, legendY, 16, 12);
        g.DrawRectangle(Pens.Black, legendX, legendY, 16, 12);
        g.DrawString("Поліном Ньютона O(n²)", labelFont, Brushes.Black, legendX + 22, legendY - 1);

        g.FillRectangle(splineBrush, legendX, legendY + 18, 16, 12);
        g.DrawRectangle(Pens.Black, legendX, legendY + 18, 16, 12);
        g.DrawString("Кубічний сплайн O(n)", labelFont, Brushes.Black, legendX + 22, legendY + 17);
    }
}
