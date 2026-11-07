; TT Tools installer. Built by tools\release\build-release.ps1.
#ifndef AppVersion
  #error Pass /DAppVersion
#endif
#ifndef SourceDir
  #error Pass /DSourceDir
#endif

[Setup]
AppId={{6F3B8E52-7C1D-4A96-9E0B-2D41C7A58F13}
AppName=TT Tools
AppVersion={#AppVersion}
AppVerName=TT Tools {#AppVersion}
AppPublisher=Willardzsw
AppPublisherURL=https://github.com/Willardzsw/OverTranslateTH
AppSupportURL=https://github.com/Willardzsw/OverTranslateTH/issues
; Per user: settings are written beside the executable, which Program Files would refuse.
PrivilegesRequired=lowest
DefaultDirName={localappdata}\Programs\TTTools
DisableProgramGroupPage=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputBaseFilename=TTTools-{#AppVersion}-win-x64-Setup
SetupIconFile=..\..\src\OverTranslate\icons\app.ico
UninstallDisplayIcon={app}\TTTools.exe
UninstallDisplayName=TT Tools
LicenseFile=..\..\LICENSE
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
chinesesimplified.KeptProfile=配置和 API 密钥保留在下面的文件夹中，方便重新安装后继续使用。如果不再需要，请手动删除：
english.KeptProfile=Your settings and API keys were kept so a reinstall can use them. Delete this folder yourself if you no longer need it:

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

; Upgrading from OverTranslateTH (1.2.0 and earlier): the program was OverTranslate.exe and its
; shortcuts were named OverTranslateTH. Same AppId, so the same folder; take the old program and
; shortcuts away so nothing starts the old version. OverTranslate.Translation.dll is still used.
[InstallDelete]
Type: files; Name: "{app}\OverTranslate.exe"
Type: files; Name: "{app}\OverTranslate.dll"
Type: files; Name: "{app}\OverTranslate.deps.json"
Type: files; Name: "{app}\OverTranslate.runtimeconfig.json"
Type: files; Name: "{app}\OverTranslate.pdb"
Type: files; Name: "{autoprograms}\OverTranslateTH.lnk"
Type: files; Name: "{autodesktop}\OverTranslateTH.lnk"

[Icons]
Name: "{autoprograms}\TT Tools"; Filename: "{app}\TTTools.exe"; AppUserModelID: "Willardzsw.TTTools"
Name: "{autodesktop}\TT Tools"; Filename: "{app}\TTTools.exe"; AppUserModelID: "Willardzsw.TTTools"; Tasks: desktopicon

[Run]
Filename: "{app}\TTTools.exe"; Description: "{cm:LaunchProgram,TT Tools}"; Flags: nowait postinstall skipifsilent

[Code]
const
  RunKey = 'Software\Microsoft\Windows\CurrentVersion\Run';
  RunValue = 'TTTools';
  AumidKey = 'Software\Classes\AppUserModelId\Willardzsw.TTTools';

  LegacyRunValue = 'OverTranslate-Thai-Dev';

// Only entries that point into this install: another copy that ran last owns them.
function PointsIntoApp(const Value: String): Boolean;
begin
  Result := Pos(Lowercase(AddBackslash(ExpandConstant('{app}'))), Lowercase(Value)) > 0;
end;

// The startup entry from before the rename pointed at OverTranslate.exe, which is gone now.
procedure CurStepChanged(CurStep: TSetupStep);
var
  Value: String;
begin
  if (CurStep = ssPostInstall)
    and RegQueryStringValue(HKCU, RunKey, LegacyRunValue, Value) and PointsIntoApp(Value) then
  begin
    RegDeleteValue(HKCU, RunKey, LegacyRunValue);
    RegWriteStringValue(HKCU, RunKey, RunValue, '"' + ExpandConstant('{app}\TTTools.exe') + '"');
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Value: String;
begin
  if CurUninstallStep = usUninstall then
  begin
    if RegQueryStringValue(HKCU, RunKey, RunValue, Value) and PointsIntoApp(Value) then
      RegDeleteValue(HKCU, RunKey, RunValue);
    if RegQueryStringValue(HKCU, AumidKey, 'IconUri', Value) and PointsIntoApp(Value) then
      RegDeleteKeyIncludingSubkeys(HKCU, AumidKey);
  end;
  if (CurUninstallStep = usPostUninstall) and (not UninstallSilent)
    and DirExists(ExpandConstant('{app}\profile-thai')) then
    MsgBox(CustomMessage('KeptProfile') + #13#10#13#10 + ExpandConstant('{app}\profile-thai'), mbInformation, MB_OK);
end;
