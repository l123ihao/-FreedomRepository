# 万能格式转换器 · 升级改造方案

> 对标项目：FileConverter（Tichau，12.2K★）、FormatMaster-EN（Gu-0312）、Morphix（moner-dev）、flyingmouse-format（3.7K★）
> 本方案覆盖四个方向：功能增强、UI/UX 现代化、系统集成、转换质量/性能。

## 实施状态（2026-09 更新）

**M1–M5 已全部完成**，测试 92/92 全绿。项目笔记见根目录 `AGENTS.md`。

| 里程碑 | 状态 | 说明 |
|---|---|---|
| M1 UI/UX 现代化 | ✅ 完成 | 主题系统、侧边导航、虚拟化、快捷键、命令面板、行内媒体信息 |
| M2 工具第一批 | ✅ 完成 | 媒体信息、格式检测、图片压缩/缩放/水印、批量重命名 |
| M3 工具第二批 | ✅ 完成 | PDF 合并/拆分、视频剪辑/抽帧/缩略图、转 GIF 参数、音频增强 |
| M4 系统集成 | ✅ 完成 | 右键菜单、`--convert` 命令行、任务栏进度、完成通知 |
| M5 质量/性能 | ✅ 完成 | 并行策略、硬件加速、格式扩展（22→32 种）、错误分类、输出校验、冲突预检 |

遗留 TODO：heic 支持、LibreOffice 可选依赖、SQLite 历史页（HistoryPage 仍为占位）。

---

## 1. 现状与对标结论

### 1.1 已有优势（保留不动）
- 转换内核分层清晰：`ConverterFactory` 路由 + `IConverter` 抽象，新增转换器成本低
- FFmpeg 参数以列表传递（中文路径/空格安全），有进度解析、取消无残留、集成测试
- 文档转换纯 .NET 自研（docx/txt/md/pptx 读写），无外部依赖，已覆盖中文/GB18030
- 拖拽到磁贴、红闪拒绝、角标计数等交互细节完整

### 1.2 短板（对照标杆）
| 维度 | 现状 | 标杆做法 | 差距 |
|---|---|---|---|
| 功能 | 只有"互转" | FormatMaster 有 PDF 合并/拆分、图片压缩/水印、视频剪辑、批量重命名、格式检测 | 缺编辑类工具 |
| UI | 单浅色主题，颜色 `StaticResource` 硬编码 | Morphix 深/浅主题、命令面板、快捷键、虚拟化列表 | 无换肤、无快捷键、大队列卡顿 |
| 系统集成 | 无 | FileConverter 右键菜单、任务栏进度 | 无右键、无任务栏进度 |
| 性能 | 引擎默认串行、无硬件加速 | FileConverter 多线程 + NVENC | 媒体串行可保留，图片/文档应并行 |
| 格式 | 22 种格式 | Morphix 200+ 路线、FileConverter 25+ 图片格式 | 缺 HEIC/TIFF/SVG/FLV/TS 等 |

---

## 2. 总体架构调整

### 2.1 UI 容器重构：单窗口 → 侧边导航 + 页面
```
MainWindow
├── 左侧导航栏（转换 / 工具 / 历史 / 设置）
├── 转换页（现有磁贴 + 队列 + 转换栏，迁移自当前 MainWindow）
├── 工具页（新：视频/音频/图片/PDF 工具卡片）
├── 历史页（新：SQLite 记录，借鉴 Morphix）
└── 设置页（新：主题、右键菜单开关、并行度、默认参数）
```
- 用 `ContentControl` + `DataTemplate` 切页，不引入第三方框架，保持 MVVM 纯净
- 现有 `MainWindow.xaml` 内容整体搬进 `Views/ConvertPage.xaml`，窗口代码拆到对应页面

### 2.2 主题系统重构（所有 UI 改动的前置）
- `App.xaml` 中所有颜色 `StaticResource` → `DynamicResource`
- 拆分 `Themes/Light.xaml`、`Themes/Dark.xaml` 两套 `ResourceDictionary`
- `SettingsService` 记录主题选择（跟随系统 / 浅色 / 深色），启动时加载，运行时可切换

### 2.3 Core 扩展点
- 新增 `FormatConverter.Core.Tools` 命名空间：`PdfTools`、`ImageTools`、`VideoTools`、`RenameService`、`FormatDetector`
- `FormatRegistry` 增加格式（见 §5），转换矩阵随格式扩展

