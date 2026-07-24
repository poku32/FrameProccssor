using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows.Media;
using FrameProccssor.Models;
using FrameProccssor.Services;
using Microsoft.Win32;

namespace FrameProccssor.ViewModels;

public class MainViewModel : BaseViewModel
{
    private const string TemplateFilter = "帧模板 JSON|*.json|所有文件|*.*";
    private const int BytesPerRow = 16;
    private const double CellWidth = 44;  // 每个字节格宽度
    private const double CellMargin = 2;  // margin 左右各1
    private const double AddressWidth = 55; // 地址标签宽度

    /// <summary>解析完成后触发，用于窗口居中</summary>
    public event Action? Parsed;

    public MainViewModel()
    {
        AddFieldInternal(new FrameField { Name = "帧头", SizeKind = SizeKind.Fixed, FixedSize = 2 });
        AddFieldInternal(new FrameField { Name = "长度", SizeKind = SizeKind.Fixed, FixedSize = 1 });
        AddFieldInternal(new FrameField { Name = "地址", SizeKind = SizeKind.Dependent, DependsOn = "长度" });
        AddFieldInternal(new FrameField { Name = "数据", SizeKind = SizeKind.Remainder });
        AddFieldInternal(new FrameField { Name = "校验", SizeKind = SizeKind.TailFixed, FixedSize = 2 });
        AddFieldInternal(new FrameField { Name = "帧尾", SizeKind = SizeKind.TailFixed, FixedSize = 0 });


        Fields.CollectionChanged += (_, _) => RefreshDependencySources();

        AddFieldCommand = new RelayCommand(AddField);
        DeleteFieldCommand = new RelayCommand(DeleteField, _ => SelectedField != null);
        ParseCommand = new RelayCommand(Parse);
        LocateCommand = new RelayCommand(Locate, _ => FieldPositions.Count > 0);
        SaveTemplateCommand = new RelayCommand(SaveTemplate);
        LoadTemplateCommand = new RelayCommand(LoadTemplate);
    }

    // ==================== 属性 ====================

    public ObservableCollection<FrameField> Fields { get; } = new();

    private FrameField? _selectedField;
    public FrameField? SelectedField
    {
        get => _selectedField;
        set
        {
            if (SetProperty(ref _selectedField, value))
            {
                OnPropertyChanged(nameof(IsDependentSelected));
                OnPropertyChanged(nameof(IsFieldSelected));
            }
        }
    }

    public bool IsFieldSelected => SelectedField != null;
    public bool IsDependentSelected => SelectedField?.SizeKind == SizeKind.Dependent;

    public List<KeyValuePair<string, SizeKind>> SizeKinds { get; } = new()
    {
        new("固定", SizeKind.Fixed),
        new("依赖", SizeKind.Dependent),
        new("剩余", SizeKind.Remainder),
        new("尾部固定", SizeKind.TailFixed)
    };

    private string _hexString = "";
    public string HexString
    {
        get => _hexString;
        set => SetProperty(ref _hexString, value);
    }

    private string _indexInput = "";

    public string IndexInput
    {
        get => _indexInput;
        set => SetProperty(ref _indexInput, value);
    }

    /// <summary>由 TextBox.TextChanged 调用，每次输入时先清高亮再定位</summary>
    public void HandleIndexChanged()
    {
        ClearAllHighlights();

        if (!int.TryParse(IndexInput.Trim(), out int index) || ParsedBytes == null)
            return;

        if (index < 0 || index >= ParsedBytes.Length)
        {
            StatusMessage = $"索引 {index} 超出范围 [0, {ParsedBytes.Length - 1}]。";
            return;
        }

        var (position, fieldOffset) = FrameParser.Locate(index, FieldPositions);

        SelectedField = position?.Field;
        HighlightIndex = position != null ? index : null;
        LocateResult = position != null
            ? $"索引 {index} → 字段「{position.Field.Name}」"
              + $"｜帧内偏移: {index}｜字段内偏移: {fieldOffset}"
              + $"｜字段范围: [{position.StartIndex}, {position.EndIndex}]"
              + $"｜字段大小: {position.Size} 字节"
            : $"索引 {index}：不在任何已定义的字段范围内。";

        RefreshHighlights();
        StatusMessage = "";
    }

