# ============================================================================
# PCL WE / WCL 发布推送脚本
# ----------------------------------------------------------------------------
# 功能：
#   1. 将 WCL 源码仓库推送到 https://github.com/Hedwig207/PCL-WE.git
#   2. 内测版 / 正式版发布时均可使用
#   3. 自动处理：
#        - Git 仓库初始化（若尚未初始化）
#        - 配置远程仓库 origin
#        - 初始化子模块（MeloongCore 等）
#        - 暂存全部源码改动并提交
#        - 按版本号自动创建 Git 标签
#        - 推送到远程仓库及标签
#
# 用法：
#   直接运行（使用代码中自定义的版本号）：
#       .\Publish-PCLWE.ps1
#   指定版本号（例如正式版）：
#       .\Publish-PCLWE.ps1 -Version "1.1.0"
#   指定提交说明：
#       .\Publish-PCLWE.ps1 -Message "发布内测版 WCL beta1.1"
#   若想将 MeloongCore 源代码直接内嵌进仓库（clone 后无需子模块即可构建）：
#       .\Publish-PCLWE.ps1 -EmbedCore
# ============================================================================

param(
    [string]$RepoUrl    = "https://github.com/Hedwig207/PCL-WE.git",
    [string]$Branch     = "main",
    [string]$Version    = "",
    [string]$Message    = "",
    [switch]$EmbedCore,
    [switch]$NoPush
)

$ErrorActionPreference = "Stop"

# 仓库根目录 = 脚本所在目录（WCL）
$Root = $PSScriptRoot
Set-Location $Root

function Write-Step($Text) { Write-Host "`n===== $Text =====" -ForegroundColor Cyan }
function Write-Err  ($Text) { Write-Host "[错误] $Text" -ForegroundColor Red }

Write-Host "=============================================" -ForegroundColor Green
Write-Host "  PCL WE / WCL 发布推送" -ForegroundColor Green
Write-Host "  仓库根目录: $Root" -ForegroundColor Green
Write-Host "  目标仓库  : $RepoUrl" -ForegroundColor Green
Write-Host "=============================================" -ForegroundColor Green

# ---------- 0. 检测 git ----------
git --version | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Err "未检测到 Git，请先安装 Git for Windows。"
    exit 1
}

# ---------- 1. 初始化仓库（若尚未初始化） ----------
Write-Step "检查并初始化 Git 仓库"
if (-not (Test-Path (Join-Path $Root ".git"))) {
    git init -b $Branch
    if ($LASTEXITCODE -ne 0) { Write-Err "Git 初始化失败"; exit 1 }
    Write-Host "已初始化 Git 仓库，默认分支: $Branch"
} else {
    Write-Host "已存在 Git 仓库，跳过初始化"
}

# ---------- 2. 配置远程仓库 origin ----------
Write-Step "配置远程仓库 origin"
$existRead = git remote
if ($existRead -match "(?m)^origin$") {
    git remote set-url origin $RepoUrl
    Write-Host "已更新 remote origin 地址 -> $RepoUrl"
} else {
    git remote add origin $RepoUrl
    Write-Host "已添加 remote origin -> $RepoUrl"
}

# ---------- 3. 处理 MeloongCore 子模块 / 内嵌 ----------
if ($EmbedCore) {
    Write-Step "内嵌 MeloongCore（自包含模式）"
    $coreGit = Join-Path $Root "MeloongCore\.git"
    if (Test-Path $coreGit) {
        Write-Host "检测到 MeloongCore 的子模块 .git，将其转换为普通目录以便纳入仓库..."
        if (Test-Path "$coreGit.bak") { Remove-Item "$coreGit.bak" -Recurse -Force }
        Move-Item $coreGit "$coreGit.bak"
        Write-Host "已临时移除 MeloongCore\.git（备份为 .git.bak）。若需恢复可将其移回。"
    }
    $modules = Join-Path $Root ".gitmodules"
    if (Test-Path $modules) {
        Remove-Item $modules -Force
        Write-Host "已移除 .gitmodules，MeloongCore 将作为普通目录直接纳入仓库。"
    }
} else {
    Write-Step "初始化子模块"
    $modules = Join-Path $Root ".gitmodules"
    if ((Test-Path (Join-Path $Root ".git")) -and (Test-Path $modules)) {
        git submodule update --init --recursive
        if ($LASTEXITCODE -ne 0) {
            Write-Host "子模块初始化出现警告（可能网络原因），继续尝试。" -ForegroundColor Yellow
        } else {
            Write-Host "子模块已初始化。"
        }
    } else {
        Write-Host "无子模块或尚未初始化主仓库，跳过。"
    }
}

