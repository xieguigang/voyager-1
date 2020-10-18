Imports System.Drawing
Imports Microsoft.VisualBasic.CommandLine.Reflection
Imports Microsoft.VisualBasic.Data.Wave
Imports Microsoft.VisualBasic.Scripting.MetaData
Imports SMRUCC.Rsharp.Runtime
Imports SMRUCC.Rsharp.Runtime.Interop

<Package("voyager1")>
<RTypeExport("image.chunk", GetType(ImageChunk))>
<RTypeExport("decode", GetType(DecoderArgument))>
Module Rscript

    <ExportAPI("decode")>
    Public Function GetImage(wav As WaveFile, chunk As ImageChunk, decode As DecoderArgument, Optional env As Environment = Nothing) As Object
        Dim samples = wav.data.LoadSamples(chunk.start, chunk.length).ToArray
        Dim data As Single() = chunk.GetSampleData(samples).PreProcessing
        Dim align As Integer = 0
        Dim pixelScan As Single() = ImageDecoder.GetScan(data, 0, decode, align)
        Dim bitmap As Bitmap = ImageDecoder.DecodeBitmap(pixelScan, 300, align)

        Return bitmap
    End Function
End Module