    private void ClearAllHighlights()
    {
        HighlightIndex = null;
        foreach (var row in HexRows)
            foreach (var cell in row.Cells)
                cell.IsHighlighted = false;
    }

    private void RefreshHighlights()
    {
        foreach (var row in HexRows)
            foreach (var cell in row.Cells)
                cell.IsHighlighted = cell.Index == HighlightIndex;
    }

    private byte[]? _parsedBytes;
    public byte[]? ParsedBytes
    {
        get => _parsedBytes;
        set
        {
            if (SetProperty(ref _parsedBytes, value))
                OnPropertyChanged(nameof(ParsedBytesDisplay));
        }
    }

    public string ParsedBytesDisplay
    {
        get
        {
            if (ParsedBytes == null || ParsedBytes.Length == 0)
                return "（尚未解析）";
            return string.Join(" ", ParsedBytes.Select(b => b.ToString("X2")));
        }
    }

    private List<FieldPosition> _fieldPositions = new();
    public List<FieldPosition> FieldPositions
    {
        get => _fieldPositions;
        set
        {
            if (SetProperty(ref _fieldPositions, value))
                OnPropertyChanged(nameof(FieldSummary));
        }
    }

    /// <summary>hex dump 行列表（每行16字节 + 地址）</summary>
    public ObservableCollection<HexRow> HexRows { get; } = new();

    /// <summary>字段映射摘要文本</summary>
    public string FieldSummary
    {
        get
        {
            if (FieldPositions.Count == 0)
                return "";
            return "字段: " + string.Join(" | ",
                FieldPositions.Select(fp =>
                    $"{fp.Field.Name}[{fp.StartIndex}-{fp.EndIndex}]={fp.Size}B"));
        }
    }

    /// <summary>字段分组条（下方彩色条）</summary>
    public ObservableCollection<FieldGroupBar> FieldGroupBars { get; } = new();

    private string _locateResult = "";
    public string LocateResult
    {
        get => _locateResult;
        set => SetProperty(ref _locateResult, value);
    }

    private int? _highlightIndex;
    public int? HighlightIndex
    {
        get => _highlightIndex;
        set => SetProperty(ref _highlightIndex, value);
    }

    // ==================== 多字节选区 ====================

    private int? _selectionStart;
    public int? SelectionStart
    {
        get => _selectionStart;
        set { if (SetProperty(ref _selectionStart, value)) ComputeSelection(); }
    }

    private int? _selectionEnd;
    public int? SelectionEnd
    {
        get => _selectionEnd;
        set { if (SetProperty(ref _selectionEnd, value)) ComputeSelection(); }
    }

    private string _selectedHex = "";
    public string SelectedHex
    {
        get => _selectedHex;
        set => SetProperty(ref _selectedHex, value);
    }

    private string _selectedInfo = "";
    public string SelectedInfo
    {
        get => _selectedInfo;
        set => SetProperty(ref _selectedInfo, value);
    }

    public bool HasSelection => SelectionStart.HasValue && SelectionEnd.HasValue;

    /// <summary>
    /// 设置选区（由代码后置在 ByteCell_Click 中调用）
    /// </summary>
    public void SetSelection(int index, bool shiftKey)
    {
        if (!shiftKey || SelectionStart == null)
        {
            // 新选区起点 = 终点（单击单选）
            SelectionStart = index;
            SelectionEnd = index;
        }
        else
        {
            // Shift+点击 = 扩展选区终点
            int start = SelectionStart.Value;
            SelectionEnd = index;
            if (SelectionStart > SelectionEnd)
            {
                SelectionStart = SelectionEnd;
                SelectionEnd = start;
            }
        }

        // 刷新所有格子选中状态
        RefreshCellSelection();
        OnPropertyChanged(nameof(HasSelection));
    }

