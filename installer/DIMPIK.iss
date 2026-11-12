#define AppVersion "1.0.6"
#ifdef InstallSmoke
  #define InstallId "{{DC29BD00-83DA-43E5-9AEC-C7C51837AB16}"
  #define RunValueName "DIMPIK-InstallSmoke"
#else
  #define InstallId "{{B1BF7543-3849-4CCB-B30D-9FB40127BC20}"
  #define RunValueName "DIMPIK"
#endif
[Setup]
AppId={#InstallId}
AppName=딤픽
AppVersion={#AppVersion}
UninstallDisplayName=딤픽 v{#AppVersion}
VersionInfoVersion={#AppVersion}
VersionInfoProductVersion={#AppVersion}
AppPublisher=AAStudio.kr
LicenseFile=..\LICENSE.txt
DefaultDirName={commonpf}\DIMPIK
UsePreviousAppDir=yes
DirExistsWarning=no
DefaultGroupName=딤픽
#ifdef InstallSmoke
PrivilegesRequired=lowest
#else
PrivilegesRequired=admin
#endif
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
OutputDir=..\release\setup
OutputBaseFilename=dimpik_inst_x64_{#AppVersion}
SetupIconFile=..\src\DIMPIK\Assets\DIMPIK.ico
UninstallDisplayIcon={app}\DIMPIK.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
AppMutex=Local\DIMPIK.NativeTray,Local\DIMPIK.Editor,Local\ASnapdo.NativeTray,Local\ASnapdo.Editor
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "바탕 화면 바로가기 만들기"; Flags: unchecked
Name: "startup"; Description: "Windows 로그인 시 자동 실행 (트레이 대기)"

[Messages]
korean.WizardLicense=MIT 오픈소스 라이선스
korean.LicenseLabel=개인 · 기업 · 관공서 모두 무료로 사용할 수 있습니다.
korean.LicenseLabel3=제작사: AAStudio.kr. 아래 MIT 라이선스를 확인한 뒤 동의해 주세요.
english.WizardLicense=MIT Open Source License
english.LicenseLabel=Free for individuals, businesses and public institutions.
english.LicenseLabel3=Publisher: AAStudio.kr. Please read and accept the MIT License below.

[Files]
Source: "..\LICENSE.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\release\app\DIMPIK.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\release\app\editor\*"; DestDir: "{app}\editor"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\딤픽"; Filename: "{app}\DIMPIK.exe"
Name: "{autodesktop}\딤픽"; Filename: "{app}\DIMPIK.exe"; Tasks: desktopicon

[Run]
#ifdef InstallSmoke
Filename: "{app}\DIMPIK.exe"; Parameters: "--enable-startup-test"; Tasks: startup; Flags: runasoriginaluser runhidden waituntilterminated
#else
Filename: "{app}\DIMPIK.exe"; Parameters: "--enable-startup"; Tasks: startup; Flags: runasoriginaluser runhidden waituntilterminated
#endif
Filename: "{app}\DIMPIK.exe"; Parameters: "--background"; Flags: nowait runasoriginaluser skipifsilent

[Code]
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'ASnapdo');
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', '{#RunValueName}');
end;
