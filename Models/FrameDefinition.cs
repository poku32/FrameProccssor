namespace FrameProccssor.Models;

/// <summary>
/// 帧结构定义（可保存/加载为JSON模板）
/// </summary>
public class FrameDefinition
{
    public string Name { get; set; } = "未命名";
    public List<FrameField> Fields { get; set; } = new();
}