    /// <summary>清除选区</summary>
    public void ClearSelection()
    {
        SelectionStart = null;
        SelectionEnd = null;
        SelectedHex = "";
        SelectedInfo = "";
        OnPropertyChanged(nameof(HasSelection));
        RefreshCellSelection();
    }

    private void RefreshCellSelection()
    {
        int start = SelectionStart ?? -1;
        int end = SelectionEnd ?? -1;
        if (end < start) end = start;

        foreach (var row in HexRows)
            foreach (var cell in row.Cells)
                cell.IsSelected = cell.Index >= start && cell.Index <= end && start >= 0;
    }

    /// <summary>根据选区计算各格式解析结果</summary>
    private void ComputeSelection()
    {
        if (ParsedBytes == null || SelectionStart == null || SelectionEnd == null)
        {
            SelectedHex = "";
            SelectedInfo = "";
            return;
        }

        int start = SelectionStart.Value;
        int end = SelectionEnd.Value;
        if (start > end) (start, end) = (end, start);
        if (start < 0 || end >= ParsedBytes.Length) return;

        int len = end - start + 1;
        var slice = ParsedBytes.AsSpan(start, len);

        // 原始 hex
        SelectedHex = string.Join(" ", slice.ToArray().Select(b => b.ToString("X2")));

        // 多格式解析
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"选区: [{start}-{end}] 共 {len} 字节");

        // 二进制
        var bin = string.Join(" ", slice.ToArray().Select(b => Convert.ToString(b, 2).PadLeft(8, '0')));
        sb.AppendLine($"二进制: {bin}");

        // ASCII（可打印的显示，否则显示 .）
        var ascii = string.Create(slice.Length, slice.ToArray(), (chars, bytes) =>
        {
            for (int i = 0; i < bytes.Length; i++)
                chars[i] = bytes[i] >= 32 && bytes[i] < 127 ? (char)bytes[i] : '.';
        });
        sb.AppendLine($"ASCII: \"{ascii}\"");

        // BCD 解码
        sb.AppendLine("── BCD ──");
        var bcdParts = new List<string>();
        ulong bcdAll = 0;
        bool bcdValid = true;
        for (int i = 0; i < slice.Length && bcdValid; i++)
        {
            int hi = slice[i] >> 4;
            int lo = slice[i] & 0x0F;
            if (hi > 9 || lo > 9) { bcdValid = false; break; }
            bcdAll = bcdAll * 100 + (ulong)(hi * 10 + lo);
            bcdParts.Add($"{slice[i]:X2}h={hi*10+lo}");
        }
        if (bcdValid && slice.Length > 0)
        {
            // 按字节的 BCD 值
            sb.Append("按字节: ");
            foreach (var p in bcdParts) sb.Append(p).Append(' ');
            sb.AppendLine();
            // 整体 BCD 数值
            if (slice.Length <= 8)
                sb.AppendLine($"整体值: {bcdAll}");
            else
                sb.AppendLine($"整体值: (超长，仅显示按字节)");
        }
        else if (slice.Length > 0)
        {
            sb.AppendLine("(含非法BCD位)");
        }

        // UInt8
        if (len == 1)
        {
            sb.AppendLine($"UInt8:  {slice[0]} (0x{slice[0]:X2})");
            sb.AppendLine($"Char:   '{(char)slice[0]}'");
        }

        // UInt16 LE/BE
        if (len >= 2)
        {
            ushort u16le = (ushort)(slice[0] | (slice[1] << 8));
            ushort u16be = (ushort)((slice[0] << 8) | slice[1]);
            sb.AppendLine($"UInt16 LE: {u16le} (0x{u16le:X4})");
            sb.AppendLine($"UInt16 BE: {u16be} (0x{u16be:X4})");
        }

