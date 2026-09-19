#ifndef MyAppVersion
  #define MyAppVersion "0.0.0-dev"
#endif

#ifndef SourceExe
  #error SourceExe must be provided by scripts/build-release.ps1
#endif

#ifndef OutputDir
  #error OutputDir must be provided by scripts/build-release.ps1
#endif

#ifndef RepoRoot
  #define RepoRoot ".."
#endif

#define MyAppName "ZomniverseGitPet"
#define MyAppPublisher "Wilder Ruiz"
#define MyAppUrl "https://github.com/wilderruiz/ZomniverseGitPet"
#define MyAppExeName "ZomniverseGitPet.exe"
#define MyAppUserModelId "Zomniverse.ZGitPet.Release"
#define MyReleaseIconName "ZGitPet-Release-v3.ico"

[Setup]
AppId={{98A22D79-31D2-4D97-93A9-D0619CF24A5D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppUrl}
AppSupportURL={#MyAppUrl}/issues
AppUpdatesURL={#MyAppUrl}/releases/latest
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#OutputDir}
OutputBaseFilename=ZomniverseGitPet-Setup-{#MyAppVersion}
SetupIconFile={#RepoRoot}\src\ZomniverseGitPet\Assets\App\ZomniverseGitPet-v2.ico
UninstallDisplayIcon={app}\{#MyReleaseIconName}
LicenseFile={#RepoRoot}\LICENSE
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
UsePreviousAppDir=yes
UsePreviousGroup=yes
SetupLogging=yes
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} Setup
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "{#SourceExe}"; DestDir: "{app}"; DestName: "{#MyAppExeName}"; Flags: ignoreversion
Source: "{#RepoRoot}\src\ZomniverseGitPet\Assets\App\ZomniverseGitPet-v2.ico"; DestDir: "{app}"; DestName: "{#MyReleaseIconName}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Comment: "ZGitPet - installed release"; IconFilename: "{app}\{#MyReleaseIconName}"; IconIndex: 0; AppUserModelID: "{#MyAppUserModelId}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Comment: "ZGitPet - installed release"; IconFilename: "{app}\{#MyReleaseIconName}"; IconIndex: 0; AppUserModelID: "{#MyAppUserModelId}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent
