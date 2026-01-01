# FastTools

一个简单的 C# WPF 桌面程序，将常用 CMD 命令转换为按钮，点击即可在应用内查看输出。

要求：已安装 .NET SDK（版本10+）以及在 Windows 上运行。

快速使用：

```powershell
dotnet build FastTools
dotnet run --project FastTools
```

编译Realse：
```
dotnet publish --configuration Release --self-contained true --runtime win-x64 -p:PublishSingleFile=true
```

Notes: The app reads and writes `config.json` (copied to output). To persist added/removed buttons, run from the published/output folder or ensure `config.json` is writable in the working directory. Each button represents a request with multiple steps (commands or delays).


说明：
- 打开后可点击左侧按钮运行常用命令，输出会显示在右侧输出窗口。
- 若命令需要管理员权限，请以管理员权限运行程序或在 CMD 中单独运行命令。

# 功能需求与架构设计
有任何功能更新，都应该更新到DESIGN.md文件