        // UInt32 LE/BE
        if (len >= 4)
        {
            uint u32le = 0;
            for (int i = 0; i < 4; i++) u32le |= (uint)slice[i] << (i * 8);
            uint u32be = 0;
            for (int i = 0; i < 4; i++) u32be = (u32be << 8) | slice[i];
            sb.AppendLine($"UInt32 LE: {u32le} (0x{u32le:X8})");
            sb.AppendLine($"UInt32 BE: {u32be} (0x{u32be:X8})");

            // Int32 LE/BE
            sb.AppendLine($"Int32  LE: {(int)u32le}");
            sb.AppendLine($"Int32  BE: {(int)u32be}");

            // Float LE/BE
            float fle = BitConverter.ToSingle(slice.Slice(0, 4));
            var beBytes = new byte[4] { slice[3], slice[2], slice[1], slice[0] };
            float fbe = BitConverter.ToSingle(beBytes);
            sb.AppendLine($"Float  LE: {fle:G7}");
            sb.AppendLine($"Float  BE: {fbe:G7}");
        }

        // Int16 LE/BE
        if (len >= 2)
        {
            short i16le = (short)(slice[0] | (slice[1] << 8));
            short i16be = (short)((slice[0] << 8) | slice[1]);
            sb.Append($"Int16  LE: {i16le}   BE: {i16be}");
        }

        SelectedInfo = sb.ToString();
        OnPropertyChanged(nameof(HasSelection));