---

## 3. 分阶段计划

### M1 — UI/UX 现代化（前 1/3）
**借鉴：Morphix**

| 项 | 说明 | 改动文件 |
|---|---|---|
| 主题系统 | 颜色改 `DynamicResource`，新增深色主题，可跟随系统 | `App.xaml`、新增 `Themes/Dark.xaml`、`Services/ThemeService.cs` |
| 导航重构 | 侧边导航 + 四页骨架 | `MainWindow.xaml`、新增 `Views/*Page.xaml` |
| 队列虚拟化 | `ItemsControl` → `ListView` + `VirtualizingStackPanel` | `ConvertPage.xaml` |
| 快捷键 | `Ctrl+O` 选文件、`Ctrl+Shift+O` 加文件夹、`Del` 移除选中、`Ctrl+K` 命令面板、`Ctrl+1..4` 切页 | `MainWindow.xaml.cs` + `InputBindings` |
| 文件行增强 | 行内媒体信息（时长/分辨率/编码，异步探测）、缩略图 | `FileItemViewModel.cs`、`Services/MediaProbeService.cs` |

**验收**：深色主题切换即时生效；1000 文件队列滚动不卡；快捷键全部可用。

### M2 — 工具页第一批（中 1/3）
**借鉴：FormatMaster-EN**

| 项 | 说明 | 改动文件 |
|---|---|---|
| 媒体信息 | 选中文件显示时长、分辨率、编码、码率、大小（复用 `FfmpegProbe`） | `Tools/MediaInfoTool.cs` |
| 格式检测 | 魔数识别真实格式（不受扩展名误导），可疑文件给出提示 | `Core/Tools/FormatDetector.cs` |
| 图片压缩 | 质量/目标尺寸/分辨率上限三模式 | `Core/Tools/ImageTools.cs` + `ImageConverter` 扩展 |
| 图片缩放/裁剪 | 预设（社交媒体尺寸）、百分比、自定义 | 同上 |
| 批量重命名 | `{n}` 序号、`{name}`、`{date}` 占位符模板 | `Core/Tools/RenameService.cs` + `Views/Tools/RenameTool.xaml` |
| 图片水印 | 文字/图片水印，5 位置 | `Core/Tools/ImageTools.cs` |

**验收**：每个工具可独立跑通并有单元测试；工具页不破坏转换页现有功能。

### M3 — 工具页第二批（后 1/3）
**借鉴：FormatMaster-EN / Morphix**

| 项 | 说明 | 改动文件 |
|---|---|---|
| PDF 合并 | 多 PDF 按序合并（纯 .NET，复用现有 PDF 基础） | `Core/Tools/PdfTools.cs` |
| PDF 拆分 | 按页数 / 按页码范围 / 提取单页 | 同上 |
| 视频剪辑 | 起止时间点，ffmpeg `-ss/-to` 秒切 | `Core/Tools/VideoTools.cs` |
| 视频抽帧 | 单帧序列 / N×M 缩略图拼图 | 同上 |
| 视频转 GIF 参数面板 | 宽度、fps、起止时间（现有只支持默认参数） | `FfmpegArgsBuilder` + 工具面板 |
| 音频增强 | 音量调节、淡入淡出 | 同上 |

**验收**：PDF 合并/拆分有单元测试；视频工具集成测试覆盖（依赖 ffmpeg 时运行）。

### M4 — 系统集成
**借鉴：FileConverter**

| 项 | 说明 | 改动文件 |
|---|---|---|
| 右键菜单集成 | `HKCU\Software\Classes\*\shell\FormatConverter` 注册表项 + 级联菜单（转 MP4/MP3/PDF/PNG…），`--convert <target> <files>` 命令行入口 | 新增 `App/ShellIntegration.cs`、`App.xaml.cs` 启动参数解析 |
| 任务栏进度 | `TaskbarItemInfo.ProgressValue/ProgressState` 绑定整体进度 | `MainWindow` |
| 完成通知 | 转换结束托盘通知（不打扰式） | `Services/NotifyService.cs` |
| 命令行静默转换 | 右键/命令行直接转换不弹主窗口（可加进度小窗） | `App.xaml.cs` |

