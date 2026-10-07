namespace Ai_DailyTracking.UI.Controls;

// Button that opens a checklist popup, so a filter can have more than one value checked at once.
public sealed class MultiSelectFilterDropDown : Button
{
    private readonly string _title;
    private readonly Func<string, Color>? _colorResolver;
    private readonly CheckedListBox _checkedListBox = new();
    private readonly ToolStripDropDown _dropDown = new() { Padding = Padding.Empty };
    private bool _updatingOptions;

    public event EventHandler? SelectionChanged;

    public MultiSelectFilterDropDown(string title, Func<string, Color>? colorResolver = null)
    {
        _title = title;
        _colorResolver = colorResolver;

        AutoSize = false;
        Size = new Size(150, 30);
        TextAlign = ContentAlignment.MiddleLeft;
        Padding = new Padding(8, 0, 0, 0);
        FlatStyle = FlatStyle.Flat;

        _checkedListBox.CheckOnClick = true;
        _checkedListBox.IntegralHeight = false;
        _checkedListBox.BorderStyle = BorderStyle.FixedSingle;
        _checkedListBox.Width = 210;
        _checkedListBox.Height = 140;
        _checkedListBox.ItemCheck += (_, _) =>
        {
            if (!_updatingOptions)
            {
                BeginInvoke(new Action(NotifySelectionChanged));
            }
        };

        if (_colorResolver is not null)
        {
            _checkedListBox.DrawMode = DrawMode.OwnerDrawFixed;
            _checkedListBox.DrawItem += CheckedListBox_DrawItem;
        }

        var host = new ToolStripControlHost(_checkedListBox)
        {
            AutoSize = false,
            Width = _checkedListBox.Width,
            Height = _checkedListBox.Height,
            Margin = Padding.Empty
        };
        _dropDown.Items.Add(host);

        Click += (_, _) => _dropDown.Show(this, new Point(0, Height));

        UpdateButtonText();
    }

    public void SetOptions(IEnumerable<string> options)
    {
        _checkedListBox.Items.Clear();

        foreach (var option in options)
        {
            _checkedListBox.Items.Add(option);
        }

        UpdateButtonText();
    }

    public void UpdateOptions(IEnumerable<string> options)
    {
        var optionList = options.ToList();
        if (_checkedListBox.Items.Cast<string>().SequenceEqual(optionList, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        var selected = GetSelectedValues();
        _updatingOptions = true;
        try
        {
            _checkedListBox.Items.Clear();
            foreach (var option in optionList)
            {
                _checkedListBox.Items.Add(option, selected.Contains(option, StringComparer.OrdinalIgnoreCase));
            }
        }
        finally
        {
            _updatingOptions = false;
        }
        UpdateButtonText();
    }

    public IReadOnlyCollection<string> GetSelectedValues()
    {
        return _checkedListBox.CheckedItems.Cast<string>().ToList();
    }

    private void NotifySelectionChanged()
    {
        UpdateButtonText();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateButtonText()
    {
        var checkedCount = _checkedListBox.CheckedItems.Count;
        Text = checkedCount == 0 ? $"{_title}: (Todos)" : $"{_title}: {checkedCount} sel.";
    }

    private void CheckedListBox_DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || _colorResolver is null)
        {
            e.DrawBackground();
            return;
        }

        e.DrawBackground();

        var text = _checkedListBox.Items[e.Index].ToString() ?? string.Empty;
        var isChecked = _checkedListBox.GetItemChecked(e.Index);

        var checkBoxRect = new Rectangle(e.Bounds.Left + 2, e.Bounds.Top + (e.Bounds.Height - 13) / 2, 13, 13);
        ControlPaint.DrawCheckBox(e.Graphics, checkBoxRect, isChecked ? ButtonState.Checked : ButtonState.Normal);

        var swatchRect = new Rectangle(checkBoxRect.Right + 6, e.Bounds.Top + (e.Bounds.Height - 12) / 2, 12, 12);

        using (var swatchBrush = new SolidBrush(_colorResolver(text)))
        {
            e.Graphics.FillRectangle(swatchBrush, swatchRect);
        }

        e.Graphics.DrawRectangle(Pens.Gray, swatchRect);

        var textRect = new Rectangle(swatchRect.Right + 6, e.Bounds.Top, e.Bounds.Width - (swatchRect.Right + 6 - e.Bounds.Left), e.Bounds.Height);
        TextRenderer.DrawText(e.Graphics, text, e.Font ?? Control.DefaultFont, textRect, e.ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        e.DrawFocusRectangle();
    }
}
