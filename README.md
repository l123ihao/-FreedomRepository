# 万能格式转换器 (FormatConverter)

[![build](https://github.com/l123ihao/-FreedomRepository/actions/workflows/build.yml/badge.svg)](https://github.com/l123ihao/-FreedomRepository/actions)

Windows 桌面应用:视频 / 音频 / 文档 / 图片格式互转。C# WPF(.NET 10),中文界面,支持拖拽、批量转换与进度显示。FFmpeg 已打包进应用,用户无需自行安装。

## 功能

| 类别 | 源格式 | 可转换目标 |
|---|---|---|
| 视频 | mp4 / mkv / avi / mov / webm / gif | 视频互转(兼容时无损秒转)、提取/转码音频、转 GIF |
| 音频 | mp3 / wav / flac / m4a / aac / ogg | 音频互转(码率 128/192/320 可选,wav/flac 无损) |
| 文档 | docx / txt / md / pdf / pptx / doc / ppt | docx→pdf/txt/md/html;txt→docx/pdf;md→docx/html/pdf;pdf→txt/docx/html(纯文字提取);pptx→docx/txt/pdf;doc→docx/pdf/txt/html;ppt→pptx/pdf/txt(转 TXT 为内置提取、零依赖;doc/ppt 转 PDF/DOCX/HTML 无 LibreOffice 时走文本模式回退) |
| 图片 | png / jpg / webp / bmp / gif / ico | 全互转(gif 动画→图片取首帧,gif→mp4 保留动画) |

## 使用

1. 解压发布包,运行 `FormatConverter.exe`(无需安装 .NET 或 FFmpeg)
2. **双栏界面:左侧文件队列、右侧格式选择**。把文件拖到左侧虚线区(转为右侧选中的格式)或直接拖到右侧某个格式磁贴/预设上——例:把 PPT 拖到「DOCX」即转成 Word,把视频拖到「MP3」即提取音频。首次拖入会弹确认窗,可勾选「不再提醒」以后拖入即转(保存在 %APPDATA%\FormatConverter\settings.json)
3. **预设**(右侧上方):一键切换「目标格式 + 参数 + 输出文件名模板 + 转换后动作」组合,内置 5 个(高清 MP4/高品质 MP3/WebP 压缩/转 PDF 归档/无损 MKV),设置页可增删改排序;右键菜单也会列出你的预设
4. 不能转成该格式的文件不会被加入,对应磁贴红闪 0.5 秒并提示原因(如旧版 .ppt 不支持);队列中不能转当前选中格式的文件会标红「不兼容」;磁贴右上角红色数字 = 该格式下待转/转换中的文件数
5. 也可先用「选择文件」「添加文件夹」加入队列(转换队列始终可见),再点右侧格式;「开始转换」在左侧队列底部,失败项可「重试失败项」
6. 右侧底部可改输出目录、展开「高级设置」调整选项(与设置页「转换默认值」实时同步、重启保留);设置页可开启「全部成功后自动退出程序」;转换可随时「取消」(会终止 ffmpeg 进程,不残留半成品)

## 构建

前置: .NET 10 SDK

```powershell
# 国内网络建议走华为云镜像并关闭 NuGetAudit(直连 nuget.org 可能卡死)
dotnet restore FormatConverter.slnx -p:NuGetAudit=false --source https://repo.huaweicloud.com/repository/nuget/v3/index.json
dotnet build FormatConverter.slnx -c Release --no-restore
```

FFmpeg(ffmpeg.exe + ffprobe.exe)不随仓库提交,克隆后先运行 `powershell -ExecutionPolicy Bypass -File scripts\fetch-ffmpeg.ps1` 自动下载到 `src\FormatConverter.App\ffmpeg\`(同时为集成测试备好副本);来源与许可见 `THIRD-PARTY-LICENSES.txt` 第 8 条。

## 测试

```powershell
dotnet test FormatConverter.slnx -c Release
```

- 单元测试:格式矩阵、ffmpeg 参数、进度解析、文档往返(含中文/GB18030)、图片转换
- 集成测试:检测到 ffmpeg 时自动用 lavfi 生成素材,验证 mp4→mkv/webm/gif/mp3、mp3→wav→flac、中文路径、取消无残留

## 打包

```powershell
powershell -ExecutionPolicy Bypass -File publish.ps1
```

产物在 `publish\`:`万能格式转换器-win-x64.zip`(自包含,约 150MB)。附带 THIRD-PARTY-LICENSES.txt 与 ffmpeg-LICENSE.txt(GPLv3 全文)。

## 命令行转换

右键菜单调用同一入口;退出码 0/1:

```powershell
FormatConverter.exe --convert mp3 "文件.mp4"              # 输出目录/重名策略/参数跟随设置
FormatConverter.exe --preset "高清 MP4(视频)" "文件.mkv"   # 按预设:模板命名+参数覆盖+转换后动作
```

## 输出文件名模板(预设可配)

留空 = 跟随全局输出设置;语法对标 File Converter:

| 占位符 | 含义 |
|---|---|
| `(p)` | 源文件所在目录(带尾部分隔符) |
| `(f)` / `(F)` | 源文件名(不含扩展名),小写/大写 |
| `(i)` / `(I)` | 输入扩展名;`(o)` / `(O)` 输出扩展名 |
| `(p:documents)` `(p:music)` `(p:videos)` `(p:pictures)` `(p:desktop)` | 系统特殊目录(另有 `(p:d)` 等短名) |
| `(d0)` `(d1)`… | 目录层级(所在目录往上,越界为空) |
| `(n:i)` / `(n:c)` | 批次内序号 / 批次文件总数 |
| `(d:yyyy-MM-dd)` | 当前日期(.NET 格式串,`/`→`-`、`:`→`'`) |

含 `(p)` 系令牌的模板输出完整路径,否则视为相对文件名(落在输出目录);重名按设置自动 ` (2)` 递增。例:`(p)(f)_小`、`sub/(f)`。

## 已知限制

- docx→pdf 为纯 .NET 渲染,复杂排版(分栏、浮动图片等)保真度有限
- 大视频转 GIF 会显著放大体积(默认已限宽 480px、12fps)
- 文档转换中的 docx 图片仅 png/jpg/gif/bmp 可嵌入导出目标
- pdf→docx/html 为文本模式(提取文字重建,不还原原排版/图片)
- 旧版二进制 `.doc`/`.ppt`:转 TXT 用内置提取器(零依赖,解析思路对照 NPOI HWPF/HSLF);转 docx/pdf/html 无 LibreOffice 时走文本模式回退、装了则自动高保真;仅 ppt→pptx 强制要求 LibreOffice

## 许可证

本软件自身代码 MIT。FFmpeg 为 GPLv3,详见 `THIRD-PARTY-LICENSES.txt`。
