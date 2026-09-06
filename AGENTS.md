# AGENTS.md — 万能格式转换器 (FormatConverter) 项目笔记

> 给 AI 助手/新贡献者的快速上下文。读完本文件即可高效继续开发。

## 1. 项目概况

Windows 桌面应用：视频/音频/文档/图片格式互转。**C# WPF (.NET 10)**，中文界面，MVVM（CommunityToolkit.Mvvm 源生成器）。FFmpeg 打包进应用，用户无需安装。

- GitHub：`https://github.com/l123ihao/-FreedomRepository`（分支 `main`）
- 许可证：自身代码 MIT；FFmpeg GPLv3（见 `THIRD-PARTY-LICENSES.txt`）
- 开发环境：Windows + .NET 10 SDK；本机 ffmpeg 在 `src/FormatConverter.App/bin/Release/net10.0-windows/ffmpeg/`（`scripts\fetch-ffmpeg.ps1` 下载）

## 2. 架构

```
src/
├── FormatConverter.Core/            # 无 UI 依赖的转换内核
│   ├── Formats/FormatRegistry.cs    # ★格式注册表:32 种格式 + 转换矩阵(唯一的格式真相)
│   ├── Converters/                  # 转换器(ConverterFactory 路由)
│   │   ├── FfmpegConverterBase.cs   # 视频/音频基类:probe→参数→运行→校验,硬件失败回退软件
│   │   ├── FfmpegVideoConverter / FfmpegAudioConverter
│   │   ├── ImageConverter.cs        # ImageSharp(png/jpg/webp/bmp/gif/ico/tiff)
│   │   └── DocumentConverter.cs     # docx/txt/md/pdf/pptx(OpenXml+QuestPDF+PdfPig+Markdig)
│   ├── Ffmpeg/                      # FfmpegArgsBuilder/Probe/Runner/ProgressParser/HardwareDetector
│   ├── Engine/ConversionEngine.cs   # 批量引擎:smartParallelism(媒体串行,图片/文档并行)
│   ├── Tools/                       # 工具页内核:FormatDetector/RenameService/ImageTools/PdfTools/VideoTools/ErrorClassifier/OutputValidator
│   ├── Documents/ Markdown/ Images/ Pdf/
│   └── Models/                      # ConversionJob/ConversionOptions/ConversionResult/FileCategory/ProgressInfo
├── FormatConverter.App/             # WPF 应用
│   ├── App.xaml(.cs)                # 主题字典 + 启动:命令行(--convert)分支/手动 new MainWindow
│   ├── MainWindow.xaml(.cs)         # 侧边导航壳 + 命令面板覆盖层 + TaskbarItemInfo + 快捷键
│   ├── Views/                       # ConvertPage(转换) / ToolsPage(工具) / SettingsPage(设置) / HistoryPage(占位)
│   ├── ViewModels/                  # MainViewModel(队列/导航/主题/右键菜单) + ToolsViewModel(13 个工具)
│   ├── Services/                    # ThemeService/SettingsService/MediaProbeService/ShellIntegration/CommandLineConverter/NotifyService/OutputPathHelper
│   └── Themes/Light.xaml + Dark.xaml  # 25 个语义色 brush(全部 DynamicResource 引用)
└── Tests/FormatConverter.Core.Tests/  # xUnit,92 项(含 ffmpeg 集成测试,检测不到 ffmpeg 自动跳过)
```

**数据流**：`FormatRegistry`（格式矩阵）→ `ConverterFactory.GetConverter(job)` 路由 → `ConversionEngine.ConvertAllAsync` 分批并行 → 各 `IConverter`。

## 3. 命令

```powershell
# 构建(国内网络走华为云镜像)
dotnet restore FormatConverter.slnx -p:NuGetAudit=false --source https://repo.huaweicloud.com/repository/nuget/v3/index.json
dotnet build FormatConverter.slnx -c Release

# 测试(92 项;本机有 ffmpeg 时集成测试真实执行)
dotnet test FormatConverter.slnx -c Release

# 打包(publish\万能格式转换器-win-x64.zip)
powershell -ExecutionPolicy Bypass -File publish.ps1

# 命令行转换(右键菜单调用)
FormatConverter.exe --convert mp3 "文件.mp4"   # 输出到源目录,重名自动加序号,退出码 0/1
```

## 4. 关键约定与坑

