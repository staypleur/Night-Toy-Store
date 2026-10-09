param([string]$LayoutJson, [string]$Output)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$plan=Get-Content -LiteralPath $LayoutJson -Raw | ConvertFrom-Json
$bitmap=New-Object System.Drawing.Bitmap 1400,1150
$graphics=[System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode=[System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([System.Drawing.Color]::FromArgb(18,24,27))
$titleFont=New-Object System.Drawing.Font 'Malgun Gothic',24
$font=New-Object System.Drawing.Font 'Malgun Gothic',12
$smallFont=New-Object System.Drawing.Font 'Malgun Gothic',10
$white=New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(239,234,220))
$muted=New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(176,190,185))
$publicBrush=New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(53,65,65))
$corridorBrush=New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(91,86,68))
$openBrush=New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(41,105,83))
$lockedBrush=New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(120,66,53))
$breakerBrush=New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(133,106,51))
$outline=New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(187,199,186)),2
$doorPen=New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(248,222,153)),5
$format=New-Object System.Drawing.StringFormat
$format.Alignment=[System.Drawing.StringAlignment]::Center
$format.LineAlignment=[System.Drawing.StringAlignment]::Center
function PlanRect($bounds) {
 return [System.Drawing.RectangleF]::new(50+($bounds.x+52)*11,155+(40-$bounds.y-$bounds.height)*11,$bounds.width*11,$bounds.height*11)
}
try {
 $graphics.DrawString('Night Toy Store / 사용자 그림 기반 첫 고정 맵',$titleFont,$white,45,25)
 $graphics.DrawString(('초록: 공동 시작 관제실 '+$plan.startRoom+' / 갈색: 잠긴 관제실 / 황토: 두꺼비집 / 통로 4m (시험값)'),$font,$muted,48,78)
 $graphics.DrawString('관제실 10×10m · 양쪽 문/창문 2m · 한 방 전용 열쇠로 양쪽 문 개방 · 시작 방만 무작위',$font,$muted,48,111)
 foreach($corridor in $plan.corridors) { $graphics.FillRectangle($corridorBrush,(PlanRect $corridor)) }
 foreach($room in $plan.rooms) {
  $rect=PlanRect $room.bounds
  $brush=if($room.control -gt 0){if($room.control -eq $plan.startRoom){$openBrush}else{$lockedBrush}}elseif($room.breaker -gt 0){$breakerBrush}else{$publicBrush}
  $graphics.FillRectangle($brush,$rect)
  $graphics.DrawRectangle($outline,$rect.X,$rect.Y,$rect.Width,$rect.Height)
  $label=$room.label
  if($room.breaker -gt 0){$label="두꺼비집`n"+$room.breaker}
  if($room.name -like 'Small room*'){ $label="작은 방`n"+$room.name.Substring(11,1)+' · 미확인' }
  if($room.name -eq 'Large party room 1'){$label="파티룸`n대형1"}
  $textFont=if($rect.Width -lt 90){$smallFont}else{$font}
  $graphics.DrawString($label,$textFont,$white,$rect,$format)
  if($room.control -gt 0) {
   $doorY=155+(40-($room.bounds.y+$room.bounds.height/2+1))*11
   foreach($doorX in $rect.Left,$rect.Right){ $graphics.DrawLine($doorPen,$doorX,$doorY-11,$doorX,$doorY+11) }
  }
 }
 $graphics.DrawString("사용자 확정: 레고방 / 파티룸1·2 / 유아방`n임시 치수: 관제실 외 방/복도와 세부 간격`nCCTV·칠판·카메라·두꺼비집은 외형만 구현. 전력/수리/괴물은 다음 단계.",$smallFont,$muted,48,1040)
 $bitmap.Save($Output,[System.Drawing.Imaging.ImageFormat]::Png)
} finally {
 $graphics.Dispose();$bitmap.Dispose()
 foreach($resource in $titleFont,$font,$smallFont,$white,$muted,$publicBrush,$corridorBrush,$openBrush,$lockedBrush,$breakerBrush,$outline,$doorPen,$format){$resource.Dispose()}
}
