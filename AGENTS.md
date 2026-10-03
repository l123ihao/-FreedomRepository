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
│   ├── Formats/FormatRegistry.cs    # ★格式注册表:35 种格式 + 转换矩阵(唯一的格式真相)
│   ├── Converters/                  # 转换器(ConverterFactory 路由)
│   │   ├── FfmpegConverterBase.cs   # 视频/音频基类:probe→参数→运行→校验,硬件失败回退软件
│   │   ├── FfmpegVideoConverter / FfmpegAudioConverter
│   │   ├── ImageConverter.cs        # ImageSharp(png/jpg/webp/bmp/gif/ico/tiff)
│   │   └── DocumentConverter.cs     # docx/txt/md/pdf/pptx(OpenXml+QuestPDF+PdfPig+Markdig)
│   ├── Ffmpeg/                      # FfmpegArgsBuilder/Probe/Runner/ProgressParser/HardwareDetector
│   ├── Engine/ConversionEngine.cs   # 批量引擎:smartParallelism(媒体串行,图片/文档并行)+ 转换后动作
│   ├── Presets/                     # ★预设系统:BuiltInPresets(内置 5 个)/PresetMerger(合并排序)/PresetValidator
│   ├── Tools/                       # 工具页内核 + OutputTemplateEngine(文件名模板)/OutputPathResolver(输出路径统一入口)
│   ├── Documents/ Markdown/ Images/ Pdf/
│   └── Models/                      # ConversionJob/ConversionOptions(+CustomFfmpegArgs)/ConversionResult/ConversionPreset/ConversionOptionsOverride/AppSettingsData/PostConversionAction
```
├── FormatConverter.App/             # WPF 应用
│   ├── App.xaml(.cs)                # 主题字典 + 启动:命令行(--convert)分支/手动 new MainWindow
│   ├── MainWindow.xaml(.cs)         # 侧边导航壳 + 命令面板覆盖层 + TaskbarItemInfo + 快捷键
│   ├── Views/                       # ConvertPage(转换) / ToolsPage(工具) / SettingsPage(设置) / HistoryPage(占位)
│   ├── ViewModels/                  # MainViewModel(队列/导航/主题/右键菜单) + ToolsViewModel(13 个工具)
│   ├── Services/                    # ThemeService/SettingsService/MediaProbeService/ShellIntegration/CommandLineConverter/NotifyService/OutputPathHelper
│   └── Themes/Light.xaml + Dark.xaml  # 25 个语义色 brush(全部 DynamicResource 引用)
└── Tests/FormatConverter.Core.Tests/  # xUnit,184 项(含 ffmpeg 集成测试,检测不到 ffmpeg 自动跳过)
```

**数据流**：`FormatRegistry`（格式矩阵）→ `ConverterFactory.GetConverter(job)` 路由 → `ConversionEngine.ConvertAllAsync` 分批并行 → 各 `IConverter`。

**预设叠加层**：预设 = 目标格式 + 参数覆盖 + 文件名模板 + 转换后动作。选中预设 → 联动磁贴 + 可映射参数写入全局高级设置;入队时按条目快照模板/动作/参数覆盖(无全局 UI 的 CRF/GIF/自定义参数经 `OptionsOverride.ApplyTo` 生效)。设置持久化:`%APPDATA%\FormatConverter\settings.json`(AppSettingsData DTO 在 Core,App 的 SettingsService 只做 IO;内置预设 Id 固定 `builtin.*` 不进 JSON)。

## 3. 命令

```powershell
# 构建(国内网络走华为云镜像)
dotnet restore FormatConverter.slnx -p:NuGetAudit=false --source https://repo.huaweicloud.com/repository/nuget/v3/index.json
dotnet build FormatConverter.slnx -c Release

# 测试(92 项;本机有 ffmpeg 时集成测试真实执行)
dotnet test FormatConverter.slnx -c Release

# 打包(publish\万能格式转换器-win-x64.zip)
powershell -ExecutionPolicy Bypass -File publish.ps1

# 命令行转换(右键菜单调用同一入口,退出码 0/1)
FormatConverter.exe --convert mp3 "文件.mp4"            # 输出目录/重名策略/参数跟随持久化设置
FormatConverter.exe --preset "高清 MP4(视频)" "文件.mkv"  # 按预设:模板命名+参数覆盖+转换后动作
```

