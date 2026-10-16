' /********************************************************************************/
'
' Summary:
'
'   Module ImageRender
'
'     Function: RenderGray, RenderIndexed, BuildLut, Render, SavePng
'
'   the intensity matrix of the decoded image is normalized by the percentile
'   based auto levels (instead of the global min/max of the original
'   implementation, which is extremely sensitive to the spike outliers), and
'   then is rendered as the gray scale image or as the pseudo color image.
'
' /********************************************************************************/

Imports System.Drawing
Imports Microsoft.VisualBasic.Imaging.Drawing2D.Colors
Imports Microsoft.VisualBasic.Linq

''' <summary>
''' the rendering parameters of the decoded image
''' </summary>
Public Class RenderOptions

    ''' <summary>
    ''' the black point percentile, 0.005 means 0.5% of the darkest pixels are clipped
    ''' </summary>
    ''' <returns></returns>
    Public Property percentile_low As Double = 0.005

    ''' <summary>
    ''' the white point percentile
    ''' </summary>
    ''' <returns></returns>
    Public Property percentile_high As Double = 0.995

    ''' <summary>
    ''' the gamma correction value, 1.0 for no correction
    ''' </summary>
    ''' <returns></returns>
    Public Property gamma As Double = 1.0

    ''' <summary>
    ''' invert the black/white polarity
    ''' </summary>
    ''' <returns></returns>
    Public Property invert As Boolean = False

    ''' <summary>
    ''' the white point gain, it is a legacy compatible parameter of the R# as.bitmap function
    ''' </summary>
    ''' <returns></returns>
    Public Property white As Double = 1.0

    ''' <summary>
    ''' the palette name of the pseudo color rendering, ``gray`` for the gray scale image
    ''' </summary>
    ''' <returns></returns>
    Public Property palette As String = "gray"

    Public Shared Function CreateDefault() As RenderOptions
        Return New RenderOptions
    End Function

    Public Overrides Function ToString() As String
        Return $"[{percentile_low},{percentile_high}] gamma={gamma} invert={invert} white={white} palette={palette}"
    End Function
End Class

''' <summary>
''' render the decoded intensity matrix as the image file
''' </summary>
Public Module ImageRender

    ''' <summary>
    ''' render an intensity matrix as a row major RGB pixel buffer
    ''' </summary>
    ''' <param name="values">the intensity matrix, the row is the image scan line</param>
    ''' <param name="options"></param>
    Public Function Render(values As Double()(), options As RenderOptions) As Byte()
        Dim height As Integer = values.Length
        Dim width As Integer = values(0).Length
        Dim lut As Color() = BuildLut(options.palette)
        Dim levels As Double() = AutoLevels(values, options)
        Dim pixels As Byte() = New Byte(width * height * 3 - 1) {}
        Dim p As Integer = 0

        For Each row As Double() In values
            For x As Integer = 0 To width - 1
                Dim level As Double = (row(x) - levels(0)) / (levels(1) - levels(0))
                Dim color As Color

                If options.invert Then
                    level = 1.0 - level
                End If

                If level < 0 Then
                    level = 0
                ElseIf level > 1 Then
                    level = 1
                End If

                If options.gamma <> 1.0 Then
                    level = level ^ options.gamma
                End If

                color = lut(CInt(Math.Round(level * (lut.Length - 1))))

                pixels(p) = color.B
                pixels(p + 1) = color.G
                pixels(p + 2) = color.R
                p += 3
            Next
        Next

        Return pixels
    End Function

    ''' <summary>
    ''' render a single precision intensity matrix as a row major RGB pixel buffer
    ''' </summary>
    ''' <param name="values">the intensity matrix, the row is the image scan line</param>
    ''' <param name="options"></param>
    Public Function Render(values As Single()(), options As RenderOptions) As Byte()
        Return Render(ToDouble(values), If(options, RenderOptions.CreateDefault))
    End Function

    ''' <summary>
    ''' render an intensity matrix and save it as a png image file
    ''' </summary>
    Public Sub SavePng(path As String, values As Double()(), options As RenderOptions)
        Dim height As Integer = values.Length
        Dim width As Integer = values(0).Length

        Call PngWriter.Save(path, Render(values, options), width, height, 3)
    End Sub

    ''' <summary>
    ''' render a single precision intensity matrix and save it as a png image file
    ''' </summary>
    Public Sub SavePng(path As String, values As Single()(), options As RenderOptions)
        Dim matrix As Double()() = ToDouble(values)

        Call PngWriter.Save(path, Render(matrix, options), matrix(0).Length, matrix.Length, 3)
    End Sub

    ''' <summary>
    ''' convert the single precision intensity matrix into a double precision matrix
    ''' </summary>
    Public Function ToDouble(values As Single()()) As Double()()
        Dim matrix As Double()() = New Double(values.Length - 1)() {}

        For i As Integer = 0 To values.Length - 1
            Dim row As Single() = values(i)
            Dim result As Double() = New Double(row.Length - 1) {}

            For j As Integer = 0 To row.Length - 1
                result(j) = row(j)
            Next

            matrix(i) = result
        Next

        Return matrix
    End Function

    ''' <summary>
    ''' repack a row major rgb pixel buffer into the BGRA layout of the GDI+ BitmapBuffer
    ''' </summary>
    Public Function ToBgra(rgb As Byte(), width As Integer, height As Integer, alpha As Byte) As Byte()
        Dim bgra As Byte() = New Byte(width * height * 4 - 1) {}
        Dim n As Integer = width * height

        For i As Integer = 0 To n - 1
            bgra(i * 4) = rgb(i * 3 + 2)
            bgra(i * 4 + 1) = rgb(i * 3 + 1)
            bgra(i * 4 + 2) = rgb(i * 3)
            bgra(i * 4 + 3) = alpha
        Next

        Return bgra
    End Function

    ''' <summary>
    ''' the black point and white point of the intensity auto levels
    ''' </summary>
    Public Function AutoLevels(values As Double()(), options As RenderOptions) As Double()
        Dim flat As Double() = values.SelectMany(Function(row) row.AsEnumerable).ToArray
        Dim low As Double = Dsp.Percentile(flat, options.percentile_low)
        Dim high As Double = Dsp.Percentile(flat, options.percentile_high)

        If options.white <> 1.0 AndAlso options.white > 0 Then
            high = low + (high - low) / options.white
        End If

        If high <= low Then
            high = low + 1
        End If

        Return {low, high}
    End Function

    ''' <summary>
    ''' the color lookup table of a given palette name
    ''' </summary>
    ''' <param name="palette">``gray``, ``jet``, ``viridis``, ``hot`` or any palette name which is
    ''' supported by the sciBASIC imaging designer</param>
    Public Function BuildLut(palette As String) As Color()
        If String.IsNullOrEmpty(palette) OrElse palette = "gray" OrElse palette = "grays" Then
            Return GrayLut()
        End If

        Try
            Dim colors As Color() = Designer.GetColors(palette, 256)

            If colors Is Nothing OrElse colors.Length < 2 Then
                Return GrayLut()
            End If

            Return colors
        Catch ex As Exception
            Return GrayLut()
        End Try
    End Function

    Public Function GrayLut() As Color()
        Dim lut As Color() = New Color(255) {}

        For i As Integer = 0 To 255
            lut(i) = Color.FromArgb(i, i, i)
        Next

        Return lut
    End Function
End Module
