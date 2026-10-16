#Region "Microsoft.VisualBasic::f78e442cf8dabd15104d446fc3babb62, voyager\Models\DecoderArgument.vb"

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

    ' Class DecoderArgument
    ' 
    '     Properties: windowSize, offset, lines, period, pixels, pixel_pitch,
    '                 guard, remove_baseline, use_rms, baseline_window
    ' 
    '     Function: Copy, ToString
    ' 
    ' /********************************************************************************/

#End Region

Imports Microsoft.VisualBasic.Serialization.JSON

''' <summary>
''' the parameters of the golden record image decoder.
''' 
''' the ``windowSize`` and the ``offset`` are the legacy parameters of the original
''' implementation which cut a fixed sample window for locating the scan line start.
''' the new decoding pipeline measures the scan line period and the pixel clock from
''' the waveform data itself, so those two parameters are kept only for the backward
''' compatibility of the existing R# scripts and they are no longer used.
''' </summary>
Public Class DecoderArgument

    ''' <summary>
    ''' (legacy) the fixed sample window size of the original scan line locator
    ''' </summary>
    ''' <returns></returns>
    Public Property windowSize As Integer

    ''' <summary>
    ''' (legacy) the sync pulse offset of the original scan line locator
    ''' </summary>
    ''' <returns></returns>
    Public Property offset As Integer

    ''' <summary>
    ''' the expected number of the scan lines of the image, zero for auto detect
    ''' </summary>
    ''' <returns></returns>
    Public Property lines As Integer = 0

    ''' <summary>
    ''' the scan line period (in samples), zero for auto detect and refine
    ''' </summary>
    ''' <returns></returns>
    Public Property period As Double = 0

    ''' <summary>
    ''' the number of the pixel cells inside a single scan line
    ''' </summary>
    ''' <returns></returns>
    Public Property pixels As Integer = SignalAnalysis.DefaultPixelCount

    ''' <summary>
    ''' the samples per pixel cell, zero means it is evaluated from the scan line period
    ''' </summary>
    ''' <returns></returns>
    Public Property pixel_pitch As Double = 0

    ''' <summary>
    ''' the number of the samples which are skipped after the sync pulse position
    ''' </summary>
    ''' <returns></returns>
    Public Property guard As Double = 0

    ''' <summary>
    ''' remove the per scan line baseline drift before the amplitude rectification
    ''' </summary>
    ''' <returns></returns>
    Public Property remove_baseline As Boolean = True

    ''' <summary>
    ''' use the rms value instead of the mean of the absolute value of the signal
    ''' </summary>
    ''' <returns></returns>
    Public Property use_rms As Boolean = True

    ''' <summary>
    ''' the length (in samples) of the baseline tracking window, zero for the whole
    ''' scan line mean value
    ''' </summary>
    ''' <returns></returns>
    Public Property baseline_window As Integer = 0

    ''' <summary>
    ''' apply the per scan line synchronization for removing the residual jitter
    ''' of the analog recording
    ''' </summary>
    ''' <returns></returns>
    Public Property align As Boolean = True

    ''' <summary>
    ''' the maximal per scan line alignment shift (in samples)
    ''' </summary>
    ''' <returns></returns>
    Public Property align_window As Integer = 48

    ''' <summary>
    ''' make a shallow copy of current decoder arguments
    ''' </summary>
    Public Function Copy() As DecoderArgument
        Return New DecoderArgument With {
            .windowSize = windowSize,
            .offset = offset,
            .lines = lines,
            .period = period,
            .pixels = pixels,
            .pixel_pitch = pixel_pitch,
            .guard = guard,
            .remove_baseline = remove_baseline,
            .use_rms = use_rms,
            .baseline_window = baseline_window,
            .align = align,
            .align_window = align_window
        }
    End Function

    Public Overrides Function ToString() As String
        Return Me.GetJson
    End Function

End Class