## 4. 关键约定与坑

1. **加新格式**：改 `FormatRegistry.AllFormats` + `Matrix`（视频/音频目标同时加 `FfmpegArgsBuilder` 的 `AudioTargets/VideoTargets/GetMuxer` 和编码分支；图片加 `ImageConverter` 的 `ImageTargets/GetEncoder`）。老版二进制 Office（doc/ppt 类）走 `LibreOfficeConverter`（soffice --convert-to，进程内串行防配置文件争用；定位器 `LibreOfficeLocator` 缓存）。
2. **加内置预设**：只改 Core 的 `Presets/BuiltInPresets.cs` 常量表（Id 固定 `builtin.*`）；用户数据不动。预设名禁止与格式扩展名同名（`PresetValidator` 拦截，防 CLI 歧义）。
3. **文件名模板**：占位符语法与完整规则在 `Tools/OutputTemplateEngine.cs` 头注释 + README；`(p)` 系令牌展开带尾部分隔符（File Converter 同款）；冲突从 ` (2)` 起递增（legacy 从 ` (1)` 起）。模板引擎/预设合并等全部可测代码必须在 Core（测试项目不引用 App）。
4. **`.part` 临时文件坑**：`FfmpegRunner` 输出先写 `.~xxx.part`，ffmpeg **无法从 .part 推断格式**，所有自定义 runner 调用必须显式加 `-f <muxer>`（见 `VideoTools`，曾踩坑）。自定义 FFmpeg 参数（预设逃生舱）插在 `-f` 之前，`{output}` 替换为 .part 路径。
5. **WPF + WinForms 共存**：`UseWindowsForms=true`（NotifyIcon 用）会注入 `System.Windows.Forms` 全局 using，与 WPF 的 `DragEventArgs/UserControl` 二义。已用 `<Using Remove="System.Windows.Forms" />` 移除；`NotifyService.cs` 内部显式 `using System.Windows.Forms;`。
6. **主题**：颜色一律 `DynamicResource` 引用（App.xaml 样式 + 各页面）；新增语义色要同时加进 `Themes/Light.xaml` 和 `Dark.xaml`（key 一致）。
7. **命名遮蔽**：VM 属性名与类型名同词时会遮蔽类型（曾踩 `WatermarkPosition`、`ResizeOptions`、`VideoMode`），必要时用完全限定名。
8. **硬件加速**：`HardwareDetector.PreferredEncoder` 进程内缓存；`FfmpegConverterBase` 硬件失败自动回退软件重试一次；webm 不走硬件分支。
9. **并行策略**：`ConversionEngine(smartParallelism: true)` 按 `job.Category` 分组——图片/文档并行、视频/音频串行。MainViewModel 已启用。
10. **中文**：QuestPDF 渲染需注册微软雅黑（`PdfRenderer.RegisterFonts`）；ImageSharp 文字水印用 `SystemFonts.CreateFont("Microsoft YaHei", ...)`。
11. **设置持久化**：`MainViewModel` 设置类属性初值从 `_settings` 用**字段直赋**载入（不触发 OnXxxChanged → 不回写);运行时变更经 `OnXxxChanged → PersistSettings()` 落盘。构造期赋属性会立即回写默认值覆盖用户数据——曾踩。

## 5. 已实现功能（M1–M6 全部完成）

