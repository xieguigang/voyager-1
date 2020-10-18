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
Imports Microsoft.VisualBasic.Imaging
Imports Microsoft.VisualBasic.Imaging.BitmapImage
Imports Microsoft.VisualBasic.Language
Imports Microsoft.VisualBasic.Linq

Module ImageDecoder

    <Extension>
    Public Iterator Function GetScan(data As Single(), args As DecoderArgument, aligns As List(Of Integer), khzRate As Integer) As IEnumerable(Of Single())
        Dim align As Integer
        Dim index As Integer = 0
        Dim ncols As Integer = Math.Floor(data.Length / args.windowSize)

        For j As Integer = 0 To ncols - 1
            Dim buffer As Single() = New Single(args.windowSize - 1) {}

            Call Array.ConstrainedCopy(data, index, buffer, Scan0, args.windowSize)

            Dim start As Integer = Which.Max(buffer.Take(1000))
            Dim ends As Integer = 2500 + Which.Min(buffer.Skip(2500))

            ' trim buffer
            buffer = buffer.Skip(start).Take(ends - start).ToArray
            align = Math.Floor((ends - start) / khzRate)
            index += ends
            aligns += align

            Yield buffer.pixels(align, khzRate)
        Next
    End Function

    <Extension>
    Private Function pixels(data As Single(), align As Integer, khzRate As Integer) As Single()
        Dim index As i32 = Scan0
        Dim sum As Single() = New Single(khzRate - 1) {}

        For j As Integer = 0 To sum.Length - 1
            For i As Integer = 0 To align - 1
                sum(j) += data(++index)
            Next
        Next

        Return sum
    End Function

    ''' <summary>
    ''' 
    ''' </summary>
    ''' <param name="scans">the pixels data, is conist with multiple column scans.</param>
    ''' <param name="width"></param>
    ''' <param name="aligns"></param>
    ''' <returns></returns>
    Public Function DecodeBitmap(scans As Single()(),
                                 width As Integer,
                                 khzRate As Integer,
                                 aligns As Integer(),
                                 Optional white_threshold As Double = 0.0) As Bitmap
        Dim x As Integer = 0
        Dim y As i32 = Scan0
        Dim c As Color
        Dim alignIndex As i32 = Scan0

        Using img As BitmapBuffer = BitmapBuffer.FromBitmap(New Bitmap(width, khzRate, PixelFormat.Format32bppArgb))
            For Each columnScan As Single() In scans
                Dim align As Integer = aligns(++alignIndex)

                For i As Integer = 0 To columnScan.Length - 1
                    If columnScan(i) >= white_threshold Then
                        c = GDIColors.Greyscale(columnScan(i), align)
                    Else
                        c = Color.White
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

