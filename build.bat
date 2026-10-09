@echo off
setlocal
cd /d "%~dp0"

rem 优先用 64 位编译器，回退到 32 位
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo [错误] 找不到 C# 编译器 csc.exe，请确认已安装 .NET Framework 4.x
    exit /b 1
)

if not exist "dist" mkdir "dist"

echo 正在编译...
"%CSC%" -nologo -target:winexe -platform:anycpu -optimize+ ^
    -out:"dist\SmbUserSwitch.exe" ^
    -reference:System.dll ^
    -reference:System.Core.dll ^
    -reference:System.Windows.Forms.dll ^
    -reference:System.Drawing.dll ^
    -reference:System.Security.dll ^
    -reference:System.Xml.dll ^
    -win32manifest:"src\app.manifest" ^
    "src\Program.cs" ^
    "src\Native\Mpr.cs" ^
    "src\Native\NetApi.cs" ^
    "src\Core\Models.cs" ^
    "src\Core\CredentialStore.cs" ^
    "src\Core\SmbSessionManager.cs" ^
    "src\UI\Theme.cs" ^
    "src\UI\AppleButton.cs" ^
    "src\UI\CardPanel.cs" ^
    "src\UI\AccountList.cs" ^
    "src\UI\MainForm.cs" ^
    "src\UI\AccountEditForm.cs" ^
    "src\UI\TrayIcon.cs"

if errorlevel 1 (
    echo.
    echo [失败] 编译出错，见上方信息。
    exit /b 1
)

echo.
echo [成功] 已生成 dist\SmbUserSwitch.exe
for %%F in ("dist\SmbUserSwitch.exe") do echo        大小: %%~zF 字节
echo.
echo 直接双击 dist\SmbUserSwitch.exe 即可运行（无需管理员权限）。
endlocal
