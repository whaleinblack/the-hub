param([Parameter(Mandatory=$true)][string]$OutputPath)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$bitmap = New-Object Drawing.Bitmap 64,64
$graphics = [Drawing.Graphics]::FromImage($bitmap)
$font = New-Object Drawing.Font 'Segoe UI',34,([Drawing.FontStyle]::Bold),([Drawing.GraphicsUnit]::Pixel)
$brush = New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(103,221,181))
$format = New-Object Drawing.StringFormat
$stream = New-Object IO.MemoryStream
try {
    $graphics.Clear([Drawing.Color]::FromArgb(18,23,31))
    $graphics.TextRenderingHint = [Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $format.Alignment = [Drawing.StringAlignment]::Center
    $format.LineAlignment = [Drawing.StringAlignment]::Center
    $graphics.DrawString('H',$font,$brush,([Drawing.RectangleF]::new(0,0,64,64)),$format)
    $bitmap.Save($stream,[Drawing.Imaging.ImageFormat]::Png)
    $png = $stream.ToArray()
    $file = [IO.File]::Create($OutputPath)
    $writer = New-Object IO.BinaryWriter $file
    try {
        $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]1)
        $writer.Write([byte]64); $writer.Write([byte]64); $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$png.Length); $writer.Write([uint32]22)
        $writer.Write($png)
    } finally { $writer.Dispose(); $file.Dispose() }
} finally { $stream.Dispose(); $format.Dispose(); $brush.Dispose(); $font.Dispose(); $graphics.Dispose(); $bitmap.Dispose() }
