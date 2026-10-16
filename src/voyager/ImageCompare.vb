' /********************************************************************************/
'
' Summary:
'
'   Module ImageCompare
'
'     Function: Resize, Normalize, Correlation, BestOrientation, BoundingBox, Profiles
'
'   the objective comparison between the decoded image and the official reference
'   image, it is used for determine the correct raster orientation (the scan line
'   is the image row or the image column) and for measuring the decoding quality.
'
' /********************************************************************************/

''' <summary>
''' the result of the orientation matching
''' </summary>
Public Class OrientationTest

    Public Property candidate_size As String
    Public Property reference_size As String

    ''' <summary>
    ''' the pearson correlation of the direct orientation
    ''' </summary>
    ''' <returns></returns>
    Public Property direct As Double

    ''' <summary>
    ''' the pearson correlation of the transposed orientation
    ''' </summary>
    ''' <returns></returns>
    Public Property transposed As Double

    ''' <summary>
    ''' the correlation of the high pass residuals of the two images in the best
    ''' orientation.
    ''' 
    ''' the low frequency correlation is dominated by the background luminance and the
    ''' framing offset, so it can not tell the ink polarity. this value only keeps the
    ''' fine details (the ink) of the image and it is polarity sensitive: a negative
    ''' value means the black/white polarity of the two images is inverted.
    ''' </summary>
    ''' <returns></returns>
    Public Property ink As Double

    Public ReadOnly Property best As String
        Get
            ' the comparison must use the absolute value: the sign of the correlation is
            ' the polarity information and not the orientation information
            If Math.Abs(direct) >= Math.Abs(transposed) Then
                Return "direct"
            Else
                Return "transposed"
            End If
        End Get
    End Property

    Public Overrides Function ToString() As String
        Return $"candidate {candidate_size} vs reference {reference_size}: direct={direct.ToString("F4")}, transposed={transposed.ToString("F4")}, ink={ink.ToString("F4")} => {best}"
    End Function
End Class

Public Module ImageCompare

    ''' <summary>
    ''' bilinear resample of an image matrix
    ''' </summary>
    Public Function Resize(image As Double()(), width As Integer, height As Integer) As Double()()
        Dim rows As Integer = image.Length
        Dim cols As Integer = image(0).Length
        Dim result As Double()() = New Double(height - 1)() {}

        For y As Integer = 0 To height - 1
            Dim row As Double() = New Double(width - 1) {}
            Dim sy As Double = (y + 0.5) * rows / height - 0.5

            For x As Integer = 0 To width - 1
                Dim sx As Double = (x + 0.5) * cols / width - 0.5

                row(x) = Sample(image, sx, sy)
            Next

            result(y) = row
        Next

        Return result
    End Function

    Private Function Sample(image As Double()(), x As Double, y As Double) As Double
        Dim x0 As Integer = CInt(Math.Floor(x))
        Dim y0 As Integer = CInt(Math.Floor(y))
        Dim fx As Double = x - x0
        Dim fy As Double = y - y0

        x0 = Math.Max(0, Math.Min(image(0).Length - 1, x0))
        y0 = Math.Max(0, Math.Min(image.Length - 1, y0))

        Dim x1 As Integer = Math.Min(image(0).Length - 1, x0 + 1)
        Dim y1 As Integer = Math.Min(image.Length - 1, y0 + 1)
        Dim v00 As Double = image(y0)(x0)
        Dim v10 As Double = image(y0)(x1)
        Dim v01 As Double = image(y1)(x0)
        Dim v11 As Double = image(y1)(x1)

        Return (v00 * (1 - fx) + v10 * fx) * (1 - fy) + (v01 * (1 - fx) + v11 * fx) * fy
    End Function

    ''' <summary>
    ''' transpose an image matrix
    ''' </summary>
    Public Function Transpose(image As Double()()) As Double()()
        Dim rows As Integer = image.Length
        Dim cols As Integer = image(0).Length
        Dim result As Double()() = New Double(cols - 1)() {}

        For c As Integer = 0 To cols - 1
            Dim row As Double() = New Double(rows - 1) {}

            For r As Integer = 0 To rows - 1
                row(r) = image(r)(c)
            Next

            result(c) = row
        Next

        Return result
    End Function

    ''' <summary>
    ''' the high pass residual of an image matrix: the value minus its local mean,
    ''' this filter keeps the ink and the fine details of the image only
    ''' </summary>
    Public Function HighPass(image As Double()(), radius As Integer) As Double()()
        Dim rows As Integer = image.Length
        Dim cols As Integer = image(0).Length
        Dim result As Double()() = New Double(rows - 1)() {}

        For y As Integer = 0 To rows - 1
            Dim row As Double() = New Double(cols - 1) {}
            Dim y0 As Integer = Math.Max(0, y - radius)
            Dim y1 As Integer = Math.Min(rows - 1, y + radius)

            For x As Integer = 0 To cols - 1
                Dim x0 As Integer = Math.Max(0, x - radius)
                Dim x1 As Integer = Math.Min(cols - 1, x + radius)
                Dim sum As Double = 0
                Dim n As Integer = 0

                For yy As Integer = y0 To y1
                    For xx As Integer = x0 To x1
                        sum += image(yy)(xx)
                        n += 1
                    Next
                Next

                row(x) = image(y)(x) - sum / n
            Next

            result(y) = row
        Next

        Return result
    End Function

    ''' <summary>
    ''' the pearson correlation coefficient of two matrix with the same shape
    ''' </summary>
    Public Function Correlation(a As Double()(), b As Double()()) As Double
        Dim n As Double = 0
        Dim ma As Double = 0
        Dim mb As Double = 0

        For y As Integer = 0 To a.Length - 1
            For x As Integer = 0 To a(y).Length - 1
                ma += a(y)(x)
                mb += b(y)(x)
                n += 1
            Next
        Next

        ma /= n
        mb /= n

        Dim cab As Double = 0
        Dim ca As Double = 0
        Dim cb As Double = 0

        For y As Integer = 0 To a.Length - 1
            For x As Integer = 0 To a(y).Length - 1
                Dim da As Double = a(y)(x) - ma
                Dim db As Double = b(y)(x) - mb

                cab += da * db
                ca += da * da
                cb += db * db
            Next
        Next

        If ca = 0 OrElse cb = 0 Then
            Return 0
        End If

        Return cab / Math.Sqrt(ca * cb)
    End Function

    ''' <summary>
    ''' match the orientation of the decoded image against the official reference image
    ''' </summary>
    Public Function OrientationTest(candidate As Double()(), reference As Double()()) As OrientationTest
        Dim rows As Integer = candidate.Length
        Dim cols As Integer = candidate(0).Length
        Dim test As New OrientationTest With {
            .candidate_size = $"{cols} x {rows}",
            .reference_size = $"{reference(0).Length} x {reference.Length}"
        }

        Dim direct As Double()() = Resize(candidate, cols, rows)
        Dim resizedReference As Double()() = Resize(reference, cols, rows)
        Dim transposed As Double()() = Resize(Transpose(candidate), reference(0).Length, reference.Length)
        Dim referenceDirect As Double()() = Resize(reference, reference(0).Length, reference.Length)

        test.direct = Correlation(direct, resizedReference)
        test.transposed = Correlation(transposed, referenceDirect)

        ' the polarity of the ink is checked on the high pass residual of the image
        If test.best = "direct" Then
            test.ink = Correlation(HighPass(direct, 2), HighPass(resizedReference, 2))
        Else
            test.ink = Correlation(HighPass(transposed, 2), HighPass(referenceDirect, 2))
        End If

        Return test
    End Function

    ''' <summary>
    ''' the bounding box of the pixels which are brighter than the given threshold
    ''' </summary>
    Public Function BoundingBox(image As Double()(), threshold As Double) As Integer()
        Dim minX As Integer = Integer.MaxValue
        Dim maxX As Integer = Integer.MinValue
        Dim minY As Integer = Integer.MaxValue
        Dim maxY As Integer = Integer.MinValue

        For y As Integer = 0 To image.Length - 1
            For x As Integer = 0 To image(y).Length - 1
                If image(y)(x) >= threshold Then
                    If x < minX Then minX = x
                    If x > maxX Then maxX = x
                    If y < minY Then minY = y
                    If y > maxY Then maxY = y
                End If
            Next
        Next

        If minX > maxX Then
            Return {0, 0, 0, 0}
        End If

        Return {minX, minY, maxX - minX + 1, maxY - minY + 1}
    End Function

    ''' <summary>
    ''' the mean intensity profile along the x axis and the y axis
    ''' </summary>
    Public Function Profiles(image As Double()(), ByRef columns As Double(), ByRef rows As Double()) As Boolean
        Dim height As Integer = image.Length
        Dim width As Integer = image(0).Length

        columns = New Double(width - 1) {}
        rows = New Double(height - 1) {}

        For y As Integer = 0 To height - 1
            For x As Integer = 0 To width - 1
                columns(x) += image(y)(x)
                rows(y) += image(y)(x)
            Next
        Next

        For x As Integer = 0 To width - 1
            columns(x) /= height
        Next

        For y As Integer = 0 To height - 1
            rows(y) /= width
        Next

        Return True
    End Function
End Module
