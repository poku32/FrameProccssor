namespace FrameProccssor.Models;

/// <summary>
/// 字段在帧中的实际位置（解析后计算得出）
/// </summary>
public class FieldPosition
{
    public FrameField Field { get; set; } = null!;

    /// <summary>字段在帧中的起始字节索引（从0开始）</summary>
    public int StartIndex { get; set; }

    /// <summary>字段在帧中的结束字节索引（包含）</summary>
    public int EndIndex { get; set; }

    /// <summary>字段占用的字节数</summary>
    public int Size => EndIndex - StartIndex + 1;

    /// <summary>判断指定索引是否在该字段范围内</summary>
    public bool Contains(int index) => index >= StartIndex && index <= EndIndex;

    /// <summary>字段内偏移</summary>
    public int FieldOffsetOf(int index) => index - StartIndex;

    public override string ToString()
        => $"[{StartIndex}-{EndIndex}] {Field.Name} ({Size}字节)";
}
