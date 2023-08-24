#Region "Microsoft.VisualBasic::9cc2f28f75dffab1478db945fd35536f, voyager\Rscript.vb"

' Author:
' 
'       xieguigang (i@xieguigang.me)
' 
' Copyright (c) 2020 i@xieguigang.me
' 
' 
' MIT License
' 
' Permission is hereby granted, free of charge, to any person obtaining a copy
' of this software and associated documentation files (the "Software"), to deal
' in the Software without restriction, including without limitation the rights
' to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
' copies of the Software, and to permit persons to whom the Software is
' furnished to do so, subject to the following conditions:
' 
' The above copyright notice and this permission notice shall be included in all
' copies or substantial portions of the Software.
' 
' THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
' IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
' FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
' AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
' LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
' OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
' SOFTWARE.



' /********************************************************************************/

' Summaries:

' Module Rscript
' 
'     Function: GetImage
' 
' /********************************************************************************/

#End Region

Imports System.Drawing
Imports Microsoft.VisualBasic.CommandLine.Reflection
Imports Microsoft.VisualBasic.Data.Wave
Imports Microsoft.VisualBasic.Language
Imports Microsoft.VisualBasic.Scripting.MetaData
Imports SMRUCC.Rsharp.Runtime
Imports SMRUCC.Rsharp.Runtime.Interop

<Package("goldenRecord")>
<RTypeExport("image.chunk", GetType(ImageChunk))>
<RTypeExport("decode", GetType(DecoderArgument))>
Module Rscript

    ''' <summary>
    ''' Decode the wav data as the pixel scans
    ''' </summary>
    ''' <param name="wav"></param>
    ''' <param name="chunk"></param>
    ''' <param name="decode"></param>
    ''' <param name="size"></param>
    ''' <param name="env"></param>
    ''' <returns></returns>
    <ExportAPI("decode")>
    <RApiReturn(GetType(PixelDecode))>
    Public Function GetImageBuffer(wav As WaveFile, chunk As ImageChunk, decode As DecoderArgument,
                                   Optional size As Integer = 364,
                                   Optional offsetLeft# = 0.2,
                                   Optional offsetRight# = 0.2,
                                   Optional env As Environment = Nothing) As Object

        Dim samples As Sample() = wav.data _
            .LoadSamples(chunk.start, chunk.length, scan0:=8) _
            .ToArray
        Dim data As Single() = chunk.GetSampleData(samples).PreProcessing
        Dim aligns As New List(Of Integer)
        Dim pixelScan As Single()() = ImageDecoder.GetScan(
            data:=data,
            args:=decode,
            aligns:=aligns,
            khzRate:=size,
            offsetLeft:=offsetLeft,
            offsetRight:=offsetRight
        ).ToArray

        Return New PixelDecode With {
            .pixels = pixelScan,
            .aligns = aligns.ToArray,
            .size = size
        }
    End Function

    <ExportAPI("as.bitmap")>
    Public Function CreateBitmap(pixels As PixelDecode, Optional luminous As Boolean = False) As Bitmap
        Return ImageDecoder.DecodeBitmap(
            scans:=pixels.pixels,
            width:=pixels.length,
            khzRate:=pixels.size,
            luminous:=luminous
        )
    End Function

    ''' <summary>
    ''' measure the data chunk size of the wav data for current image chunk
    ''' </summary>
    ''' <param name="wav"></param>
    ''' <param name="chunk"></param>
    ''' <returns></returns>
    <ExportAPI("chunk_size")>
    Public Function ChunkSize(wav As WaveFile, chunk As ImageChunk) As Long
        Return DirectCast(wav.data, LazyDataChunk).MeasureChunkSize(chunk.length)
    End Function
End Module

