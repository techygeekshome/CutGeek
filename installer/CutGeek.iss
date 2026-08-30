; CutGeek installer
;
; Same shape as installer\CleanGeek.iss and installer\PDFGeek.iss. Two things here are
; decisions rather than defaults, and both are explained where they appear: PrivilegesRequired,
; and what the uninstaller does and does not remove.
;
; Build it locally with:  build.cmd installer
; CI builds it in .github\workflows\release.yml.

#define AppName        "CutGeek"
#define AppSourceDir   "..\publish\app"
#define AppExeName     "CutGeek.exe"
#define AppPublisher   "TechyGeeksHome"
#define AppURL         "https://techygeekshome.info/cutgeek/"
#define AppSupportURL  "https://github.com/techygeekshome/CutGeek/issues"
#define AppUpdatesURL  "https://github.com/techygeekshome/CutGeek/releases"
#define FirstYear      "2026"
#define CurrentYear    GetDateTimeString('yyyy', '', '')

; Read straight off the executable that is about to be packaged, so the installer can never
; claim a different version from the thing inside it.
#define AppVersion GetVersionNumbersString(AppSourceDir + "\" + AppExeName)

[Setup]
; NEVER regenerate this. Windows uses the AppId to tell an upgrade from a second parallel
; install; a new one means the next version installs alongside this one instead of over it.
AppId={{C91CA6F5-D34D-4C62-AC50-8388F15CD452}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppSupportURL}
AppUpdatesURL={#AppUpdatesURL}
AppCopyright=Copyright (C) {#FirstYear}-{#CurrentYear} {#AppPublisher}

VersionInfoVersion={#AppVersion}
VersionInfoCompany={#AppPublisher}
VersionInfoDescription={#AppName} Setup

WizardStyle=modern
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName} {#AppVersion}
LicenseFile=..\LICENSE
SetupIconFile=..\icons\cutgeek.ico

OutputDir=..\dist
OutputBaseFilename={#AppName}Setup

DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
DisableDirPage=auto
AllowNoIcons=yes

; The app's own manifest is asInvoker - it reads image files and writes a new one beside them,
; and there is nothing in that which needs administrator rights. Installing it somewhere
; only an administrator can write would be pretending otherwise, so this is a per-user install
; with no UAC prompt. Anyone who wants it machine-wide can pass /ALLUSERS.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=commandline dialog

Compression=lzma2/max
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
english.CreateDesktopShortcut=Create a &desktop shortcut
english.LaunchApp=Open {#AppName}
english.WebSite={#AppName} on the web

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopShortcut}"; GroupDescription: "Shortcuts:"

[Files]
Source: "{#AppSourceDir}\{#AppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE";   DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; DestName: "README.md";   Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}";                       Filename: "{app}\{#AppExeName}"
Name: "{group}\{cm:WebSite}";                     Filename: "{#AppURL}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}";                 Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchApp}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Downloaded models are left behind on purpose. Somebody who uninstalls to try a newer build
; should not have to fetch 176 MB again, and throwing away that much of somebody's disk without
; asking is not a decision an uninstaller gets to make. The folders go if they are empty, and
; there is a Remove button on the Models screen for anyone who wants them gone.
Type: dirifempty; Name: "{localappdata}\TechyGeeksHome\CutGeek\models"
Type: dirifempty; Name: "{localappdata}\TechyGeeksHome\CutGeek"
Type: dirifempty; Name: "{localappdata}\TechyGeeksHome"
