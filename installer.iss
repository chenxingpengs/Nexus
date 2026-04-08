#define MyAppName "Nexus"
#define MyAppVersion "2.6.1"
#define MyAppPublisher "红旗中学"
#define MyAppURL "https://hqzx.me"
#define MyAppExeName "Nexus.exe"

[Setup]
AppId={{8A7C5D3E-9F1B-4C2A-8E6D-5B3F1A2C9E7D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
LicenseFile=
InfoBeforeFile=
InfoAfterFile=
OutputDir=.\Output
OutputBaseFilename=Nexus-{#MyAppVersion}-win-x64
SetupIconFile=.\Assets\hqzx.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible

[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\Unofficial\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
chinesesimplified.UninstallPasswordTitle=卸载密码验证
chinesesimplified.UninstallPasswordPrompt=请输入卸载密码以继续卸载：
chinesesimplified.UninstallPasswordIncorrect=密码错误，无法卸载程序。
english.UninstallPasswordTitle=Uninstall Password Verification
english.UninstallPasswordPrompt=Please enter the uninstall password to continue:
english.UninstallPasswordIncorrect=Incorrect password. Cannot uninstall the program.

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "bin\Release\net8.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:ProgramOnTheWeb,{#MyAppName}}"; Filename: "{#MyAppURL}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Code]
const
  UninstallPassword = 'zhhqzx';

function CheckPassword(const Password: string): Boolean;
begin
  Result := (Password = UninstallPassword);
end;

function GetPasswordInput: string;
var
  Script: Variant;
begin
  Result := '';
  try
    Script := CreateOleObject('WScript.Shell');
    Result := Script.InputBox('请输入卸载密码以继续卸载：', '卸载密码验证', '');
  except
    Result := '';
  end;
end;

function PromptForUninstallPassword: Boolean;
var
  Password: string;
  Attempts: Integer;
begin
  Result := False;
  Attempts := 0;
  
  while Attempts < 3 do
  begin
    Password := GetPasswordInput;
    
    if Password = '' then
      Exit;
      
    if CheckPassword(Password) then
    begin
      Result := True;
      Exit;
    end;
    
    MsgBox('密码错误，无法卸载程序。', mbError, MB_OK);
    Inc(Attempts);
  end;
end;

function InitializeUninstall: Boolean;
begin
  Result := PromptForUninstallPassword;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ConfigPath: string;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    ConfigPath := ExpandConstant('{localappdata}\Nexus');
    if DirExists(ConfigPath) then
    begin
      DelTree(ConfigPath, True, True, True);
    end;
  end;
end;