- **转换**：35 种格式矩阵互转；拖拽到格式磁贴；批量队列（虚拟化 ListView）；进度/速度/取消；行内媒体信息（时长/分辨率/编码/码率）。
- **UI**：深色/浅色/跟随系统主题；侧边导航四页（148px 窄栏）；转换页双栏布局（左文件队列+拖放空态/右预设+32 磁贴紧凑网格 4 列+输出设置，1200×720 一屏见全）；4 类别色点（视频蓝/音频紫/文档橙/图片绿，主题双写）；队列「不兼容」红标；快捷键（Ctrl+O / Ctrl+Shift+O / Del / Ctrl+K 命令面板 / Ctrl+1..4）；任务栏进度；完成托盘通知。
- **预设系统（M6）**：预设 = 目标格式 + 参数覆盖 + 文件名模板 + 转换后动作;内置 5 个 + 用户可增删改排序（设置页「预设」卡片 + 转换页预设条 + 编辑对话框);右键菜单列出预设子命令。
- **设置与后置动作（M6）**：设置全量持久化（输出目录/码率/硬件加速/自动退出等,旧 2 字段 JSON 无损迁移);文件名模板引擎（File Converter 同款语法）;转换后动作（删除原文件/移入归档,Core 引擎层执行,GUI/CLI 共用）;完成后自动退出倒计时。
- **工具页（13 个）**：媒体信息、格式检测（魔数）、图片压缩/缩放/裁剪/水印、批量重命名、PDF 合并/拆分、视频剪辑/抽帧/缩略图、视频转 GIF、音频增强。
- **系统集成**：右键菜单（HKCU 免管理员，设置页可卸载，预设子命令增量同步）；`--convert`/`--preset` 命令行静默转换（跟随持久化设置）。
- **质量/性能**：图片/文档并行；NVENC/QSV/AMF 硬件加速 + 回退；输出非空校验；错误分类（输入损坏/磁盘满/占用/编码不支持）；覆盖冲突预检 + 批次内同名互撞防护（blacklist）。

## 6. 已知限制与 TODO

- `heic` 图片未支持（需 libheif，ImageSharp 无内置）——列为后续候选。
- `docx→pdf` 纯 .NET 渲染保真度有限；可加 LibreOffice 检测（装了就 `soffice` 提升保真度，否则回退现有渲染）。
- HistoryPage 还是占位（方案 M3 的 SQLite 历史未做——当前里程碑已完成，历史页按需补）。
- 旧版二进制 `.doc`/`.ppt` 已支持：转 TXT 走内置提取器（`Documents/Ole2Reader` + `DocTextExtractor`/`PptTextExtractor`，解析思路对照 NPOI HWPF/HSLF，含 Mac 版 Word 的 +512 数据偏移与目录 size 偏小两个兼容坑）；转 docx/pdf/html 无 LibreOffice 时走文本模式回退（结果带 Note 提示）、装了自动高保真；仅 ppt→pptx 强制要求 LibreOffice。
- pdf→docx/html 为文本模式（`PdfToTxtConverter.ExtractText` → TextToModel → DocxWriter/ModelToHtml，不还原排版）。
- 大视频转 GIF 体积大（默认限宽 480px、12fps）。

## 7. 里程碑状态（docs/upgrade-plan.md）

| 里程碑 | 内容 | 状态 |
|---|---|---|
| M1 | 主题/导航/虚拟化/快捷键/媒体信息 | ✅ 完成 |
| M2 | 格式检测/重命名/图片工具/媒体信息面板 | ✅ 完成 |
| M3 | PDF 合并拆分/视频剪辑抽帧/GIF 参数/音频增强 | ✅ 完成 |
| M4 | 右键菜单/命令行/任务栏进度/完成通知 | ✅ 完成 |
| M5 | 并行/硬件加速/格式扩展/错误分类/输出校验/冲突预检 | ✅ 完成 |
| M6 | 预设系统/设置全量持久化/文件名模板/转换后动作/CLI 预设(对标 File Converter) | ✅ 完成 |
| M7 | 界面双栏化:左文件右格式/紧凑磁贴网格/类别色点/不兼容标记/侧栏收窄/一屏见全 | ✅ 完成 |

测试基线：**184/184 全绿**（`dotnet test FormatConverter.slnx -c Release`）。
