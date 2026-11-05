# 泰语 OCR 验证记录

主机 zswrog；工程 `E:\Claude\overtranslate`（2026-11-05 由 `F:\ChatGPT\overtranslate` 迁入，迁移记录之前的验证均在 F 盘完成）；分支 `feature/thai-ocr`。本记录对应当前未提交工作区。

以下为首次泰语 OCR 验证记录；后续改动按时间追加，当前状态见文末；历史 Typhoon 记录已被最后的移除记录取代。

## 构建与回归

- .NET 8 Release 构建：成功，0 错误。8 条已有编译告警涉及 AnnotationTextEditor 的成员/空值与 RealtimeSessionController 的 Windows API 版本。
- 完整回归：3118 通过、17 跳过、0 失败，共 3135；原始结果 `src/OverTranslate.Tests/TestResults/thai-full-regression.trx`。
- 重点语言/资源/泰语/原语言契约测试：86 通过。
- 原中英日通用、韩语、检测/方向模型文件的 Git blob 与上游基线全部一致。
- 泰语识别模型和字典的 SHA-256 与 provenance.json 一致；Release 输出的文件也一致。
- 模型输出 526 类 = 524 字典项 + CTC blank + space；去掉 metadata 尾部 CR/LF 后，嵌入字符顺序与字典完全一致。

## 离线识别

| 样例 | 源语言 | 已知文本条数 | 原始 / 分组 / 即时框 |
|---|---|---:|---:|
| thai-menu | TH | 4 | 4 / 4 / 4 |
| thai-sentence | TH | 3 | 3 / 3 / 3 |
| english | EN | 3 | 3 / 3 / 3 |
| japanese | JA | 3 | 3 / 3 / 3 |
| chinese | ZH | 3 | 3 / 3 / 3 |
| korean | KO | 3 | 3 / 3 / 3 |
| thai-small | TH | 3 | 3 / 3 / 3 |
| thai-dark | TH | 3 | 3 / 3 / 3 |
| official-thai-crop | TH | 1 | 1 / 1 / 1 |
| official-thai-page | TH | 0 | 4 / 4 / 4 |

25 条本地生成的已知文本，加官方裁剪行 1 条，均在原始、分组、即时结果中找到（比较时忽略 ASCII 空格）；官方整页读出 4 块，没有整页逐字人工标注，故不声明其准确率。原始报告为 `.test-artifacts/thai-ocr/report.json`。

官方裁剪行 `อานุภาพทำลายล้างสูง` 置信度约 0.973。紧贴边缘的框从原先 1296×176 修正为图像实际范围 1280×172。测试包含泰语组合符、泰文数字、问号、括号、英语单词、22px 小字号与深色背景。

## 翻译与覆盖

- 用现有 OpenAiCompatibleProvider 的模拟 HTTP handler 验证逐块及批次两种模式。泰文原文进入相同请求流程，返回译文后保留 Bounds、SourceLineBounds、RenderGlyphHeight。没有读取用户密钥或发起真实 API 调用。
- 真实 WPF OverlayWindow 在 150% DPI 下，用 OCR 的真实坐标、固定模拟中文译文，渲染了 4 个菜单项和 3 个句子的覆盖图。
- 覆盖框相对源框左上角为 -3px/-3px，宽高各增加 6px，为现有背景边距；没有出现整组选区合并成一个覆盖框的情况。
- 已人工查看 `thai-menu-overlay.png`、`thai-sentence-overlay.png`，对应中文译文覆盖在原文字位置，无残留泰文。预览使用模拟译文，不能作为线上翻译质量证明。

## 许可与来源

OverTranslate 官方项目 https://github.com/Hon-Lu/OverTranslate ；泰语 PaddleOCR 模型卡 https://huggingface.co/PaddlePaddle/th_PP-OCRv5_mobile_rec （Apache-2.0）；ONNX 和字典来自 RapidAI/RapidOCR v3.10.0，见 provenance.json 与 THIRD-PARTY-NOTICES.txt。

## 实际限制与后续验收

尚未验收用户实际游戏/软件截图、实时运动画面或真实 OpenAI 服务。测试版没有导入原安装配置或密钥；请由用户在新测试版设置中自行填写服务配置，再以不含敏感信息的泰语画面验收。竖排泰文、艺术字、极低清晰度和复杂混排尚未作保证。AUTO 仍使用原通用模型，泰语必须明确选 TH。

启动入口：项目根目录 Start-Thai.cmd。使用说明：README-THAI.md。当前修改未发布、未安装覆盖，未改动原应用的配置、密钥或安装目录。

## 更新提示追加验证（2026-11-04）

