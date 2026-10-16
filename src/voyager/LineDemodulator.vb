' /********************************************************************************/
'
' Summary:
'
'   Module LineDemodulator
'
'     Function: Demodulate, PixelValue, LineBaseline
'
'   the brightness demodulation of the golden record image data.
'
'   the empirical analysis shows that the image brightness is encoded as the local
'   amplitude of the analog signal (a continuous amplitude modulation), so the
'   intensity of an image pixel cell is the area weighted mean of the rectified
'   signal over the cell samples.
'
'   the pixel cells are sampled with a sub sample precision, and the baseline
'   drift of each scan line is removed before the rectification, those two steps
'   are required for removing the "water wave" like streaks on the decoded image.
'
' /********************************************************************************/

Imports Microsoft.VisualBasic.Linq

''' <summary>
''' the demodulation parameters of the golden record image data
''' </summary>
Public Class DemodulateOptions

    ''' <summary>
    ''' the number of the image pixel cells inside a single scan line (the image rows)
    ''' </summary>
    ''' <returns></returns>
    Public Property pixels As Integer = SignalAnalysis.DefaultPixelCount

    ''' <summary>
    ''' the samples per pixel cell, zero means it is evaluated from the scan line
    ''' period and the measured pixel clock (see <see cref="SignalAnalysis.CellSlotsPerPeriod"/>)
    ''' </summary>
    ''' <returns></returns>
    Public Property pixel_pitch As Double = 0

    ''' <summary>
    ''' the number of the samples which are skipped after the sync pulse position
    ''' </summary>
    ''' <returns></returns>
    Public Property guard As Double = 0

    ''' <summary>
    ''' remove the per scan line baseline drift before the rectification
    ''' </summary>
    ''' <returns></returns>
    Public Property remove_baseline As Boolean = True

    ''' <summary>
    ''' use the rms value instead of the mean of the absolute value of the signal
    ''' </summary>
    ''' <returns></returns>
    Public Property use_rms As Boolean = True

    ''' <summary>
    ''' the length (in samples) of the baseline tracking window, zero for the
    ''' whole scan line mean value
    ''' </summary>
    ''' <returns></returns>
    Public Property baseline_window As Integer = 0
End Class

''' <summary>
''' the brightness demodulation of the golden record scan line image data
''' </summary>
Public Module LineDemodulator

    ''' <summary>
    ''' the measured pixel clock of the golden record image data: the samples per pixel
    ''' cell which is evaluated from the scan line period.
    ''' 
    ''' one scan line period contains <see cref="SignalAnalysis.CellSlotsPerPeriod"/>
    ''' pixel cell slots (the leading blanking/sync region takes some of them), so the
    ''' pixel pitch is NOT ``period / pixels``: using the image pixel number as the cell
    ''' slot count misses the blanking region and scales the decoded image about 10% in
    ''' the vertical direction (the calibration circle is then an ellipse instead of a
    ''' true circle).
    ''' </summary>
    Public Function PixelPitch(period As Double) As Double
        Return period / SignalAnalysis.CellSlotsPerPeriod
    End Function

    ''' <summary>
    ''' demodulate the scan line grid into the image raster.
    ''' 
    ''' the scan line of the analog signal is one image column, so the returned
    ''' matrix is already in the image row major layout: ``matrix(row)(column)``
    ''' </summary>
    ''' <param name="data">the sample data of a single audio channel</param>
    ''' <param name="grid">the scan line time grid</param>
    ''' <param name="options"></param>
    Public Function Demodulate(data As Single(), grid As LineGrid, options As DemodulateOptions) As Single()()
        Dim pitch As Double = options.pixel_pitch

        If pitch <= 0 Then
            pitch = PixelPitch(grid.period)
        End If

        Dim rows As Integer = options.pixels
        Dim cols As Integer = grid.rows.Length
        Dim image As Single()() = New Single(rows - 1)() {}

        For i As Integer = 0 To rows - 1
            image(i) = New Single(cols - 1) {}
        Next

        For c As Integer = 0 To cols - 1
            Dim origin As Double = grid.rows(c).start + grid.sync_offset + options.guard
            Dim baseline As Double = 0

            If options.remove_baseline Then
                baseline = LineBaseline(data, origin, grid.period, options.baseline_window)
            End If

            For r As Integer = 0 To rows - 1
                image(r)(c) = PixelValue(data, origin + r * pitch, pitch, baseline, options.use_rms)
            Next
        Next

        Return image
    End Function

    ''' <summary>
    ''' the area weighted pixel intensity of a pixel cell which is started at the
    ''' given fractional sample position
    ''' </summary>
    Public Function PixelValue(data As Single(), start As Double, pitch As Double,
                               baseline As Double, useRms As Boolean) As Single

        Dim [end] As Double = start + pitch
        Dim i0 As Integer = CInt(Math.Floor(start))
        Dim i1 As Integer = CInt(Math.Floor([end]))
        Dim frac0 As Double = start - i0
        Dim frac1 As Double = [end] - i1
        Dim sum As Double = 0
        Dim weight As Double = 0

        Dim leftWeight As Double = 1 - frac0

        If leftWeight > 0 Then
            Dim v As Double = Sample(data, i0) - baseline

            sum += Math.Abs(v) * leftWeight
            weight += leftWeight
        End If

        For i As Integer = i0 + 1 To i1 - 1
            Dim v As Double = Sample(data, i) - baseline

            If useRms Then
                sum += v * v
            Else
                sum += Math.Abs(v)
            End If

            weight += 1
        Next

        If frac1 > 0 Then
            Dim v As Double = Sample(data, i1) - baseline

            If useRms Then
                sum += v * v * frac1
            Else
                sum += Math.Abs(v) * frac1
            End If

            weight += frac1
        End If

        If weight <= 0 Then
            Return 0
        End If

        Dim value As Double = sum / weight

        Return If(useRms, Math.Sqrt(value), value)
    End Function

    Public Function Sample(data As Single(), i As Integer) As Double
        If i < 0 OrElse i >= data.Length Then
            Return 0
        End If

        Return data(i)
    End Function

    ''' <summary>
    ''' the baseline of a scan line: the mean level of the analog signal on the line.
    ''' 
    ''' the baseline is removed before the amplitude rectification, otherwise the
    ''' baseline drift of the analog recording appears as the streaks on the
    ''' decoded image.
    ''' </summary>
    Public Function LineBaseline(data As Single(), origin As Double, period As Double,
                                 window As Integer) As Double

        If window > 0 Then
            ' the local baseline is not supported by the whole line scan,
            ' the whole line mean value is used instead
            window = 0
        End If

        Dim i0 As Integer = Math.Max(0, CInt(Math.Floor(origin)))
        Dim i1 As Integer = Math.Min(data.Length, CInt(Math.Floor(origin + period)))
        Dim sum As Double = 0
        Dim n As Integer = 0

        For i As Integer = i0 To i1 - 1
            sum += data(i)
            n += 1
        Next

        If n = 0 Then
            Return 0
        End If

        Return sum / n
    End Function
End Module
