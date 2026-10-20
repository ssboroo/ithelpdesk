#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
[Setup]
AppId={{D6354DA8-4BB5-4F8A-9E94-7D3EE222D081}
AppName=IT Helpdesk Mongolia
AppVersion={#AppVersion}
AppPublisher=RAINY
DefaultDirName={localappdata}\Programs\RainyHelpdesk
DefaultGroupName=IT Helpdesk Mongolia
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\release
OutputBaseFilename=ITHelpdesk-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\ITHelpdesk.exe
CloseApplications=yes
[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked
[Files]
Source: "..\dist\ITHelpdesk\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{group}\IT Helpdesk Mongolia"; Filename: "{app}\ITHelpdesk.exe"
Name: "{autodesktop}\IT Helpdesk Mongolia"; Filename: "{app}\ITHelpdesk.exe"; Tasks: desktopicon
[Run]
Filename: "{app}\ITHelpdesk.exe"; Description: "Launch IT Helpdesk"; Flags: nowait postinstall skipifsilent
; User data lives outside {app}; upgrades and uninstall keep the database.
