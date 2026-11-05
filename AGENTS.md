# OverTranslate Thai OCR development fork

- Work in E:\Claude\overtranslate; branch feature/thai-ocr, upstream base fddeb35b0e39264f135e3c9ab375ea61ff07fc93.
- Preserve the installed OverTranslate application and its user profile. Never inspect, copy, print, or import API keys.
- All development configuration/logs/assets use profile-thai beside each executable. Do not add automatic credential migration, installers, publishing, or upstream binary auto-update hooks.
- User-approved update policy: check public official stable release metadata once at startup only, with no periodic polling. Show a notification and release notes link; never download/apply official packages. Merge upstream source only after the user asks, preserving Thai OCR and profile isolation.
- Thai OCR must use the real thai/rec.onnx + matching dictionary. AUTO remains the existing CJK recognizer; select TH explicitly.
- Keep original EN/ZH/ZH-HANT/JA/KO models and behavior. Retain OCR source coordinates for translation and overlays.
- Model provenance and hashes are in src/OverTranslate/ocrmodels/onnx/thai/provenance.json; preserve Apache-2.0 notices.
- Use project-local .packages and official NuGet. Build with dotnet build src/OverTranslate/OverTranslate.csproj -c Release.
- Relevant checks: dotnet test src/OverTranslate.Tests/OverTranslate.Tests.csproj -c Release; offline OCR and overlay commands are in README-THAI.md.
- Generated public/synthetic image reports live under .test-artifacts and must be labeled as such. Do not claim a live API call or user-game screenshot was tested unless it actually was.

- User-approved compact UI: expose only text translation and settings, with text translation as the startup/default home. Keep screenshot OCR on that page and via its shortcut. Do not restore realtime navigation, realtime start/pause hotkeys, or the manga-model download card.
- OCR model selection belongs to OcrModels in the independent profile. Default preserves language routing. Explicit local selections must match supported source languages. The user rejected large local-service OCR models; Typhoon and its service configuration are removed. Do not add a large inference runtime or model without a new user request. Prefer lightweight in-process candidates and verify accuracy before offering a replacement.

- User-approved single build location: `src/OverTranslate/bin/Release/net8.0-windows10.0.26100.0/win-x64/OverTranslate.exe`; the only user launch script is `Start-Thai.cmd`. The `OverTranslate-Thai-Dev` Windows startup entry must point to that latest executable. Keep its existing `profile-thai` in place.
- Before replacing/building that executable, check for running project copies. If any are running, ask the user to exit and wait for the process to end; never force-stop it or work around a file lock with alternate output directories or additional versions. Use Release for builds, tests and probes. Remove redundant generated build copies after verification instead of leaving multiple user launch choices.
