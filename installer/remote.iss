#ifndef AppVersion
  #define AppVersion "2.0.0"
#endif
[Setup]
; Same AppId upgrades the previous app in-place, without touching user databases.
AppId={{D6354DA8-4BB5-4F8A-9E94-7D3EE222D081}
AppName=RAINY Remote
AppVersion={#AppVersion}
AppPublisher=RAINY
DefaultDirName={localappdata}\Programs\RainyRemote
DefaultGroupName=RAINY Remote
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\release
OutputBaseFilename=RainyRemote-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\RainyRemote.exe
CloseApplications=yes
[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked
[InstallDelete]
Type: files; Name: "{app}\ITHelpdesk.exe"
Type: files; Name: "{userdesktop}\IT Helpdesk Mongolia.lnk"
Type: files; Name: "{userprograms}\IT Helpdesk Mongolia\IT Helpdesk Mongolia.lnk"
[Files]
Source: "..\dist\RainyRemote\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\THIRD_PARTY.md"; DestDir: "{app}"; Flags: ignoreversion
[Icons]
Name: "{group}\RAINY Remote"; Filename: "{app}\RainyRemote.exe"
Name: "{autodesktop}\RAINY Remote"; Filename: "{app}\RainyRemote.exe"; Tasks: desktopicon
[Run]
Filename: "{app}\RainyRemote.exe"; Description: "Launch RAINY Remote"; Flags: nowait postinstall skipifsilent
