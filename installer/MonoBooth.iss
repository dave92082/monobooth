; Inno Setup script for MonoBooth.
; Build locally with:  ISCC.exe installer\MonoBooth.iss
; CI overrides version/source:  ISCC.exe installer\MonoBooth.iss /DMyAppVersion=2.0.0

#define MyAppName "MonoBooth"
#ifndef MyAppVersion
  #define MyAppVersion "2.0.0"
#endif
#define MyAppPublisher "dave92082"
#define MyAppURL "https://github.com/dave92082/monobooth"
#define MyAppExeName "MonoBooth.exe"

; Self-contained publish output (no .NET install required on the target machine).
#ifndef SourceDir
  #define SourceDir "..\src\MonoBooth\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish"
#endif

[Setup]
; A stable AppId keeps upgrades/uninstalls tied to the same product.
AppId={{B9F3A2E1-4C7D-4E8A-9B12-7F3C5D6A8E20}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=Output
OutputBaseFilename=MonoBooth-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
