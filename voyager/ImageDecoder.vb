Imports System.Drawing
Imports System.Drawing.Imaging
Imports System.Runtime.CompilerServices
Imports Microsoft.VisualBasic.Imaging
Imports Microsoft.VisualBasic.Imaging.BitmapImage
Imports Microsoft.VisualBasic.Language
Imports Microsoft.VisualBasic.Linq

Module ImageDecoder

    Const size As Integer = 370

    <Extension>
    Public Iterator Function GetScan(data As Single(), args As DecoderArgument, aligns As List(Of Integer)) As IEnumerable(Of Single())
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
            align = Math.Floor((ends - start) / size)
            index += ends
            aligns += align

            Yield buffer.pixels(align)
        Next
    End Function

    <Extension>
    Private Function pixels(data As Single(), align As Integer) As Single()
        Dim index As i32 = Scan0
        Dim sum As Single() = New Single(size - 1) {}

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
    Public Function DecodeBitmap(scans As Single()(), width As Integer, aligns As Integer()) As Bitmap
        Dim x As Integer = 0
        Dim y As i32 = Scan0
        Dim c As Color
        Dim alignIndex As i32 = Scan0

        Using img As BitmapBuffer = BitmapBuffer.FromBitmap(New Bitmap(width, size, PixelFormat.Format32bppArgb))
            For Each columnScan As Single() In scans
                Dim align As Integer = aligns(++alignIndex)

                For i As Integer = 0 To columnScan.Length - 1
                    If columnScan(i) >= 0 Then
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
