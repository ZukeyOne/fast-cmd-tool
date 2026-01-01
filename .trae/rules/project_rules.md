# FastTools 项目规则

## 构建命令
- 构建项目：`dotnet build FastTools`
- 运行项目：`dotnet run --project FastTools`
- 发布 Release 版本：`dotnet publish --configuration Release --self-contained true --runtime win-x64 -p:PublishSingleFile=true`

## 项目结构
- 主项目：`FastTools/FastTools.csproj`
- 目标框架：.NET 10.0 Windows
- UI 框架：WPF 支持 WindowsForms
- 配置文件：`commands.json`（复制到输出目录）

## 代码风格
- 语言：C#
- 可空引用类型：已启用
- 遵循 C# 编码规范
- 使用有意义的变量和方法名称

## 注意事项
- 应用程序读写 `commands.json` 以持久化按钮配置
- 每次修改功能，应该更新到DESIGN.md文件
