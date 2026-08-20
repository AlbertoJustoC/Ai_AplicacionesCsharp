using System.Windows.Forms.VisualStyles;

namespace Ai_preAgresso.UI.Controls;

// A combo-like control that opens a checkbox list popup, allowing several values to be selected at once.
// Typing in the box narrows which options are shown in the popup; the checked selection is kept
// independently of the current search text. An empty (or full) selection means "no filter".
public sealed class MultiSelectComboBox : UserControl
{
    private readonly TextBox _displayBox = new() { BackColor = SystemColors.Window };
    private readonly ComboDropButton _dropButton = new() { Dock = DockStyle.Right };
    private readonly CheckedListBox _checkList = new() { CheckOnClick = true, BorderStyle = BorderStyle.FixedSingle, IntegralHeight = false };
    private readonly ToolStripDropDown _popup = new() { AutoClose = true, Padding = Padding.Empty };

    private List<object> _allItems = [];
    private readonly HashSet<object> _checkedItems = new();
    private bool _suppressItemCheck;
    private bool _suppressTextChanged;
    private bool _isSearchText;

    public event EventHandler? SelectionChanged;

    public MultiSelectComboBox()
    {
        Height = _displayBox.PreferredHeight + 2;

        _displayBox.Dock = DockStyle.Fill;
        _displayBox.TextChanged += DisplayBox_TextChanged;
        _displayBox.Enter += (_, _) =>
        {
            if (!_isSearchText)
            {
                _displayBox.SelectAll();
            }
        };

        _dropButton.Click += (_, _) => ToggleDropDown();

        Controls.Add(_displayBox);
        Controls.Add(_dropButton);

        var host = new ToolStripControlHost(_checkList) { Margin = Padding.Empty, Padding = Padding.Empty, AutoSize = false };
        _popup.Items.Add(host);
        _popup.Closed += (_, _) => RestoreDisplayText();

        // ItemCheck fires before the item's own CheckState updates, so read the final state on the next tick.
        _checkList.ItemCheck += (_, e) =>
        {
            if (_suppressItemCheck)
            {
                return;
            }
            var item = _checkList.Items[e.Index];
            BeginInvoke(new Action(() =>
            {
                if (e.NewValue == CheckState.Checked)
                {
                    _checkedItems.Add(item);
                }
                else
                {
                    _checkedItems.Remove(item);
                }
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }));
        };
    }

    public IReadOnlyList<object> CheckedItems => _allItems.Where(_checkedItems.Contains).ToList();

    // Repopulates the option list, keeping any previously checked values that are still present.
    public void SetItems(IEnumerable<object> items)
    {
        _allItems = items.ToList();
        _checkedItems.IntersectWith(_allItems);
        PopulateCheckList(_allItems);
        RestoreDisplayText();
    }

    // Forces a specific selection (e.g. from a shortcut button); values not yet among the available
    // options are kept pending until the next SetItems call reconciles them.
    public void SetCheckedValues(IEnumerable<object> values)
    {
        _checkedItems.Clear();
        foreach (var value in values)
        {
            _checkedItems.Add(value);
        }
        PopulateCheckList(_allItems);
        RestoreDisplayText();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ClearSelection()
    {
        if (_checkedItems.Count == 0)
        {
            return;
        }
        _checkedItems.Clear();
        PopulateCheckList(_allItems);
        RestoreDisplayText();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void PopulateCheckList(IEnumerable<object> items)
    {
        _suppressItemCheck = true;
        try
        {
            _checkList.Items.Clear();
            foreach (var item in items)
            {
                _checkList.Items.Add(item, _checkedItems.Contains(item));
            }
        }
        finally
        {
            _suppressItemCheck = false;
        }
    }

    private void DisplayBox_TextChanged(object? sender, EventArgs e)
    {
        if (_suppressTextChanged)
        {
            return;
        }

        _isSearchText = true;
        var filtered = GetFilteredItems(_displayBox.Text);
        PopulateCheckList(filtered);
        if (_popup.Visible)
        {
            ResizePopup();
        }
    }

    private void ToggleDropDown()
    {
        if (_popup.Visible)
        {
            _popup.Close();
            return;
        }

        PopulateCheckList(GetFilteredItems(_displayBox.Text));
        ShowPopup();
    }

    private IEnumerable<object> GetFilteredItems(string search)
    {
        if (!_isSearchText || search.Length == 0)
        {
            return _allItems;
        }

        return _allItems.Where(item => (item.ToString() ?? string.Empty).Contains(search, StringComparison.OrdinalIgnoreCase));
    }

    private void ShowPopup()
    {
        ResizePopup();

        if (!_popup.Visible)
        {
            _popup.Show(this, new Point(0, Height));
            BeginInvoke(new Action(() =>
            {
                _displayBox.Focus();
                _displayBox.SelectionStart = _displayBox.TextLength;
                _displayBox.SelectionLength = 0;
            }));
        }
    }

    private void ResizePopup()
    {
        var host = (ToolStripControlHost)_popup.Items[0];
        var width = Math.Max(Width, 160);
        var height = Math.Clamp(_checkList.Items.Count * 18 + 4, 40, 220);
        host.Size = new Size(width - 2, height);
        _checkList.Size = host.Size;
    }

    // Restores the box to a summary of the current selection once the popup closes (discards any leftover search text).
    private void RestoreDisplayText()
    {
        var checkedCount = _checkedItems.Count;
        var text = checkedCount switch
        {
            0 => "Todos",
            _ when checkedCount == _allItems.Count => "Todos",
            1 => _allItems.First(_checkedItems.Contains).ToString() ?? string.Empty,
            _ => "(varios)"
        };

        _suppressTextChanged = true;
        try
        {
            _displayBox.Text = text;
            _isSearchText = false;
        }
        finally
        {
            _suppressTextChanged = false;
        }
    }

    // Custom-painted arrow button that mirrors the native ComboBox dropdown button used elsewhere in the app.
    private sealed class ComboDropButton : Control
    {
        private ComboBoxState _state = ComboBoxState.Normal;

        public ComboDropButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            TabStop = false;
            Width = 17;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _state = ComboBoxState.Hot;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _state = ComboBoxState.Normal;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            _state = ComboBoxState.Pressed;
            Invalidate();
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _state = ClientRectangle.Contains(e.Location) ? ComboBoxState.Hot : ComboBoxState.Normal;
            Invalidate();
            base.OnMouseUp(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (ComboBoxRenderer.IsSupported)
            {
                ComboBoxRenderer.DrawDropDownButton(e.Graphics, ClientRectangle, _state);
            }
            else
            {
                ControlPaint.DrawComboButton(e.Graphics, ClientRectangle, _state == ComboBoxState.Pressed ? ButtonState.Pushed : ButtonState.Normal);
            }
        }
    }
}
