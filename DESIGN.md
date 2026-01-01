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
6. **ADB设备检测**：实时检测并显示已连接的adb设备信息
7. **ADB设备状态**：显示设备的rooted和remount状态
8. **ADB设备选择**：支持单选设备，用于执行特定设备的ADB命令
9. **ADB命令支持**：添加adb_command类型，支持{dev}占位符替换为选中设备ID
10. **智能按钮控制**：包含adb_command的按钮在无设备时自动禁用
11. **本地目录选择**：adb_command支持LocalDir属性，当为true时弹出文件夹选择对话框，将{local_dir}占位符替换为用户选择的目录
12. **工作目录配置**：adb_command支持WorkDir属性，可配置固定目录路径，将{work_dir}占位符替换为配置的目录

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
- **格式**：JSON 对象，包含 "work_dir" 和 "requests" 两个属性
  - work_dir: 全局工作目录路径（可选）
  - requests: 命令请求数组，每个对象包含 alias 和 steps 列表
- **示例**：
```json
{
  "work_dir": "D:\\downloads",
  "requests": [
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
    },
    {
      "alias": "pull to local dir",
      "steps": [
        { "type": "adb_command", "value": "adb -s {dev} pull /sdcard/Download/12 {local_dev}", "LocalDir": true }
      ]
    },
    {
      "alias": "pull to work dir",
      "steps": [
        { "type": "adb_command", "value": "adb -s {dev} pull /sdcard/Download/test.txt {work_dir}" }
      ]
    }
  ]
}
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
    public string Type { get; set; } = string.Empty;  // "command", "delay", 或 "adb_command"
    public string Value { get; set; } = string.Empty; // 命令字符串 或 延时毫秒数
    public bool? LocalDir { get; set; } = false;      // 是否需要选择本地目录（仅adb_command有效）
}
```

### MainWindow 类字段
```csharp
class MainWindow : Window
{
    private string _workDir = string.Empty;  // 全局工作目录路径
    private List<RequestItem> _requests = new();
    // ... 其他字段
}
```

### 数据流
1. 应用启动 → 读取 commands.json → 解析 JSON 对象获取 work_dir 和 requests
2. 生成按钮 → 绑定点击事件
3. 用户点击 → 串行执行步骤列表（命令或延时） → 显示输出（请求间串行执行）
4. 添加/删除 → 更新 List → 保存到 JSON（包含 work_dir 和 requests）

## 用户界面设计

### 布局说明
- **左侧面板**：
  - **上部区域（占1/3高度）**：已连接设备显示
    - 显示已连接设备标题
    - 可滚动设备列表，显示设备ID和连接状态
  - **下部区域（占2/3高度）**：常用命令按钮
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

### ADB设备检测实现
- **USB事件触发检测**：使用 WMI (Windows Management Instrumentation) 监听USB设备变化事件，当设备插入或拔出时触发ADB设备检测
  - 使用 `ManagementEventWatcher` 监听 `__InstanceCreationEvent` 和 `__InstanceDeletionEvent` 事件
  - 事件过滤条件：`TargetInstance ISA 'Win32_USBHub'`
  - 检测间隔：0.5秒
- **1秒延迟机制**：USB设备变化后，使用 `System.Threading.Timer` 延迟1秒再执行ADB设备检查，避免频繁检查
- **命令执行**：通过 `Process` 类执行 `adb devices` 命令
- **结果解析**：解析命令输出获取设备列表和连接状态
- **设备状态检查**：
  - 新设备加入时自动检查root和remount状态
  - 点击按钮执行命令前重新检查当前选中设备的状态
- **UI更新**：使用 `Dispatcher.Invoke` 确保线程安全地更新设备列表显示
- **设备显示**：无设备时显示"未检测到设备"提示，有设备时显示设备ID、连接状态和root/remount状态
- **依赖**：使用 `System.Management` NuGet包实现WMI功能

### 本地目录选择功能实现
- **功能触发**：当adb_command步骤的LocalDir属性为true且命令包含{local_dev}占位符时触发
- **对话框显示**：使用Windows Forms的FolderBrowserDialog在UI线程上显示文件夹选择对话框
  - 通过`Dispatcher.Invoke()`确保在UI线程上创建和显示对话框
  - 对话框为模态对话框，会阻塞当前线程直到用户完成选择或取消
- **占位符替换**：用户选择目录后，将{local_dev}占位符替换为选中的目录路径
- **取消处理**：用户取消选择时，跳过当前命令的执行，显示提示信息
- **依赖**：项目需要启用Windows Forms支持（<UseWindowsForms>true</UseWindowsForms>）
- **配置要求**：commands.json中使用"LocalDir"属性名（驼峰命名），与C#类属性名保持一致

### 工作目录配置功能实现
- **配置方式**：在 commands.json 的顶层添加 "work_dir" 属性，配置全局工作目录路径（可选）
- **功能触发**：当 adb_command 命令包含 {work_dir} 占位符时触发
- **占位符替换**：将 {work_dir} 占位符替换为全局 work_dir 配置的路径
- **使用场景**：适用于需要固定工作目录的场景，避免每次执行时重复选择目录
- **配置要求**：在 JSON 对象的顶层配置 "work_dir" 属性，所有命令共享同一个工作目录
- **优先级**：{work_dir} 替换在 {local_dev} 之前执行，两者可以同时使用
- **实现细节**：
  - MainWindow 类维护 _workDir 字段存储全局工作目录
  - LoadRequestsAsync 方法解析 JSON 对象的 work_dir 属性
  - SaveRequestsAsync 方法将 work_dir 序列化到 JSON 对象的顶层
  - ExecuteRequestAsync 方法使用全局 _workDir 替换命令中的 {work_dir} 占位符

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
- 2026-01-01: JSON结构重构 - 将WorkDir从步骤级配置改为全局配置；commands.json从数组结构改为对象结构，包含顶层"work_dir"属性和"requests"数组；所有命令共享同一个工作目录，简化配置管理
- 2026-01-01: 工作目录配置功能 - 为adb_command添加WorkDir属性支持，可配置固定目录路径，将{work_dir}占位符替换为配置的目录；适用于需要固定工作目录的场景，避免每次执行时重复选择目录
- 2025-12-31: 本地目录选择功能 - 为adb_command添加LocalDir属性支持，当值为true时弹出文件夹选择对话框，将{local_dev}占位符替换为用户选择的目录；修复对话框非阻塞问题，确保命令执行等待用户完成目录选择；在WPF项目中集成Windows Forms的FolderBrowserDialog控件
- 2025-12-29: 设备列表刷新机制优化 - 实现USB事件触发的ADB设备检测，设备变化后1秒自动检查；设备状态刷新规则改进，新设备加入或点击按钮时自动检查root/remount状态；移除定时检测机制，使用WMI监听USB事件提高效率
- 2025-12-28: UI 重构与新功能 - 左侧面板分为上下两部分，上部显示ADB设备信息，下部显示命令按钮；添加ADB设备实时检测功能，每5秒更新一次设备列表；按钮面板高度调整为主界面的三分之二
