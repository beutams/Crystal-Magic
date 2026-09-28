$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$old = [System.Drawing.Bitmap]::FromFile((Join-Path $PSScriptRoot '../MountainStyleStrip-v1/Mountain-Front-16x96.png'))
$new = [System.Drawing.Bitmap]::FromFile((Join-Path $PSScriptRoot 'Mountain-Front-16x96.png'))
$canvas = [System.Drawing.Bitmap]::new(460, 664)
$graphics = [System.Drawing.Graphics]::FromImage($canvas)
$titleFont = [System.Drawing.Font]::new('Microsoft YaHei', 16, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
$labelFont = [System.Drawing.Font]::new('Microsoft YaHei', 14, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
$smallFont = [System.Drawing.Font]::new('Microsoft YaHei', 11, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
$textBrush = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#333932'))
$mutedBrush = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#71796C'))
$labels = @('透明背缘', '纯色山顶', '山顶与山腰衔接', '山腰', '山脚', '现有草地')
try {
    $graphics.Clear([System.Drawing.ColorTranslator]::FromHtml('#F4F3EB'))
    $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $graphics.DrawString('山体 · 细节对比', $titleFont, $textBrush, 24, 12)
    $graphics.DrawString('原版', $labelFont, $mutedBrush, 24, 42)
    $graphics.DrawString('细节版 v2', $labelFont, $textBrush, 160, 42)
    for ($i = 0; $i -lt 6; $i++) {
        $centerY = 68 + $i * 96 + 48
        $graphics.DrawString($labels[$i], $labelFont, $textBrush, 282, $centerY - 15)
        $detail = if ($i -in @(0, 1, 5)) { '保持不变' } else { '补充裂隙与岩层断口' }
        $graphics.DrawString($detail, $smallFont, $mutedBrush, 282, $centerY + 10)
    }
    foreach ($entry in @(@{Image=$old;X=24}, @{Image=$new;X=160})) {
        for ($y = 0; $y -lt 576; $y++) {
            for ($x = 0; $x -lt 96; $x++) {
                $color = $entry.Image.GetPixel([int][Math]::Floor($x / 6), [int][Math]::Floor($y / 6))
                if ($color.A -eq 0) {
                    $hex = if (([int][Math]::Floor($x / 12) + [int][Math]::Floor($y / 12)) % 2 -eq 0) { '#D8D8D3' } else { '#F8F8F4' }
                    $color = [System.Drawing.ColorTranslator]::FromHtml($hex)
                }
                $canvas.SetPixel($entry.X + $x, 68 + $y, $color)
            }
        }
    }
    $canvas.Save((Join-Path $PSScriptRoot 'Mountain-Detail-comparison.png'), [System.Drawing.Imaging.ImageFormat]::Png)
} finally {
    $graphics.Dispose(); $canvas.Dispose(); $old.Dispose(); $new.Dispose()
    $titleFont.Dispose(); $labelFont.Dispose(); $smallFont.Dispose()
    $textBrush.Dispose(); $mutedBrush.Dispose()
}
