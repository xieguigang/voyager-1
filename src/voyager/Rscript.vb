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

Imports System.IO
Imports Microsoft.VisualBasic.CommandLine.Reflection
Imports Microsoft.VisualBasic.Data.Wave
Imports Microsoft.VisualBasic.Imaging
Imports Microsoft.VisualBasic.Language
Imports Microsoft.VisualBasic.Scripting.MetaData
Imports Microsoft.VisualBasic.Text
Imports SMRUCC.Rsharp.Runtime
Imports SMRUCC.Rsharp.Runtime.Interop

<Package("goldenRecord")>
<RTypeExport("image.chunk", GetType(ImageChunk))>
<RTypeExport("decode", GetType(DecoderArgument))>
<RTypeExport("decode.report", GetType(DecodeReport))>
Module Rscript

    ''' <summary>
    ''' Decode the wav data as the image pixels.
    ''' 
    ''' the scan line period and the pixel clock are measured from the waveform data
    ''' itself (see the ``decode.report`` function for the empirical analysis), the
    ''' ``offsetLeft``/``offsetRight`` parameters of the original implementation are
    ''' kept only for the backward compatibility of the existing scripts.
    ''' </summary>
    ''' <param name="wav"></param>
    ''' <param name="chunk"></param>
    ''' <param name="decode">the decoder arguments, the invalid values are filled by the measured ones</param>
    ''' <param name="size">the pixel cells (the image rows) per scan line, 0 for the default 364</param>
    ''' <param name="pixels">the alias of the ``size`` parameter, the value of ``size`` wins when both are assigned</param>
    ''' <param name="lines">the number of the scan lines (the image columns), 0 for the default 512</param>
    ''' <param name="align">apply the per scan line synchronization</param>
    ''' <param name="guard">the samples which are skipped after the sync pulse position</param>
    ''' <param name="env"></param>
    <ExportAPI("decode")>
    <RApiReturn(GetType(PixelDecode))>
    Public Function GetImageBuffer(wav As WaveFile, chunk As ImageChunk, decode As DecoderArgument,
                                   Optional size As Integer = 0,
                                   Optional offsetLeft# = 0.2,
                                   Optional offsetRight# = 0.2,
                                   Optional pixels As Integer = 0,
                                   Optional lines As Integer = 0,
                                   Optional align As Boolean = True,
                                   Optional guard# = 0,
                                   Optional env As Environment = Nothing) As Object

        Dim samples As Sample() = wav.data _
            .LoadSamples(chunk.start, chunk.length, scan0:=8) _
            .ToArray
        Dim data As Single() = chunk.GetSampleData(samples)
        Dim args As DecoderArgument = If(decode Is Nothing, New DecoderArgument, decode.Copy)

        ' the legacy ``size`` parameter and the new ``pixels`` parameter are the same
        ' thing: the number of the pixel cells (the image rows) inside a scan line
        If size > 0 Then
            args.pixels = size
        End If

        If pixels > 0 Then
            args.pixels = pixels
        End If

        If lines > 0 Then
            args.lines = lines
        End If

        If guard > 0 Then
            args.guard = guard
        End If

        args.align = align

        Return ImageDecoder.DecodeSection(data, args)
    End Function

    ''' <summary>
    ''' the empirical analysis of an image data section: the scan line period, the
    ''' pixel clock, the brightness encoding scheme, the left/right channel
    ''' relationship and the color component detection result.
    ''' </summary>
    ''' <param name="wav"></param>
    ''' <param name="chunk"></param>
    ''' <param name="env_window">the low pass window of the signal envelope</param>
    ''' <param name="env"></param>
    <ExportAPI("decode.report")>
    <RApiReturn(GetType(DecodeReport))>
    Public Function GetDecodeReport(wav As WaveFile, chunk As ImageChunk,
                                    Optional env_window As Integer = 96,
                                    Optional env As Environment = Nothing) As Object

        Dim samples As Sample() = wav.data _
            .LoadSamples(chunk.start, chunk.length, scan0:=8) _
            .ToArray
        Dim data As Single() = chunk.GetSampleData(samples)

        Return SignalAnalysis.AnalyseFrames(
            raw:={data},
            sampleRate:=wav.fmt.SampleRate,
            source:=chunk.ToString,
            startFrame:=chunk.start,
            lengthFrame:=chunk.length,
            channel:=0,
            envWindow:=env_window
        )
    End Function

    ''' <summary>
    ''' write the image decode intensity data as csv file
    ''' </summary>
    ''' <param name="decode"></param>
    ''' <param name="file"></param>
    ''' <returns></returns>
    <ExportAPI("write_csv")>
    Public Function writeExcel(decode As PixelDecode, file As String) As Object
        Using s As Stream = file.Open(FileMode.OpenOrCreate, doClear:=True)
            Dim table As New StreamWriter(s, Encodings.ASCII.CodePage)

            ' the intensity matrix is written with one scan line per row
            For Each r As Single() In decode.pixels.MatrixTranspose
                Call table.WriteLine(r.JoinBy(","))
            Next

            Call table.Flush()
        End Using

        Return True
    End Function

    ''' <summary>
    ''' render the decoded pixels as a gray scale or a pseudo color bitmap image.
    ''' 
    ''' the intensity data is normalized by the percentile based auto levels, and the
    ''' polarity is inverted by default because the image data of the golden record is
    ''' stored as a photographic negative.
    ''' </summary>
    ''' <param name="pixels"></param>
    ''' <param name="luminous">render as a pseudo color image</param>
    ''' <param name="white">the white point gain, 0 or 1 means no adjustment</param>
    ''' <param name="gamma">the gamma correction, 1 means no correction</param>
    ''' <param name="invert">the black/white polarity</param>
    ''' <param name="percentile">the black point and the white point percentiles, like ``c(0.005, 0.995)``</param>
    ''' <param name="palette">the palette name of the pseudo color rendering</param>
    ''' <param name="smooth">the radius of the across scan line smoothing, 0 for disable</param>
    ''' <param name="height">the output image height in pixels, 0 for keep the original size</param>
    <ExportAPI("as.bitmap")>
    Public Function CreateBitmap(pixels As PixelDecode,
                                 Optional luminous As Boolean = False,
                                 Optional white# = 1,
                                 Optional gamma# = 1,
                                 Optional invert As Boolean = True,
                                 Optional percentile As Double() = Nothing,
                                 Optional palette As String = "gray",
                                 Optional smooth As Integer = 0,
                                 Optional height As Integer = 0) As Bitmap

        Dim options As New RenderOptions With {
            .percentile_low = If(percentile IsNot Nothing AndAlso percentile.Length > 0, percentile(0), 0.005),
            .percentile_high = If(percentile IsNot Nothing AndAlso percentile.Length > 1, percentile(1), 0.995),
            .gamma = gamma,
            .white = If(white <= 0, 1, white),
            .invert = invert,
            .smooth = smooth,
            .palette = If(luminous AndAlso palette = "gray", "jet", palette)
        }
        Dim matrix As Double()() = ImageRender.ToDouble(pixels.pixels)

        ' the optional image up scaling: the golden record images are quite small
        ' (about 512 x 364 pixels), a larger image is easier to look at
        If height > 0 AndAlso matrix.Length <> height Then
            Dim width As Integer = Math.Max(1, CInt(Math.Round(matrix(0).Length * height / matrix.Length)))

            matrix = ImageCompare.Resize(matrix, width, height)
        End If

        Dim w As Integer = matrix(0).Length
        Dim h As Integer = matrix.Length
        Dim rgb As Byte() = ImageRender.Render(matrix, options)

        Return New Bitmap(New Imaging.BitmapImage.BitmapBuffer(ImageRender.ToBgra(rgb, w, h, 255), New System.Drawing.Size(w, h), 4))
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

