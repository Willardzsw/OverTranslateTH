# OverTranslate 泰语 OCR 独立开发版

项目：`E:\Claude\overtranslate`，分支 `feature/thai-ocr`。基于官方源码 `fddeb35b0e39264f135e3c9ab375ea61ff07fc93`，保留 MIT 许可。

## 启动和使用

1. 双击项目根目录 `Start-Thai.cmd`。也可直接运行 `src\OverTranslate\bin\Release\net8.0-windows10.0.26100.0\win-x64\OverTranslate.exe`。
2. 在截图翻译的源语言中明确选 **泰语 / Thai / TH**。`AUTO` 仍使用原来的中英日识别模型，不会自动切换泰语。
3. 选择目标语言，例如简体中文；翻译服务选择 OpenAI。沿用原项目已有的 OpenAI 兼容接口、模型设置和逐块翻译流程。
4. 测试版配置是新的独立配置。开发过程中没有读取、导入、复制或输出原安装的 API 密钥；需要真实服务测试时，由用户在测试版设置界面自行填写服务器、模型和密钥，不要把密钥发到聊天中。
5. 用区域截图测试泰文。界面按钮/多栏文字可用 Interface（界面）模式，段落用 General（一般）模式。现有覆盖逻辑仍按识别块及原文字行坐标绘制。

原版和测试版的全局快捷键默认相同，测试时请只运行其中一个，避免快捷键注册冲突。测试版有独立的单实例事件、启动项名称、配置、日志和资产目录，未运行安装器、覆盖原程序或导入旧配置。

## 精简界面与 OCR 模型选择

- 主界面仅有“文字翻译”和“设置”，启动和普通打开窗口默认进入文字翻译。
- 文字翻译页右上角保留“截图翻译”按钮；原截图快捷键继续可用。
- 已移除实时翻译导航、启动/暂停快捷键注册及运行入口；设置中不再展示实时翻译快捷键或漫画竖排模型下载块。
- 历史配置字段及内部回归用到的旧实现保留用于兼容，不会因此重新启用实时功能，也不会删除旧配置或资产。

在“设置 → 常规设置 → OCR 识别模型”选择：

| 选项 | 适用源语言 | 运行方式 |
|---|---|---|
| 按源语言选择（默认） | 原有全部 OCR 源语言 | 保持原路由：TH 泰语、KO 韩语，其余通用模型 |
| PP-OCRv6 通用 | 中英日及 AUTO | 已安装本地模型 |
| PP-OCRv5 泰语 | TH、EN | 已安装本地模型，支持泰英混排 |
| 韩语专用 | KO | 已安装本地模型 |

### 模型规模与显卡（2026-11-04）

唯一启动入口：项目根目录的 `Start-Thai.cmd`（桌面的“泰语翻译”快捷方式也指向它）。固定程序目录为 `src/OverTranslate/bin/Release/net8.0-windows10.0.26100.0/win-x64/`，Windows 开机启动项 `OverTranslate-Thai-Dev` 指向其中的最新版 `OverTranslate.exe`。保留该目录原有的 `profile-thai` 配置；额外的 Debug、OCR 性能/界面构建副本及旧启动脚本已清理。更新前先检查进程；若程序仍在运行，必须请用户退出并等待，不强制结束、不另建输出目录绕过文件占用。

在同一张 OCR 设置卡中可选择：

- **文字检测规模**：Tiny（原默认）、Small、Medium，适用于包括泰语在内的所有现有源语言。
- **通用文字识别规模**：Small（原默认）、Medium，仅作用于通用中英日模型；泰语、韩语保持其原专用模型，显式选择专用模型时此项禁用。默认仍按语言路由，AUTO 不会切换到泰语。
- **运行设备**：CPU（原默认），或列出的硬件显卡（DirectML）。多显卡按独立显存排序，选择项标明显卡名称。无需安装 CUDA。
- **准备所选模型和显卡组件**：仅点击时下载缺少的组件，不在启动或识别时自动下载。新模型位于 `profile-thai/assets/models/ppocrv6`，微软签名的 DirectML 1.15.4 位于 `profile-thai/assets/runtimes/directml/1.15.4`。下载检查长度和 SHA-256，取消或失败不覆盖已安装文件。