1. **加新格式**：改 `FormatRegistry.AllFormats` + `Matrix`（视频/音频目标同时加 `FfmpegArgsBuilder` 的 `AudioTargets/VideoTargets/GetMuxer` 和编码分支；图片加 `ImageConverter` 的 `ImageTargets/GetEncoder`）。
2. **`.part` 临时文件坑**：`FfmpegRunner` 输出先写 `.~xxx.part`，ffmpeg **无法从 .part 推断格式**，所有自定义 runner 调用必须显式加 `-f <muxer>`（见 `VideoTools`，曾踩坑）。
3. **WPF + WinForms 共存**：`UseWindowsForms=true`（NotifyIcon 用）会注入 `System.Windows.Forms` 全局 using，与 WPF 的 `DragEventArgs/UserControl` 二义。已用 `<Using Remove="System.Windows.Forms" />` 移除；`NotifyService.cs` 内部显式 `using System.Windows.Forms;`。
4. **主题**：颜色一律 `DynamicResource` 引用（App.xaml 样式 + 各页面）；新增语义色要同时加进 `Themes/Light.xaml` 和 `Dark.xaml`（key 一致）。
5. **命名遮蔽**：VM 属性名与类型名同词时会遮蔽类型（曾踩 `WatermarkPosition`、`ResizeOptions`），必要时用完全限定名。
6. **硬件加速**：`HardwareDetector.PreferredEncoder` 进程内缓存；`FfmpegConverterBase` 硬件失败自动回退软件重试一次；webm 不走硬件分支。
7. **并行策略**：`ConversionEngine(smartParallelism: true)` 按 `job.Category` 分组——图片/文档并行、视频/音频串行。MainViewModel 已启用。
8. **中文**：QuestPDF 渲染需注册微软雅黑（`PdfRenderer.RegisterFonts`）；ImageSharp 文字水印用 `SystemFonts.CreateFont("Microsoft YaHei", ...)`。

## 5. 已实现功能（M1–M5 全部完成）

- **转换**：32 种格式矩阵互转；拖拽到格式磁贴；批量队列（虚拟化 ListView）；进度/速度/取消；行内媒体信息（时长/分辨率/编码/码率）。
- **UI**：深色/浅色/跟随系统主题；侧边导航四页；快捷键（Ctrl+O / Ctrl+Shift+O / Del / Ctrl+K 命令面板 / Ctrl+1..4）；任务栏进度；完成托盘通知。
- **工具页（13 个）**：媒体信息、格式检测（魔数）、图片压缩/缩放/裁剪/水印、批量重命名、PDF 合并/拆分、视频剪辑/抽帧/缩略图、视频转 GIF、音频增强。
- **系统集成**：右键菜单（HKCU 免管理员，设置页可卸载）；`--convert` 命令行静默转换。
- **质量/性能**：图片/文档并行；NVENC/QSV/AMF 硬件加速 + 回退；输出非空校验；错误分类（输入损坏/磁盘满/占用/编码不支持）；覆盖冲突预检。

## 6. 已知限制与 TODO

- `heic` 图片未支持（需 libheif，ImageSharp 无内置）——列为后续候选。
- `docx→pdf` 纯 .NET 渲染保真度有限；可加 LibreOffice 检测（装了就 `soffice`，否则回退现有渲染）。
- HistoryPage 还是占位（方案 M3 的 SQLite 历史未做——当前里程碑已完成，历史页按需补）。
- 旧版 `.ppt`（二进制）不支持。
- 大视频转 GIF 体积大（默认限宽 480px、12fps）。

## 7. 里程碑状态（docs/upgrade-plan.md）

| 里程碑 | 内容 | 状态 |
|---|---|---|
| M1 | 主题/导航/虚拟化/快捷键/媒体信息 | ✅ 完成 |
| M2 | 格式检测/重命名/图片工具/媒体信息面板 | ✅ 完成 |
| M3 | PDF 合并拆分/视频剪辑抽帧/GIF 参数/音频增强 | ✅ 完成 |
| M4 | 右键菜单/命令行/任务栏进度/完成通知 | ✅ 完成 |
| M5 | 并行/硬件加速/格式扩展/错误分类/输出校验/冲突预检 | ✅ 完成 |

测试基线：**92/92 全绿**（`dotnet test FormatConverter.slnx -c Release`）。
