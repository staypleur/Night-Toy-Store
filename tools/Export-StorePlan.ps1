param([string]$LayoutJson, [string]$Output)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$layout = Get-Content -LiteralPath $LayoutJson -Raw | ConvertFrom-Json
$bitmap = New-Object System.Drawing.Bitmap 940,760
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([System.Drawing.Color]::FromArgb(18,22,23))
$titleFont = New-Object System.Drawing.Font 'Malgun Gothic',24
$font = New-Object System.Drawing.Font 'Malgun Gothic',14
$smallFont = New-Object System.Drawing.Font 'Malgun Gothic',11
$white = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(235,232,218))
$muted = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(155,168,166))
$public = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(53,65,61))
$open = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(41,95,81))
$locked = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(91,53,47))
$gold = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(223,178,78))
$linkPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(189,180,153)),20
function Center([int]$Cell) {
    return [System.Drawing.PointF]::new(145+($Cell%3)*180,205+(2-[math]::Floor($Cell/3))*180)
}
try {
    $graphics.DrawString('Night Toy Store / 첫 조립식 평면도',$titleFont,$white,40,28)
    $graphics.DrawString(('맵 번호 '+$layout.seed+' · 단층 · 연결과 방 배치를 매 판 변경'),$font,$muted,42,78)
    foreach ($edge in $layout.edges) {
        $a = Center ([math]::Floor($edge/9)); $b = Center ($edge%9)
        $graphics.DrawLine($linkPen,$a,$b)
    }
    for ($cell=0;$cell -lt 9;$cell++) {
        $center = Center $cell
        $room = [array]::IndexOf([int[]]$layout.controlCells,$cell)+1
        $brush = if($room -eq 0){$public}elseif($room -eq $layout.startRoom){$open}else{$locked}
        $graphics.FillRectangle($brush,$center.X-79,$center.Y-77,158,154)
        $label = if($room -gt 0){'관제실 '+$room}else{@('장난감 진열','창고','수리 작업 공간')[$layout.themes[$cell]]}
        $graphics.DrawString($label,$font,$white,$center.X-67,$center.Y-38)
        $detail = if($room -eq 0){'탐색 구역'}elseif($room -eq $layout.startRoom){'네 명 함께 시작'}else{'전용 열쇠로 개방'}
        $graphics.DrawString($detail,$smallFont,$muted,$center.X-67,$center.Y-8)
        if($room -eq $layout.startRoom) {
            for($i=0;$i -lt 4;$i++){ $graphics.FillEllipse($white,$center.X-56+$i*30,$center.Y+31,12,12) }
        }
    }
    for ($room=1;$room -le 3;$room++) {
        if($room -eq $layout.startRoom){continue}
        $key = $layout.keys[$room-1]
        $x = 145+($key.x+8)*22.5; $y = 205+(8-$key.z)*22.5
        $graphics.FillEllipse($gold,$x-13,$y-13,26,26)
        $graphics.DrawString([string]$room,$smallFont,[System.Drawing.Brushes]::Black,$x-7,$y-10)
    }
    $graphics.DrawString('이번 배치',$font,$white,660,152)
    $graphics.DrawString(('시작: 관제실 '+$layout.startRoom),$font,$white,660,195)
    $graphics.DrawString("초록: 시작 관제실`n갈색: 잠긴 관제실`n회색: 탐색 구역`n연결선: 통로`n노란 번호: 해당 방 열쇠",$font,$muted,660,243)
    $graphics.DrawString("열쇠를 찾은 순서대로`n원하는 관제실 개방`n`n문 폭 2.8m`n공 지름 1.3m",$font,$muted,660,435)
    $graphics.DrawString('테스트 배치 예시 · 크기와 가구는 시제품 값 · CCTV/칠판은 아직 장식',$smallFont,$muted,42,709)
    $bitmap.Save($Output,[System.Drawing.Imaging.ImageFormat]::Png)
} finally {
    $graphics.Dispose(); $bitmap.Dispose()
    foreach($resource in $titleFont,$font,$smallFont,$white,$muted,$public,$open,$locked,$gold,$linkPen){$resource.Dispose()}
}
