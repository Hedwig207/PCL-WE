# PCL WE - 快速编译脚本
# 用法：右键此文件 → 使用 PowerShell 运行

$ErrorActionPreference = "Stop"
$Host.UI.RawUI.WindowTitle = "PCL WE 快速编译"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  PCL WE 快速编译" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 路径配置
$slnPath = "$PSScriptRoot\Plain Craft Launcher 2.sln"
$msbuild = "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe"
$exePath = "$PSScriptRoot\Plain Craft Launcher 2\bin\PCL WE.exe"

# 1. 检查文件
if (!(Test-Path $slnPath)) {
    Write-Host "❌ 找不到解决方案文件：$slnPath" -ForegroundColor Red
    Read-Host "按回车键退出"
    exit 1
}

if (!(Test-Path $msbuild)) {
    Write-Host "❌ 找不到 MSBuild，请安装 Visual Studio 2022" -ForegroundColor Red
    Read-Host "按回车键退出"
    exit 1
}

# 2. 关闭旧进程
Write-Host "🔄 检查并关闭旧进程..." -ForegroundColor Yellow
Get-Process -Name "PCL WE" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

# 3. 编译
Write-Host ""
Write-Host "⚙️  开始编译..." -ForegroundColor Green
Write-Host ""

& $msbuild $slnPath /p:Configuration=Debug /p:Platform="Any CPU" /v:minimal /m /t:Rebuild

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "  ✅ 编译成功！" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "输出路径：" -ForegroundColor White
    Write-Host "  $exePath" -ForegroundColor Cyan
    Write-Host ""
    
    # 询问是否启动
    $answer = Read-Host "是否立即启动程序？(Y/n)"
    if ($answer -ne 'n' -and $answer -ne 'N') {
        Start-Process $exePath
        Write-Host "✅ 程序已启动！" -ForegroundColor Green
    }
} else {
    Write-Host ""
    Write-Host "❌ 编译失败！请检查错误信息。" -ForegroundColor Red
}

Write-Host ""
Read-Host "按回车键退出"