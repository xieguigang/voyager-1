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