- 用户最终约定：仅启动时检查一次官方最新正式 Release；不每小时检查，不后台轮询。仅提示并提供官方说明链接；用户通知后再人工合并官方源码。
- 新通知数据类型仅包含版本号及说明页地址，不包含安装器或更新管理器。原 `UpdateService` 的检查入口返回空，下载/安装入口直接拒绝，不能通过旧按钮或测试环境变量启用官方二进制更新。
- Release 构建成功，0 错误、8 条既有告警；原始日志 `.test-artifacts/update-notice/release-build.log`。最初一次构建因正在运行的测试版锁住 EXE 而失败；用户确认退出后，最终构建成功。
- 通知协议、泰语契约及语言资源专项：66 通过、0 失败；结果 `src/OverTranslate.Tests/TestResults/thai-notify-focused.trx`。
- 最新完整回归：3141 通过、17 跳过、0 失败，共 3158；结果 `src/OverTranslate.Tests/TestResults/thai-notify-regression.trx`。xUnit 另外报告既有 `Settings_ReplaceMalformedUtf16RatherThanFailingToSave` 的两条参数化用例生成相同 ID，忽略其中一条，因此与旧结果加上新增 24 项后的计数相差一项；没有修改该既有测试。
- 新协议测试覆盖语义版本比较、旧版/同版/异常标签、草稿/预发布排除、未发布 Release、限流、坏 JSON、取消、跳过后更高版本仍提示，以及旧安装入口禁用。
- 真实通知窗口已离屏渲染六种语言、明暗两种主题，共 12 张预览；虚构版本为 `99.0.0`，仅用于 UI，不代表官方存在此版本。输出 `.test-artifacts/update-notice/notice-*.png` 及 `geometry.json`；已抽查简体中文浅色与俄文深色布局，无按钮裁切或文字溢出。
- 使用新代码完成一次无认证的公开版本接口实连，官方基线 `2.10.0`，未发现更高正式版本；仅请求版本元数据。记录 `.test-artifacts/update-notice/official-release-check.log`。GitHub 接口说明：https://docs.github.com/en/rest/releases/releases#get-the-latest-release 。
- 没有启动真实 OpenAI 翻译请求、读取用户密钥、安装官方更新、自动合并源码或重新打开用户桌面上的主程序。此前真实游戏画面和在线翻译的未验收范围仍有效。

## 精简界面与可选 OCR 模型（2026-11-04）

- 最新产品界面只有文字翻译、设置两个目的页；启动/普通打开默认进入文字翻译。截图按钮移至文字翻译页。实时翻译的页面实例、导航、全局快捷键注册和启动分支已移除；设置中的漫画模型下载块已移除。未大规模删除内部旧实现和兼容配置字段，旧回归仍验证底层算法；这些不是产品可用的实时入口。
- 设置增加按源语言、通用、泰语、韩语和 Typhoon 本地服务五个模型选项。默认路由及已有模型文件不变。Typhoon 仅允许本机服务，不读取翻译凭据，不自动下载；保留检测器坐标，逐行裁图识别，无真实逐字置信度时保留空值。
- 最终 Release 构建成功，0 错误、8 条既有告警；日志 `.test-artifacts/compact-ocr/release-build.log`。用户确认退出旧测试版后完成构建，没有主动重新运行主程序。
- 最新完整回归：3173 通过、17 跳过、0 失败，总计 3190；TRX 为 `src/OverTranslate.Tests/TestResults/compact-ocr-regression.trx`。包含 32 项新 OCR 模型选择/本地协议/坐标/取消/缓存测试及 4 项精简界面测试。删除 4 条针对已移除侧栏快捷工具的旧界面断言，保留其余窗口框架检查。既有 UTF-16 参数化用例重复 ID 警告仍存在，与前次验证一致。
- 新后端采用模拟 HTTP 验证，无真实 Typhoon 推理或 OpenAI 翻译请求。覆盖本机地址限制、模型列表、视觉请求、模型选择持久化、源语言兼容、坐标保留、不伪造置信度、忙时跳帧、取消后释放、失败时不静默回退、切换模型后缓存失效。
- 已用真实已安装模型处理标记为合成的旧样例：默认 TH 4 块、手动泰语 TH 4 块、手动通用 EN 3 块、手动韩语 KO 3 块；已知文本均找到，框没有越出图像。结果 `.test-artifacts/compact-ocr/local-models.json`。
- 离屏生成 32 个合成设置/界面预览：六种语言、明暗主题、默认/Typhoon 模型卡，以及中英两种语言的两页主界面；没有读取用户设置或显示真实窗口。修复并验证了自定义 ComboBox 的所选模型标签，增加实际视觉树非空断言。已查看中文主页、设置、两种模型卡及英文深色主页。输出 `.test-artifacts/compact-ocr/*.png`、`geometry.json`。
- 本机常用 Typhoon 服务端口 8000 和 11434 未响应。未安装权重或服务，未验证真实 Typhoon 的图片识别、GPU/内存需求、游戏精度、耗时或覆盖质量；这部分是剩余部署/验收工作，不应将模拟协议成功称为模型已可用。
- 既定更新策略继续有效：只启动检查一次官方正式 Release，仅提示；由用户通知后再人工合并官方源码。

