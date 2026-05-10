using System.Globalization;
using InterpolationApp.Models;
using InterpolationApp.Services;

namespace InterpolationApp.Forms;

/// <summary>
/// Діалог для генерування набору точок з функції-зразка.
/// </summary>
public sealed class GenerateDialog : Form
{
    private readonly ComboBox functionCombo = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Anchor = AnchorStyles.Left | AnchorStyles.Right,
    };
    private readonly NumericUpDown lowerBox = new()
    {
        DecimalPlaces = 2, Minimum = -1000, Maximum = 1000, Value = -1,
        Increment = 0.5m,
        Anchor = AnchorStyles.Left | AnchorStyles.Right,
    };
    private readonly NumericUpDown upperBox = new()
    {
        DecimalPlaces = 2, Minimum = -1000, Maximum = 1000, Value = 1,
        Increment = 0.5m,
        Anchor = AnchorStyles.Left | AnchorStyles.Right,
    };
    private readonly NumericUpDown countBox = new()
    {
        Minimum = 2, Maximum = 1000, Value = 11,
        Anchor = AnchorStyles.Left | AnchorStyles.Right,
    };

    public SourceFunction Function { get; private set; }
    public double LowerBound { get; private set; }
    public double UpperBound { get; private set; }
    public int PointCount { get; private set; }

    public GenerateDialog()
    {
        Text = "Генерування точок з функції";
        // Адекватний фіксований розмір, що гарантовано вмістить усе
        // навіть на 150% масштабі. Користувач може розтягнути ще більше.
        ClientSize = new Size(560, 340);
        MinimumSize = new Size(500, 320);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Dpi;
        ShowIcon = false;

        // Заповнення комбобоксу
        foreach (SourceFunction f in Enum.GetValues<SourceFunction>())
            functionCombo.Items.Add(FunctionGenerator.GetDisplayName(f));
        functionCombo.SelectedIndex = 0;

        // Кнопки знизу — окрема панель з DockStyle.Bottom,
        // тоді вони НІКОЛИ не зрізаються
        var okBtn = new Button
        {
            Text = "Згенерувати",
            DialogResult = DialogResult.OK,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(140, 36),
        };
        var cancelBtn = new Button
        {
            Text = "Скасувати",
            DialogResult = DialogResult.Cancel,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(120, 36),
            Margin = new Padding(0, 0, 8, 0),
        };

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(12, 8, 12, 12),
            Height = 60,
            BackColor = SystemColors.Control,
        };
        buttonPanel.Controls.Add(okBtn);
        buttonPanel.Controls.Add(cancelBtn);

        // Основна сітка — поля вводу
        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16, 16, 16, 8),
            ColumnCount = 2,
            RowCount = 4,
        };
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 4; i++)
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        AddRow(mainLayout, 0, "Функція:", functionCombo);
        AddRow(mainLayout, 1, "Початок відрізка a:", lowerBox);
        AddRow(mainLayout, 2, "Кінець відрізка b:", upperBox);
        AddRow(mainLayout, 3, "Кількість точок:", countBox);

        // ВАЖЛИВО: Bottom-панель додаємо першою, щоб вона "зайняла" низ;
        // потім Fill-панель розтягнеться на залишок
        Controls.Add(mainLayout);
        Controls.Add(buttonPanel);

        AcceptButton = okBtn;
        CancelButton = cancelBtn;

        okBtn.Click += OnOk;
    }

    private static void AddRow(TableLayoutPanel layout, int row, string labelText, Control input)
    {
        var label = new Label
        {
            Text = labelText,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(3, 10, 12, 6),
        };
        input.Margin = new Padding(3, 6, 3, 6);
        layout.Controls.Add(label, 0, row);
        layout.Controls.Add(input, 1, row);
    }

    private void OnOk(object? sender, EventArgs e)
    {
        if (lowerBox.Value >= upperBox.Value)
        {
            MessageBox.Show(this, "Початок відрізка має бути меншим за кінець.",
                "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }

        Function = (SourceFunction)functionCombo.SelectedIndex;
        LowerBound = (double)lowerBox.Value;
        UpperBound = (double)upperBox.Value;
        PointCount = (int)countBox.Value;
    }
}
