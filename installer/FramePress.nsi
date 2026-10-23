Unicode True
!include "MUI2.nsh"
Name "FramePress"
OutFile "..\artifacts\FramePress-v0.1.0-Setup.exe"
InstallDir "$LOCALAPPDATA\Programs\FramePress"
InstallDirRegKey HKCU "Software\FramePress" "InstallDir"
RequestExecutionLevel user
SetCompressor /SOLID lzma
!define MUI_ICON "..\src\FramePress.App\Assets\FramePress.ico"
!define MUI_UNICON "..\src\FramePress.App\Assets\FramePress.ico"
!define MUI_ABORTWARNING
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!define MUI_FINISHPAGE_RUN "$INSTDIR\FramePress.App.exe"
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "English"
!insertmacro MUI_LANGUAGE "Arabic"
Section "FramePress" SEC_MAIN
 SectionIn RO
 SetShellVarContext current
 SetOutPath "$INSTDIR"
 File /r "..\artifacts\publish\*"
 WriteUninstaller "$INSTDIR\Uninstall.exe"
 CreateDirectory "$SMPROGRAMS\FramePress"
 CreateShortcut "$SMPROGRAMS\FramePress\FramePress.lnk" "$INSTDIR\FramePress.App.exe"
 CreateShortcut "$SMPROGRAMS\FramePress\Uninstall.lnk" "$INSTDIR\Uninstall.exe"
 WriteRegStr HKCU "Software\FramePress" "InstallDir" "$INSTDIR"
 WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\FramePress" "DisplayName" "FramePress"
 WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\FramePress" "DisplayVersion" "0.1.0"
 WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\FramePress" "DisplayIcon" "$INSTDIR\FramePress.App.exe"
 WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\FramePress" "UninstallString" '"$INSTDIR\Uninstall.exe"' WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\FramePress" "NoModify" 1
 WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\FramePress" "NoRepair" 1
SectionEnd
Section /o "Desktop shortcut" SEC_DESKTOP
 CreateShortcut "$DESKTOP\FramePress.lnk" "$INSTDIR\FramePress.App.exe"
SectionEnd
Section "Uninstall"
 SetShellVarContext current
 !include "UninstallFiles.nsh"
 Delete "$INSTDIR\Uninstall.exe"
 RMDir "$INSTDIR"
 Delete "$DESKTOP\FramePress.lnk"
 Delete "$SMPROGRAMS\FramePress\FramePress.lnk"
 Delete "$SMPROGRAMS\FramePress\Uninstall.lnk"
 RMDir "$SMPROGRAMS\FramePress"
 DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\FramePress"
 DeleteRegKey HKCU "Software\FramePress"
SectionEnd