## 移除 Typhoon 与轻量候选复核（2026-11-04，当前状态）

- 用户取消部署大型本地服务，明确要求移除 Typhoon 并寻找其他候选。已删除 Typhoon 引擎和客户端、服务配置字段与界面，六种语言移除相应资源。菜单只剩按源语言、通用、泰语、韩语四项；旧 Typhoon/未知配置选择安全回退至按源语言。没有安装 Ollama、WSL 或大型模型。
- 专项测试 63 通过、0 失败：`src/OverTranslate.Tests/TestResults/local-ocr-only-focused.trx`。完整回归 3157 通过、17 跳过、0 失败，共 3174：`src/OverTranslate.Tests/TestResults/local-ocr-only-regression.trx`。数量减少源于删除 Typhoon 协议测试，保留 16 个本地模型选择、兼容与配置/缓存用例；既有 UTF-16 用例重复 ID 提示仍存在。
- 用户随后确认已从托盘退出。核对正常启动路径没有运行中实例后，最终 Release 构建成功，0 错误、8 条既有警告，已输出至 `src/OverTranslate/bin/Release/net8.0-windows10.0.26100.0/win-x64/`。`Start-Thai.cmd` 现在指向本次不含 Typhoon 的新版本。最终日志 `.test-artifacts/local-ocr-only/release-final.log`；此前隔离预构建日志为 `release-prepared.log`。未主动启动主程序，也未读取或修改用户配置、密钥。
- 更新后的模型卡与两页主界面共 32 个离屏合成预览，检查六语/明暗主题、真实选中标签和两页导航。输出 `.test-artifacts/local-ocr-only/ui/`；已查看中文泰语模型卡，标签清楚且没有服务配置块。未读取真实用户设置、显示真实主窗口或调用 OpenAI。
- 两个 Tesseract 官方 Thai 权重在隔离项目测试，下载共约 8.7 MB；主程序未新增依赖。fast 为 1,072,600 bytes，best 为 7,614,571 bytes，均在同组 14 条已知文本中匹配 11 条，现有 PP-OCRv5 报告对应 14/14（模型 7,915,294 bytes）。测试覆盖四组合成图和一张公开裁剪行，使用既有检测框、4px 裁剪边距、LSTM SingleLine、仅 tha；未测试 tha+eng 或调参。三条未匹配内容经过 NFKC 仍不匹配。此样本不能代表游戏准确率，也不支持速度或泛化优劣结论。
- 可复核源码、下载版本/散列、结果与日志：`.test-artifacts/local-ocr-only/benchmark/`。在该目录执行 `python download_models.py`、`dotnet restore --configfile NuGet.Config`、`dotnet run --no-restore -- E:\Claude\overtranslate .` 可复测；需联网获取官方小模型与 NuGet 运行库，不读取用户配置。
- EasyOCR Thai ONNX 公开文件大小 215,216,839 bytes，仅调查元数据，未下载、未验证。当前没有证据说明可比现有模型更好，故未增加新的菜单选项。模型来源链接和有限比较见 README-THAI.md。
- 既定精简界面与启动一次的更新提示政策保持；真实用户游戏画面和真实 OpenAI 调用仍未验收，不能将合成预览和小样本结果当作实际游戏验收。

## 迁移到 E 盘（2026-11-05）

- 工程由 `F:\ChatGPT\overtranslate` 复制到 `E:\Claude\overtranslate`。确认无运行中的项目副本后，用 `.packages` 重新 restore；`project.assets.json` 已不含 F 盘路径。
- Release 构建成功，0 错误。完整回归（Release）：3234 通过、17 跳过、0 失败，共 3251。泰语 `rec.onnx`、`dict.txt` 的 SHA-256 与 provenance.json 一致。
- 启动项 `OverTranslate-Thai-Dev` 和桌面快捷方式“泰语翻译”已指向 E 盘；`profile-thai` 保留原位，未读取其内容。测试生成的 `src/OverTranslate.Tests/bin` 已清理。
- F 盘副本未改动、未删除。迁移后未启动 E 盘主程序，也未调用真实翻译服务。

## 品牌与更新策略调整（2026-11-05）

- 显示名改为 OverTranslateTH，图标换为暖色双 T（`tools/icons/generate_app_icon.py`）；`OverTranslate.exe`、开机启动项名和 `profile-thai` 不变。
- 移除官方版本检查、更新提示窗口和主窗口更新徽章；关于页移除 GitHub、官网、赞助入口，保留上游署名并可打开随程序分发的 `LICENSE.txt`、`THIRD-PARTY-NOTICES.txt`。
- 关于页由 `LayoutProbe about` 离屏渲染核对，输出在 `.test-artifacts/rebrand/about/`，是合成渲染；未启动真实主程序，也未读取用户配置或密钥。