设备或规模改变会重建推理会话并使截图 OCR 复用缓存失效；设置显示上次实际加载的执行设备。显式 GPU 选择失败会报错，不会静默改成 CPU。DirectML 会话关闭 memory pattern、使用顺序执行，并对同一组会话的并发截图串行处理，保持原图坐标。模型来源、固定下载版本、散列与 small/medium 的预处理参数见 `src/OverTranslate/ocrmodels/ppocrv6-performance.json`，沿用 PaddleOCR/RapidOCR Apache-2.0 声明。

离线验证使用四组合成泰文图、一条公开泰文裁剪行及原中英日韩合成图，不涉及用户截图或在线翻译/API：

- CPU：原默认、Small 检测、Medium 检测、Medium 通用识别四组合，各 9 张，均匹配全部 26 条已知文本，原图坐标检查通过。
- RTX 4060 Laptop：原规模、Small 检测、Medium 检测+识别三组合，各 9 张，均匹配全部 26 条已知文本；另检查各组合的三个并发截图。推理 profile 确认检测和识别实际执行 DirectML kernel，少量辅助算子仍在 CPU。结果见 `.test-artifacts/ocr-performance/gpu-final/`。
- 六种界面语言、两种主题、默认/Medium 与通用/泰语组合共 56 张合成设置/主窗口视图，见 `.test-artifacts/ocr-performance/ui-final/`。

完整回归：3,169 项通过、17 项原有跳过。未启用 profiling 的同一服务内 CPU/GPU/模型规模切换、不可用 GPU 明确报错及切回 CPU 恢复也已验证，见 `.test-artifacts/ocr-performance/runtime-switch/report.json`。

小样本没有体现更大模型的准确率优势，不能据此推断真实游戏准确率或固定加速倍数。首次会话编译、显卡负载和截图大小都会影响耗时；Intel 核显选项尚未实测。建议先用 Small 检测配合 RTX 4060，再以出错截图比较 Medium。

```powershell
dotnet build src/OverTranslate/OverTranslate.csproj -c Release
dotnet test src/OverTranslate.Tests/OverTranslate.Tests.csproj -c Release
# Harness 可通过 OVERTRANSLATE_THAI_ASSETS_DIR 指向该构建的 profile-thai/assets；不读取任何设置文件。
dotnet run --project tools/OcrHarness -c Release -- --ocr-performance .test-artifacts/ocr-performance/results
```

“按源语言选择”只根据用户所选源语言路由，不会让 AUTO 自动识别泰语。模型和源语言不兼容时会明确报错。切换模型会使旧截图 OCR 缓存失效，避免拿旧结果冒充新模型结果。

### 轻量模型调查（2026-11-04）

已按用户要求删除 Typhoon 及其本地服务配置、检查按钮和调用代码；不准备 Ollama、WSL 或大型模型。旧配置中的 Typhoon/未知选择在加载时回退至“按源语言选择”。当前四个选项均在进程内使用本地模型，无需另开推理服务。

| 候选 | 识别模型文件大小（十进制 MB） | 同组 14 条已知文本匹配 |
|---|---:|---:|
| 现用 PP-OCRv5 泰语 ONNX | 7.9 MB | 14/14（已有报告） |
| Tesseract tessdata_fast / tha | 1.1 MB | 11/14 |
| Tesseract tessdata_best / tha | 7.6 MB | 11/14 |

这只是四组合成图和一张公开裁剪行的小样本；候选使用既有检测框、4px 裁剪边距、LSTM SingleLine、仅 `tha`，未调参或测试 `tha+eng`。比较忽略 ASCII 空格；NFKC 复核不改变三个漏匹配结果。候选主要在泰英混排与公开裁剪行上出错，不能据此断言通用准确率或游戏效果，也不比较整条流水线速度。文件大小不含推理运行库。当前证据不足以作为升级版加入菜单，继续使用现有模型。