        // 同时更新高亮（定位到选区起点）
        HighlightIndex = start;
        foreach (var row in HexRows)
            foreach (var cell in row.Cells)
                cell.IsHighlighted = cell.Index == HighlightIndex;
    }

    private string _statusMessage = "就绪";
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    private string _templateName = "未命名";
    public string TemplateName
    {
        get => _templateName;
        set => SetProperty(ref _templateName, value);
    }

    public List<string> DependencySourceFields
    {
        get
        {
            return Fields
                .Where(f => f.SizeKind != SizeKind.Remainder && !string.IsNullOrEmpty(f.Name))
                .Select(f => f.Name)
                .ToList();
        }
    }

    // ==================== 命令 ====================

    public RelayCommand AddFieldCommand { get; }
    public RelayCommand DeleteFieldCommand { get; }
    public RelayCommand ParseCommand { get; }
    public RelayCommand LocateCommand { get; }
    public RelayCommand SaveTemplateCommand { get; }
    public RelayCommand LoadTemplateCommand { get; }

    private void AddField(object? _)
    {
        AddFieldInternal(new FrameField { Name = $"字段{Fields.Count + 1}" });
        RefreshDependencySources();
    }

    private void AddFieldInternal(FrameField field)
    {
        field.PropertyChanged += (_, _) => RefreshDependencySources();
        Fields.Add(field);
    }

    private void DeleteField(object? parameter)
    {
        FrameField? toRemove = parameter as FrameField ?? SelectedField;
        if (toRemove != null && Fields.Contains(toRemove))
        {
            Fields.Remove(toRemove);
            if (SelectedField == toRemove) SelectedField = null;
            RefreshDependencySources();
        }
    }

    /// <summary>右键菜单：将字段终点设为指定字节索引</summary>
    public void SetFieldEnd(string fieldName, int endIndex)
    {
        var field = Fields.FirstOrDefault(f => f.Name == fieldName);
        if (field == null) return;

        // 找到该字段当前的起始位置
        if (ParsedBytes == null) return;
        var positions = FrameParser.CalculatePositions(Fields, ParsedBytes);
        var pos = positions.FirstOrDefault(p => p.Field.Name == fieldName);
        int startIndex = pos?.StartIndex ?? 0;

        int newSize = endIndex - startIndex + 1;
        if (newSize <= 0) return;

        if (field.SizeKind == SizeKind.Fixed || field.SizeKind == SizeKind.TailFixed)
        {
            field.FixedSize = newSize;
        }
        else if (field.SizeKind == SizeKind.Dependent)
        {
            // 尝试反推：size = depValue × Multiplier + Offset → Offset = size - depValue × Multiplier
            // 保持 Multiplier 不变，调整 Offset
            var depPos = positions.FirstOrDefault(p => p.Field.Name == field.DependsOn);
            if (depPos != null && ParsedBytes != null)
            {
                int depValue = ReadBigInt(ParsedBytes, depPos.StartIndex, depPos.Size);
                field.Offset = newSize - depValue * field.Multiplier;
            }
            else
            {
                field.Offset = newSize;
                field.Multiplier = 1;
            }
        }

        StatusMessage = $"字段「{fieldName}」终点已设为索引 {endIndex}（新大小: {newSize} 字节）";
        // 重新解析
        ParseCommand.Execute(null);
    }

    private static int ReadBigInt(byte[] data, int start, int length)
    {
        int value = 0;
        for (int i = 0; i < length && start + i < data.Length; i++)
            value = (value << 8) | data[start + i];
        return value;
    }

    /// <summary>拖拽排序：移动字段位置</summary>
    public void MoveField(int oldIndex, int newIndex)
    {
        if (oldIndex < 0 || oldIndex >= Fields.Count) return;
        if (newIndex < 0 || newIndex >= Fields.Count) return;
        if (oldIndex == newIndex) return;

        Fields.Move(oldIndex, newIndex);
        RefreshDependencySources();
    }

    private void Parse(object? _)
    {
        try
        {
            ParsedBytes = FrameParser.ParseHex(HexString);
            if (ParsedBytes.Length == 0)
            {
                StatusMessage = "请输入十六进制帧数据。";
                ClearVisuals();
                return;
            }

            FieldPositions = FrameParser.CalculatePositions(Fields, ParsedBytes);
            HighlightIndex = null;
            LocateResult = "";

            BuildHexRows();
            BuildFieldGroupBars();

            StatusMessage = $"解析完成：{ParsedBytes.Length} 个字节，{FieldPositions.Count} 个字段。";
            Parsed?.Invoke();
        }
        catch (Exception ex)
        {
            StatusMessage = $"解析失败：{ex.Message}";
            ParsedBytes = null;
            FieldPositions = new();
            ClearVisuals();
        }
    }

    private void Locate(object? _)
    {
        if (!int.TryParse(IndexInput.Trim(), out int index))
        {
            StatusMessage = "请输入有效的数字索引。";
            return;
        }

        if (ParsedBytes == null || ParsedBytes.Length == 0)
        {
            StatusMessage = "请先解析帧数据。";
            return;
        }

        if (index < 0 || index >= ParsedBytes.Length)
        {
            StatusMessage = $"索引 {index} 超出范围 [0, {ParsedBytes.Length - 1}]。";
            return;
        }

        var (position, fieldOffset) = FrameParser.Locate(index, FieldPositions);

        SelectedField = position?.Field;
        if (position == null)
        {
            LocateResult = $"索引 {index}：不在任何已定义的字段范围内。";
            HighlightIndex = null;
        }
        else
        {
            LocateResult = $"索引 {index} → 字段「{position.Field.Name}」"
                         + $"｜帧内偏移: {index}｜字段内偏移: {fieldOffset}"
                         + $"｜字段范围: [{position.StartIndex}, {position.EndIndex}]"
                         + $"｜字段大小: {position.Size} 字节";
            HighlightIndex = index;
        }

        // 更新所有行的高亮状态
        foreach (var row in HexRows)
            foreach (var cell in row.Cells)
                cell.IsHighlighted = cell.Index == HighlightIndex;

        StatusMessage = "定位完成。";
    }

    private void SaveTemplate(object? _)
    {
        var dialog = new SaveFileDialog
        {
            Filter = TemplateFilter,
            DefaultExt = ".json",
            FileName = $"{TemplateName}.json"
        };

        if (dialog.ShowDialog() == true)
        {
            var def = new FrameDefinition { Name = TemplateName, Fields = Fields.ToList() };
            var json = JsonSerializer.Serialize(def, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(dialog.FileName, json);
            StatusMessage = $"模板已保存到：{dialog.FileName}";
        }
    }

    private void LoadTemplate(object? _)
    {
        var dialog = new OpenFileDialog { Filter = TemplateFilter, DefaultExt = ".json" };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var json = File.ReadAllText(dialog.FileName);
                var def = JsonSerializer.Deserialize<FrameDefinition>(json);
                if (def == null || def.Fields.Count == 0)
                {
                    StatusMessage = "模板文件为空或格式错误。";
                    return;
                }

                Fields.Clear();
                foreach (var f in def.Fields)
                {
                    f.PropertyChanged += (_, _) => RefreshDependencySources();
                    Fields.Add(f);
                }

                TemplateName = def.Name;
                RefreshDependencySources();
                StatusMessage = $"模板「{def.Name}」已加载，{Fields.Count} 个字段。";
            }
            catch (Exception ex)
            {
                StatusMessage = $"加载模板失败：{ex.Message}";
            }
        }
    }

    // ==================== 可视化构建 ====================

    private static readonly Brush[] FieldBrushes = new Brush[]
    {
        new SolidColorBrush(Color.FromRgb(0xE8, 0xF5, 0xE9)),
        new SolidColorBrush(Color.FromRgb(0xE3, 0xF2, 0xFD)),
        new SolidColorBrush(Color.FromRgb(0xFF, 0xF3, 0xE0)),
        new SolidColorBrush(Color.FromRgb(0xF3, 0xE5, 0xF5)),
        new SolidColorBrush(Color.FromRgb(0xE0, 0xF7, 0xFA)),
        new SolidColorBrush(Color.FromRgb(0xFF, 0xF9, 0xC4)),
    };

    private void BuildHexRows()
    {
        HexRows.Clear();
        if (ParsedBytes == null) return;

        int totalRows = (ParsedBytes.Length + BytesPerRow - 1) / BytesPerRow;

        for (int row = 0; row < totalRows; row++)
        {
            var hexRow = new HexRow
            {
                Address = (row * BytesPerRow).ToString("X4")
            };

            int start = row * BytesPerRow;
            int end = Math.Min(start + BytesPerRow, ParsedBytes.Length);

            for (int i = start; i < end; i++)
            {
                int colorIndex = 0;
                for (int fi = 0; fi < FieldPositions.Count; fi++)
                {
                    if (FieldPositions[fi].Contains(i))
                    {
                        colorIndex = fi % FieldBrushes.Length;
                        break;
                    }
                }

                hexRow.Cells.Add(new ByteDisplayInfo
                {
                    HexDisplay = ParsedBytes[i].ToString("X2"),
                    Index = i,
                    IsHighlighted = i == HighlightIndex,
                    FieldColorIndex = colorIndex
                });
            }

            HexRows.Add(hexRow);
        }
    }

    private void BuildFieldGroupBars()
    {
        FieldGroupBars.Clear();
        if (ParsedBytes == null || ParsedBytes.Length == 0) return;

        double totalWidth = ParsedBytes.Length * (CellWidth + CellMargin);

        foreach (var fp in FieldPositions)
        {
            int fieldIndex = FieldPositions.IndexOf(fp);
            var brush = FieldBrushes[fieldIndex % FieldBrushes.Length];

            // 按比例计算宽度
            double ratio = (double)fp.Size / ParsedBytes.Length;
            double barWidth = Math.Max(ratio * totalWidth, 24);

            FieldGroupBars.Add(new FieldGroupBar
            {
                Label = fp.Field.Name,
                ToolTip = $"{fp.Field.Name} [{fp.StartIndex}-{fp.EndIndex}] {fp.Size}字节",
                DisplayWidth = barWidth,
                DisplayColor = brush
            });
        }
    }

    private void ClearVisuals()
    {
        HexRows.Clear();
        FieldGroupBars.Clear();
    }

    public void RefreshDependencySources()
    {
        OnPropertyChanged(nameof(DependencySourceFields));
    }
}
