; Установщик Glazecs для Windows (Inno Setup 7).
; Собирает то, что публикует профиль Portable-win-x64: лаунчер Glazecs.exe и папку app\.
; Сборка целиком — Installer\build.ps1 (публикация + этот скрипт).

#define AppName "Glazecs"
#define PublishDir "..\Glazecs.App.Desktop\bin\Publish\Portable-win-x64"
#define AppExe "Glazecs.exe"
; Версия берётся из самого приложения (1.5.0.0 → 1.5.0), чтобы не вести её в двух местах
#define AppVersion RemoveFileExt(GetVersionNumbersString(PublishDir + "\app\" + AppExe))

[Setup]
AppId={{EBC90645-47B6-4463-BE0B-A6D0BABA3E22}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=Glaz-almaz-GL
AppPublisherURL=https://github.com/Glaz-almaz-GL/Glazecs
VersionInfoVersion={#AppVersion}

; По умолчанию — для текущего пользователя, без запроса прав администратора (как VS Code, Discord);
; установка для всех пользователей — выбором в первом окне
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes

ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Windows 10 1809 — минимальная версия приложения
MinVersion=10.0.17763

WizardStyle=modern dynamic
SetupIconFile=..\Glazecs.App.Desktop\Resources\AppIcon\glazecs.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}

Compression=lzma2/max
SolidCompression=yes
OutputDir={#PublishDir}\..
OutputBaseFilename={#AppName}-{#AppVersion}-Setup

; Запущенное приложение закрывается перед обновлением и удалением
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[InstallDelete]
; Обновление ставится начисто: библиотеки прежней версии в app\ не должны смешиваться с новыми
Type: filesandordirs; Name: "{app}\app"

[Files]
Source: "{#PublishDir}\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PublishDir}\app\*"; DestDir: "{app}\app"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Файлы, которые приложение создаёт рядом с собой при работе. Настройки пользователя
; (%LOCALAPPDATA%\Glazecs) не трогаем — они переживают переустановку
Type: filesandordirs; Name: "{app}\app"

[CustomMessages]
russian.WebView2Missing=Для работы Glazecs нужен компонент Microsoft Edge WebView2 Runtime, а он не найден.%n%nВ Windows 11 он есть всегда; в Windows 10 его можно установить бесплатно с сайта Microsoft:%nhttps://go.microsoft.com/fwlink/p/?LinkId=2124703%n%nПродолжить установку?
english.WebView2Missing=Glazecs needs Microsoft Edge WebView2 Runtime, which was not found.%n%nIt ships with Windows 11; on Windows 10 it can be installed for free from Microsoft:%nhttps://go.microsoft.com/fwlink/p/?LinkId=2124703%n%nContinue installation?

[Code]
const
  WebView2ClientKey = 'Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}';

{ WebView2 Runtime регистрируется для машины (HKLM, 32-битная ветка) или для пользователя (HKCU) }
function IsWebView2Installed(): Boolean;
var
  Version: String;
begin
  Result :=
    (RegQueryStringValue(HKLM32, 'SOFTWARE\' + WebView2ClientKey, 'pv', Version) and (Version <> '') and (Version <> '0.0.0.0')) or
    (RegQueryStringValue(HKCU, 'Software\' + WebView2ClientKey, 'pv', Version) and (Version <> '') and (Version <> '0.0.0.0'));
end;

function InitializeSetup(): Boolean;
begin
  Result := True;

  if not IsWebView2Installed() then
    Result := SuppressibleMsgBox(CustomMessage('WebView2Missing'), mbConfirmation, MB_YESNO, IDYES) = IDYES;
end;

function GetLongPathName(ShortPath: String; LongPath: String; BufferLength: Integer): Integer;
  external 'GetLongPathNameW@kernel32.dll stdcall';

{ Один и тот же путь может прийти в коротком (8.3) и длинном виде — сравниваем длинные }
function ToLongPath(const Path: String): String;
var
  Buffer: String;
  Len: Integer;
begin
  Result := Path;
  Buffer := StringOfChar(#0, 1024);
  Len := GetLongPathName(Path, Buffer, 1024);
  if (Len > 0) and (Len < 1024) then
    Result := Copy(Buffer, 1, Len);
end;

{ Закрывает запущенные копии Glazecs из этой папки установки. Restart Manager Inno Setup
  работает только при установке, поэтому при удалении занятые файлы иначе остались бы на диске.
  Копии из других папок (сборки разработчика, другая установка) не трогаются. }
procedure CloseRunningApp();
var
  Locator, Service, Processes, Process: Variant;
  InstalledPaths: array[0..1] of String;
  ProcessPath: String;
  I, J: Integer;
begin
  InstalledPaths[0] := ToLongPath(ExpandConstant('{app}\app\{#AppExe}'));
  InstalledPaths[1] := ToLongPath(ExpandConstant('{app}\{#AppExe}'));

  try
    Locator := CreateOleObject('WbemScripting.SWbemLocator');
    Service := Locator.ConnectServer('.', 'root\CIMV2');
    Processes := Service.ExecQuery('SELECT ProcessId, ExecutablePath FROM Win32_Process WHERE Name = ''{#AppExe}''');

    for I := 0 to Processes.Count - 1 do
    begin
      Process := Processes.ItemIndex(I);
      if VarIsNull(Process.ExecutablePath) then
        Continue;

      ProcessPath := ToLongPath(Process.ExecutablePath);
      for J := 0 to 1 do
        if CompareText(ProcessPath, InstalledPaths[J]) = 0 then
        begin
          Log('Closing running Glazecs: ' + ProcessPath);
          Process.Terminate();
        end;
    end;

    { Процессу нужно время, чтобы отпустить файлы }
    Sleep(1000);
  except
    Log('Could not check running Glazecs: ' + GetExceptionMessage());
  end;
end;

function InitializeUninstall(): Boolean;
begin
  CloseRunningApp();
  Result := True;
end;
