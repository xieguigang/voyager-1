﻿Imports System.Drawing
Imports System.Drawing.Imaging
Imports System.Runtime.CompilerServices
Imports Microsoft.VisualBasic.Imaging
Imports Microsoft.VisualBasic.Language
Imports Microsoft.VisualBasic.Linq

Module ImageDecoder

    <Extension>
    Public Function GetScan(data As Single(), index As Integer, args As DecoderArgument, ByRef align As Integer) As Single()
        Dim buffer As Single() = New Single(args.windowSize - 1) {}

        Call Array.ConstrainedCopy(data, index, buffer, Scan0, args.windowSize)

        Dim start As Integer = Which.Max(buffer)
        Dim ends As Integer = (buffer.Length \ 2) + Which.Min(buffer.Take(buffer.Length \ 2))

        ' trim buffer
        buffer = buffer.Skip(start).Take(ends - start).ToArray
        align = (ends - start) / 384

        Return buffer.pixels(align)
    End Function

    <Extension>
    Private Function pixels(data As Single(), align As Integer) As Single()
        Dim index As i32 = Scan0
        Dim sum As Single() = New Single(383) {}

        For j As Integer = 0 To 383
            For i As Integer = 0 To 4 - 1
                sum(j) += data(++index)
            Next
        Next

        Return sum
    End Function

    ''' <summary>
    ''' 
    ''' </summary>
    ''' <param name="scan">the pixels data, is conist with multiple column scans.</param>
    ''' <param name="width"></param>
    ''' <param name="align"></param>
    ''' <returns></returns>
    Public Function DecodeBitmap(scan As Single(), width As Integer, align As Integer) As Bitmap
        Dim img As New Bitmap(width, 384, PixelFormat.Format32bppArgb)
        Dim x = 0
        Dim y As i32 = Scan0
        Dim c As Color

        For i As Integer = 0 To scan.Length - 1
            If scan(i) >= 0 Then
                c = GDIColors.Greyscale(scan(i), align)
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
            img.SetPixel(x, ++y, c)
        Next

        Return img
    End Function
End Module
