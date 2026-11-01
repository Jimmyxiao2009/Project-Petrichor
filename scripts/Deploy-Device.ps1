[CmdletBinding()]
param([string]$Address='https://192.168.3.159',[switch]$Build)
$ErrorActionPreference='Stop'
$taskRoot=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
if($Build){& 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe' (Join-Path $taskRoot 'src\Petrichor.App\Petrichor.App.csproj') /restore /t:Build /p:Configuration=Release /p:Platform=ARM /v:minimal /nologo;if($LASTEXITCODE -ne 0){throw 'ARM build failed'}}
[xml]$manifest=Get-Content -LiteralPath (Join-Path $taskRoot 'src\Petrichor.App\Package.appxmanifest')
$version=[string]$manifest.Package.Identity.Version
$identity=[string]$manifest.Package.Identity.Name
$packagePath=Join-Path $taskRoot ('src\Petrichor.App\AppPackages\Petrichor.App_'+$version+'_ARM_Test\Petrichor.App_'+$version+'_ARM.appx')
if(!(Test-Path -LiteralPath $packagePath)){throw 'Build the ARM package first'}
$portalUri=[Uri]$Address
if($portalUri.Scheme -ne 'https'){throw 'Use the HTTPS device portal address'}
$session=New-Object Microsoft.PowerShell.Commands.WebRequestSession
function PortalRequest([string]$Path,[string]$Method='GET',[object]$Form=$null){
 $headers=@{}
 $cookie=$session.Cookies.GetCookies($portalUri)['CSRF-Token']
 if($cookie){$headers['X-CSRF-Token']=$cookie.Value}
 $args=@{Uri=$Address.TrimEnd('/')+$Path;Method=$Method;WebSession=$session;Headers=$headers;SkipCertificateCheck=$true;SkipHttpErrorCheck=$true;TimeoutSec=120;NoProxy=$true;DisableKeepAlive=$true}
 if($null -ne $Form){$args.Form=$Form}
 Invoke-WebRequest @args
}
function ParseResponse($response){
 if([int]$response.StatusCode -ge 400){throw ('Device portal HTTP '+[int]$response.StatusCode+' '+$response.Content)}
 if([string]::IsNullOrWhiteSpace($response.Content)){return $null}
 $response.Content | ConvertFrom-Json
}
[void](PortalRequest '/')
$os=ParseResponse (PortalRequest '/api/os/info')
Write-Output ('Device: '+$os.OsEdition+' '+$os.OsVersion)
if($os.OsEdition -notmatch 'Mobile' -or $os.OsVersion -notmatch 'arm'){throw 'Expected Windows 10 Mobile ARM device'}
if([int]($os.OsVersion.Split('.')[0]) -lt 15063){throw 'Device build is older than the app target'}
$dependencyRoot=Join-Path $env:USERPROFILE '.nuget\packages'
$framework=Join-Path $dependencyRoot 'microsoft.net.native.sharedlibrary-arm\1.7.3\tools\SharedLibrary\ret\Native\Microsoft.NET.Native.Framework.1.7.appx'
$runtime=Join-Path $dependencyRoot 'microsoft.net.native.compiler\1.7.6\tools\Runtime\arm\Microsoft.NET.Native.Runtime.1.7.appx'
$cookie=$session.Cookies.GetCookies($portalUri)['CSRF-Token']
if(!$cookie){throw 'Device portal did not issue a CSRF session cookie'}
$uploadUri=$Address.TrimEnd('/')+'/api/app/packagemanager/package?package='+[Uri]::EscapeDataString([IO.Path]::GetFileName($packagePath))
# curl emits the quoted filename fields expected by the Mobile portal multipart parser.
$uploadResult=& curl.exe --silent --show-error --insecure --noproxy '*' --max-time 120 --cookie ('CSRF-Token='+$cookie.Value) --header ('X-CSRF-Token: '+$cookie.Value) --form ('appFile=@'+$packagePath) --form ('dependency1=@'+$framework) --form ('dependency2=@'+$runtime) --write-out '
%{http_code}' $uploadUri
if($LASTEXITCODE -ne 0){throw 'Package upload transport failed'}
$uploadText=$uploadResult -join "`n"
$split=$uploadText.LastIndexOf("`n")
$upload=[pscustomobject]@{StatusCode=[int]$uploadText.Substring($split+1).Trim();Content=$uploadText.Substring(0,$split)}

if([int]$upload.StatusCode -ge 400){throw ('Install request failed: '+[int]$upload.StatusCode+' '+$upload.Content)}
Write-Output ('Install accepted: HTTP '+[int]$upload.StatusCode)
$deadline=[DateTimeOffset]::UtcNow.AddMinutes(3)
$installed=$null
do{
 $state=PortalRequest '/api/app/packagemanager/state'
 if([int]$state.StatusCode -eq 200){Write-Output ('Install result: '+$state.Content)}
 $packages=ParseResponse (PortalRequest '/api/app/packagemanager/packages')
 $installed=$packages.InstalledPackages | Where-Object {$_.PackageFullName.StartsWith($identity+'_'+$version+'_arm_', [StringComparison]::OrdinalIgnoreCase)} | Select-Object -First 1
 if($installed){break}
 if([int]$state.StatusCode -eq 200 -and $state.Content -match '0x[89A-Fa-f][0-9A-Fa-f]{7}'){throw ('Deployment failed: '+$state.Content)}
 Start-Sleep -Seconds 2
}while([DateTimeOffset]::UtcNow -lt $deadline)
if(!$installed){throw 'The requested app version was not found on the device'}
function EncodeParameter([string]$value){[Uri]::EscapeDataString([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($value)))}
$launch=PortalRequest ('/api/taskmanager/app?appid='+(EncodeParameter $installed.PackageRelativeId)+'&package='+(EncodeParameter $installed.PackageFullName)) 'POST'
if([int]$launch.StatusCode -ge 400){throw ('Launch failed: '+[int]$launch.StatusCode+' '+$launch.Content)}
$record=@{Address=$Address;OsVersion=$os.OsVersion;PackageFullName=$installed.PackageFullName;PackageRelativeId=$installed.PackageRelativeId;InstallHttp=[int]$upload.StatusCode;LaunchHttp=[int]$launch.StatusCode;VerifiedAt=[DateTimeOffset]::UtcNow.ToString('o')}
$folder=Join-Path $taskRoot 'artifacts\device'
New-Item -ItemType Directory -Path $folder -Force | Out-Null
$record | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $folder 'deployment.json') -Encoding utf8
Write-Output ('Installed and launched '+$installed.PackageFullName)



