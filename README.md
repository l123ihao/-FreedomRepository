# 万能格式转换器 (FormatConverter)

Windows 桌面应用:视频 / 音频 / 文档 / 图片格式互转。C# WPF(.NET 10),中文界面,支持拖拽、批量转换与进度显示。FFmpeg 已打包进应用,用户无需自行安装。

## 功能

| 类别 | 源格式 | 可转换目标 |
|---|---|---|
| 视频 | mp4 / mkv / avi / mov / webm / gif | 视频互转(兼容时无损秒转)、提取/转码音频、转 GIF |
| 音频 | mp3 / wav / flac / m4a / aac / ogg | 音频互转(码率 128/192/320 可选,wav/flac 无损) |
| 文档 | docx / txt / md / pdf / pptx | docx→pdf/txt/md/html;txt→docx/pdf;md→docx/html/pdf;pdf→txt;pptx→docx/txt/pdf(幻灯片标题→Word 标题) |
| 图片 | png / jpg / webp / bmp / gif / ico | 全互转(gif 动画→图片取首帧,gif→mp4 保留动画) |

## 使用

1. 解压发布包,运行 `FormatConverter.exe`(无需安装 .NET 或 FFmpeg)
2. **直接转换:把文件拖到上方某个格式磁贴上**——例:把 PPT 拖到「DOCX」即转成 Word,把视频拖到「MP3」即提取音频。首次拖入会弹确认窗,可勾选「不再提醒」以后拖入即转(保存在 %APPDATA%\FormatConverter\settings.json)
3. 不能转成该格式的文件不会被加入,对应磁贴红闪 0.5 秒并提示原因(如旧版 .ppt 不支持);磁贴右上角红色数字 = 该格式下待转/转换中的文件数
4. 也可先点选目标格式,再用「选择文件」「添加文件夹」加入队列(转换队列始终可见,不会不知道加了哪些文件);「开始转换」手动启动(确认窗选「取消」后也用它),失败项可「重试失败项」
5. 底部可改输出目录、展开「高级设置」调整选项;转换可随时「取消」(会终止 ffmpeg 进程,不残留半成品)

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

## 已知限制

- docx→pdf 为纯 .NET 渲染,复杂排版(分栏、浮动图片等)保真度有限
- 大视频转 GIF 会显著放大体积(默认已限宽 480px、12fps)
- 文档转换中的 docx 图片仅 png/jpg/gif/bmp 可嵌入导出目标
- 旧版 .ppt(二进制格式)不支持,请先在 PowerPoint 里另存为 .pptx 再转换

## 许可证

本软件自身代码 MIT。FFmpeg 为 GPLv3,详见 `THIRD-PARTY-LICENSES.txt`。
