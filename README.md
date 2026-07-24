# FrameProccssor — 帧处理工具

可变长帧结构的定位与解析工具，用于通信协议帧的快速分析。

## 功能

- **帧结构定义** — 可视化编辑字段，支持拖拽排序
- **四种字段类型** — 固定 / 依赖 / 剩余 / 尾部固定
- **Hex 自动解析** — 支持空格、换行、单字符自动补零等格式
- **字节布局** — 传统 hex dump 风格，16 字节/行 + 地址标签
- **索引定位** — 输入索引自动定位到对应字段
- **多字节选区** — Shift+点击或鼠标拖拽选择范围，右侧面板实时显示：
  - 原始十六进制 · 二进制 · ASCII · BCD
  - UInt8 / UInt16 / UInt32 / Int16 / Int32（LE & BE）
  - Float（LE & BE）
- **右键设置字段边界** — 右键点击字节 → 快速调整字段终点
- **模板保存/加载** — JSON 格式，可复用帧定义

## 下载

前往 [Releases](https://github.com/poku32/FrameProccssor/releases) 下载最新 `FrameProccssor.exe`，需安装 [.NET 8 运行时](https://dotnet.microsoft.com/download/dotnet/8.0)。

## 字段类型说明

| 类型 | 含义 | 示例 |
|------|------|------|
| **固定** | 固定字节数 | 帧头 2 字节 |
| **依赖** | 大小 = 依赖字段值 × 倍率 + 偏移 | 数据区 = 长度字段值（变长） |
| **剩余** | 前面分完剩下的全归它 | 可变长度填充 |
| **尾部固定** | 从帧尾倒数固定字节，不受前面变长影响 | 帧尾校验 CRC 2 字节 |

## 快速上手

1. 定义帧结构（上方 DataGrid）
2. 粘贴 hex 数据 → 点击「解析」
3. 输入索引自动定位字段
4. Shift+点击 / 拖拽选取字节范围 → 右侧查看多格式解析
5. 右键字节 → 快速调整字段边界

## 快捷键

| 按键 | 功能 |
|------|------|
| `Esc` | 清除高亮和选区 |
| `Enter` | 定位 |
| `Shift + 点击` | 扩展选区 |
| 鼠标拖拽 | 连续选取 |
| 点击空白 | 取消选区 |

## 从源码构建

```bash
git clone https://github.com/poku32/FrameProccssor.git
cd FrameProccssor
dotnet run

# 发布单文件
dotnet publish -c Release -o ./publish
```

.NET 8.0 SDK required.

## 协议

MIT
