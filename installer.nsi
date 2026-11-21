Unicode true
RequestExecutionLevel admin
SetCompressor /SOLID lzma

!define APPNAME   "AutoDarkModeX"
!define APPEXE    "AutoDarkModeX.exe"
!ifndef APPVER
  !define APPVER "0.0"
!endif
!ifndef APPVER4
  !define APPVER4 "0.0.0.0"
!endif
!define PUBLISHER "Shax"
!define COPYRIGHT "Copyright (c) 2026 Created by Shax"
!define UNINSTKEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\AutoDarkModeX"
; the program's earlier name "Auto Dark Mode": removed when AutoDarkModeX is installed
!define OLDKEY    "Software\Microsoft\Windows\CurrentVersion\Uninstall\AutoDarkMode"
!define OLDEXE    "AutoDarkMode.exe"

Name "${APPNAME}"
OutFile "AutoDarkModeX-Setup-${APPVER}.exe"
; standard location; the user can pick another folder on the directory page
InstallDir "$PROGRAMFILES64\${APPNAME}"
BrandingText "${COPYRIGHT}"

VIProductVersion "${APPVER4}"
VIAddVersionKey "ProductName"     "${APPNAME} Setup"
VIAddVersionKey "FileDescription" "${APPNAME} Installer"
VIAddVersionKey "FileVersion"     "${APPVER}"
VIAddVersionKey "ProductVersion"  "${APPVER}"
VIAddVersionKey "CompanyName"     "${PUBLISHER}"
VIAddVersionKey "LegalCopyright"  "${COPYRIGHT}"

!include "MUI2.nsh"
!include "x64.nsh"
!include "LogicLib.nsh"

Var Harden
Var OldDir
Var LegacyDir

!define MUI_ICON   "assets/app.ico"
!define MUI_UNICON "assets/app.ico"
!define MUI_ABORTWARNING

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!define MUI_FINISHPAGE_RUN
!define MUI_FINISHPAGE_RUN_FUNCTION LaunchApp
!define MUI_FINISHPAGE_RUN_TEXT "$(RUNTEXT)"
; second checkbox (checked by default): start with Windows
!define MUI_FINISHPAGE_SHOWREADME ""
!define MUI_FINISHPAGE_SHOWREADME_TEXT "$(STARTUPTEXT)"
!define MUI_FINISHPAGE_SHOWREADME_FUNCTION EnableAutostart
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "English"
!insertmacro MUI_LANGUAGE "Russian"
!insertmacro MUI_LANGUAGE "French"
!insertmacro MUI_LANGUAGE "Dutch"
!insertmacro MUI_LANGUAGE "German"
!insertmacro MUI_LANGUAGE "Ukrainian"
!insertmacro MUI_LANGUAGE "Spanish"

LangString RUNTEXT ${LANG_ENGLISH} "Launch AutoDarkModeX"
LangString RUNTEXT ${LANG_RUSSIAN} "Запустить AutoDarkModeX"
LangString RUNTEXT ${LANG_FRENCH}  "Lancer AutoDarkModeX"
LangString RUNTEXT ${LANG_DUTCH}   "AutoDarkModeX starten"
LangString RUNTEXT ${LANG_GERMAN}  "AutoDarkModeX starten"
LangString RUNTEXT ${LANG_UKRAINIAN} "Запустити AutoDarkModeX"
LangString RUNTEXT ${LANG_SPANISH} "Iniciar AutoDarkModeX"

LangString STARTUPTEXT ${LANG_ENGLISH} "Start with Windows"
LangString STARTUPTEXT ${LANG_RUSSIAN} "Запускать вместе с Windows"
LangString STARTUPTEXT ${LANG_FRENCH}  "Démarrer avec Windows"
LangString STARTUPTEXT ${LANG_DUTCH}   "Starten met Windows"
LangString STARTUPTEXT ${LANG_GERMAN}  "Mit Windows starten"
LangString STARTUPTEXT ${LANG_UKRAINIAN} "Запускати разом із Windows"
LangString STARTUPTEXT ${LANG_SPANISH} "Iniciar con Windows"

Function .onInit
  ; earlier AutoDarkModeX install: offer the same folder again
  ReadRegStr $OldDir HKLM "${UNINSTKEY}" "InstallLocation"
  ${If} $OldDir != ""
    StrCpy $INSTDIR $OldDir
  ${EndIf}
  ; the old "Auto Dark Mode" (any version): its folder is cleaned up during install
  ReadRegStr $LegacyDir HKLM "${OLDKEY}" "InstallLocation"
FunctionEnd

Function EnableAutostart
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "AutoDarkModeX" '"$INSTDIR\${APPEXE}" --tray'
  ; clear a "disabled" mark from Task Manager -> Startup apps
  DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run" "AutoDarkModeX"
FunctionEnd

; Start the app as the normal (non-elevated) user, so it edits the right HKCU
Function LaunchApp
  Exec '"$WINDIR\explorer.exe" "$INSTDIR\${APPEXE}"'
FunctionEnd

