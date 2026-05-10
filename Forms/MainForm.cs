using System.Globalization;
using InterpolationApp.Interpolators;
using InterpolationApp.Models;
using InterpolationApp.Services;

namespace InterpolationApp.Forms;

/// <summary>
/// Головна форма додатку. UI створюється повністю програмно (без Designer.cs)
/// для прозорості розуміння та зручності рев'ю в курсовій роботі.
/// </summary>
public sealed class MainForm : Form
{
    // Інтерполятори (синхронні набори точок)
    private readonly NewtonInterpolator newton = new();
    private readonly SplineInterpolator spline = new();
    private readonly ChartRenderer renderer = new();
    private readonly FileManager fileManager = new();

    // Контроли — AutoSize, де доречно, щоб не обрізати при DPI > 100%
    private readonly TextBox xBox = new() { Width = 100 };
    private readonly TextBox yBox = new() { Width = 100 };
    private readonly Button addPointBtn = new()
    {
        Text = "Додати", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
        MinimumSize = new Size(90, 32),
    };
    private readonly Button removePointBtn = new()
    {
        Text = "Видалити", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
        MinimumSize = new Size(90, 32),
    };
    private readonly Button clearBtn = new()
    {
        Text = "Очистити всі", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
        MinimumSize = new Size(120, 32),
    };
    private readonly ListBox pointsList = new() { Dock = DockStyle.Fill };

    private readonly CheckBox showNewtonCheck = new()
    {
        Text = "Показати поліном Ньютона",
        Checked = true, AutoSize = true,
        Margin = new Padding(12, 10, 4, 4),
    };
    private readonly CheckBox showSplineCheck = new()
    {
        Text = "Показати кубічний сплайн",
        Checked = true, AutoSize = true,
        Margin = new Padding(12, 10, 4, 4),
    };
    private readonly Button buildBtn = new()
    {
        Text = "Інтерполювати",
        AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
        MinimumSize = new Size(180, 36),
        Margin = new Padding(4, 4, 4, 4),
    };

    private readonly Panel chartBox = new() { Dock = DockStyle.Fill, BackColor = Color.White };
    private readonly StatusStrip statusStrip = new();
    private readonly ToolStripStatusLabel statusLabel = new() { Text = "Готово" };

    public MainForm()
    {
        Text = "Інтерполяція функції — Курсова робота, Варіант 10";
        ClientSize = new Size(1280, 800);
        MinimumSize = new Size(1000, 650);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;

        BuildUI();
        WireEvents();
    }