另找到 [EasyOCR 泰语 ONNX](https://huggingface.co/itextresearch/itext-EasyOCR-thai)，模型约 215 MB；只核对公开元数据，未下载或验证兼容性/精度。候选来源：[Tesseract fast](https://github.com/tesseract-ocr/tessdata_fast/blob/main/tha.traineddata)、[Tesseract best](https://github.com/tesseract-ocr/tessdata_best/blob/main/tha.traineddata)、[官方模型说明](https://tesseract-ocr.github.io/tessdoc/Data-Files.html)。

隔离测试的源码、固定版本与散列、原始结果保存在 `.test-artifacts/local-ocr-only/benchmark/`；未把 Tesseract 依赖或模型加入主程序。现用模型报告为 `.test-artifacts/thai-ocr/report.json`。

## 实现

- `TH` -> `ocrmodels/onnx/thai/rec.onnx` + `dict.txt`，使用真实泰语 PP-OCRv5 识别模型。
- 共享原来的检测器和 RapidOcrNet 3.0.0；中英日通用模型、韩语模型及路由保留。
- 识别结果继续进入现有 OpenAI 翻译模块并保留 Bounds、SourceLineBounds、RenderGlyphHeight。
- 修复泰文紧贴截图边缘时检测框伸进对齐补边的问题，将返回坐标限制到原图范围。
- 模型来源、固定版本、SHA-256、字典约定见 `src/OverTranslate/ocrmodels/onnx/thai/provenance.json`；Apache-2.0 许可见原 `THIRD-PARTY-NOTICES.txt` 的 PaddleOCR 部分。
- 配置/日志：每个可执行文件旁的 `profile-thai/`；资产：`profile-thai/assets/`。独立资产覆盖环境变量为 `OVERTRANSLATE_THAI_ASSETS_DIR`。

## 精简说明

漫画竖排模型、Velopack 安装与更新组件、卸载清理代码都已删除。日语等竖排文字继续走常规 OCR 流程。

## 更新方式

- 程序不检查官方版本，启动和运行时都不会请求官方 Release 接口，也不提示更新。
- 需要跟进官方版本时，用户自己在项目里查看，再要求合并官方源码；合并时保留泰语 OCR 与独立配置，并完成构建、回归和实际画面验收。
- 更新保留原位置的 `profile-thai/`，不读取或迁移密钥。

## 构建和验证

已安装 .NET 8 SDK，依赖放在项目的 `.packages` 中：

```powershell
cd E:\Claude\overtranslate
dotnet restore src\OverTranslate.Tests\OverTranslate.Tests.csproj --source https://api.nuget.org/v3/index.json
dotnet build src\OverTranslate\OverTranslate.csproj -c Release --no-restore
dotnet test src\OverTranslate.Tests\OverTranslate.Tests.csproj -c Release --no-restore
```

离线 OCR 验证（不读取密钥、不联网调用翻译）：

```powershell
dotnet run --project tools\OcrHarness -c Release --no-launch-profile -- --thai-verify .test-artifacts\thai-ocr .test-artifacts\thai-public\official-thai-crop.png .test-artifacts\thai-public\official-thai-page.png
dotnet run --project tools\LayoutProbe -c Release --no-launch-profile -- thai-overlay .test-artifacts\thai-ocr
```

若官方样例图片尚不存在，可省略命令末尾两个路径，只生成并识别本地测试图。外部用户图片不会被上传；不要把含敏感信息的截图提交到 Git。

验证产物：

- `.test-artifacts/thai-ocr/report.json`：模型字典核对、识别文字、置信度、原始/分组/即时框及坐标。
- `.test-artifacts/thai-ocr/*-boxes.png`：识别框标注图。
- `.test-artifacts/thai-ocr/thai-menu-overlay.png`、`thai-sentence-overlay.png`：真实 WPF OverlayWindow 的渲染预览，使用**固定的模拟中文译文**，不是在线 API 返回结果。
- 对应 `*-overlay-geometry.json`：实际 WPF 覆盖框物理位置、尺寸和 DPI；验证机器缩放为 150%。
- `src/OverTranslate.Tests/TestResults/thai-full-regression.trx`：完整回归结果。
- `THAI-VALIDATION.md`：本次验证结果和限制。

官方公开样例来源：PaddlePaddle 模型卡 https://huggingface.co/PaddlePaddle/th_PP-OCRv5_mobile_rec 。裁剪行预期为 `อานุภาพทำลายล้างสูง`；整页没有逐字人工标注，不把“读出若干行”当作整页准确率。

## 未验证范围

尚未使用用户实际游戏/软件截图进行最终验收，也没有调用用户的真实 OpenAI 服务。模拟 HTTP 验证覆盖了逐块和批次翻译的坐标保留；覆盖预览使用固定译文。复杂艺术字、运动画面、极低分辨率、斜排/竖排泰文和混排版面仍需实际样例验证。
