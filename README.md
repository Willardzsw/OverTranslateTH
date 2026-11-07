<p align="center">
  <img src="docs/images/icon.svg" width="140" alt="TT Tools icon"/>
</p>

<h1 align="center">TT Tools</h1>

<p align="center">带泰语 OCR 的 Windows 屏幕翻译工具</p>

> 这是基于 [OverTranslate](https://github.com/Hon-Lu/OverTranslate) 修改的独立版本，并非官方发布，也与原作者无关。使用中的问题请提交到[本仓库的 Issues](https://github.com/Willardzsw/OverTranslateTH/issues)，不要反馈给原项目。

## 主要功能

- **文字翻译**：程序启动后的主页。
- **截图翻译**：可从文字翻译页或快捷键进入，在屏幕上框选区域后识别并翻译。
- **实时翻译**：框选屏幕区域后持续识别并翻译，也可用快捷键启动和暂停。
- **泰语 OCR**：使用 PP-OCRv5 泰语识别模型，在本地运行。源语言需要明确选择“泰语 / TH”，`AUTO` 不会自动切换到泰语。
- **保留原有语言**：英语、简体中文、繁体中文、日语、韩语的识别模型和路由不变。
- **翻译服务**：沿用原项目的翻译服务，包括 OpenAI 兼容接口。需要密钥的服务由你在设置里自行填写。
- **截图与贴图**：截图标记（马赛克、模糊、序号等），把选区或剪贴板内容贴在屏幕最上层。
- **便利贴**：复制文字或图片后按快捷键（默认 F2），在指针处贴一张可编辑的便利贴；图片会先在本地识别成文字。可置顶、调透明度、鼠标穿透，重启后自动恢复，可选把每次保存记到日志文件。主界面有“便利贴”页统一管理。
- **鼠标增强**：按住组合键滚动滚轮调节窗口透明度、横向滚动，按住组合键拖动调整窗口大小（做法参考 AltSnap，代码为独立实现）。
- **托盘菜单开关**：各功能的开关可以直接在托盘图标的右键菜单里勾选。
- **界面语言**：简体中文、繁体中文、英语、日语、韩语、俄语、泰语。

和原项目相比，界面保留“文字翻译”“实时翻译”“便利贴”和“设置”，移除了漫画模型下载和更新检查。程序启动后停在系统托盘，不自动打开主窗口。

## 下载和运行

到 [Releases](https://github.com/Willardzsw/OverTranslateTH/releases/latest) 下载，需要 Windows 10/11 x64，不用另装 .NET：

- **安装版** `TTTools-<版本>-win-x64-Setup.exe`：装到当前用户目录，不需要管理员权限。
- **便携版** `TTTools-<版本>-win-x64-Portable.zip`：解压后运行 `TTTools.exe`。请解压到可写的文件夹，不要放进 `Program Files`。

程序没有代码签名，Windows SmartScreen 可能提示“未知发布者”，可以选择“更多信息”“仍要运行”。程序不会自动更新，新版本需要手动下载；覆盖安装或替换程序文件时，请保留 `profile-thai` 文件夹。卸载时这个文件夹也会保留，其中含有你填写的 API 密钥，不再使用时请手动删除。

从源码构建需要 .NET 8 SDK：

```powershell
git clone https://github.com/Willardzsw/OverTranslateTH.git
cd OverTranslateTH
dotnet build src\OverTranslate\OverTranslate.csproj -c Release
.\Start-Thai.cmd
```

配置、API 密钥、日志和资产都保存在程序旁边的 `profile-thai` 文件夹，不会读取已安装的 OverTranslate 的配置。不要把这个文件夹分享或上传给别人。

如果同时运行原版，两者默认的全局快捷键相同，会发生冲突。

## 文档

- [README-THAI.md](README-THAI.md)：使用方式、OCR 模型选择和验证命令
- [THAI-VALIDATION.md](THAI-VALIDATION.md)：验证记录和尚未覆盖的范围
- [PRIVACY.md](PRIVACY.md)：隐私说明

## 许可和致谢

本项目基于 [Hon-Lu/OverTranslate](https://github.com/Hon-Lu/OverTranslate) 修改，沿用 [MIT 许可](LICENSE)，原始版权归 Hon-Lu 所有。

第三方组件和模型的声明见 [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt)，泰语模型的来源和散列见 [provenance.json](src/OverTranslate/ocrmodels/onnx/thai/provenance.json)。
