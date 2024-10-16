; Script generated by the Inno Setup Script Wizard.
; SEE THE DOCUMENTATION FOR DETAILS ON CREATING INNO SETUP SCRIPT FILES!

#define MyAppName "AvmWinNode"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Galaxy Pay, LLC"
#define MyAppPublisherURL "https://galaxy-pay.com"
#define MyPublishPath "publish"

[Setup]
; NOTE: The value of AppId uniquely identifies this application. Do not use the same AppId value in installers for other applications.
; (To generate a new GUID, click Tools | Generate GUID inside the IDE.)
AppId={{5A7F3586-4D20-473C-8D0D-851B2B6E20DE}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
;AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppPublisherURL}
;AppSupportURL={#MyAppURL}
;AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DisableDirPage=yes
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
; Uncomment the following line to run in non administrative install mode (install for current user only.)
;PrivilegesRequired=lowest
OutputBaseFilename=AvmWinNode_Setup
SetupIconFile={#MyPublishPath}\node.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "{#MyPublishPath}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; NOTE: Don't use "Flags: ignoreversion" on any shared system files

[Icons]
Name: "{group}\AvmWinNode"; Filename: "http://localhost:3536/"; IconFilename: "{app}\node.ico"
Name: "{commondesktop}\AvmWinNode"; Filename: "http://localhost:3536/"; IconFilename: "{app}\node.ico"

[Run]
Filename: "sc.exe"; Parameters: "create ""AvmWinNode"" binPath= ""{app}\AvmWinNode.exe"" start= auto"
Filename: "sc.exe"; Parameters: "start ""AvmWinNode"""
Filename: "cmd.exe"; Parameters: "/c mkdir {commonappdata}\AvmWinNode"

[UninstallRun]
Filename: "sc.exe"; Parameters: "stop ""AvmWinNode"""; RunOnceId: "StopService"
Filename: "sc.exe"; Parameters: "delete ""AvmWinNode"""; RunOnceId: "DelService"
