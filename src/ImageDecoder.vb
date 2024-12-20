﻿#Region "Microsoft.VisualBasic::b5a8d941be013ae58fbdab3706f8a33f, voyager\ImageDecoder.vb"

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

' Module ImageDecoder
' 
'     Function: DecodeBitmap, GetScan, pixels
' 
' /********************************************************************************/

#End Region

Imports System.Drawing
Imports System.Drawing.Imaging
Imports System.Runtime.CompilerServices
Imports Microsoft.VisualBasic.ComponentModel.Ranges.Model
Imports Microsoft.VisualBasic.Imaging.BitmapImage
Imports Microsoft.VisualBasic.Imaging.Drawing2D.Colors
Imports Microsoft.VisualBasic.Language
Imports Microsoft.VisualBasic.Linq

#If NET8_0_OR_GREATER Then
Imports Bitmap = Microsoft.VisualBasic.Imaging.Bitmap
#Else
#End If

Module ImageDecoder

    ''' <summary>
    ''' demodulation of a image from a given analog signal data
    ''' </summary>
    ''' <param name="data">a given analog signal data, a wave audio data</param>
    ''' <param name="args"></param>
    ''' <param name="aligns"></param>
    ''' <param name="khzRate"></param>
    ''' <param name="offsetLeft"></param>
    ''' <param name="offsetRight"></param>
    ''' <returns>the matrix of pixel gray scale intensity data of the result image</returns>
    <Extension>
    Public Iterator Function GetScan(data As Single(), args As DecoderArgument, aligns As List(Of Integer),
                                     khzRate As Integer,
                                     Optional offsetLeft# = 0.2,
                                     Optional offsetRight# = 0.2) As IEnumerable(Of Single())
        Dim align As Integer
        Dim index As Integer = 0
        Dim ncols As Integer = Math.Floor(data.Length / args.windowSize)
        Dim buffer As Single()
        Dim start, ends As Integer
        Dim startOffset As Integer = args.windowSize * offsetLeft
        Dim endOffset As Integer = (args.windowSize - args.windowSize * offsetRight)

        For j As Integer = 0 To ncols - 1
            buffer = New Single(args.windowSize - 1) {}

            Call Array.ConstrainedCopy(data, index, buffer, Scan0, args.windowSize)

            start = which.Max(buffer.Take(startOffset))
            ends = endOffset + which.Min(buffer.Skip(endOffset))

            ' trim buffer
            buffer = buffer.Skip(start).Take(ends - start).ToArray
            align = Math.Floor((ends - start) / khzRate)
            index += ends
            aligns += align

            Yield buffer.pixels(align, khzRate)
        Next
    End Function

    ''' <summary>
    ''' analog signal demodulation of a scan line
    ''' </summary>
    ''' <param name="data">analog signal data of a scan line</param>
    ''' <param name="align"></param>
    ''' <param name="khzRate">the image width</param>
    ''' <returns>pixels signal intensity data on a scan line for decode the image</returns>
    <Extension>
    Private Function pixels(data As Single(), align As Integer, khzRate As Integer) As Single()
        Dim index As i32 = Scan0
        Dim sum As Single() = New Single(khzRate - 1) {}

        For j As Integer = 0 To sum.Length - 1
            ' sum the chunk data as pixel intensity value
            ' chunk size is the [align] size
            For i As Integer = 0 To align - 1
                sum(j) += data(++index)
            Next
        Next

        ' pixel data of a scan line
        Return sum
    End Function

    ''' <summary>
    ''' mapping the data to gray scale for display as image
    ''' </summary>
    ''' <param name="scans">the pixels data, is conist with multiple column scans.</param>
    ''' <param name="width"></param>
    ''' <returns></returns>
    Public Function DecodeBitmap(scans As Single()(), width As Integer, khzRate As Integer, Optional luminous As Boolean = False) As Bitmap
        Dim x As Integer = 0
        Dim y As i32 = Scan0
        Dim c As Color
        Dim alignIndex As i32 = Scan0
        Dim globalMax As Single = Aggregate col As Single()
                                  In scans
                                  Let colMax As Single = col.Max
                                  Into Max(colMax)
        Dim globalMin As Single = Aggregate col As Single()
                                  In scans
                                  Let colMin As Single = col.Min
                                  Into Min(colMin)
        ' get global range of the scan data 
        ' for make transform of the signal intensity data
        ' to gray scale data
        Dim globalRange As New DoubleRange(globalMin, globalMax)
        Dim alphaRange As DoubleRange = New Double() {0, 255}
        Dim grayAlpha As Integer
        Dim memory As Bitmap
        Dim grays As Color() = Designer.GetColors("gray", alphaRange.Max)

#If NET48 Then
        memory = New Bitmap(width, khzRate, PixelFormat.Format32bppArgb)
#Else
        memory = New Bitmap(width, khzRate)
#End If

        Using img As BitmapBuffer = BitmapBuffer.FromBitmap(memory)
            For Each columnScan As Single() In scans
                For i As Integer = 0 To columnScan.Length - 1
                    If luminous Then
                        c = Color.FromArgb(CInt(globalRange.ScaleMapping(columnScan(i), New Double() {0, 20000})))
                    Else
                        grayAlpha = globalRange.ScaleMapping(columnScan(i), alphaRange)
                        grayAlpha = grays.Length - grayAlpha

                        If grayAlpha < 0 Then
                            grayAlpha = 0
                        ElseIf grayAlpha > grays.Length - 1 Then
                            grayAlpha = grays.Length - 1
                        End If

                        c = grays(grayAlpha)
                    End If

                    If y > img.Height - 1 Then
                        y = 0
                        x += 1
                    End If

                    If x > img.Width - 1 Then
                        x = 0
                    End If

                    ' the data is a column scan
                    Call img.SetPixel(x, ++y, c)
                Next
            Next

            Return img.GetImage
        End Using
    End Function
End Module

