# OverTranslate Thai OCR development fork

- Work in E:\Claude\overtranslate; branch feature/thai-ocr, upstream base fddeb35b0e39264f135e3c9ab375ea61ff07fc93.
- Preserve the installed OverTranslate application and its user profile. Never inspect, copy, print, or import API keys.
- All development configuration/logs/assets use profile-thai beside each executable. Do not add automatic credential migration or upstream binary auto-update hooks. Release packages are built only by `tools/release/build-release.ps1`, when the user asks, and must never contain `profile-thai`.
- User-approved update policy: the app performs no official release checks or update notices at all. The user checks official versions themselves and asks before upstream source is merged, preserving Thai OCR and profile isolation. Never download/apply official packages.
- User-approved branding: display name `OverTranslateTH` with the warm double-T icon from `tools/icons/generate_app_icon.py`. The executable stays `OverTranslate.exe`, and the startup entry and `profile-thai` stay unchanged. Keep the MIT LICENSE, THIRD-PARTY-NOTICES and the "based on OverTranslate" attribution in the About panel and build output.
- Thai OCR must use the real thai/rec.onnx + matching dictionary. AUTO remains the existing CJK recognizer; select TH explicitly.
- Keep original EN/ZH/ZH-HANT/JA/KO models and behavior. Retain OCR source coordinates for translation and overlays.
- Model provenance and hashes are in src/OverTranslate/ocrmodels/onnx/thai/provenance.json; preserve Apache-2.0 notices.
- Use project-local .packages and official NuGet. Build with dotnet build src/OverTranslate/OverTranslate.csproj -c Release.
- Relevant checks: dotnet test src/OverTranslate.Tests/OverTranslate.Tests.csproj -c Release; offline OCR and overlay commands are in README-THAI.md.
- Generated public/synthetic image reports live under .test-artifacts and must be labeled as such. Do not claim a live API call or user-game screenshot was tested unless it actually was.

- User-approved direction: a streamlined Thai office tool. The manga models, Velopack and installer/uninstall code are removed on purpose; do not restore them when merging upstream.
- User-approved UI: expose text translation, realtime translation and settings, with text translation as the startup home. Keep screenshot OCR on the text translation page and via its shortcut. Realtime navigation and its start/pause shortcut are back at the user's request. Do not restore the manga-model download card.
- User-approved diagnostics policy: export only. Do not compile in an upload endpoint or send diagnostics to the upstream project's server.

- OCR model selection belongs to OcrModels in the independent profile. Default preserves language routing. Explicit local selections must match supported source languages. The user rejected large local-service OCR models; Typhoon and its service configuration are removed. Do not add a large inference runtime or model without a new user request. Prefer lightweight in-process candidates and verify accuracy before offering a replacement.

- User-approved single build location: `src/OverTranslate/bin/Release/net8.0-windows10.0.26100.0/win-x64/OverTranslate.exe`; the desktop shortcut `OverTranslateTH` opens that executable directly; `Start-Thai.cmd` remains for source builds. The `OverTranslate-Thai-Dev` Windows startup entry must point to that latest executable. Keep its existing `profile-thai` in place.
- Before replacing/building that executable, check for running project copies. If any are running, ask the user to exit and wait for the process to end; never force-stop it or work around a file lock with alternate output directories or additional versions. Use Release for builds, tests and probes. Remove redundant generated build copies after verification instead of leaving multiple user launch choices.
