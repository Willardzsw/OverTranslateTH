; TextTack installer. Built by tools\release\build-release.ps1.
#ifndef AppVersion
  #error Pass /DAppVersion
#endif
#ifndef SourceDir
  #error Pass /DSourceDir
#endif

[Setup]
AppId={{6F3B8E52-7C1D-4A96-9E0B-2D41C7A58F13}
AppName=TextTack
AppVersion={#AppVersion}
AppVerName=TextTack {#AppVersion}
AppPublisher=Willardzsw
AppPublisherURL=https://github.com/Willardzsw/TextTack
AppSupportURL=https://github.com/Willardzsw/TextTack/issues
; Per user: settings are written beside the executable, which Program Files would refuse.
PrivilegesRequired=lowest
DefaultDirName={localappdata}\Programs\TextTack
DisableProgramGroupPage=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputBaseFilename=TextTack-{#AppVersion}-win-x64-Setup
SetupIconFile=..\..\src\OverTranslate\icons\app.ico
UninstallDisplayIcon={app}\TextTack.exe
UninstallDisplayName=TextTack
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
; Upgrading from TT Tools (1.5.x): the program was TTTools.exe and its shortcuts were named TT Tools.
Type: files; Name: "{app}\TTTools.exe"
Type: files; Name: "{app}\TTTools.dll"
Type: files; Name: "{app}\TTTools.deps.json"
Type: files; Name: "{app}\TTTools.runtimeconfig.json"
Type: files; Name: "{app}\TTTools.pdb"
Type: files; Name: "{autoprograms}\TT Tools.lnk"
Type: files; Name: "{autodesktop}\TT Tools.lnk"

[Icons]
Name: "{autoprograms}\TextTack"; Filename: "{app}\TextTack.exe"; AppUserModelID: "Willardzsw.TextTack"
Name: "{autodesktop}\TextTack"; Filename: "{app}\TextTack.exe"; AppUserModelID: "Willardzsw.TextTack"; Tasks: desktopicon

[Run]
Filename: "{app}\TextTack.exe"; Description: "{cm:LaunchProgram,TextTack}"; Flags: nowait postinstall skipifsilent

[Code]
const
  RunKey = 'Software\Microsoft\Windows\CurrentVersion\Run';
  RunValue = 'TextTack';
  AumidKey = 'Software\Classes\AppUserModelId\Willardzsw.TextTack';
  LegacyAumidKey = 'Software\Classes\AppUserModelId\Willardzsw.TTTools';

// Only entries that point into this install: another copy that ran last owns them.
function PointsIntoApp(const Value: String): Boolean;
begin
  Result := Pos(Lowercase(AddBackslash(ExpandConstant('{app}'))), Lowercase(Value)) > 0;
end;

// Moves a startup entry from before a rename, which pointed at an executable that is gone now.
procedure MigrateRunValue(const LegacyName: String);
var
  Value: String;
begin
  if RegQueryStringValue(HKCU, RunKey, LegacyName, Value) and PointsIntoApp(Value) then
  begin
    RegDeleteValue(HKCU, RunKey, LegacyName);
    RegWriteStringValue(HKCU, RunKey, RunValue, '"' + ExpandConstant('{app}\TextTack.exe') + '"');
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  Value: String;
begin
  if CurStep = ssPostInstall then
  begin
    MigrateRunValue('TTTools');
    MigrateRunValue('OverTranslate-Thai-Dev');
    if RegQueryStringValue(HKCU, LegacyAumidKey, 'IconUri', Value) and PointsIntoApp(Value) then
      RegDeleteKeyIncludingSubkeys(HKCU, LegacyAumidKey);
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