Section "Install"
  SetShellVarContext current

  ; Lock down the program folder (only administrators may change files there) when it is new
  ; or an earlier Auto Dark Mode folder. A folder with other content keeps its permissions.
  StrCpy $Harden 1
  IfFileExists "$INSTDIR\*.*" 0 harden_checked
    IfFileExists "$INSTDIR\${APPEXE}" harden_checked 0
    StrCpy $Harden 0
  harden_checked:

  SetOutPath "$INSTDIR"

  ; close a running copy (also of the old "Auto Dark Mode") so files can be replaced
  nsExec::Exec 'taskkill /F /IM ${APPEXE}'
  nsExec::Exec 'taskkill /F /IM ${OLDEXE}'
  Sleep 400

  File "${APPEXE}"
  File "assets/app.ico"
  WriteUninstaller "$INSTDIR\Uninstall.exe"

  ${If} $Harden == 1
    ; Administrators + SYSTEM: full control, Users: read & run (SIDs work in every Windows language)
    nsExec::Exec 'icacls "$INSTDIR" /inheritance:r /grant:r *S-1-5-32-544:(OI)(CI)F *S-1-5-18:(OI)(CI)F *S-1-5-32-545:(OI)(CI)RX /C /Q'
  ${EndIf}

  ; moved to a new folder: remove the program files from the old one (only our own files)
  ${If} $OldDir != ""
  ${AndIf} $OldDir != $INSTDIR
  ${AndIf} ${FileExists} "$OldDir\${APPEXE}"
    Delete "$OldDir\${APPEXE}"
    Delete "$OldDir\app.ico"
    Delete "$OldDir\Uninstall.exe"
    RMDir "$OldDir"
    ; autostart pointed to the old folder: move it along
    ReadRegStr $0 HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "AutoDarkModeX"
    ${If} $0 != ""
      Call EnableAutostart
    ${EndIf}
  ${EndIf}

  ; ---- remove the old "Auto Dark Mode" ----
  ; settings are taken over first (only if AutoDarkModeX has none yet)
  CreateDirectory "$APPDATA\AutoDarkModeX"
  ${IfNot} ${FileExists} "$APPDATA\AutoDarkModeX\settings.ini"
  ${AndIf} ${FileExists} "$APPDATA\AutoDarkMode\settings.ini"
    CopyFiles /SILENT "$APPDATA\AutoDarkMode\settings.ini" "$APPDATA\AutoDarkModeX"
  ${EndIf}
  ${If} $LegacyDir != ""
  ${AndIf} ${FileExists} "$LegacyDir\${OLDEXE}"
    Delete "$LegacyDir\${OLDEXE}"
    Delete "$LegacyDir\app.ico"
    Delete "$LegacyDir\Uninstall.exe"
    RMDir "$LegacyDir"
  ${EndIf}
  ${If} ${FileExists} "C:\AutoDarkMode\${OLDEXE}"
    Delete "C:\AutoDarkMode\${OLDEXE}"
    Delete "C:\AutoDarkMode\app.ico"
    Delete "C:\AutoDarkMode\Uninstall.exe"
    RMDir "C:\AutoDarkMode"
  ${EndIf}
  DeleteRegKey HKLM "${OLDKEY}"
  Delete "$DESKTOP\Auto Dark Mode.lnk"
  RMDir /r "$APPDATA\AutoDarkMode"
  ; old autostart entry: replaced by the new one
  ReadRegStr $0 HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "AutoDarkMode"
  ${If} $0 != ""
    DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "AutoDarkMode"
    DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run" "AutoDarkMode"
    Call EnableAutostart
  ${EndIf}

  ; silent install (/S) has no finish page: turn autostart on there
  IfSilent 0 +2
    Call EnableAutostart

  ; Desktop shortcut
  CreateShortCut "$DESKTOP\${APPNAME}.lnk" "$INSTDIR\${APPEXE}" "" "$INSTDIR\app.ico" 0

  ; Add / Remove Programs entry
  WriteRegStr   HKLM "${UNINSTKEY}" "DisplayName"     "${APPNAME}"
  WriteRegStr   HKLM "${UNINSTKEY}" "DisplayVersion"  "${APPVER}"
  WriteRegStr   HKLM "${UNINSTKEY}" "Publisher"       "${PUBLISHER}"
  WriteRegStr   HKLM "${UNINSTKEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr   HKLM "${UNINSTKEY}" "DisplayIcon"     "$INSTDIR\app.ico"
  WriteRegStr   HKLM "${UNINSTKEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegStr   HKLM "${UNINSTKEY}" "Comments"        "${COPYRIGHT}"
  WriteRegDWORD HKLM "${UNINSTKEY}" "NoModify" 1
  WriteRegDWORD HKLM "${UNINSTKEY}" "NoRepair" 1
  WriteRegDWORD HKLM "${UNINSTKEY}" "EstimatedSize" 200
SectionEnd

Section "Uninstall"
  SetShellVarContext current
  nsExec::Exec 'taskkill /F /IM ${APPEXE}'
  Sleep 400

  Delete "$DESKTOP\${APPNAME}.lnk"
  DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "AutoDarkModeX"
  DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run" "AutoDarkModeX"
  RMDir /r "$APPDATA\AutoDarkModeX"
  DeleteRegKey HKLM "${UNINSTKEY}"

  Delete "$INSTDIR\${APPEXE}"
  Delete "$INSTDIR\app.ico"
  Delete "$INSTDIR\Uninstall.exe"
  SetOutPath "$TEMP"
  RMDir "$INSTDIR"
SectionEnd
