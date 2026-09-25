; Inno Setup script for Zass. Requires Inno Setup 6.3+.
;
; Per-user install (no administrator rights): installs to %LOCALAPPDATA%\Programs\Zass,
; matching the app's per-user model (settings in %APPDATA%, autostart in HKCU).
;
; Build the app first, then compile this script:
;     pwsh -File build\publish.ps1          ; produces dist\app and passes the version
;     ISCC.exe installer\Zass.iss           ; or let publish.ps1 invoke ISCC for you
;
; Unsigned setups may trigger a SmartScreen warning.

#ifndef AppVersion
  #define AppVersion "2.0.1"
#endif

#define AppName "Zass"
#define AppPublisher "Zass"
#define AppExeName "Zass.exe"
#define AppUrl "https://github.com/tonipp77/zass"
#define SourceDir "..\dist\app"

[Setup]
AppId={{8B2E6C1A-9F4D-4E7B-A3C2-1D5F8A0B6E94}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\dist
OutputBaseFilename=Zass-Setup-{#AppVersion}-win-x64
SetupIconFile=..\Zass.App\Resources\zass.ico
UninstallDisplayIcon={app}\{#AppExeName}
WizardStyle=modern
Compression=lzma2/max
SolidCompression=yes

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "es"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Remove the optional "start with Windows" entry the app may have created (HKCU Run).
; '& exit /b 0' masks reg.exe's error when the value is absent so uninstall stays clean.
Filename: "{cmd}"; Parameters: "/c reg delete ""HKCU\Software\Microsoft\Windows\CurrentVersion\Run"" /v Zass /f & exit /b 0"; Flags: runhidden; RunOnceId: "RemoveZassAutostart"
