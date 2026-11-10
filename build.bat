@echo off
chcp 65001 >nul 2>&1
title PCL WE 快速编译

echo ========================================
echo   PCL WE 快速编译
echo ========================================
echo.

set "SLN=%~dp0Plain Craft Launcher 2.sln"
set "MSBUILD=C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe"
set "EXE=%~dp0Plain Craft Launcher 2\bin\PCL WE.exe"

:: 检查文件
if not exist "%SLN%" (
    echo ❌ 找不到解决方案文件
    pause
    exit /b 1
)

if not exist "%MSBUILD%" (
    echo ❌ 找不到 MSBuild，请安装 Visual Studio 2022
    pause
    exit /b 1
)

:: 关闭旧进程
echo 🔄 关闭旧进程...
taskkill /F /IM "PCL WE.exe" >nul 2>&1
timeout /t 1 >nul

:: 编译
echo.
echo ⚙️  开始编译...
echo.
call "%MSBUILD%" "%SLN%" /p:Configuration=Debug /p:Platform="Any CPU" /v:minimal /m /t:Rebuild

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ========================================
    echo   ✅ 编译成功！
    echo ========================================
    echo.
    echo 输出路径：
    echo   %EXE%
    echo.
    set /p ANSWER="是否立即启动程序？(Y/n): "
    if /i "%ANSWER%" neq "n" (
        start "" "%EXE%"
        echo ✅ 程序已启动！
    )
) else (
    echo.
    echo ❌ 编译失败！请检查错误信息。
)

echo.
pause