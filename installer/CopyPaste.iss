#ifndef AppVersion
  #define AppVersion "1.3.1"
#endif
#ifndef SourceDir
  #define SourceDir "..\artifacts\CopyPaste-1.3.1-win-x64"
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts"
#endif

[Setup]
AppId={{6A8D54D7-71C7-4E75-B5F0-6EB630E81452}
AppName=CopyPaste
AppVersion={#AppVersion}
AppVerName=CopyPaste {#AppVersion}
AppPublisher=EddizEge
AppPublisherURL=https://github.com/EddizEge/CopyPaste
AppSupportURL=https://github.com/EddizEge/CopyPaste/issues
AppUpdatesURL=https://github.com/EddizEge/CopyPaste/releases/latest
SetupIconFile=..\src\CopyPaste.App\Assets\CopyPaste.ico
DefaultDirName={localappdata}\Programs\CopyPaste
DefaultGroupName=CopyPaste
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\CopyPaste.App.exe
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#OutputDir}
OutputBaseFilename=CopyPaste-{#AppVersion}-Setup
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
CloseApplicationsFilter=CopyPaste.App.exe
RestartApplications=no
ChangesAssociations=yes
VersionInfoVersion={#AppVersion}.0
VersionInfoCompany=EddizEge
VersionInfoDescription=CopyPaste Windows Installer
VersionInfoProductName=CopyPaste
VersionInfoProductVersion={#AppVersion}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "explorer"; Description: "CopyPaste Explorer sağ tık menülerini etkinleştir"; GroupDescription: "Windows entegrasyonu:"; Flags: checkedonce

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\CopyPaste"; Filename: "{app}\CopyPaste.App.exe"
Name: "{autodesktop}\CopyPaste"; Filename: "{app}\CopyPaste.App.exe"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Classes\Directory\shell\CopyPaste.Copy"; ValueType: string; ValueName: ""; ValueData: "CopyPaste ile kopyala"; Flags: uninsdeletekey; Tasks: explorer
Root: HKCU; Subkey: "Software\Classes\Directory\shell\CopyPaste.Copy"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\CopyPaste.App.exe"; Tasks: explorer
Root: HKCU; Subkey: "Software\Classes\Directory\shell\CopyPaste.Copy"; ValueType: string; ValueName: "Position"; ValueData: "Top"; Tasks: explorer
Root: HKCU; Subkey: "Software\Classes\Directory\shell\CopyPaste.Copy"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Player"; Tasks: explorer
Root: HKCU; Subkey: "Software\Classes\Directory\shell\CopyPaste.Copy\command"; ValueType: string; ValueName: ""; ValueData: """{app}\CopyPaste.App.exe"" --copy %*"; Tasks: explorer
Root: HKCU; Subkey: "Software\Classes\Directory\shell\CopyPaste.Paste"; ValueType: string; ValueName: ""; ValueData: "CopyPaste: Buraya yapıştır"; Flags: uninsdeletekey; Tasks: explorer
Root: HKCU; Subkey: "Software\Classes\Directory\shell\CopyPaste.Paste"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\CopyPaste.App.exe"; Tasks: explorer
Root: HKCU; Subkey: "Software\Classes\Directory\shell\CopyPaste.Paste"; ValueType: string; ValueName: "Position"; ValueData: "Top"; Tasks: explorer
Root: HKCU; Subkey: "Software\Classes\Directory\shell\CopyPaste.Paste\command"; ValueType: string; ValueName: ""; ValueData: """{app}\CopyPaste.App.exe"" --paste ""%1"""; Tasks: explorer
Root: HKCU; Subkey: "Software\Classes\Directory\Background\shell\CopyPaste.Paste"; ValueType: string; ValueName: ""; ValueData: "CopyPaste: Buraya yapıştır"; Flags: uninsdeletekey; Tasks: explorer
Root: HKCU; Subkey: "Software\Classes\Directory\Background\shell\CopyPaste.Paste"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\CopyPaste.App.exe"; Tasks: explorer
Root: HKCU; Subkey: "Software\Classes\Directory\Background\shell\CopyPaste.Paste"; ValueType: string; ValueName: "Position"; ValueData: "Top"; Tasks: explorer
Root: HKCU; Subkey: "Software\Classes\Directory\Background\shell\CopyPaste.Paste\command"; ValueType: string; ValueName: ""; ValueData: """{app}\CopyPaste.App.exe"" --paste ""%V"""; Tasks: explorer

[Run]
Filename: "{app}\CopyPaste.App.exe"; Description: "CopyPaste'i başlat"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{app}\CopyPaste.App.exe"; Parameters: "--uninstall-cleanup"; Flags: runhidden waituntilterminated skipifdoesntexist; RunOnceId: "CopyPasteScheduledTaskCleanup"