**验收**：右键任意支持文件 → 选格式 → 原地输出；任务栏进度与整体进度一致；设置页可卸载右键菜单。

### M5 — 转换质量/性能
**借鉴：FileConverter / Morphix**

| 项 | 说明 | 改动文件 |
|---|---|---|
| 并行策略 | 媒体保持串行；图片/文档按 CPU 核数并行（引擎按类别分组） | `ConversionEngine.cs` |
| 硬件加速 | 检测 NVENC/QSV/AMF 可用性，视频转码优先硬件编码，失败回退 libx264 | `FfmpegArgsBuilder.cs`、新增 `Ffmpeg/HardwareDetector.cs` |
| 格式扩展 | 见 §5 | `FormatRegistry.cs` + 各转换器 |
| 错误分类 | 失败原因归类：输入损坏/编码不支持/磁盘空间不足/文件被占用，配修复建议 | `ConversionResult` + UI 展示 |
| 输出校验 | 转换后检查输出存在且非 0 字节，异常时标记失败 | `FfmpegRunner.cs`、`ImageConverter.cs` |
| 输出冲突预检 | 入队时检测同名目标（含重名策略）提前提示 | `OutputPathHelper.cs` |

**验收**：100 张图片批量转换耗时显著下降；NVENC 机器上转码速度提升；失败文件均带可读原因。

---

## 4. 里程碑验收标准汇总

| 里程碑 | 核心验收 |
|---|---|
| M1 | 深色主题 + 导航 + 虚拟化 + 快捷键，千文件不卡 |
| M2 | 媒体信息、格式检测、图片压缩/缩放/水印、批量重命名，均有测试 |
| M3 | PDF 合并/拆分、视频剪辑/抽帧/GIF 参数、音频增强，均有测试 |
| M4 | 右键菜单 + 命令行 + 任务栏进度 + 完成通知 |
| M5 | 并行、硬件加速、格式扩展、错误分类、输出校验 |

每个里程碑结束时：`dotnet build` 通过 + `dotnet test` 全绿 + 手动冒烟（拖拽/右键/工具各一次）。

---

## 5. 格式扩展清单

### 5.1 立即加入（ffmpeg 原生支持，成本低）
- 视频：`flv` `ts` `m4v` `3gp` `wmv`（解码支持）
- 音频：`opus` `m4b` `wma`（解码）`aiff`
- 图片：`tiff` `heic`（ffmpeg 解码，转为 png/jpg/webp）

### 5.2 条件支持（检测到外部引擎才启用，借鉴 Morphix "when installed" 模式）
- 文档 `odt`/`rtf`/`epub`：检测到 LibreOffice（`soffice`）启用高质量转换；未安装时目标置灰并提示
- `docx→pdf` 保真度：检测到 LibreOffice 时走 `soffice --convert-to pdf`；否则回退现有纯 .NET 渲染
- `svg→png`：检测到 ImageMagick 时启用

### 5.3 暂不做
- Office 家族（xlsx/ods/csv）：需 Excel/复杂库，偏离当前定位
- RAR 归档：版权/依赖问题，不引入

---

## 6. 关键决策与风险

| # | 决策点 | 建议 | 风险 |
|---|---|---|---|
| 1 | 导航重构是否引入第三方 UI 框架 | 不引入，手写轻量导航 | 样式工作量可控 |
| 2 | docx→pdf 保真度 | LibreOffice 可选增强 + 现有纯 .NET 兜底 | 用户机器可能无 LibreOffice |
| 3 | 右键菜单注册位置 | HKCU（免管理员），设置页可卸载 | 注册表残留需清理逻辑 |
| 4 | 历史记录存储 | SQLite（`Microsoft.Data.Sqlite`，纯托管） | 增加一个 NuGet 依赖 |
| 5 | 并行媒体转码 | 媒体保持串行（CPU 饱和） | 用户可能期待并行，设置页提供开关 |
| 6 | 方案落盘 | 本文件随仓库维护，实施时更新完成状态 | 需保持同步 |

---

## 7. 实施顺序建议（滚动交付）
```
M1（UI 底座）→ M2（工具第一批）→ M3（工具第二批）→ M4（系统集成）→ M5（质量/性能）
```
每个 M 独立可发布；如中途想调整优先级，直接改本文档并继续。