    private void BuildUI()
    {
        // === Меню ===
        var menu = new MenuStrip();

        var fileMenu = new ToolStripMenuItem("Файл");
        var loadItem = new ToolStripMenuItem("Завантажити з файлу…", null, OnLoadFile) { ShortcutKeys = Keys.Control | Keys.O };
        var saveItem = new ToolStripMenuItem("Зберегти результати…", null, OnSaveFile) { ShortcutKeys = Keys.Control | Keys.S };
        var savePngItem = new ToolStripMenuItem("Зберегти зображення PNG…", null, OnSavePng);
        var exitItem = new ToolStripMenuItem("Вихід", null, (_, _) => Close());
        fileMenu.DropDownItems.AddRange(new ToolStripItem[]
        {
            loadItem, saveItem, savePngItem, new ToolStripSeparator(), exitItem
        });

        var inputMenu = new ToolStripMenuItem("Введення");
        var clearAllItem = new ToolStripMenuItem("Очистити всі точки", null, (_, _) => ClearAll());
        var generateItem = new ToolStripMenuItem("Згенерувати з функції…", null, OnGenerate);
        inputMenu.DropDownItems.AddRange(new ToolStripItem[] { clearAllItem, generateItem });

        var analysisMenu = new ToolStripMenuItem("Аналіз");
        var complexityItem = new ToolStripMenuItem("Виміряти час виконання…", null, OnComplexity);
        analysisMenu.DropDownItems.Add(complexityItem);

        var helpMenu = new ToolStripMenuItem("Довідка");
        var aboutItem = new ToolStripMenuItem("Про програму", null, OnAbout);
        helpMenu.DropDownItems.Add(aboutItem);

        menu.Items.AddRange(new ToolStripItem[] { fileMenu, inputMenu, analysisMenu, helpMenu });

        MainMenuStrip = menu;
        Controls.Add(menu);

        // === Розмітка ===
        var rootLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(8),
        };
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280));
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // Ліва панель
        var leftPanel = BuildLeftPanel();
        rootLayout.Controls.Add(leftPanel, 0, 0);

        // Права панель — графік + контроли над ним
        var rightPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
        };
        // Фіксована висота 70px у логічних піксельах — Windows сам масштабує
        // її під DPI завдяки AutoScaleMode.Dpi на формі
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var topControls = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(4, 8, 4, 8),
        };
        topControls.Controls.Add(buildBtn);
        topControls.Controls.Add(showNewtonCheck);
        topControls.Controls.Add(showSplineCheck);

        rightPanel.Controls.Add(topControls, 0, 0);
        rightPanel.Controls.Add(chartBox, 0, 1);

        rootLayout.Controls.Add(rightPanel, 1, 0);

        Controls.Add(rootLayout);

        // === Статус-рядок ===
        statusStrip.Items.Add(statusLabel);
        Controls.Add(statusStrip);

        chartBox.Paint += OnChartPaint;
        chartBox.Resize += (_, _) => chartBox.Invalidate();
    }

    private GroupBox BuildLeftPanel()
    {
        var box = new GroupBox
        {
            Text = "Точки інтерполяції",
            Dock = DockStyle.Fill,
            Padding = new Padding(8, 12, 8, 8),
        };

        // Рядки: 0 — X, 1 — Y, 2 — Add/Remove, 3 — список (тягнеться), 4 — Очистити
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 5,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // X
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // Y
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // Add/Remove
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // ListBox — весь залишок
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // Clear

        var xLabel = new Label
        {
            Text = "X:", AutoSize = true,
            Anchor = AnchorStyles.Right,
            Margin = new Padding(3, 8, 6, 3),
        };
        var yLabel = new Label
        {
            Text = "Y:", AutoSize = true,
            Anchor = AnchorStyles.Right,
            Margin = new Padding(3, 8, 6, 3),
        };

        xBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        yBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        xBox.Margin = new Padding(3, 4, 3, 4);
        yBox.Margin = new Padding(3, 4, 3, 4);

        layout.Controls.Add(xLabel, 0, 0);
        layout.Controls.Add(xBox, 1, 0);
        layout.Controls.Add(yLabel, 0, 1);
        layout.Controls.Add(yBox, 1, 1);

        var addRemoveLayout = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true,
            Margin = new Padding(0, 6, 0, 6),
        };
        addPointBtn.Margin = new Padding(0, 0, 6, 0);
        addRemoveLayout.Controls.Add(addPointBtn);
        addRemoveLayout.Controls.Add(removePointBtn);
        layout.SetColumnSpan(addRemoveLayout, 2);
        layout.Controls.Add(addRemoveLayout, 0, 2);

        pointsList.Margin = new Padding(0, 0, 0, 6);
        layout.SetColumnSpan(pointsList, 2);
        layout.Controls.Add(pointsList, 0, 3);

        clearBtn.Margin = new Padding(0, 0, 0, 0);
        layout.SetColumnSpan(clearBtn, 2);
        layout.Controls.Add(clearBtn, 0, 4);

        box.Controls.Add(layout);
        return box;
    }

    private void WireEvents()
    {
        addPointBtn.Click += OnAddPoint;
        removePointBtn.Click += OnRemovePoint;
        clearBtn.Click += (_, _) => ClearAll();
        buildBtn.Click += OnBuild;

        showNewtonCheck.CheckedChanged += (_, _) => chartBox.Invalidate();
        showSplineCheck.CheckedChanged += (_, _) => chartBox.Invalidate();

        yBox.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                OnAddPoint(s, e);
            }
        };
    }

    // ============ Обробники ============

    private void OnAddPoint(object? sender, EventArgs e)
    {
        var culture = CultureInfo.InvariantCulture;
        string xs = xBox.Text.Trim().Replace(',', '.');
        string ys = yBox.Text.Trim().Replace(',', '.');

        if (!double.TryParse(xs, NumberStyles.Float, culture, out double x) ||
            !double.TryParse(ys, NumberStyles.Float, culture, out double y))
        {
            MessageBox.Show(this, "Введіть коректні числа для X та Y.",
                "Помилка вводу", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            var p = new DataPoint(x, y);
            newton.AddPoint(p);
            spline.AddPoint(p);
            RebuildPointsList();
            xBox.Clear();
            yBox.Clear();
            xBox.Focus();
            chartBox.Invalidate();
            statusLabel.Text = $"Точок: {newton.Count}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Помилка",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void OnRemovePoint(object? sender, EventArgs e)
    {
        int idx = pointsList.SelectedIndex;
        if (idx < 0)
        {
            MessageBox.Show(this, "Спочатку оберіть точку зі списку.",
                "Інформація", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        newton.RemovePoint(idx);
        spline.RemovePoint(idx);
        RebuildPointsList();
        chartBox.Invalidate();
        statusLabel.Text = $"Точок: {newton.Count}";
    }

    private void ClearAll()
    {
        newton.Clear();
        spline.Clear();
        RebuildPointsList();
        chartBox.Invalidate();
        statusLabel.Text = "Готово";
    }

    private void OnBuild(object? sender, EventArgs e)
    {
        if (newton.Count < 2)
        {
            MessageBox.Show(this, "Потрібно мінімум 2 точки для побудови.",
                "Інформація", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            newton.Build();
            spline.Build();
            chartBox.Invalidate();
            statusLabel.Text = $"Інтерполянти побудовано. Точок: {newton.Count}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Помилка побудови",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnLoadFile(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Filter = "CSV-файли|*.csv;*.txt|Усі файли|*.*",
            Title = "Завантажити точки з файлу",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            var pts = fileManager.LoadPoints(dlg.FileName);
            newton.Clear();
            spline.Clear();
            foreach (var p in pts)
            {
                newton.AddPoint(p);
                spline.AddPoint(p);
            }
            RebuildPointsList();
            chartBox.Invalidate();
            statusLabel.Text = $"Завантажено {pts.Count} точок";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Помилка завантаження",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnSaveFile(object? sender, EventArgs e)
    {
        if (newton.Count < 2)
        {
            MessageBox.Show(this, "Немає даних для збереження.",
                "Інформація", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dlg = new SaveFileDialog
        {
            Filter = "CSV-файли|*.csv|Текстові файли|*.txt",
            Title = "Зберегти результати інтерполяції",
            FileName = "interpolation_results.csv",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            if (!newton.IsBuilt) newton.Build();
            if (!spline.IsBuilt) spline.Build();

            var nodes = newton.GetPoints();
            double xa = nodes[0].X, xb = nodes[^1].X;
            const int steps = 200;
            var evaluated = new List<(double, double, double)>(steps + 1);
            for (int i = 0; i <= steps; i++)
            {
                double x = xa + (xb - xa) * i / steps;
                evaluated.Add((x, newton.Evaluate(x), spline.Evaluate(x)));
            }

            fileManager.SaveResults(dlg.FileName, nodes, evaluated);
            statusLabel.Text = $"Збережено: {dlg.FileName}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Помилка збереження",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnSavePng(object? sender, EventArgs e)
    {
        using var dlg = new SaveFileDialog
        {
            Filter = "PNG-зображення|*.png",
            Title = "Зберегти графік як PNG",
            FileName = "interpolation_chart.png",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            using var bmp = new Bitmap(chartBox.Width, chartBox.Height);
            using (var g = Graphics.FromImage(bmp))
            {
                renderer.Render(g, new Rectangle(0, 0, bmp.Width, bmp.Height),
                                newton, spline,
                                showNewtonCheck.Checked, showSplineCheck.Checked);
            }
            bmp.Save(dlg.FileName, System.Drawing.Imaging.ImageFormat.Png);
            statusLabel.Text = $"PNG збережено: {dlg.FileName}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Помилка",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnGenerate(object? sender, EventArgs e)
    {
        using var dlg = new GenerateDialog();
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            var pts = FunctionGenerator.Generate(dlg.Function, dlg.LowerBound, dlg.UpperBound, dlg.PointCount);
            newton.Clear();
            spline.Clear();
            foreach (var p in pts)
            {
                newton.AddPoint(p);
                spline.AddPoint(p);
            }
            RebuildPointsList();
            chartBox.Invalidate();
            statusLabel.Text = $"Згенеровано {pts.Count} точок з {FunctionGenerator.GetDisplayName(dlg.Function)}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Помилка",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnComplexity(object? sender, EventArgs e)
    {
        using var form = new ComplexityForm();
        form.ShowDialog(this);
    }

    private void OnAbout(object? sender, EventArgs e)
    {
        MessageBox.Show(this,
            "Інтерполяція функції\n\n" +
            "Курсова робота з дисципліни «Основи програмування. Курсова робота»\n" +
            "Варіант 10: Інтерполяція кубічними сплайнами та методом Ньютона\n\n" +
            "Виконавець: Крикун Софія Олексіївна, ІП-54\n" +
            "Керівник: Вітковська І.І.\n\n" +
            "КПІ ім. Ігоря Сікорського, 2026",
            "Про програму", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void OnChartPaint(object? sender, PaintEventArgs e)
    {
        renderer.Render(e.Graphics, chartBox.ClientRectangle,
                        newton, spline,
                        showNewtonCheck.Checked, showSplineCheck.Checked);
    }

    private void RebuildPointsList()
    {
        pointsList.BeginUpdate();
        pointsList.Items.Clear();
        foreach (var p in newton.GetPoints())
            pointsList.Items.Add(p.ToString());
        pointsList.EndUpdate();
    }
}
