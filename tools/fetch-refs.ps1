<#
.SYNOPSIS
    准备编译 TurnEndOpenUrlMod 需要的引用程序集。

.DESCRIPTION
    把 CesiumLoader.SDK.dll 放到 refs\，把游戏程序集放到 refs\game\。
    这些文件都不在仓库里（SDK 体积与游戏程序集版权都不适合入库），
    所以首次编译前先运行本脚本。

    默认从公开的 CesiumLoader 仓库与 Release 下载:
      - 游戏程序集: refs/game/*.dll (按 sources.json 校验 SHA256)
      - SDK:        Release 里的 cesium-sdk-tools-<版本>.zip

    已经在本机 clone 了 CesiumLoader 时，用 -Source 直接从本地复制更快，
    也不需要联网。

.PARAMETER CesiumLoaderRef
    取自 CesiumLoader 仓库的 tag，决定游戏程序集与 SDK 的版本。

.PARAMETER Source
    本地 CesiumLoader 仓库根目录。给出时从本地复制，忽略 -CesiumLoaderRef。

.PARAMETER Force
    已存在的文件也重新下载/复制。

.EXAMPLE
    pwsh -File tools\fetch-refs.ps1

.EXAMPLE
    pwsh -File tools\fetch-refs.ps1 -Source C:\src\Study\astralparty\modding\msvc
#>
[CmdletBinding()]
param(
    [string] $CesiumLoaderRef = 'modloader-2.9.1',
    [string] $Source,
    [switch] $Force
)

$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12

$repoRoot = Split-Path -Parent $PSScriptRoot
$refsDir = Join-Path $repoRoot 'refs'
$gameDir = Join-Path $refsDir 'game'
$sdkPath = Join-Path $refsDir 'CesiumLoader.SDK.dll'

New-Item -ItemType Directory -Force -Path $refsDir, $gameDir | Out-Null

# mod 源码直接引用的游戏程序集
$gameAssemblies = @(
    'AstralParty.Runtime.dll',
    'Google.Protobuf.Runtime.dll',
    'UniTask.dll',
    'UnityEngine.CoreModule.dll'
)

function Write-Step([string] $message) { Write-Host "== $message" }

if ($Source) {
    $Source = (Resolve-Path -LiteralPath $Source).Path
    Write-Step "从本地 CesiumLoader 仓库复制: $Source"

    foreach ($name in $gameAssemblies) {
        $from = Join-Path $Source "refs\game\$name"
        if (-not (Test-Path -LiteralPath $from)) { throw "缺少 $from，先在该仓库准备好 refs\game\。" }
        Copy-Item -LiteralPath $from -Destination (Join-Path $gameDir $name) -Force
    }

    $sdkCandidates = @(
        (Join-Path $Source 'loader\CesiumLoader.SDK\bin\Release\netstandard2.0\CesiumLoader.SDK.dll'),
        (Join-Path $Source 'dist\modloader\AstralParty_ModLoader\sdk\CesiumLoader.SDK.dll')
    )
    $sdkFrom = $sdkCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if (-not $sdkFrom) {
        throw "找不到已构建的 CesiumLoader.SDK.dll。先在 CesiumLoader 仓库运行 dotnet build loader\CesiumLoader.SDK\CesiumLoader.SDK.csproj -c Release。"
    }
    Copy-Item -LiteralPath $sdkFrom -Destination $sdkPath -Force
    Write-Host "SDK <- $sdkFrom"
}
else {
    Write-Step "从 github.com/higashitaniyume/CesiumLoader ($CesiumLoaderRef) 下载"

    $rawBase = "https://raw.githubusercontent.com/higashitaniyume/CesiumLoader/$CesiumLoaderRef"

    $sourcesJson = Invoke-RestMethod -Uri "$rawBase/refs/game/sources.json" -UseBasicParsing
    foreach ($name in $gameAssemblies) {
        $target = Join-Path $gameDir $name
        $expected = $sourcesJson.assemblies.$name
        if ($expected -and (Test-Path -LiteralPath $target) -and -not $Force) {
            $actual = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant()
            if ($actual -eq $expected) { Write-Host "$name 已就绪"; continue }
        }
        Write-Host "下载 $name"
        Invoke-WebRequest -Uri "$rawBase/refs/game/$name" -OutFile $target -UseBasicParsing
        if ($expected) {
            $actual = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant()
            if ($actual -ne $expected) {
                throw "$name 校验失败: 期望 $expected, 实际 $actual"
            }
        }
    }

    if ((Test-Path -LiteralPath $sdkPath) -and -not $Force) {
        Write-Host 'CesiumLoader.SDK.dll 已就绪'
    }
    else {
        $sdkZipName = 'cesium-sdk-tools.zip'
        $sdkZipUrl = "https://github.com/higashitaniyume/CesiumLoader/releases/download/$CesiumLoaderRef/$sdkZipName"
        $tempZip = Join-Path ([IO.Path]::GetTempPath()) "cesium-sdk-tools-$([guid]::NewGuid().ToString('N')).zip"
        $tempDir = Join-Path ([IO.Path]::GetTempPath()) "cesium-sdk-tools-$([guid]::NewGuid().ToString('N'))"
        try {
            Write-Host "下载 SDK: $sdkZipUrl"
            Invoke-WebRequest -Uri $sdkZipUrl -OutFile $tempZip -UseBasicParsing
            Expand-Archive -LiteralPath $tempZip -DestinationPath $tempDir -Force
            $dll = Get-ChildItem -LiteralPath $tempDir -Recurse -File -Filter 'CesiumLoader.SDK.dll' | Select-Object -First 1
            if (-not $dll) { throw 'SDK 压缩包里没有 CesiumLoader.SDK.dll。' }
            Copy-Item -LiteralPath $dll.FullName -Destination $sdkPath -Force
            $pdb = Get-ChildItem -LiteralPath $tempDir -Recurse -File -Filter 'CesiumLoader.SDK.pdb' | Select-Object -First 1
            if ($pdb) { Copy-Item -LiteralPath $pdb.FullName -Destination (Join-Path $refsDir 'CesiumLoader.SDK.pdb') -Force }
        }
        finally {
            if (Test-Path -LiteralPath $tempZip) { Remove-Item -LiteralPath $tempZip -Force }
            if (Test-Path -LiteralPath $tempDir) { Remove-Item -LiteralPath $tempDir -Recurse -Force }
        }
    }
}

Write-Step '就绪'
Write-Host "refs\CesiumLoader.SDK.dll"
Get-ChildItem -LiteralPath $gameDir -File -Filter '*.dll' | ForEach-Object { Write-Host "refs\game\$($_.Name)" }
Write-Host '下一步: dotnet build source/TurnEndOpenUrlMod.csproj -c Release'
