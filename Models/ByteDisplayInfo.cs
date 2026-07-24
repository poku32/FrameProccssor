using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace FrameProccssor.Models;

/// <summary>
/// 单个字节的显示信息（用于字节布局可视化，支持属性变更通知）
/// </summary>
public class ByteDisplayInfo : INotifyPropertyChanged
{
    private string _hexDisplay = string.Empty;
    private int _index;
    private bool _isHighlighted;
    private bool _isSelected;
    private int _fieldColorIndex;

    public string HexDisplay
    {
        get => _hexDisplay;
        set { _hexDisplay = value; OnPropertyChanged(); }
    }

    public int Index
    {
        get => _index;
        set { _index = value; OnPropertyChanged(); }
    }

    public bool IsHighlighted
    {
        get => _isHighlighted;
        set { _isHighlighted = value; OnPropertyChanged(); }
    }

    /// <summary>是否在选中范围内（多字节选择）</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    /// <summary>所属字段的颜色索引（用于交替着色区分字段）</summary>
    public int FieldColorIndex
    {
        get => _fieldColorIndex;
        set { _fieldColorIndex = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>
/// 一行 hex dump（16字节 + 地址标签）
/// </summary>
public class HexRow
{
    public string Address { get; set; } = string.Empty;
    public List<ByteDisplayInfo> Cells { get; set; } = new();
}

/// <summary>
/// 字段分组条显示信息
/// </summary>
public class FieldGroupBar
{
    public string Label { get; set; } = string.Empty;
    public string ToolTip { get; set; } = string.Empty;
    public double DisplayWidth { get; set; } = 40;
    public Brush DisplayColor { get; set; } = Brushes.LightGray;
}
