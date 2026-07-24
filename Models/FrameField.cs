using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FrameProccssor.Models;

/// <summary>
/// 字段大小类型
/// </summary>
public enum SizeKind
{
    /// <summary>固定字节数（从帧头开始计）</summary>
    Fixed,
    /// <summary>依赖其他字段的值动态计算</summary>
    Dependent,
    /// <summary>占据帧中剩余所有字节</summary>
    Remainder,
    /// <summary>固定字节数，但从帧尾往前倒数（如尾部 CRC）</summary>
    TailFixed
}

/// <summary>
/// 帧字段定义（支持属性变更通知，用于 DataGrid 编辑）
/// </summary>
public class FrameField : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private SizeKind _sizeKind = SizeKind.Fixed;
    private int _fixedSize = 1;
    private string _dependsOn = string.Empty;
    private int _multiplier = 1;
    private int _offset = 0;

    public string Name
    {
        get => _name;
        set { if (_name != value) { _name = value; OnPropertyChanged(); OnPropertyChanged(nameof(SizeDisplay)); } }
    }

    public SizeKind SizeKind
    {
        get => _sizeKind;
        set { if (_sizeKind != value) { _sizeKind = value; OnPropertyChanged(); OnPropertyChanged(nameof(SizeDisplay)); } }
    }

    /// <summary>固定大小时的字节数</summary>
    public int FixedSize
    {
        get => _fixedSize;
        set { if (_fixedSize != value) { _fixedSize = value; OnPropertyChanged(); OnPropertyChanged(nameof(SizeDisplay)); } }
    }

    /// <summary>依赖的字段名（Dependent时使用）</summary>
    public string DependsOn
    {
        get => _dependsOn;
        set { if (_dependsOn != value) { _dependsOn = value; OnPropertyChanged(); OnPropertyChanged(nameof(SizeDisplay)); } }
    }

    /// <summary>倍率：实际大小 = 依赖字段值 × Multiplier + Offset</summary>
    public int Multiplier
    {
        get => _multiplier;
        set { if (_multiplier != value) { _multiplier = value; OnPropertyChanged(); OnPropertyChanged(nameof(SizeDisplay)); } }
    }

    /// <summary>偏移量</summary>
    public int Offset
    {
        get => _offset;
        set { if (_offset != value) { _offset = value; OnPropertyChanged(); OnPropertyChanged(nameof(SizeDisplay)); } }
    }

    /// <summary>显示用的大小描述文本</summary>
    public string SizeDisplay => SizeKind switch
    {
        SizeKind.Fixed => FixedSize.ToString(),
        SizeKind.Dependent => $"={DependsOn}×{Multiplier}+{Offset}",
        SizeKind.Remainder => "剩余",
        SizeKind.TailFixed => $"尾部{FixedSize}",
        _ => "?"
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
