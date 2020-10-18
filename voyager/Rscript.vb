Imports System.Drawing
Imports Microsoft.VisualBasic.CommandLine.Reflection
Imports Microsoft.VisualBasic.Data.Wave
Imports Microsoft.VisualBasic.Language
Imports Microsoft.VisualBasic.Scripting.MetaData
Imports SMRUCC.Rsharp.Runtime
Imports SMRUCC.Rsharp.Runtime.Interop

<Package("voyager1")>
<RTypeExport("image.chunk", GetType(ImageChunk))>
<RTypeExport("decode", GetType(DecoderArgument))>
Module Rscript

    <ExportAPI("decode")>
    Public Function GetImage(wav As WaveFile, chunk As ImageChunk, decode As DecoderArgument, Optional env As Environment = Nothing) As Object
        Dim samples = wav.data.LoadSamples(chunk.start, chunk.length, scan0:=8).ToArray
        Dim data As Single() = chunk.GetSampleData(samples).PreProcessing
        Dim aligns As New List(Of Integer)
        Dim khz As Integer = wav.fmt.SampleRate / 1000
        Dim pixelScan As Single()() = ImageDecoder.GetScan(data, decode, aligns, khzRate:=khz).ToArray
        Dim bitmap As Bitmap = ImageDecoder.DecodeBitmap(pixelScan, pixelScan.Length, khz, aligns)

        Return bitmap
    End Function
End Module
