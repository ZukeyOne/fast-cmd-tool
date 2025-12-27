# FastTools 设计文档

## 项目概述

FastTools 是一个简单的 Windows 桌面应用程序，使用 C# 和 WPF 框架开发。它的主要目的是将常用的 CMD 命令转换为易用的按钮界面，让用户无需记忆复杂的命令行参数，只需点击按钮即可执行命令并查看结果。

### 主要目标
- 简化 CMD 命令的使用
- 提供动态可配置的按钮
- 支持中文输出和界面
- 易于扩展和维护

## 功能特性

### 核心功能
1. **动态按钮加载**：从 `commands.json` 文件加载请求按钮，支持多步骤
2. **请求执行**：点击按钮串行执行步骤列表（命令或延时），请求间串行执行
3. **输出显示**：实时显示请求执行结果，每个请求独立可收缩面板
4. **删除按钮**：右键菜单删除不需要的按钮
5. **输出清除**：一键清除输出窗口

### 用户体验
- 悬停显示完整命令（ToolTip）
- 滚动查看多个按钮
- 错误处理和提示
- 中文界面支持

## 系统架构

### 整体架构
```
[用户界面] <-> [MainWindow.xaml.cs] <-> [commands.json]
     |
     v
[Process 执行] -> [CMD 命令] -> [输出捕获]
```

### 组件关系
- **UI 层**：WPF XAML 文件定义界面布局
- **逻辑层**：MainWindow.xaml.cs 处理用户交互和业务逻辑
- **数据层**：commands.json 存储命令配置
- **执行层**：System.Diagnostics.Process 执行 CMD 命令

## 组件描述

### 1. 主窗口 (MainWindow.xaml)
- **布局**：左侧按钮面板 + 右侧输出面板
- **控件**：
  - ScrollViewer：容纳动态按钮
  - StackPanel (BtnPanel)：动态添加按钮
  - TextBox (TxtOutput)：显示命令输出
  - 输入框：用于添加新按钮和自定义命令

### 2. 代码后端 (MainWindow.xaml.cs)
- **初始化**：加载 commands.json，生成按钮
- **事件处理**：
  - 按钮点击：创建可收缩输出面板，串行执行请求
  - 添加按钮：验证输入，更新配置
  - 删除按钮：确认删除，更新配置
- **命令执行**：使用 Process 类异步执行 CMD，输出到对应面板
- **编码处理**：使用 OEM 编码页处理中文输出

### 3. 配置文件 (commands.json)
- **格式**：JSON 数组，每个对象包含 alias 和 steps 列表
- **示例**：
```json
[
  {
    "alias": "网络信息",
    "steps": [
      { "type": "command", "value": "ipconfig /all" }
    ]
  },
  {
    "alias": "示例多步骤请求",
    "steps": [
      { "type": "command", "value": "echo 开始" },
      { "type": "delay", "value": "1000" },
      { "type": "command", "value": "echo 结束" }
    ]
  }
]
```

## 数据模型

### RequestItem 类
```csharp
class RequestItem
{
    public string Alias { get; set; } = string.Empty;
    public List<StepItem> Steps { get; set; } = new();
}

class StepItem
{
    public string Type { get; set; } = string.Empty;  // "command" 或 "delay"
    public string Value { get; set; } = string.Empty; // 命令字符串 或 延时毫秒数
}
```

### 数据流
1. 应用启动 → 读取 commands.json → 反序列化为 List<RequestItem>
2. 生成按钮 → 绑定点击事件
3. 用户点击 → 串行执行步骤列表（命令或延时） → 显示输出（请求间串行执行）
4. 添加/删除 → 更新 List → 保存到 JSON

## 用户界面设计

### 布局说明
- **左侧面板**：
  - 常用命令标题
  - 可滚动按钮列表

- **右侧面板**：
  - 可滚动输出面板列表，每个请求一个可收缩 Expander
  - Expander 默认收缩，Header 显示请求状态（等待中/执行中/已完成）和别名；状态显示在别名前面，用emoji（⏳/🔄/✅）表示
  - Content 为只读 TextBox 显示输出
  - 清除输出按钮，位于右下角，用图标表示
  - 说明文字

### 交互设计
- **按钮**：显示别名，悬停显示完整命令，点击立即创建输出面板并开始执行
- **右键菜单**：删除按钮选项
- **输入验证**：添加按钮时检查必填字段
- **反馈**：MessageBox 显示提示和确认
- **输出面板**：每个请求独立可收缩，默认收缩，实时显示状态和输出

## 技术实现细节

### WPF 框架
- 使用 .NET 7.0 Windows 桌面 SDK
- XAML 定义 UI，C# 处理逻辑
- 异步编程：async/await 处理命令执行

### 命令执行机制
- **ProcessStartInfo** 配置：
  - FileName: "cmd.exe"
  - Arguments: "/c " + command
  - RedirectStandardOutput/Error: true
  - UseShellExecute: false
  - CreateNoWindow: true
- **编码处理**：GetOEMCP() 获取系统编码页
- **事件驱动**：OutputDataReceived 处理输出

### 文件操作
- **读取**：File.ReadAllTextAsync + JsonSerializer.Deserialize
- **写入**：JsonSerializer.Serialize + File.WriteAllTextAsync
- **路径**：AppContext.BaseDirectory + "commands.json"

### 错误处理
- try-catch 包围命令执行
- 异常信息显示在输出框
- 保存配置失败时弹出 MessageBox

## 部署指南

### 构建要求
- .NET 7.0 SDK 或更高版本
- Windows 操作系统

### 构建步骤
```powershell
dotnet build FastTools
```

### 运行步骤
```powershell
dotnet run --project FastTools
```

### 发布说明
- 项目会自动复制 commands.json 到输出目录
- 配置文件可手动编辑或通过应用界面修改
- 持久化修改需要从输出目录运行或确保文件可写

## 未来扩展

### 可能的功能增强
1. **命令分组**：按类别组织按钮
2. **历史记录**：保存执行历史
3. **快捷键**：为按钮添加键盘快捷键
4. **主题切换**：支持深色/浅色主题
5. **导出/导入**：配置文件导入导出
6. **多语言**：支持更多语言界面

### 技术改进
1. **MVVM 模式**：分离 UI 和逻辑
2. **依赖注入**：提高可测试性
3. **日志系统**：记录执行和错误
4. **单元测试**：添加自动化测试

### 兼容性
- 支持更多 .NET 版本
- 跨平台考虑（使用 .NET Core）

---

**更新日志**：
- 2025-12-28: 支持串行多任务，每个按钮可包含多个任务（命令或延时）；确保请求间串行执行；输出改为可收缩面板，默认收缩，显示执行状态；修复编译警告（null 引用检查）；移除即时运行自定义命令功能
- 2025-12-28: UI 修改 - 清除输出按钮改为图标（删除图标），放置在输出面板右下角
- 2025-12-28: 编码处理改进 - 命令输出从GBK编码转换为UTF-8再显示（通过设置CMD代码页为UTF-8）
- 2025-12-28: UI 优化 - 根据管理员权限动态显示或隐藏管理员权限说明

本文档基于当前代码实现编写，如有功能更新请及时同步。