param()
# 图标与内容图生成。
# 图标用 8x 超采样绘制后高质量缩小，并输出 UWP 的 scale-* / targetsize-* 变体，
# 让系统在手机等高 DPI 屏幕上取到原生分辨率的图，而不是放大 100% 的位图。
Add-Type -AssemblyName System.Drawing
$assetsPath=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\src\Petrichor.App\Assets'))
$supersample=8
# UWP 标准缩放因子
$scales=[ordered]@{'100'=1.0;'125'=1.25;'150'=1.5;'200'=2.0;'250'=2.5;'300'=3.0;'400'=4.0}

function Render-Logo([string]$file,[int]$width,[int]$height,[bool]$transparent){
 $bw=[int]($width*$supersample);$bh=[int]($height*$supersample)
 $bmp=[Drawing.Bitmap]::new($bw,$bh)
 $g=[Drawing.Graphics]::FromImage($bmp)
 $g.SmoothingMode=[Drawing.Drawing2D.SmoothingMode]::AntiAlias
 $g.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::HighQuality
 $g.CompositingQuality=[Drawing.Drawing2D.CompositingQuality]::HighQuality
 if($transparent){$g.Clear([Drawing.Color]::Transparent)}else{$g.Clear([Drawing.ColorTranslator]::FromHtml('#080808'))}
 $size=[Math]::Min($width,$height)*0.48
 $bx=[single](($width-$size)/2*$supersample);$by=[single](($height-$size)/2*$supersample);$bs=[single]($size*$supersample)
 $block=[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#ef233c'))
 $g.FillRectangle($block,$bx,$by,$bs,$bs);$block.Dispose()
 $pen=[Drawing.Pen]::new([Drawing.Color]::White,[single]($bs*0.085))
 $pen.StartCap=[Drawing.Drawing2D.LineCap]::Round;$pen.EndCap=[Drawing.Drawing2D.LineCap]::Round
 foreach($dx in @(.26,.5,.74)){
  $g.DrawLine($pen,[single]($bx+$bs*($dx+0.09)),[single]($by+$bs*0.22),[single]($bx+$bs*($dx-0.09)),[single]($by+$bs*0.78))
 }
 $pen.Dispose()
 $out=[Drawing.Bitmap]::new($width,$height)
 $og=[Drawing.Graphics]::FromImage($out)
 $og.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
 $og.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::HighQuality
 $og.CompositingQuality=[Drawing.Drawing2D.CompositingQuality]::HighQuality
 $og.DrawImage($bmp,0,0,$width,$height)
 $og.Dispose();$g.Dispose();$bmp.Dispose()
 $out.Save((Join-Path $assetsPath $file),[Drawing.Imaging.ImageFormat]::Png);$out.Dispose()
}

function New-LogoSet([string]$base,[int]$width,[int]$height){
 Render-Logo ($base+'.png') $width $height $false
 foreach($key in $scales.Keys){
  # 用 AwayFromZero，避免 62.5 被银行家舍入成 62（Windows 要求 StoreLogo scale-125 为 63）
  $sw=[int][Math]::Round($width*$scales[$key],[MidpointRounding]::AwayFromZero);$sh=[int][Math]::Round($height*$scales[$key],[MidpointRounding]::AwayFromZero)
  Render-Logo ($base+'.scale-'+$key+'.png') $sw $sh $false
 }
}

New-LogoSet 'Square44x44Logo' 44 44
New-LogoSet 'Square150x150Logo' 150 150
New-LogoSet 'Wide310x150Logo' 310 150
New-LogoSet 'StoreLogo' 50 50
New-LogoSet 'SplashScreen' 620 300

# 任务栏 / 开始屏幕小图标按目标尺寸提供，unplated 版本透明底
foreach($target in @(16,24,32,48,256)){
 Render-Logo ('Square44x44Logo.targetsize-'+$target+'.png') $target $target $false
 Render-Logo ('Square44x44Logo.targetsize-'+$target+'_altform-unplated.png') $target $target $true
 Render-Logo ('Square44x44Logo.targetsize-'+$target+'_altform-lightunplated.png') $target $target $true
}

# 内容图（示例数据用的封面），同样超采样后缩小
function New-Art($name,$warm){
 $bw=720*$supersample
 $bmp=[Drawing.Bitmap]::new($bw,$bw);$g=[Drawing.Graphics]::FromImage($bmp);$g.SmoothingMode=[Drawing.Drawing2D.SmoothingMode]::AntiAlias
 $bg=if($warm){'#57352f'}else{'#20372f'};$g.Clear([Drawing.ColorTranslator]::FromHtml($bg))
 if($warm){$b=[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#b89073'));$g.FillEllipse($b,350*$supersample,130*$supersample,180*$supersample,180*$supersample);$b.Dispose()}
 $layers=@(@('#43594b',@(0,420,140,240,270,354,430,180,720,370,720,720,0,720)),@('#2a483c',@(0,460,180,330,360,490,570,284,720,360,720,720,0,720)),@('#153328',@(0,572,230,488,340,570,460,426,720,538,720,720,0,720)))
 foreach($layer in $layers){$pts=@();for($i=0;$i -lt $layer[1].Count;$i+=2){$pts+=[Drawing.Point]::new($layer[1][$i]*$supersample,$layer[1][$i+1]*$supersample)};$col=if($warm){@('#43352e','#332c2b','#1c2422')[[Array]::IndexOf($layers,$layer)]}else{$layer[0]};$brush=[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml($col));$g.FillPolygon($brush,[Drawing.Point[]]$pts);$brush.Dispose()}
 if(!$warm){$fog=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(24,190,210,195));$g.FillRectangle($fog,0,280*$supersample,720*$supersample,24*$supersample);$g.FillRectangle($fog,0,316*$supersample,720*$supersample,18*$supersample);$fog.Dispose();$tree=[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#09271f'));foreach($x in @(30,95,605,695)){$pts=[Drawing.Point[]]@([Drawing.Point]::new($x*$supersample,350*$supersample),[Drawing.Point]::new(($x-60)*$supersample,720*$supersample),[Drawing.Point]::new(($x+60)*$supersample,720*$supersample));$g.FillPolygon($tree,$pts)};$tree.Dispose()}
 $out=[Drawing.Bitmap]::new(720,720);$og=[Drawing.Graphics]::FromImage($out);$og.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic;$og.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::HighQuality;$og.DrawImage($bmp,0,0,720,720);$og.Dispose();$g.Dispose();$bmp.Dispose()
 $out.Save((Join-Path $assetsPath $name),[Drawing.Imaging.ImageFormat]::Png);$out.Dispose()
}
New-Art 'Forest.png' $false; New-Art 'Sunset.png' $true
Write-Output ('Assets written to '+$assetsPath)