# ---------- 4. 读取版本号 ----------
Write-Step "确定版本号"
if ([string]::IsNullOrWhiteSpace($Version)) {
    $modBase = Join-Path $Root "Plain Craft Launcher 2\Modules\Base\ModBase.vb"
    if (Test-Path $modBase) {
        $content = Get-Content $modBase -Raw -Encoding UTF8
        if ($content -match 'VersionBaseName\s*As\s*String\s*=\s*"([^"]+)"') {
            $Version = $matches[1].Trim()
        }
    }
}
if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = Get-Date -Format "yyyy.MM.dd"
    Write-Host "未读取到版本号，使用日期作为标签: $Version"
} else {
    Write-Host "版本号: $Version"
}

# 转换为合法的 Git 标签名（Git 标签不支持空格，统一替换为下划线）
$TagName = "v" + ($Version -replace '\s+', '_' -replace '[^0-9A-Za-z._-]', '')
Write-Host "生成的 Git 标签: $TagName"

# ---------- 5. 生成提交说明 ----------
if ([string]::IsNullOrWhiteSpace($Message)) {
    $OuterFolder = Split-Path (Split-Path $Root -Parent) -Leaf
    $Message = "PCL WE 发布 $Version"
}

# ---------- 6. 添加并提交 ----------
Write-Step "暂存全部源码改动"
git add -A
if ($LASTEXITCODE -ne 0) { Write-Err "git add 失败"; exit 1 }

$dirty = git status --porcelain
if ($dirty) {
    Write-Host "待提交的文件数: $($dirty.Count)"
    Write-Step "创建提交"
    git commit -m $Message
    if ($LASTEXITCODE -ne 0) { Write-Err "git commit 失败"; exit 1 }
} else {
    Write-Host "没有需要提交的改动（工作区干净）。"
}

# ---------- 7. 打标签 ----------
Write-Step "创建 Git 标签"
$tagExist = git tag -l $TagName
if ($tagExist) {
    # 强制覆盖，方便内测版反复迭代同一标签
    git tag -f $TagName
    Write-Host "标签已存在，已强制更新: $TagName"
} else {
    git tag $TagName
    Write-Host "已创建标签: $TagName"
}

# ---------- 8. 推送到远程 ----------
if ($NoPush) {
    Write-Host "`n[提示] 已跳过推送（使用了 -NoPush）。" -ForegroundColor Yellow
} else {
    Write-Step "推送到远程仓库"
    Write-Host "正在推送分支 $Branch 及标签 $TagName ..."
    git push -u origin $Branch --tags
    if ($LASTEXITCODE -ne 0) {
        # 常见原因：远程已有本地没有的无关提交（如 GitHub 新建仓库自动生成的 README/LICENSE）。
        # 这是专属发布仓库，可安全地用本地内容覆盖远程。
        Write-Host "`n第一次推送被拒绝，尝试拉取远程信息..." -ForegroundColor Yellow
        git fetch origin 2>$null

        $remoteHead = git rev-parse --verify "origin/$Branch" 2>$null
        $localHead  = git rev-parse --verify "HEAD" 2>$null
        if ($remoteHead -and ($remoteHead -ne $localHead)) {
            # 判断远程是否与本地存在共同祖先
            $mergeBase = git merge-base $localHead "origin/$Branch" 2>$null
            if (-not $mergeBase) {
                Write-Host "远程分支与本地没有共同历史（多为 GitHub 自动生成的初始提交）。" -ForegroundColor Yellow
                Write-Host "该仓库为专属发布仓库，将以本地内容覆盖远程 $Branch 分支。" -ForegroundColor Yellow
                git push -u --force-with-lease origin $Branch --tags
                if ($LASTEXITCODE -ne 0) {
                    Write-Err "强制推送仍失败。请检查推送权限、凭证与网络。"
                    exit 1
                }
                Write-Host "`n已覆盖远程分支并推送成功！" -ForegroundColor Green
            } else {
                Write-Err "远程存在本地没有且无冲突的新提交，为避免破坏历史，未自动推送。"
                Write-Err "请手动执行: git pull --rebase origin $Branch 后重试。"
                exit 1
            }
        } else {
            Write-Err "推送失败。请检查：推送权限 / 凭证（credential-manager 或 PAT）/ 网络。"
            exit 1
        }
    } else {
        Write-Host "`n推送成功！" -ForegroundColor Green
    }
}

Write-Host "`n完成！" -ForegroundColor Green
Write-Host "远端分支: origin/$Branch" -ForegroundColor Green
Write-Host "标签    : $TagName" -ForegroundColor Green