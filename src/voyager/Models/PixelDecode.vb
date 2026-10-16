' /********************************************************************************/
'
' Summary:
'
'   Class PixelDecode
'
'     Properties: size, aligns, pixels, line_period, pixel_pitch, sync_offset,
'                 line_score, width, height
'
'     the decoded image raster of a golden record image data section.
'
'     the ``pixels`` matrix is the image row major layout: ``pixels(row)(column)``,
'     the row is one scan line of the analog signal and the column is the pixel cell
'     inside the scan line. the ``size`` is the number of the pixel cells inside a
'     single scan line (the value 364 of the original implementation was a wrong
'     guess, the measured value of the golden record is 384).
'
' /********************************************************************************/

Public Class PixelDecode

    ''' <summary>
    ''' the number of the pixel cells inside a single scan line
    ''' </summary>
    ''' <returns></returns>
    Public Property size As Integer = SignalAnalysis.DefaultPixelCount

    ''' <summary>
    ''' the sample length of each scan line, this is the legacy metadata of the
    ''' original implementation and it is kept for the backward compatibility
    ''' </summary>
    ''' <returns></returns>
    Public Property aligns As Integer()

    ''' <summary>
    ''' the image raster matrix in the row major layout: ``pixels(row)(column)``
    ''' </summary>
    ''' <returns></returns>
    Public Property pixels As Single()()

    ''' <summary>
    ''' the image width in pixels (the number of the pixel cells inside a scan line)
    ''' </summary>
    ''' <returns></returns>
    Public Property width As Integer

    ''' <summary>
    ''' the image height in pixels (the number of the scan lines)
    ''' </summary>
    ''' <returns></returns>
    Public Property height As Integer

    ''' <summary>
    ''' the measured scan line period (in samples)
    ''' </summary>
    ''' <returns></returns>
    Public Property line_period As Double

    ''' <summary>
    ''' the measured pixel clock (in samples per pixel)
    ''' </summary>
    ''' <returns></returns>
    Public Property pixel_pitch As Double

    ''' <summary>
    ''' the sample position of the sync pulse inside one scan line period
    ''' </summary>
    ''' <returns></returns>
    Public Property sync_offset As Double

    ''' <summary>
    ''' the normalized auto correlation score of the measured scan line period
    ''' </summary>
    ''' <returns></returns>
    Public Property line_score As Double

    ''' <summary>
    ''' the standard deviation (in samples) of the per scan line alignment shifts,
    ''' it is the residual jitter which is removed by the synchronization
    ''' </summary>
    ''' <returns></returns>
    Public Property jitter_std As Double

    ''' <summary>
    ''' has the per scan line synchronization been applied
    ''' </summary>
    ''' <returns></returns>
    Public Property aligned As Boolean

    ''' <summary>
    ''' the number of the image rows (the scan lines)
    ''' </summary>
    ''' <returns></returns>
    Public ReadOnly Property length As Integer
        Get
            Return pixels.Length
        End Get
    End Property

    Public Overrides Function ToString() As String
        Return $"{width} x {height} pixels, line period {line_period.ToString("F3")} samples"
    End Function
End Class
