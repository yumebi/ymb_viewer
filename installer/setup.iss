#define MyAppName "YMB イメージビューワー"
#define MyAppVersion "1.0.2"
#define MyAppPublisher "yumebi"
#define MyAppExeName "Hamana.Viewer.exe"
#define PublishDir "..\src\Hamana.Viewer\bin\Release\net8.0-windows\win-x64\publish"

[Setup]
AppId={{586A2C99-9CCE-429B-B7E3-E4D5BFFD3BD1}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=Output
OutputBaseFilename=YmbImageViewer-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=..\src\Hamana.Viewer\Assets\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
LicenseFile=..\LICENSE

[Languages]
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:DesktopIconDesc}"; GroupDescription: "{cm:AdditionalIconsGroup}"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[CustomMessages]
japanese.DesktopIconDesc=デスクトップにアイコンを作成する
japanese.AdditionalIconsGroup=追加のアイコン:

english.DesktopIconDesc=Create a desktop icon
english.AdditionalIconsGroup=Additional icons:
