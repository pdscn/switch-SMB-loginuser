@echo off
setlocal enabledelayedexpansion
cd /d "%~dp0"

rem 编译并运行回归测试。测试会临时建立/断开本机回环 SMB 连接，
rem 请确保没有正在使用的 SMB 共享（测试结束会自动清理）。

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo [错误] 找不到 csc.exe
    exit /b 1
)

set SRC=src\Native\Mpr.cs src\Native\NetApi.cs src\Core\Models.cs src\Core\CredentialStore.cs src\Core\SmbSessionManager.cs

if not exist "tests\bin" mkdir "tests\bin"

set TESTS=AllTest SafetyTest CoreTest ForceTest SwitchProof
set FAILED=0

for %%T in (%TESTS%) do (
    echo.
    echo ============ %%T ============
    "%CSC%" -nologo -target:exe -platform:anycpu ^
        -out:"tests\bin\%%T.exe" ^
        -reference:System.dll -reference:System.Core.dll ^
        -reference:System.Security.dll -reference:System.Xml.dll ^
        "tests\regression\%%T.cs" %SRC%
    if errorlevel 1 (
        echo [编译失败] %%T
        set FAILED=1
    ) else (
        "tests\bin\%%T.exe"
        if errorlevel 1 set FAILED=1
    )
)

echo.
if "%FAILED%"=="1" (
    echo [结果] 有测试失败
    exit /b 1
) else (
    echo [结果] 全部通过
)
endlocal
