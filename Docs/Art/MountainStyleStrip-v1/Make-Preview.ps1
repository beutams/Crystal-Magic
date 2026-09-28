$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$strip = [System.Drawing.Bitmap]::FromFile((Join-Path $PSScriptRoot 'Mountain-Front-16x96.png'))
$canvas = [System.Drawing.Bitmap]::new(360, 656)
$graphics = [System.Drawing.Graphics]::FromImage($canvas)
$titleFont = [System.Drawing.Font]::new('Microsoft YaHei', 16, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
$labelFont = [System.Drawing.Font]::new('Microsoft YaHei', 15, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
$smallFont = [System.Drawing.Font]::new('Microsoft YaHei', 11, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
$textBrush = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#333932'))
$mutedBrush = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#71796C'))
$linePen = [System.Drawing.Pen]::new([System.Drawing.ColorTranslator]::FromHtml('#B3BAAA'))
$labels = @('山顶另一侧边缘', '纯色山顶', '山顶与山腰衔接', '山腰', '正面山脚', '现有草地')
$fileIds = @('05', '04', '03', '02', '01', '06')
try {
    $graphics.Clear([System.Drawing.ColorTranslator]::FromHtml('#F4F3EB'))
    $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $graphics.DrawString('山正面 · 16×96', $titleFont, $textBrush, 24, 15)
    $graphics.DrawString('5 张山体 + 1 张草地 / 风格样条', $smallFont, $mutedBrush, 24, 38)
    for ($i = 0; $i -lt 6; $i++) {
        $centerY = 60 + $i * 96 + 48
        $graphics.DrawLine($linePen, 132, $centerY, 148, $centerY)
        $graphics.DrawString($labels[$i], $labelFont, $textBrush, 162, $centerY - 15)
        $detail = if ($i -eq 0) { '05  /  外侧透明，棋盘格仅示意' } else { $fileIds[$i] + '  /  16×16' }
        $graphics.DrawString($detail, $smallFont, $mutedBrush, 162, $centerY + 9)
    }
    # Literal integer pixel replication; no smoothing on the art itself.
    for ($y = 0; $y -lt 576; $y++) {
        for ($x = 0; $x -lt 96; $x++) {
            $color = $strip.GetPixel([int][Math]::Floor($x / 6), [int][Math]::Floor($y / 6))
            if ($color.A -eq 0) {
                $hex = if (([int][Math]::Floor($x / 12) + [int][Math]::Floor($y / 12)) % 2 -eq 0) { '#D8D8D3' } else { '#F8F8F4' }
                $color = [System.Drawing.ColorTranslator]::FromHtml($hex)
            }
            $canvas.SetPixel(24 + $x, 60 + $y, $color)
        }
    }
    $canvas.Save((Join-Path $PSScriptRoot 'Mountain-Front-labeled-preview.png'), [System.Drawing.Imaging.ImageFormat]::Png)
} finally {
    $graphics.Dispose(); $canvas.Dispose(); $strip.Dispose()
    $titleFont.Dispose(); $labelFont.Dispose(); $smallFont.Dispose()
    $textBrush.Dispose(); $mutedBrush.Dispose(); $linePen.Dispose()
}
