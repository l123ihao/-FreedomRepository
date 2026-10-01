# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

> 本仓库已有详尽项目笔记 **AGENTS.md**(架构、关键约定与坑、里程碑状态),每次会话自动加载,细节以它为准。本文件只补充 AGENTS.md 未覆盖的操作性信息,不重复其内容。

## 技术栈速览

- .NET 10 / C# WPF(MVVM,CommunityToolkit.Mvvm 源生成器),解决方案为 XML 格式 `FormatConverter.slnx`
- `Directory.Build.props`:Nullable enable、ImplicitUsings enable、LangVersion latest
- Core 依赖:ImageSharp 3.x(+ Drawing 2.x)、DocumentFormat.OpenXml、QuestPDF、PdfPig、Markdig;App 依赖:CommunityToolkit.Mvvm
- FFmpeg 二进制不随仓库提交,`scripts\fetch-ffmpeg.ps1` 下载到 `src\FormatConverter.App\ffmpeg\`
- 测试:xUnit,位于 `src\Tests\FormatConverter.Core.Tests\`(目标框架 net10.0)

## 常用命令(PowerShell)

```powershell
# 国内网络先走华为云镜像 restore(直连 nuget.org 可能卡死)
dotnet restore FormatConverter.slnx -p:NuGetAudit=false --source https://repo.huaweicloud.com/repository/nuget/v3/index.json
dotnet build FormatConverter.slnx -c Release --no-restore

# 全量测试;ffmpeg 集成测试检测不到 ffmpeg 时自动跳过(不算失败)
dotnet test FormatConverter.slnx -c Release

# 单个测试类 / 单个测试方法(xUnit filter)
dotnet test src/Tests/FormatConverter.Core.Tests -c Release --filter "FullyQualifiedName~FormatRegistryTests"
dotnet test src/Tests/FormatConverter.Core.Tests -c Release --filter "FullyQualifiedName~FfmpegIntegrationTests.Mp4_To_Mkv_Copies_And_Keeps_Duration"

# 下载 ffmpeg(克隆后/跑集成测试前先执行;为 App 与测试输出目录各备副本)
powershell -ExecutionPolicy Bypass -File scripts\fetch-ffmpeg.ps1

# 打包发布包(publish\万能格式转换器-win-x64.zip)
powershell -ExecutionPolicy Bypass -File publish.ps1

# 开发运行 / 命令行静默转换(右键菜单调用;退出码 0/1)
dotnet run --project src/FormatConverter.App -c Release
FormatConverter.exe --convert mp3 "文件.mp4"   # 输出到源目录,重名自动加序号
```

CI:`.github/workflows/build.yml`(windows-latest → fetch-ffmpeg → restore → build → test)。

## 架构一句话(详见 AGENTS.md §2)

`FormatRegistry`(32 种格式 + 转换矩阵,唯一格式真相)→ `ConverterFactory.GetConverter(job)` → 各 `IConverter`(视频/音频走 `FfmpegConverterBase`,图片走 ImageSharp,文档走 OpenXml+QuestPDF+PdfPig+Markdig)→ `ConversionEngine.ConvertAllAsync`(smartParallelism:媒体串行、图片/文档并行)。App 侧 MVVM:`MainWindow` 导航壳 + `Views/`(Convert/Tools/Settings/History)+ `ViewModels/`,主题色全部 `DynamicResource`(`Themes/Light.xaml` + `Dark.xaml` 成对维护)。

## 高频改动检查单(完整踩坑清单见 AGENTS.md §4)

- **加新格式**要同步改多处:`FormatRegistry.AllFormats`+`Matrix`、`FfmpegArgsBuilder` 的 `AudioTargets/VideoTargets/GetMuxer`+编码分支、`ImageConverter` 的 `ImageTargets/GetEncoder`——只改注册表会导致运行时找不到转换器。
- **自定义 ffmpeg 调用必须显式加 `-f <muxer>`**:输出先写 `.~xxx.part`,ffmpeg 无法从 .part 推断格式(工具页 `VideoTools` 等 runner 调用处,曾踩坑)。
- **新增语义色**要同时加进 `Themes/Light.xaml` 和 `Dark.xaml`(key 一致)。
- **WPF+WinForms 共存**:App 已 `<Using Remove="System.Windows.Forms" />`,需要 WinForms 类型(如 NotifyService)时在文件内显式 using。
- **中文文本**:QuestPDF 渲染需注册微软雅黑(`PdfRenderer.RegisterFonts`);ImageSharp 文字水印用 `SystemFonts.CreateFont("Microsoft YaHei", ...)`。

## 测试注意事项

- 集成测试(`FfmpegIntegrationTests`)构造函数用 lavfi 自生成素材,`FfmpegLocator.IsAvailable` 为 false 时整个跳过。`FfmpegLocator` 查找 `AppContext.BaseDirectory\ffmpeg\`(测试运行时即测试输出目录),找不到时回退 PATH 上的 `ffmpeg`/`ffprobe`。
- 文档往返测试覆盖中文/GB18030 编码;改文档转换器时保持该覆盖。
- 改 Core 后 `dotnet build` 不编译 App 时注意:App 引用 Core,单测 Core 项目即可快速验证(`dotnet test src/Tests/FormatConverter.Core.Tests -c Release`)。
