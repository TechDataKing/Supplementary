#define MyAppName "TechKingPOS"
#define MyAppVersion "1.9.3"
#define MyAppPublisher "TechDataKing"
#define MyAppExeName "TechKingPOS.App.exe"

[Setup]
AppId={{TechKingPOS}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={commonpf}\TechKingPOS
DefaultGroupName=TechKingPOS
DisableProgramGroupPage=yes
OutputDir=Installer
OutputBaseFilename=TechKingPOS_{#MyAppVersion}
Compression=lzma
SolidCompression=yes
WizardStyle=modern
SetupIconFile=Assets\TechKingPOS.ico
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "bin\Release\net8.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{group}\TechKingPOS"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\TechKingPOS"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop icon"; Flags: unchecked

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch TechKingPOS"; Flags: nowait postinstall skipifsilent
