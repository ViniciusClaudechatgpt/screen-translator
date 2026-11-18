#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

[Setup]
AppId={{B4E5A0C2-6C1D-4B7F-9D43-5E2A1F7C9A10}
AppName=Screen Translator
AppVersion={#AppVersion}
AppPublisher=Screen Translator
DefaultDirName={autopf}\Screen Translator
DefaultGroupName=Screen Translator
OutputDir=Output
OutputBaseFilename=Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
PrivilegesRequired=lowest
UninstallDisplayIcon={app}\ScreenTranslator.exe
CloseApplications=yes

[Languages]
Name: "ptbr"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na Área de Trabalho"; Flags: unchecked
Name: "startup"; Description: "Iniciar com o Windows"; Flags: unchecked

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Screen Translator"; Filename: "{app}\ScreenTranslator.exe"
Name: "{autodesktop}\Screen Translator"; Filename: "{app}\ScreenTranslator.exe"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "ScreenTranslator"; ValueData: """{app}\ScreenTranslator.exe"""; Flags: uninsdeletevalue; Tasks: startup

[Run]
Filename: "{app}\ScreenTranslator.exe"; Description: "Abrir Screen Translator"; Flags: nowait postinstall skipifsilent
