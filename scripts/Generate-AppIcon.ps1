param(
    [Parameter(Mandatory = $true)]
    [string] $SourcePng,

    [Parameter(Mandatory = $true)]
    [string] $DestinationIco
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$source = [System.Drawing.Image]::FromFile((Resolve-Path -LiteralPath $SourcePng))
$images = [System.Collections.Generic.List[byte[]]]::new()

try {
    foreach ($size in $sizes) {
        $bitmap = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
            try {
                $graphics.Clear([System.Drawing.Color]::Transparent)
                $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
                $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
                $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
                $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
                $graphics.DrawImage($source, 0, 0, $size, $size)
            }
            finally {
                $graphics.Dispose()
            }

            $stream = [System.IO.MemoryStream]::new()
            try {
                $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
                $images.Add($stream.ToArray())
            }
            finally {
                $stream.Dispose()
            }
        }
        finally {
            $bitmap.Dispose()
        }
    }
}
finally {
    $source.Dispose()
}

$destinationDirectory = Split-Path -Parent $DestinationIco
if ($destinationDirectory) {
    [System.IO.Directory]::CreateDirectory($destinationDirectory) | Out-Null
}

$file = [System.IO.File]::Create($DestinationIco)
$writer = [System.IO.BinaryWriter]::new($file)
try {
    $writer.Write([UInt16] 0)
    $writer.Write([UInt16] 1)
    $writer.Write([UInt16] $sizes.Count)

    $offset = 6 + (16 * $sizes.Count)
    for ($index = 0; $index -lt $sizes.Count; $index++) {
        $sizeByte = if ($sizes[$index] -eq 256) { 0 } else { $sizes[$index] }
        $writer.Write([Byte] $sizeByte)
        $writer.Write([Byte] $sizeByte)
        $writer.Write([Byte] 0)
        $writer.Write([Byte] 0)
        $writer.Write([UInt16] 1)
        $writer.Write([UInt16] 32)
        $writer.Write([UInt32] $images[$index].Length)
        $writer.Write([UInt32] $offset)
        $offset += $images[$index].Length
    }

    foreach ($image in $images) {
        $writer.Write($image)
    }
}
finally {
    $writer.Dispose()
    $file.Dispose()
}
