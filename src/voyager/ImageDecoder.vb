' /********************************************************************************/
'
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
'
:
' /********************************************************************************/
'
' Summary:
'
' Module ImageDecoder
'
'     Function: DecodeSection, GetScan, pixels, DecodeBitmap, DecodeReport
'
' the image decoding pipeline of the golden record:
'
' ```
' wave samples -> line sync (LineSync) -> brightness demodulation (LineDemodulator)
'              -> intensity matrix -> auto levels rendering (ImageRender)
' ```
'
' the original implementation decoded the image data by cutting a fixed 3400 sample
' window and treating the maximum value of the window start section as the scan line
' start position. that approach accumulates the alignment error along the scan lines
' and the image is smeared. the new implementation measures the scan line period and
' the pixel clock from the waveform data itself (see the ``SignalAnalysis`` module)
' and then demodulates the image on the exact scan line time grid.
'
' /********************************************************************************/

Imports System.Drawing
Imports System.Runtime.CompilerServices
Imports Microsoft.VisualBasic.Imaging
Imports Microsoft.VisualBasic.Linq
Imports Microsoft.VisualBasic.Math.SignalProcessing
Imports Microsoft.VisualBasic.Math.SignalProcessing.Waves

Public Module ImageDecoder

    ''' <summary>
    ''' decode an image data section of the golden record wave file
    ''' </summary>
    ''' <param name="data">the sample data of a single audio channel</param>
    ''' <param name="args">the decoder parameters, the scan line period and the pixel
    ''' clock are measured from the waveform data automatically when they are not assigned</param>
    Public Function DecodeSection(data As Single(), args As DecoderArgument) As PixelDecode
        If args Is Nothing Then
            args = New DecoderArgument
        End If

        Dim expectedLines As Integer = If(args.lines > 0, args.lines, 0)
        Dim grid As LineGrid = ScanLineSync.Sync(
            data:=data,
            lines:=expectedLines,
            period:=args.period,
            align:=args.align,
            alignWindow:=args.align_window
        )

        If args.lines > 0 AndAlso grid.count > args.lines Then
            ' trim the grid into the desired number of the scan lines
            Dim rows As LineRow() = grid.rows.Take(args.lines).ToArray

            grid = New LineGrid With {
                .period = grid.period,
                .score = grid.score,
                .sync_offset = grid.sync_offset,
                .count = rows.Length,
                .rows = rows,
                .jitter_std = grid.jitter_std,
                .align_score = grid.align_score,
                .aligned = grid.aligned
            }
        End If

        Dim options As New DemodulateOptions With {
            .pixels = args.pixels,
            .pixel_pitch = args.pixel_pitch,
            .slots_per_period = GoldenRecord.CellSlotsPerPeriod,
            .guard = args.guard,
            .remove_baseline = args.remove_baseline,
            .mode = If(args.use_rms, DemodulationMode.Rms, args.mode),
            .use_rms = args.use_rms,
            .baseline_window = args.baseline_window
        }
        Dim image As Single()() = CellDemodulator.Demodulate(data, grid, options)

        Return New PixelDecode With {
            .pixels = image,
            .size = args.pixels,
            .width = grid.count,
            .height = args.pixels,
            .line_period = grid.period,
            .pixel_pitch = If(options.pixel_pitch > 0, options.pixel_pitch, CellDemodulator.PixelPitch(grid.period, GoldenRecord.CellSlotsPerPeriod)),
            .sync_offset = grid.sync_offset,
            .line_score = grid.score,
            .jitter_std = grid.jitter_std,
            .aligned = grid.aligned
        }
    End Function

    ''' <summary>
    ''' demodulation of a image from a given analog signal data
    ''' </summary>
    ''' <param name="data">a given analog signal data, a wave audio data</param>
    ''' <param name="args"></param>
    ''' <param name="aligns"></param>
    ''' <param name="khzRate">the number of the pixel cells inside a single scan line</param>
    ''' <param name="offsetLeft"></param>
    ''' <param name="offsetRight"></param>
    ''' <returns>the matrix of pixel gray scale intensity data of the result image</returns>
    <Extension>
    Public Iterator Function GetScan(data As Single(), args As DecoderArgument, aligns As List(Of Integer),
                                     khzRate As Integer,
                                     Optional offsetLeft# = 0.2,
                                     Optional offsetRight# = 0.2) As IEnumerable(Of Single())

        ' the legacy api is kept for compatibility: the returned sequence contains
        ' the scan lines of the image, and the align list contains the measured
        ' scan line period of each scan line
        If args Is Nothing Then
            args = New DecoderArgument
        End If

        Dim decodeArgs As DecoderArgument = args.Copy

        If khzRate > 0 Then
            decodeArgs.pixels = khzRate
        End If

        Dim image As PixelDecode = DecodeSection(data, decodeArgs)

        For Each row As Single() In image.pixels
            Call aligns.Add(decodeArgs.pixels)

            Yield row
        Next
    End Function

    ''' <summary>
    ''' analog signal demodulation of a scan line
    ''' </summary>
    ''' <param name="data">analog signal data of a scan line</param>
    ''' <param name="align">the samples per pixel</param>
    ''' <param name="khzRate">the image width</param>
    ''' <returns>pixels signal intensity data on a scan line for decode the image</returns>
    <Extension>
    Private Function pixels(data As Single(), align As Integer, khzRate As Integer) As Single()
        Dim index As Single() = New Single(khzRate - 1) {}

        For j As Integer = 0 To khzRate - 1
            index(j) = CellDemodulator.PixelValue(data, j * align, align, 0, DemodulationMode.Mean)
        Next

        Return index
    End Function

    ''' <summary>
    ''' mapping the data to gray scale for display as image
    ''' 
    ''' the pixel data is rendered by the percentile based auto levels, the global
    ''' min/max normalization of the original implementation is replaced because a
    ''' single spike outlier would compress the whole image into a flat gray.
    ''' </summary>
    ''' <param name="scans">the pixels data, each row is a scan line of the image</param>
    ''' <param name="width"></param>
    ''' <param name="khzRate"></param>
    ''' <param name="luminous"></param>
    Public Function DecodeBitmap(scans As Single()(), width As Integer, khzRate As Integer,
                                 Optional luminous As Boolean = False) As Bitmap

        Dim options As RenderOptions = RenderOptions.CreateDefault

        options.percentile_low = 0.01
        options.percentile_high = 0.99

        ' the image data of the golden record is stored as a photographic negative:
        ' the ink of the picture is a negative amplitude excursion
        options.invert = True

        Dim rgb As Byte() = ImageRender.Render(scans, options)
        Dim height As Integer = scans.Length
        Dim pixelsWide As Integer = scans(0).Length
        Dim buffer As Byte() = ImageRender.ToBgra(rgb, pixelsWide, height, 255)

        Return New Bitmap(New Imaging.BitmapImage.BitmapBuffer(buffer, New Size(pixelsWide, height), 4))
    End Function

    ''' <summary>
    ''' render the decoded image into a gray scale or a pseudo color bitmap
    ''' </summary>
    Public Function Render(pixels As PixelDecode, options As RenderOptions) As Byte()
        Return ImageRender.Render(pixels.pixels, options)
    End Function
End Module
