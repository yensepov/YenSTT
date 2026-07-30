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
russian.ModelPageTitle=Модель распознавания
russian.ModelPageDescription=Выберите качество локального распознавания речи
russian.ModelPageSubcaption=Модель будет скачана во время установки. Чем крупнее модель, тем выше качество и нагрузка на процессор.
russian.ModelTiny=tiny — ~80 МБ · самая быстрая · базовое качество · ОЗУ 4 ГБ+
russian.ModelBase=base — ~150 МБ · быстрая · качество выше tiny · ОЗУ 4 ГБ+
russian.ModelSmall=small — ~500 МБ · баланс качества и скорости · ОЗУ 8 ГБ+ (рекомендуется)
russian.ModelMedium=medium — ~1,5 ГБ · повышенное качество · медленная на CPU · ОЗУ 12 ГБ+
russian.ModelLarge=large-v3 — ~3,1 ГБ · максимальное качество · очень медленная на CPU · ОЗУ 16 ГБ+
russian.ModelNone=Не скачивать сейчас (small загрузится при первом распознавании)
russian.ModelStatus=Скачивание и подготовка выбранной модели faster-whisper... Это может занять несколько минут.
english.ModelPageTitle=Recognition model
english.ModelPageDescription=Choose the quality of local speech recognition
english.ModelPageSubcaption=The model will be downloaded during setup. Larger models improve quality but require more CPU and memory.
english.ModelTiny=tiny — ~80 MB · fastest · basic quality · 4 GB+ RAM
english.ModelBase=base — ~150 MB · fast · better than tiny · 4 GB+ RAM
english.ModelSmall=small — ~500 MB · balanced quality and speed · 8 GB+ RAM (recommended)
english.ModelMedium=medium — ~1.5 GB · higher quality · slow on CPU · 12 GB+ RAM
english.ModelLarge=large-v3 — ~3.1 GB · best quality · very slow on CPU · 16 GB+ RAM
english.ModelNone=Do not download now (small will download on first transcription)
english.ModelStatus=Downloading and preparing the selected faster-whisper model... This may take several minutes.

[Tasks]
Name: "desktopicon"; Description: "Создать ярлык на рабочем столе"; GroupDescription: "Ярлыки:"; Flags: unchecked
Name: "startup"; Description: "Запускать YenSTT вместе с Windows"; GroupDescription: "Автозапуск:"; Flags: unchecked

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\YenSTT"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Удалить YenSTT"; Filename: "{uninstallexe}"
Name: "{autodesktop}\YenSTT"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "YenSTT"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Tasks: startup
Root: HKCU; Subkey: "Software\YenSTT"; ValueType: string; ValueName: "PendingModel"; ValueData: "tiny"; Flags: uninsdeletevalue uninsdeletekeyifempty; Check: TinyModelSelected
Root: HKCU; Subkey: "Software\YenSTT"; ValueType: string; ValueName: "PendingModel"; ValueData: "base"; Flags: uninsdeletevalue uninsdeletekeyifempty; Check: BaseModelSelected
Root: HKCU; Subkey: "Software\YenSTT"; ValueType: string; ValueName: "PendingModel"; ValueData: "small"; Flags: uninsdeletevalue uninsdeletekeyifempty; Check: SmallModelSelected
Root: HKCU; Subkey: "Software\YenSTT"; ValueType: string; ValueName: "PendingModel"; ValueData: "medium"; Flags: uninsdeletevalue uninsdeletekeyifempty; Check: MediumModelSelected
Root: HKCU; Subkey: "Software\YenSTT"; ValueType: string; ValueName: "PendingModel"; ValueData: "large-v3"; Flags: uninsdeletevalue uninsdeletekeyifempty; Check: LargeModelSelected

[Run]
Filename: "{app}\Runtime\faster-whisper-worker.exe"; Parameters: "--download-model tiny ""{userappdata}\YenSTT\models"""; StatusMsg: "{cm:ModelStatus}"; Flags: runhidden waituntilterminated logoutput; Check: TinyModelSelected
Filename: "{app}\Runtime\faster-whisper-worker.exe"; Parameters: "--download-model base ""{userappdata}\YenSTT\models"""; StatusMsg: "{cm:ModelStatus}"; Flags: runhidden waituntilterminated logoutput; Check: BaseModelSelected
Filename: "{app}\Runtime\faster-whisper-worker.exe"; Parameters: "--download-model small ""{userappdata}\YenSTT\models"""; StatusMsg: "{cm:ModelStatus}"; Flags: runhidden waituntilterminated logoutput; Check: SmallModelSelected
Filename: "{app}\Runtime\faster-whisper-worker.exe"; Parameters: "--download-model medium ""{userappdata}\YenSTT\models"""; StatusMsg: "{cm:ModelStatus}"; Flags: runhidden waituntilterminated logoutput; Check: MediumModelSelected
Filename: "{app}\Runtime\faster-whisper-worker.exe"; Parameters: "--download-model large-v3 ""{userappdata}\YenSTT\models"""; StatusMsg: "{cm:ModelStatus}"; Flags: runhidden waituntilterminated logoutput; Check: LargeModelSelected
Filename: "{app}\{#MyAppExeName}"; Description: "Запустить YenSTT"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{cmd}"; Parameters: "/C taskkill /IM {#MyAppExeName} /F"; Flags: runhidden; RunOnceId: "StopYenSTT"

[Code]
var
  ModelPage: TInputOptionWizardPage;

function TinyModelSelected: Boolean;
begin
  Result := ModelPage.SelectedValueIndex = 0;
end;

function BaseModelSelected: Boolean;
begin
  Result := ModelPage.SelectedValueIndex = 1;
end;

function SmallModelSelected: Boolean;
begin
  Result := ModelPage.SelectedValueIndex = 2;
end;

function MediumModelSelected: Boolean;
begin
  Result := ModelPage.SelectedValueIndex = 3;
end;

function LargeModelSelected: Boolean;
begin
  Result := ModelPage.SelectedValueIndex = 4;
end;

procedure InitializeWizard;
var
  ModelParameter: String;
begin
  ModelPage := CreateInputOptionPage(
    wpSelectTasks,
    CustomMessage('ModelPageTitle'),
    CustomMessage('ModelPageDescription'),
    CustomMessage('ModelPageSubcaption'),
    True,
    False
  );
  ModelPage.Add(CustomMessage('ModelTiny'));
  ModelPage.Add(CustomMessage('ModelBase'));
  ModelPage.Add(CustomMessage('ModelSmall'));
  ModelPage.Add(CustomMessage('ModelMedium'));
  ModelPage.Add(CustomMessage('ModelLarge'));
  ModelPage.Add(CustomMessage('ModelNone'));

  ModelParameter := Lowercase(ExpandConstant('{param:MODEL|small}'));
  if ModelParameter = 'tiny' then
    ModelPage.SelectedValueIndex := 0
  else if ModelParameter = 'base' then
    ModelPage.SelectedValueIndex := 1
  else if ModelParameter = 'medium' then
    ModelPage.SelectedValueIndex := 3
  else if ModelParameter = 'large-v3' then
    ModelPage.SelectedValueIndex := 4
  else if ModelParameter = 'none' then
    ModelPage.SelectedValueIndex := 5
  else
    ModelPage.SelectedValueIndex := 2;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
    RegDeleteValue(HKCU, 'Software\YenSTT', 'PendingModel');
end;
