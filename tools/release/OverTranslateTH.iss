; OverTranslateTH installer. Built by tools\release\build-release.ps1.
#ifndef AppVersion
  #error Pass /DAppVersion
#endif
#ifndef SourceDir
  #error Pass /DSourceDir
#endif

[Setup]
AppId={{6F3B8E52-7C1D-4A96-9E0B-2D41C7A58F13}
AppName=OverTranslateTH
AppVersion={#AppVersion}
AppVerName=OverTranslateTH {#AppVersion}
AppPublisher=Willardzsw
AppPublisherURL=https://github.com/Willardzsw/OverTranslateTH
AppSupportURL=https://github.com/Willardzsw/OverTranslateTH/issues
; Per user: settings are written beside the executable, which Program Files would refuse.
PrivilegesRequired=lowest
DefaultDirName={localappdata}\Programs\OverTranslateTH
DisableProgramGroupPage=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputBaseFilename=OverTranslateTH-{#AppVersion}-win-x64-Setup
SetupIconFile=..\..\src\OverTranslate\icons\app.ico
UninstallDisplayIcon={app}\OverTranslate.exe
UninstallDisplayName=OverTranslateTH
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

[Icons]
Name: "{autoprograms}\OverTranslateTH"; Filename: "{app}\OverTranslate.exe"; AppUserModelID: "Willardzsw.OverTranslateTH"
Name: "{autodesktop}\OverTranslateTH"; Filename: "{app}\OverTranslate.exe"; AppUserModelID: "Willardzsw.OverTranslateTH"; Tasks: desktopicon

[Run]
Filename: "{app}\OverTranslate.exe"; Description: "{cm:LaunchProgram,OverTranslateTH}"; Flags: nowait postinstall skipifsilent

[Code]
const
  RunKey = 'Software\Microsoft\Windows\CurrentVersion\Run';
  RunValue = 'OverTranslate-Thai-Dev';
  AumidKey = 'Software\Classes\AppUserModelId\Willardzsw.OverTranslateTH';

// Only entries that point into this install: another copy that ran last owns them.
function PointsIntoApp(const Value: String): Boolean;
begin
  Result := Pos(Lowercase(AddBackslash(ExpandConstant('{app}'))), Lowercase(Value)) > 0;
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
