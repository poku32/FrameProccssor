using System.Globalization;
using FrameProccssor.Models;

namespace FrameProccssor.Services;

/// <summary>
/// 帧解析服务：hex 字符串解析 + 字段边界计算 + 索引定位
/// </summary>
public static class FrameParser
{
    /// <summary>
    /// 解析十六进制字符串为字节数组。支持空格、换行、0x前缀等分隔。
    /// 自动补全单个 hex 字符（如 "8" → "08", "A" → "0A"）。
    /// 示例: "AA 55 08 01 02 03" 或 "AA5508010203" 或 "fb 69 8 50"
    /// </summary>
    public static byte[] ParseHex(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
            return Array.Empty<byte>();

        // 先按空格和常见分隔符拆成 token，逐 token 补零
        var tokens = hex
            .Replace("0x", " ")
            .Replace("0X", " ")
            .Replace(",", " ")
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Replace("\t", " ")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var cleaned = new System.Text.StringBuilder();
        foreach (var token in tokens)
        {
            if (token.Length == 1)
                cleaned.Append('0').Append(token);  // 单字符补零
            else if (token.Length == 2)
                cleaned.Append(token);
            else if (token.Length > 2)
                cleaned.Append(token);  // 可能是无分隔符的连续 hex
            // 长度为 0 跳过
        }

        var hexString = cleaned.ToString();

        if (hexString.Length % 2 != 0)
            throw new FormatException($"十六进制字符串长度必须为偶数（当前长度: {hexString.Length}）。请检查是否有单个字符的 hex 值。");

        var bytes = new byte[hexString.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = byte.Parse(hexString.AsSpan(i * 2, 2), NumberStyles.HexNumber);
        }
        return bytes;
    }

    /// <summary>
    /// 根据字段定义和实际数据，计算每个字段的起始/结束位置。
    /// </summary>
    public static List<FieldPosition> CalculatePositions(IReadOnlyList<FrameField> fields, byte[] data)
    {
        var positions = new List<FieldPosition>();
        var remainderFields = new List<(FrameField field, int order)>();
        var tailFixedFields = new List<(FrameField field, int order)>();

        // 统计尾部固定字段总字节数
        int totalTailSize = 0;
        foreach (var f in fields)
            if (f.SizeKind == SizeKind.TailFixed)
                totalTailSize += f.FixedSize;

        int startSpace = data.Length - totalTailSize;
        if (startSpace < 0) startSpace = 0;

        // 第一遍：从前往后处理 Fixed / Dependent
        int offset = 0;
        int order = 0;
        foreach (var field in fields)
        {
            if (field.SizeKind == SizeKind.Remainder)
            {
                remainderFields.Add((field, order++));
                continue;
            }
            if (field.SizeKind == SizeKind.TailFixed)
            {
                tailFixedFields.Add((field, order++));
                continue;
            }

            int size = CalculateFieldSize(field, data, positions);
            if (offset + size > startSpace)
                size = startSpace - offset;
            if (size <= 0) continue;

            positions.Add(new FieldPosition
            {
                Field = field,
                StartIndex = offset,
                EndIndex = offset + size - 1
            });
            offset += size;
        }

        // 第二遍：处理 Remainder（在 startSpace 剩余空间内瓜分）
        if (remainderFields.Count > 0 && offset < startSpace)
        {
            int remaining = startSpace - offset;
            int perField = remaining / remainderFields.Count;
            int extra = remaining % remainderFields.Count;

            foreach (var (field, _) in remainderFields)
            {
                int size = perField + (extra > 0 ? 1 : 0);
                if (extra > 0) extra--;
                if (size <= 0) continue;

                positions.Add(new FieldPosition
                {
                    Field = field,
                    StartIndex = offset,
                    EndIndex = offset + size - 1
                });
                offset += size;
            }
        }

        // 第三遍：从后往前处理 TailFixed（倒序：最后一个定义紧贴帧尾）
        int tailOffset = data.Length;
        for (int i = tailFixedFields.Count - 1; i >= 0; i--)
        {
            var field = tailFixedFields[i].field;
            int size = field.FixedSize;
            if (size > tailOffset || size <= 0) continue;

            positions.Add(new FieldPosition
            {
                Field = field,
                StartIndex = tailOffset - size,
                EndIndex = tailOffset - 1
            });
            tailOffset -= size;
        }

        return positions;
    }

    /// <summary>
    /// 计算单个字段的大小（字节数）
    /// </summary>
    private static int CalculateFieldSize(FrameField field, byte[] data, List<FieldPosition> resolvedPositions)
    {
        return field.SizeKind switch
        {
            SizeKind.Fixed => field.FixedSize,
            SizeKind.Dependent => GetDependentSize(field, data, resolvedPositions),
            _ => 0
        };
    }

    /// <summary>
    /// 获取 Dependent 字段的实际大小：依赖字段的运行时值 × Multiplier + Offset
    /// </summary>
    private static int GetDependentSize(FrameField field, byte[] data, List<FieldPosition> resolvedPositions)
    {
        // 找到依赖字段的位置
        var depPosition = resolvedPositions.FirstOrDefault(p => p.Field.Name == field.DependsOn);
        if (depPosition == null)
            return 0;

        // 从 data 中提取依赖字段的值（大端序解析）
        int value = ReadBigEndianInt(data, depPosition.StartIndex, depPosition.Size);
        return value * field.Multiplier + field.Offset;
    }

    /// <summary>
    /// 以大端序从字节数组中读取整数值
    /// </summary>
    private static int ReadBigEndianInt(byte[] data, int start, int length)
    {
        int value = 0;
        for (int i = 0; i < length && start + i < data.Length; i++)
        {
            value = (value << 8) | data[start + i];
        }
        return value;
    }

    /// <summary>
    /// 定位指定索引属于哪个字段。返回字段位置和字段内偏移。
    /// </summary>
    public static (FieldPosition? Position, int FieldOffset) Locate(int index, List<FieldPosition> positions)
    {
        foreach (var pos in positions)
        {
            if (pos.Contains(index))
                return (pos, pos.FieldOffsetOf(index));
        }
        return (null, -1);
    }
}
