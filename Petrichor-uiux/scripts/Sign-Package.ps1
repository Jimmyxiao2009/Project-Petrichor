[CmdletBinding()]
param([ValidateSet('ARM','x86')][string]$Platform='ARM')
$ErrorActionPreference='Stop'
$taskRoot=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
[xml]$manifest=Get-Content -LiteralPath (Join-Path $taskRoot 'src\Petrichor.App\Package.appxmanifest')
$version=[string]$manifest.Package.Identity.Version
$publisher=[string]$manifest.Package.Identity.Publisher
$suffix=if($Platform -eq 'ARM'){'_ARM'}else{'_x86_Debug'}
$folder=Join-Path $taskRoot ('src\Petrichor.App\AppPackages\Petrichor.App_'+$version+$suffix+'_Test')
$package=Join-Path $folder ('Petrichor.App_'+$version+$suffix+'.appx')
if(!(Test-Path -LiteralPath $package)){throw ('Package not found: '+$package)}
# 证书主题必须与清单 Publisher 完全一致，否则签名失败或包族名变化导致设备上无法覆盖升级
$cert=Get-ChildItem Cert:\CurrentUser\My | Where-Object {$_.Subject -eq $publisher -and $_.HasPrivateKey -and $_.NotAfter -gt (Get-Date)} | Sort-Object NotAfter -Descending | Select-Object -First 1
if(!$cert){
 $cert=New-SelfSignedCertificate -Type Custom -Subject $publisher -FriendlyName 'Petrichor 开发签名' -KeyUsage DigitalSignature -KeyAlgorithm RSA -KeyLength 2048 -HashAlgorithm SHA256 -CertStoreLocation Cert:\CurrentUser\My -NotAfter (Get-Date).AddYears(3) -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3','2.5.29.19={text}')
 Write-Output ('Created certificate '+$cert.Thumbprint)
}
$signtool='C:\Program Files (x86)\Windows Kits\10\bin\10.0.19041.0\x64\signtool.exe'
& $signtool sign /fd SHA256 /sha1 $cert.Thumbprint /s My $package
if($LASTEXITCODE -ne 0){throw 'Signing failed'}
$cer=Join-Path $folder 'Petrichor.Development.cer'
Export-Certificate -Cert $cert -FilePath $cer -Type CERT | Out-Null
Write-Output ('Signed '+$package)
Write-Output ('Public certificate '+$cer)
