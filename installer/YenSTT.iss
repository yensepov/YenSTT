#define MyAppName "YenSTT"
#ifndef MyAppVersion
  #define MyAppVersion "1.0.1"
#endif
#define MyAppPublisher "yensepov"
#define MyAppURL "https://github.com/yensepov/YenSTT"
#define MyAppExeName "YenSTT.exe"

[Setup]
AppId={{C2CB34CF-95D2-4ED8-B00E-0D353371B72A}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE
OutputDir=..\artifacts\release
OutputBaseFilename=YenSTT-Setup-v{#MyAppVersion}-win-x64
SetupIconFile=..\Resources\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
CloseApplicationsFilter={#MyAppExeName}
RestartApplications=no
AppMutex=YenSTT_SingleInstance_Mutex
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription=YenSTT local speech-to-text installer
VersionInfoCopyright=Copyright (c) 2026 yensepov contributors

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
russian.ModelTask=Скачать модель распознавания small сейчас (~464 МБ, рекомендуется)
russian.ModelGroup=Локальная модель:
russian.ModelStatus=Скачивание и подготовка модели faster-whisper small... Это может занять несколько минут.
english.ModelTask=Download the small recognition model now (~464 MB, recommended)
english.ModelGroup=Local model:
english.ModelStatus=Downloading and preparing the faster-whisper small model... This may take several minutes.

[Tasks]
Name: "desktopicon"; Description: "Создать ярлык на рабочем столе"; GroupDescription: "Ярлыки:"; Flags: unchecked
Name: "startup"; Description: "Запускать YenSTT вместе с Windows"; GroupDescription: "Автозапуск:"; Flags: unchecked
Name: "downloadmodel"; Description: "{cm:ModelTask}"; GroupDescription: "{cm:ModelGroup}"; Flags: checkedonce

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\YenSTT"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Удалить YenSTT"; Filename: "{uninstallexe}"
Name: "{autodesktop}\YenSTT"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "YenSTT"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Tasks: startup

[Run]
Filename: "{app}\Runtime\faster-whisper-worker.exe"; Parameters: "--download-model ""{userappdata}\YenSTT\models"""; StatusMsg: "{cm:ModelStatus}"; Flags: runhidden waituntilterminated logoutput; Tasks: downloadmodel
Filename: "{app}\{#MyAppExeName}"; Description: "Запустить YenSTT"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{cmd}"; Parameters: "/C taskkill /IM {#MyAppExeName} /F"; Flags: runhidden; RunOnceId: "StopYenSTT"
