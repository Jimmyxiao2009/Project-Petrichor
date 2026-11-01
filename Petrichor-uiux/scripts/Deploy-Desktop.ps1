[CmdletBinding()]
param([switch]$Build,[switch]$Test,[switch]$EnableLocalProxy)
$ErrorActionPreference='Stop'
$taskRoot=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$msbuild='C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe'
if($Build){& $msbuild (Join-Path $taskRoot 'src\Petrichor.App\Petrichor.App.csproj') /restore /t:Build /p:Configuration=Debug /p:Platform=x86 /v:minimal /nologo;if($LASTEXITCODE -ne 0){throw 'Build failed'}}
[xml]$manifest=Get-Content -LiteralPath (Join-Path $taskRoot 'src\Petrichor.App\Package.appxmanifest')
$version=$manifest.Package.Identity.Version
$appId=$manifest.Package.Applications.Application.Id
$packagePath=Join-Path $taskRoot (''+'src\Petrichor.App\AppPackages\Petrichor.App_'+$version+'_x86_Debug_Test\Petrichor.App_'+$version+'_x86_Debug.appx')
$deployPath=Join-Path $taskRoot ('artifacts\desktop-x86-'+$version)
New-Item -ItemType Directory -Path $deployPath -Force | Out-Null
$artifactRoot=(Join-Path $taskRoot 'artifacts')+[IO.Path]::DirectorySeparatorChar
Get-Process -Name 'Petrichor.App' -ErrorAction SilentlyContinue | Where-Object {$_.Path -and $_.Path.StartsWith($artifactRoot,[StringComparison]::OrdinalIgnoreCase)} | Stop-Process
$makeappx='C:\Program Files (x86)\Windows Kits\10\bin\10.0.19041.0\x64\makeappx.exe'
& $makeappx unpack /p $packagePath /d $deployPath /o | Out-Null
if($LASTEXITCODE -ne 0){throw 'Unpack failed'}
Add-AppxPackage -Register (Join-Path $deployPath 'AppxManifest.xml')
$package=Get-AppxPackage -Name 'Petrichor.Music'
if($EnableLocalProxy){& CheckNetIsolation.exe LoopbackExempt -a ('-n='+$package.PackageFamilyName);if($LASTEXITCODE -ne 0){throw 'Loopback exemption failed'}}
if($Test){Start-Process -FilePath 'explorer.exe' -ArgumentList 'petrichor://test'}
else{Start-Process -FilePath 'explorer.exe' -ArgumentList ('shell:AppsFolder\'+$package.PackageFamilyName+'!'+$appId)}
Write-Output ('Deployed '+$package.PackageFamilyName+' to '+$deployPath)




