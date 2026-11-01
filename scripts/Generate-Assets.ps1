param()
Add-Type -AssemblyName System.Drawing
$assetsPath=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\src\Petrichor.App\Assets'))
function New-Art($name,$warm){
 $bmp=[Drawing.Bitmap]::new(720,720); $g=[Drawing.Graphics]::FromImage($bmp); $g.SmoothingMode='AntiAlias'
 $bg=if($warm){'#57352f'}else{'#20372f'}; $g.Clear([Drawing.ColorTranslator]::FromHtml($bg))
 if($warm){$b=[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#b89073'));$g.FillEllipse($b,350,130,180,180);$b.Dispose()}
 $layers=@(@('#43594b',@(0,420,140,240,270,354,430,180,720,370,720,720,0,720)),@('#2a483c',@(0,460,180,330,360,490,570,284,720,360,720,720,0,720)),@('#153328',@(0,572,230,488,340,570,460,426,720,538,720,720,0,720)))
 foreach($layer in $layers){$pts=@();for($i=0;$i -lt $layer[1].Count;$i+=2){$pts+=[Drawing.Point]::new($layer[1][$i],$layer[1][$i+1])};$col=if($warm){@('#43352e','#332c2b','#1c2422')[[Array]::IndexOf($layers,$layer)]}else{$layer[0]};$brush=[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml($col));$g.FillPolygon($brush,[Drawing.Point[]]$pts);$brush.Dispose()}
 if(!$warm){$fog=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(24,190,210,195));$g.FillRectangle($fog,0,280,720,24);$g.FillRectangle($fog,0,316,720,18);$fog.Dispose();$tree=[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#09271f'));foreach($x in @(30,95,605,695)){$pts=[Drawing.Point[]]@([Drawing.Point]::new($x,350),[Drawing.Point]::new($x-60,720),[Drawing.Point]::new($x+60,720));$g.FillPolygon($tree,$pts)};$tree.Dispose()}
 $bmp.Save((Join-Path $assetsPath $name),[Drawing.Imaging.ImageFormat]::Png);$g.Dispose();$bmp.Dispose()
}
New-Art 'Forest.png' $false; New-Art 'Sunset.png' $true
function New-Logo($name,$width,$height){$bmp=[Drawing.Bitmap]::new($width,$height);$g=[Drawing.Graphics]::FromImage($bmp);$g.SmoothingMode='AntiAlias';$g.Clear([Drawing.ColorTranslator]::FromHtml('#080808'));$s=[Math]::Min($width,$height)*0.48;$x=($width-$s)/2;$y=($height-$s)/2;$b=[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#ef233c'));$g.FillRectangle($b,[single]$x,[single]$y,[single]$s,[single]$s);$pen=[Drawing.Pen]::new([Drawing.Color]::White,[single]([Math]::Max(1,$s/35)));foreach($dx in @(.28,.5,.72)){$g.DrawLine($pen,[single]($x+$s*$dx),[single]($y+$s*.27),[single]($x+$s*($dx-.12)),[single]($y+$s*.72))};$bmp.Save((Join-Path $assetsPath $name),[Drawing.Imaging.ImageFormat]::Png);$pen.Dispose();$b.Dispose();$g.Dispose();$bmp.Dispose()}
New-Logo 'Square150x150Logo.png' 150 150;New-Logo 'Square44x44Logo.png' 44 44;New-Logo 'StoreLogo.png' 50 50;New-Logo 'Wide310x150Logo.png' 310 150;New-Logo 'SplashScreen.png' 620 300
