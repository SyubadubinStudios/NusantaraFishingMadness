#ifndef SourceDir
  #error SourceDir is required
#endif
#ifndef OutputDir
  #error OutputDir is required
#endif
#ifndef AppVersion
  #define AppVersion "0.3.0-preview.1"
#endif

[Setup]
AppId={{64F69348-1B04-48D7-9CCD-840FF07D5DA9}
AppName=Nusantara Fishing Madness
AppVersion={#AppVersion}
AppPublisher=Syubadubin Studios
AppPublisherURL=https://github.com/SyubadubinStudios/NusantaraFishingMadness
DefaultDirName={localappdata}\Programs\SyubadubinStudios\NusantaraFishingMadness
DefaultGroupName=Nusantara Fishing Madness
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#OutputDir}
OutputBaseFilename=NusantaraFishingMadness-{#AppVersion}-win-x64-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupIconFile={#SourceDir}\assets\art\game-icon.ico
UninstallDisplayIcon={app}\assets\art\game-icon.ico
LicenseFile={#SourceDir}\LICENSE
CloseApplications=yes
RestartApplications=no

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Nusantara Fishing Madness"; Filename: "{app}\NusantaraFishing.Desktop.exe"; WorkingDir: "{app}"; IconFilename: "{app}\assets\art\game-icon.ico"

[Run]
Filename: "{app}\NusantaraFishing.Desktop.exe"; Description: "Launch Nusantara Fishing Madness"; Flags: nowait postinstall skipifsilent
