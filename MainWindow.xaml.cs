using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FrameProccssor.Models;
using FrameProccssor.ViewModels;

namespace FrameProccssor;

public partial class MainWindow : Window
{
    private MainViewModel VM => (MainViewModel)DataContext;

    private Point _dragStartPoint;
    private int _dragSourceIndex = -1;
    private bool _isDragging;
    private DataGridRow? _lastHighlightedRow;
    private bool _needsCenter;

    public MainWindow()
    {
        InitializeComponent();

        var iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico");
        if (System.IO.File.Exists(iconPath))
            Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri(iconPath));

        FieldsGrid.AddHandler(PreviewMouseLeftButtonDownEvent,
            new MouseButtonEventHandler(FieldsGrid_PreviewMouseLeftButtonDown), true);
        FieldsGrid.AddHandler(PreviewMouseMoveEvent,
            new MouseEventHandler(FieldsGrid_PreviewMouseMove), true);

        VM.Parsed += () => _needsCenter = true;
        SizeChanged += (_, _) =>
        {
            if (_needsCenter)
            {
                _needsCenter = false;
                Left = (SystemParameters.WorkArea.Width - ActualWidth) / 2;
                Top = (SystemParameters.WorkArea.Height - ActualHeight) / 2;
            }
        };

        KeyDown += (s, e) =>
        {
            if (e.Key == Key.Escape)
            {
                VM.ClearSelection();
                VM.HighlightIndex = null;
                foreach (var row in VM.HexRows)
                    foreach (var cell in row.Cells)
                        cell.IsHighlighted = false;
            }
            else if (e.Key == Key.Enter && !IsDataGridEditing())
            {
                VM.LocateCommand.Execute(null);
            }
        };
    }

    // ==================== 索引输入自动定位 ====================

    private void IndexTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        VM.HandleIndexChanged();
    }

    // ==================== 字段拖拽排序 ====================

    private void FieldsGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
        _isDragging = false;

        var row = FindParent<DataGridRow>(e.OriginalSource as DependencyObject);
        _dragSourceIndex = row != null
            ? FieldsGrid.ItemContainerGenerator.IndexFromContainer(row)
            : -1;
    }

    private void FieldsGrid_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragSourceIndex < 0 || _isDragging)
            return;

        var currentPos = e.GetPosition(null);
        var diff = _dragStartPoint - currentPos;

        if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
            Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
        {
            _isDragging = true;
            var data = new DataObject(typeof(int), _dragSourceIndex);
            DragDrop.DoDragDrop(FieldsGrid, data, DragDropEffects.Move);
            _isDragging = false;
            _dragSourceIndex = -1;
            ClearRowHighlight();
        }
    }

    private void DataGrid_DragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(int)))
        {
            e.Effects = DragDropEffects.None;
            return;
        }

        e.Effects = DragDropEffects.Move;
        e.Handled = true;

        var targetRow = FindRowAtPoint(e.GetPosition(FieldsGrid));
        if (targetRow != _lastHighlightedRow)
        {
            ClearRowHighlight();
            if (targetRow != null)
            {
                targetRow.Background = new SolidColorBrush(Color.FromRgb(0xCD, 0xE6, 0xFD));
                _lastHighlightedRow = targetRow;
            }
        }
    }

    private void DataGrid_Drop(object sender, DragEventArgs e)
    {
        ClearRowHighlight();
        if (!e.Data.GetDataPresent(typeof(int))) return;

        int oldIndex = (int)e.Data.GetData(typeof(int));
        var targetRow = FindRowAtPoint(e.GetPosition(FieldsGrid));
        int newIndex = targetRow != null
            ? FieldsGrid.ItemContainerGenerator.IndexFromContainer(targetRow)
            : VM.Fields.Count - 1;

        VM.MoveField(oldIndex, newIndex);
    }

    private void DataGrid_DragLeave(object sender, DragEventArgs e)
    {
        ClearRowHighlight();
    }

    private DataGridRow? FindRowAtPoint(Point point)
    {
        var hit = VisualTreeHelper.HitTest(FieldsGrid, point);
        return hit?.VisualHit != null
            ? FindParent<DataGridRow>(hit.VisualHit)
            : null;
    }

    private void ClearRowHighlight()
    {
        if (_lastHighlightedRow != null)
        {
            _lastHighlightedRow.Background = Brushes.Transparent;
            _lastHighlightedRow = null;
        }
    }

    // ==================== 选区清除 ====================

    private void ClearSelection_Click(object sender, RoutedEventArgs e)
    {
        VM.ClearSelection();
    }

    // ==================== 字节点击 ====================

    private void ByteCell_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is ByteDisplayInfo info)
        {
            bool shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
            VM.IndexInput = info.Index.ToString();
            VM.SetSelection(info.Index, shift);
            VM.LocateCommand.Execute(null);
        }
    }

    // ==================== 右键菜单：设为字段终点 ====================

    private int _rightClickIndex = -1;

    private void ByteCell_RightClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is ByteDisplayInfo info)
            _rightClickIndex = info.Index;
    }

    private void FieldContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (_rightClickIndex < 0) return;
        if (sender is ContextMenu menu)
        {
            menu.Items.Clear();
            foreach (var field in VM.Fields)
            {
                var item = new MenuItem { Header = $"设为「{field.Name}」终点" };
                var fieldName = field.Name;
                var idx = _rightClickIndex;
                item.Click += (_, _) => VM.SetFieldEnd(fieldName, idx);
                menu.Items.Add(item);
            }
        }
    }

    private void ByteCell_MouseEnter(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        if (sender is FrameworkElement element && element.DataContext is ByteDisplayInfo info)
        {
            if (VM.SelectionStart.HasValue)
            {
                VM.SetSelection(info.Index, shiftKey: true);
                VM.IndexInput = info.Index.ToString();
                VM.HighlightIndex = info.Index;
                foreach (var row in VM.HexRows)
                    foreach (var cell in row.Cells)
                        cell.IsHighlighted = cell.Index == VM.HighlightIndex;
            }
        }
    }

    private void ByteLayout_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var src = e.OriginalSource as DependencyObject;
        var cellBorder = FindParent<Border>(src);
        if (cellBorder == null || cellBorder.DataContext is not ByteDisplayInfo)
        {
            VM.ClearSelection();
            VM.HighlightIndex = null;
            foreach (var row in VM.HexRows)
                foreach (var cell in row.Cells)
                    cell.IsHighlighted = false;
        }
    }

    // ==================== 辅助 ====================

    private static T? FindParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child != null && child is not T)
            child = VisualTreeHelper.GetParent(child);
        return child as T;
    }

    private bool IsDataGridEditing()
    {
        var focused = FocusManager.GetFocusedElement(this);
        if (focused is TextBox tb)
            return tb.Name == "DG_EditingControl" || tb.Parent is DataGridCell;
        return false;
    }
}
