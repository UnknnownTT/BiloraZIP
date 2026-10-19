; BiloraZIP installer context-menu integration Shell Fix 3.
; Fixes v0.9.3 upgrade issue: checkedonce automatically unchecks the old
; contextmenu task on upgrades. The newly named explorermenu task is checked
; by default, and Setup runs the already-verified per-user repair script.
; Additional v0.9.4 security and UI changes are included.
#define MyAppName "BiloraZIP"
#define MyAppVersion "1.0.0-rc1-shellui1"
#define MyAppExeName "BiloraZIP.exe"

[Setup]
AppId={{139644F5-1DBE-487B-A7D4-9542B88A0D84}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=BiloraZIP contributors
DefaultDirName={autopf}\BiloraZIP
DefaultGroupName=BiloraZIP
OutputDir=output
OutputBaseFilename=BiloraZIP-Setup-{#MyAppVersion}-win-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=..\src\BiloraZIP\BiloraZIP.ico
UninstallDisplayIcon={app}\BiloraZIP.exe
PrivilegesRequired=admin
UsePreviousAppDir=yes
CloseApplications=yes
RestartApplications=no
AppVerName=BiloraZIP 1.0 RC1 - Explorer background actions hotfix
ChangesAssociations=yes

[Files]
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\NOTICE"; DestDir: "{app}"; Flags: ignoreversion
; Bundle the verified per-user shell repair (run AFTER the application is installed).
Source: "..\Repair-BiloraZIP-ExplorerMenu.ps1"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\BiloraZIP"; Filename: "{app}\BiloraZIP.exe"; Tasks: startmenu
Name: "{autodesktop}\BiloraZIP"; Filename: "{app}\BiloraZIP.exe"; Tasks: desktopicon

[Tasks]
Name: "startmenu"; Description: "Create Start Menu shortcut"; Flags: checkedonce
Name: "desktopicon"; Description: "Create desktop shortcut"; Flags: unchecked
Name: "explorermenu"; Description: "Explorer right-click commands (ZIP/BILZ creation and archive extraction)"
Name: "assoc"; Description: "Offer BiloraZIP as an archive opener"; Flags: unchecked
Name: "assoc\zip"; Description: "ZIP (.zip)"; Flags: checkedonce
Name: "assoc\rar"; Description: "RAR (.rar)"; Flags: checkedonce
Name: "assoc\7z"; Description: "7Z (.7z)"; Flags: checkedonce
Name: "assoc\tar"; Description: "TAR (.tar)"; Flags: checkedonce
Name: "assoc\zipx"; Description: "ZIPX (.zipx)"; Flags: unchecked
Name: "assoc\xapk"; Description: "XAPK (.xapk)"; Flags: unchecked
Name: "assoc\bilz"; Description: "BILZ (.bilz)"; Flags: checkedonce

[Registry]
; Classic file, folder and mixed-selection compression verbs. Document model is merged by app hub.
Root: HKLM; Subkey: "Software\Classes\*\shell\BiloraZIP.Compress"; ValueType: string; ValueData: "Compress with BiloraZIP (ZIP / BILZ)..."; Flags: uninsdeletekey; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\*\shell\BiloraZIP.Compress"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Document"; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\*\shell\BiloraZIP.Compress"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\BiloraZIP.exe,0"; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\*\shell\BiloraZIP.Compress\command"; ValueType: string; ValueData: """{app}\BiloraZIP.exe"" --compress ""%1"""; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\Directory\shell\BiloraZIP.Compress"; ValueType: string; ValueData: "Compress with BiloraZIP (ZIP / BILZ)..."; Flags: uninsdeletekey; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\Directory\shell\BiloraZIP.Compress"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Document"; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\Directory\shell\BiloraZIP.Compress"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\BiloraZIP.exe,0"; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\Directory\shell\BiloraZIP.Compress\command"; ValueType: string; ValueData: """{app}\BiloraZIP.exe"" --compress ""%1"""; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\AllFilesystemObjects\shell\BiloraZIP.Compress"; ValueType: string; ValueData: "Compress with BiloraZIP (ZIP / BILZ)..."; Flags: uninsdeletekey; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\AllFilesystemObjects\shell\BiloraZIP.Compress"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Document"; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\AllFilesystemObjects\shell\BiloraZIP.Compress"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\BiloraZIP.exe,0"; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\AllFilesystemObjects\shell\BiloraZIP.Compress\command"; ValueType: string; ValueData: """{app}\BiloraZIP.exe"" --compress ""%1"""; Tasks: explorermenu
; File types are registered as a possible default, never forced.
Root: HKLM; Subkey: "Software\BiloraZIP\Capabilities"; ValueType: string; ValueName: "ApplicationName"; ValueData: "BiloraZIP"; Flags: uninsdeletevalue
Root: HKLM; Subkey: "Software\BiloraZIP\Capabilities"; ValueType: string; ValueName: "ApplicationDescription"; ValueData: "Open ZIP, RAR and 7Z archives"; Flags: uninsdeletevalue
Root: HKLM; Subkey: "Software\RegisteredApplications"; ValueType: string; ValueName: "BiloraZIP"; ValueData: "Software\BiloraZIP\Capabilities"; Flags: uninsdeletevalue
Root: HKLM; Subkey: "Software\Classes\BiloraZIP.Archive"; ValueType: string; ValueData: "BiloraZIP archive"; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\Classes\BiloraZIP.Archive\DefaultIcon"; ValueType: string; ValueData: "{app}\BiloraZIP.exe,0"
Root: HKLM; Subkey: "Software\Classes\BiloraZIP.Archive\shell\open\command"; ValueType: string; ValueData: """{app}\BiloraZIP.exe"" --open ""%1"""
Root: HKLM; Subkey: "Software\Classes\.zip\OpenWithProgids"; ValueType: none; ValueName: "BiloraZIP.Archive"; Flags: uninsdeletevalue; Tasks: assoc\zip
Root: HKLM; Subkey: "Software\BiloraZIP\Capabilities\FileAssociations"; ValueType: string; ValueName: ".zip"; ValueData: "BiloraZIP.Archive"; Flags: uninsdeletevalue; Tasks: assoc\zip
; Classic cascading context-menu item; Windows 11 normally shows it in "Show more options".
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.zip\shell\BiloraZIP"; ValueType: string; ValueName: "MUIVerb"; ValueData: "BiloraZIP"; Flags: uninsdeletekey; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.zip\shell\BiloraZIP"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\BiloraZIP.exe,0"; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.zip\shell\BiloraZIP"; ValueType: string; ValueName: "SubCommands"; ValueData: ""; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.zip\shell\BiloraZIP\shell\Open"; ValueType: string; ValueData: "Open with BiloraZIP"; Flags: uninsdeletekey; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.zip\shell\BiloraZIP\shell\Open\command"; ValueType: string; ValueData: """{app}\BiloraZIP.exe"" --open ""%1"""; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.zip\shell\BiloraZIP\shell\ExtractTo"; ValueType: string; ValueData: "Extract files..."; Flags: uninsdeletekey; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.zip\shell\BiloraZIP\shell\ExtractTo\command"; ValueType: string; ValueData: """{app}\BiloraZIP.exe"" --extract-to ""%1"""; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.zip\shell\BiloraZIP\shell\ExtractHere"; ValueType: string; ValueData: "Extract Here"; Flags: uninsdeletekey; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.zip\shell\BiloraZIP\shell\ExtractHere\command"; ValueType: string; ValueData: """{app}\BiloraZIP.exe"" --extract-here ""%1"""; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.zip\shell\BiloraZIP\shell\ExtractFolder"; ValueType: string; ValueData: "Extract to named folder"; Flags: uninsdeletekey; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.zip\shell\BiloraZIP\shell\ExtractFolder\command"; ValueType: string; ValueData: """{app}\BiloraZIP.exe"" --extract-subfolder ""%1"""; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\.rar\OpenWithProgids"; ValueType: none; ValueName: "BiloraZIP.Archive"; Flags: uninsdeletevalue; Tasks: assoc\rar
Root: HKLM; Subkey: "Software\BiloraZIP\Capabilities\FileAssociations"; ValueType: string; ValueName: ".rar"; ValueData: "BiloraZIP.Archive"; Flags: uninsdeletevalue; Tasks: assoc\rar
; Classic cascading context-menu item; Windows 11 normally shows it in "Show more options".
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.rar\shell\BiloraZIP"; ValueType: string; ValueName: "MUIVerb"; ValueData: "BiloraZIP"; Flags: uninsdeletekey; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.rar\shell\BiloraZIP"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\BiloraZIP.exe,0"; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.rar\shell\BiloraZIP"; ValueType: string; ValueName: "SubCommands"; ValueData: ""; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.rar\shell\BiloraZIP\shell\Open"; ValueType: string; ValueData: "Open with BiloraZIP"; Flags: uninsdeletekey; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.rar\shell\BiloraZIP\shell\Open\command"; ValueType: string; ValueData: """{app}\BiloraZIP.exe"" --open ""%1"""; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.rar\shell\BiloraZIP\shell\ExtractTo"; ValueType: string; ValueData: "Extract files..."; Flags: uninsdeletekey; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.rar\shell\BiloraZIP\shell\ExtractTo\command"; ValueType: string; ValueData: """{app}\BiloraZIP.exe"" --extract-to ""%1"""; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.rar\shell\BiloraZIP\shell\ExtractHere"; ValueType: string; ValueData: "Extract Here"; Flags: uninsdeletekey; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.rar\shell\BiloraZIP\shell\ExtractHere\command"; ValueType: string; ValueData: """{app}\BiloraZIP.exe"" --extract-here ""%1"""; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.rar\shell\BiloraZIP\shell\ExtractFolder"; ValueType: string; ValueData: "Extract to named folder"; Flags: uninsdeletekey; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.rar\shell\BiloraZIP\shell\ExtractFolder\command"; ValueType: string; ValueData: """{app}\BiloraZIP.exe"" --extract-subfolder ""%1"""; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\.7z\OpenWithProgids"; ValueType: none; ValueName: "BiloraZIP.Archive"; Flags: uninsdeletevalue; Tasks: assoc\7z
Root: HKLM; Subkey: "Software\BiloraZIP\Capabilities\FileAssociations"; ValueType: string; ValueName: ".7z"; ValueData: "BiloraZIP.Archive"; Flags: uninsdeletevalue; Tasks: assoc\7z
; Classic cascading context-menu item; Windows 11 normally shows it in "Show more options".
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.7z\shell\BiloraZIP"; ValueType: string; ValueName: "MUIVerb"; ValueData: "BiloraZIP"; Flags: uninsdeletekey; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.7z\shell\BiloraZIP"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\BiloraZIP.exe,0"; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.7z\shell\BiloraZIP"; ValueType: string; ValueName: "SubCommands"; ValueData: ""; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.7z\shell\BiloraZIP\shell\Open"; ValueType: string; ValueData: "Open with BiloraZIP"; Flags: uninsdeletekey; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.7z\shell\BiloraZIP\shell\Open\command"; ValueType: string; ValueData: """{app}\BiloraZIP.exe"" --open ""%1"""; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.7z\shell\BiloraZIP\shell\ExtractTo"; ValueType: string; ValueData: "Extract files..."; Flags: uninsdeletekey; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.7z\shell\BiloraZIP\shell\ExtractTo\command"; ValueType: string; ValueData: """{app}\BiloraZIP.exe"" --extract-to ""%1"""; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.7z\shell\BiloraZIP\shell\ExtractHere"; ValueType: string; ValueData: "Extract Here"; Flags: uninsdeletekey; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.7z\shell\BiloraZIP\shell\ExtractHere\command"; ValueType: string; ValueData: """{app}\BiloraZIP.exe"" --extract-here ""%1"""; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.7z\shell\BiloraZIP\shell\ExtractFolder"; ValueType: string; ValueData: "Extract to named folder"; Flags: uninsdeletekey; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.7z\shell\BiloraZIP\shell\ExtractFolder\command"; ValueType: string; ValueData: """{app}\BiloraZIP.exe"" --extract-subfolder ""%1"""; Tasks: explorermenu

Root: HKLM; Subkey: "Software\Classes\.tar\OpenWithProgids"; ValueType: none; ValueName: "BiloraZIP.Archive"; Flags: uninsdeletevalue; Tasks: assoc\tar
Root: HKLM; Subkey: "Software\BiloraZIP\Capabilities\FileAssociations"; ValueType: string; ValueName: ".tar"; ValueData: "BiloraZIP.Archive"; Flags: uninsdeletevalue; Tasks: assoc\tar
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.tar\shell\BiloraZIP"; ValueType: string; ValueName: "MUIVerb"; ValueData: "BiloraZIP"; Flags: uninsdeletekey; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.tar\shell\BiloraZIP"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\BiloraZIP.exe,0"; Flags: uninsdeletekey; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.tar\shell\BiloraZIP"; ValueType: string; ValueName: "SubCommands"; ValueData: ""; Flags: uninsdeletekey; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.tar\shell\BiloraZIP\shell\Open"; ValueType: string; ValueData: "Open with BiloraZIP"; Flags: uninsdeletekey; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.tar\shell\BiloraZIP\shell\Open\command"; ValueType: string; ValueData: """{app}\BiloraZIP.exe"" --open ""%1"""; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.tar\shell\BiloraZIP\shell\ExtractTo"; ValueType: string; ValueData: "Extract files..."; Flags: uninsdeletekey; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.tar\shell\BiloraZIP\shell\ExtractTo\command"; ValueType: string; ValueData: """{app}\BiloraZIP.exe"" --extract-to ""%1"""; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.tar\shell\BiloraZIP\shell\ExtractHere"; ValueType: string; ValueData: "Extract Here"; Flags: uninsdeletekey; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.tar\shell\BiloraZIP\shell\ExtractHere\command"; ValueType: string; ValueData: """{app}\BiloraZIP.exe"" --extract-here ""%1"""; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.tar\shell\BiloraZIP\shell\ExtractFolder"; ValueType: string; ValueData: "Extract to named folder"; Flags: uninsdeletekey; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.tar\shell\BiloraZIP\shell\ExtractFolder\command"; ValueType: string; ValueData: """{app}\BiloraZIP.exe"" --extract-subfolder ""%1"""; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\.zipx\OpenWithProgids"; ValueType: none; ValueName: "BiloraZIP.Archive"; Flags: uninsdeletevalue; Tasks: assoc\zipx
Root: HKLM; Subkey: "Software\BiloraZIP\Capabilities\FileAssociations"; ValueType: string; ValueName: ".zipx"; ValueData: "BiloraZIP.Archive"; Flags: uninsdeletevalue; Tasks: assoc\zipx
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.zipx\shell\BiloraZIP"; ValueType: string; ValueName: "MUIVerb"; ValueData: "BiloraZIP"; Flags: uninsdeletekey; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.zipx\shell\BiloraZIP"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\BiloraZIP.exe,0"; Flags: uninsdeletekey; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.zipx\shell\BiloraZIP"; ValueType: string; ValueName: "SubCommands"; ValueData: ""; Flags: uninsdeletekey; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.zipx\shell\BiloraZIP\shell\Open"; ValueType: string; ValueData: "Open with BiloraZIP"; Flags: uninsdeletekey; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.zipx\shell\BiloraZIP\shell\Open\command"; ValueType: string; ValueData: """{app}\BiloraZIP.exe"" --open ""%1"""; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.zipx\shell\BiloraZIP\shell\ExtractTo"; ValueType: string; ValueData: "Extract files..."; Flags: uninsdeletekey; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.zipx\shell\BiloraZIP\shell\ExtractTo\command"; ValueType: string; ValueData: """{app}\BiloraZIP.exe"" --extract-to ""%1"""; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.zipx\shell\BiloraZIP\shell\ExtractHere"; ValueType: string; ValueData: "Extract Here"; Flags: uninsdeletekey; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.zipx\shell\BiloraZIP\shell\ExtractHere\command"; ValueType: string; ValueData: """{app}\BiloraZIP.exe"" --extract-here ""%1"""; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.zipx\shell\BiloraZIP\shell\ExtractFolder"; ValueType: string; ValueData: "Extract to named folder"; Flags: uninsdeletekey; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.zipx\shell\BiloraZIP\shell\ExtractFolder\command"; ValueType: string; ValueData: """{app}\BiloraZIP.exe"" --extract-subfolder ""%1"""; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\.xapk\OpenWithProgids"; ValueType: none; ValueName: "BiloraZIP.Archive"; Flags: uninsdeletevalue; Tasks: assoc\xapk
Root: HKLM; Subkey: "Software\BiloraZIP\Capabilities\FileAssociations"; ValueType: string; ValueName: ".xapk"; ValueData: "BiloraZIP.Archive"; Flags: uninsdeletevalue; Tasks: assoc\xapk
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.xapk\shell\BiloraZIP"; ValueType: string; ValueName: "MUIVerb"; ValueData: "BiloraZIP"; Flags: uninsdeletekey; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.xapk\shell\BiloraZIP"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\BiloraZIP.exe,0"; Flags: uninsdeletekey; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.xapk\shell\BiloraZIP"; ValueType: string; ValueName: "SubCommands"; ValueData: ""; Flags: uninsdeletekey; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.xapk\shell\BiloraZIP\shell\Open"; ValueType: string; ValueData: "Open with BiloraZIP"; Flags: uninsdeletekey; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.xapk\shell\BiloraZIP\shell\Open\command"; ValueType: string; ValueData: """{app}\BiloraZIP.exe"" --open ""%1"""; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.xapk\shell\BiloraZIP\shell\ExtractTo"; ValueType: string; ValueData: "Extract files..."; Flags: uninsdeletekey; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.xapk\shell\BiloraZIP\shell\ExtractTo\command"; ValueType: string; ValueData: """{app}\BiloraZIP.exe"" --extract-to ""%1"""; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.xapk\shell\BiloraZIP\shell\ExtractHere"; ValueType: string; ValueData: "Extract Here"; Flags: uninsdeletekey; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.xapk\shell\BiloraZIP\shell\ExtractHere\command"; ValueType: string; ValueData: """{app}\BiloraZIP.exe"" --extract-here ""%1"""; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.xapk\shell\BiloraZIP\shell\ExtractFolder"; ValueType: string; ValueData: "Extract to named folder"; Flags: uninsdeletekey; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.xapk\shell\BiloraZIP\shell\ExtractFolder\command"; ValueType: string; ValueData: """{app}\BiloraZIP.exe"" --extract-subfolder ""%1"""; Tasks: explorermenu

; Experimental BILZ association and legacy extraction menu.
Root: HKLM; Subkey: "Software\Classes\.bilz\OpenWithProgids"; ValueType: none; ValueName: "BiloraZIP.Archive"; Flags: uninsdeletevalue; Tasks: assoc\bilz
Root: HKLM; Subkey: "Software\BiloraZIP\Capabilities\FileAssociations"; ValueType: string; ValueName: ".bilz"; ValueData: "BiloraZIP.Archive"; Flags: uninsdeletevalue; Tasks: assoc\bilz
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.bilz\shell\BiloraZIP"; ValueType: string; ValueName: "MUIVerb"; ValueData: "BiloraZIP"; Flags: uninsdeletekey; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.bilz\shell\BiloraZIP"; ValueType: string; ValueName: "SubCommands"; ValueData: ""; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.bilz\shell\BiloraZIP\shell\Open"; ValueType: string; ValueData: "Open with BiloraZIP"; Flags: uninsdeletekey; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.bilz\shell\BiloraZIP\shell\Open\command"; ValueType: string; ValueData: """{app}\BiloraZIP.exe"" --open ""%1"""; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.bilz\shell\BiloraZIP\shell\ExtractHere"; ValueType: string; ValueData: "Extract Here"; Flags: uninsdeletekey; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.bilz\shell\BiloraZIP\shell\ExtractHere\command"; ValueType: string; ValueData: """{app}\BiloraZIP.exe"" --extract-here ""%1"""; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.bilz\shell\BiloraZIP\shell\ExtractFolder"; ValueType: string; ValueData: "Extract to named folder"; Flags: uninsdeletekey; Tasks: explorermenu
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.bilz\shell\BiloraZIP\shell\ExtractFolder\command"; ValueType: string; ValueData: """{app}\BiloraZIP.exe"" --extract-subfolder ""%1"""; Tasks: explorermenu

[Run]
; At this point {app}\BiloraZIP.exe exists. Execute the VERIFIED working
; repair for the user who started Setup (not the elevated installer account).
; See Inno Setup runasoriginaluser documentation for admin-launch limitations.
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File ""{app}\Repair-BiloraZIP-ExplorerMenu.ps1"" -ExePath ""{app}\BiloraZIP.exe"""; Flags: runhidden runasoriginaluser; Tasks: explorermenu; StatusMsg: "Registering BiloraZIP Explorer menus for current user..."
Filename: "{app}\BiloraZIP.exe"; Description: "Launch BiloraZIP"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Remove only BiloraZIP's HKCU menu commands and SendTo shortcut for
; the account running uninstall. A different elevated account cannot reliably
; clean the original user's HKCU; -Remove is available for that user.
; Inno Setup DOES NOT support runasoriginaluser for [UninstallRun].
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File ""{app}\Repair-BiloraZIP-ExplorerMenu.ps1"" -Remove"; Flags: runhidden; RunOnceId: "RemoveBiloraUserShellMenus"

[Code]
// Remove BiloraZIP's previous MACHINE registrations during in-place upgrades so
// choices deselected in this installer don't remain enabled from an older version.
// Never deletes another application's ProgId or Windows UserChoice defaults.
procedure CurStepChanged(CurStep: TSetupStep);
var
  Extensions: array[0..6] of String;
  I: Integer;
  Ext: String;
begin
  if CurStep <> ssInstall then Exit;
  Extensions[0] := '.zip';
  Extensions[1] := '.rar';
  Extensions[2] := '.7z';
  Extensions[3] := '.tar';
  Extensions[4] := '.zipx';
  Extensions[5] := '.xapk';
  Extensions[6] := '.bilz';
  for I := 0 to 6 do
  begin
    Ext := Extensions[I];
    RegDeleteValue(HKLM, 'Software\BiloraZIP\Capabilities\FileAssociations', Ext);
    RegDeleteValue(HKLM, 'Software\Classes\' + Ext + '\OpenWithProgids', 'BiloraZIP.Archive');
    RegDeleteKeyIncludingSubkeys(HKLM, 'Software\Classes\SystemFileAssociations\' + Ext + '\shell\BiloraZIP');
  end;
  RegDeleteKeyIncludingSubkeys(HKLM, 'Software\Classes\*\shell\BiloraZIP.Compress');
  RegDeleteKeyIncludingSubkeys(HKLM, 'Software\Classes\Directory\shell\BiloraZIP.Compress');
  RegDeleteKeyIncludingSubkeys(HKLM, 'Software\Classes\AllFilesystemObjects\shell\BiloraZIP.Compress');
end;
