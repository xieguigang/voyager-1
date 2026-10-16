' /********************************************************************************/
'
' Summary:
'
'   Module Probe
'
'     Function: Oscillogram, Fold, FoldPixel
'
'   the debug helpers for make the raw waveform structure visible, they are used
'   by the ``--probe`` mode of the command line tool:
' 
'   1. `Oscillogram`: the classic amplitude waveform view of a sample block
'   2. `Fold`: fold the signal at the estimated scan line period, this makes the
'      scan line structure of the golden record image data directly visible
'   3. `FoldPixel`: fold the signal and decode the pixel cells, this gives a first
'      fast preview of the decoded image
'
' /********************************************************************************/

Imports Microsoft.VisualBasic.Linq

''' <summary>
''' the debug visualization helpers of the golden record waveform data
''' </summary>
Public Module Probe

    ''' <summary>
    ''' the classic amplitude oscillogram of a sample block
    ''' </summary>
    Public Function Oscillogram(data As Single(), start As Integer, count As Integer,
                                width As Integer, height As Integer) As Double()()

        Dim image As Double()() = Matrix(height, width)
        Dim maxAbs As Double = 0

        For i As Integer = start To Math.Min(data.Length - 1, start + count - 1)
            Dim v As Double = Math.Abs(data(i))

            If v > maxAbs Then
                maxAbs = v
            End If
        Next

        If maxAbs = 0 Then
            maxAbs = 1
        End If

        Dim block As Double = count / width
        Dim middle As Double = height / 2.0

        For x As Integer = 0 To width - 1
            Dim from As Integer = start + CInt(Math.Floor(x * block))
            Dim [to] As Integer = Math.Min(start + count, start + CInt(Math.Floor((x + 1) * block)))
            Dim mn As Double = Double.MaxValue
            Dim mx As Double = Double.MinValue

            For i As Integer = from To [to] - 1
                If i < 0 OrElse i >= data.Length Then
                    Continue For
                End If

                Dim v As Double = data(i)

                If v < mn Then
                    mn = v
                End If

                If v > mx Then
                    mx = v
                End If
            Next

            If mn > mx Then
                Continue For
            End If

            Dim y1 As Integer = CInt(Math.Round(middle - mx / maxAbs * (middle - 1)))
            Dim y2 As Integer = CInt(Math.Round(middle - mn / maxAbs * (middle - 1)))

            For y As Integer = Math.Max(0, Math.Min(y1, y2)) To Math.Min(height - 1, Math.Max(y1, y2))
                image(y)(x) = 1
            Next

            image(CInt(middle))(x) = 0.5 ' the zero reference line
        Next

        Return image
    End Function

    ''' <summary>
    ''' fold the signal at the given scan line period, one sample per pixel column
    ''' </summary>
    Public Function Fold(data As Single(), start As Integer, period As Double, lines As Integer,
                         Optional samplesPerRow As Integer = 0) As Double()()

        If samplesPerRow <= 0 Then
            samplesPerRow = CInt(Math.Floor(period))
        End If

        Dim image As Double()() = Matrix(lines, samplesPerRow)

        For r As Integer = 0 To lines - 1
            Dim origin As Double = start + r * period

            For c As Integer = 0 To samplesPerRow - 1
                image(r)(c) = Dsp.Sample(data, origin + c)
            Next
        Next

        Return image
    End Function

    ''' <summary>
    ''' fold the signal at the scan line period and decode the intensity of each pixel cell,
    ''' this gives a fast preview of the decoded image
    ''' </summary>
    ''' <param name="rms">use the rms value instead of the sum of the raw sample amplitudes</param>
    Public Function FoldPixel(data As Single(), start As Integer, period As Double, lines As Integer,
                              pixels As Integer, Optional rms As Boolean = True) As Double()()

        Dim image As Double()() = Matrix(lines, pixels)
        Dim cell As Double = period / pixels

        For r As Integer = 0 To lines - 1
            Dim origin As Double = start + r * period

            For c As Integer = 0 To pixels - 1
                Dim from As Double = origin + c * cell
                Dim [to] As Double = from + cell
                Dim v As Double = 0
                Dim n As Integer = 0
                Dim p As Integer = CInt(Math.Floor(from))

                While p < [to] AndAlso p < data.Length
                    If p >= 0 Then
                        If rms Then
                            v += data(p) ^ 2
                        Else
                            v += data(p)
                        End If

                        n += 1
                    End If

                    p += 1
                End While

                If n > 0 Then
                    image(r)(c) = If(rms, Math.Sqrt(v / n), v / n)
                End If
            Next
        Next

        Return image
    End Function

    ''' <summary>
    ''' decode the image data section into the transposed raster layout:
    ''' each scan line of the analog signal is one image column, and the pixel cells
    ''' inside the scan line are the image rows.
    ''' </summary>
    ''' <param name="cells">the number of the pixel cells inside a single scan line</param>
    ''' <param name="cellOffset">the number of the sample cells which are skipped at the
    ''' beginning of each scan line (the sync/blanking region)</param>
    Public Function DecodeRaster(data As Single(), start As Integer, period As Double, lines As Integer,
                                 cells As Integer, Optional cellOffset As Integer = 0,
                                 Optional rms As Boolean = True) As Double()()

        Dim image As Double()() = Matrix(cells, lines)
        Dim cell As Double = period / (cells + cellOffset)

        For r As Integer = 0 To lines - 1
            Dim origin As Double = start + r * period

            For c As Integer = 0 To cells - 1
                Dim from As Double = origin + (c + cellOffset) * cell
                Dim [to] As Double = from + cell
                Dim v As Double = 0
                Dim n As Integer = 0
                Dim p As Integer = CInt(Math.Floor(from))

                While p < [to] AndAlso p < data.Length
                    If p >= 0 Then
                        If rms Then
                            v += data(p) ^ 2
                        Else
                            v += data(p)
                        End If

                        n += 1
                    End If

                    p += 1
                End While

                If n > 0 Then
                    image(c)(r) = If(rms, Math.Sqrt(v / n), v / n)
                End If
            Next
        Next

        Return image
    End Function

    ''' <summary>
    ''' the measured bounding box of the calibration circle on the transposed raster,
    ''' it is used for calibrating the pixel clock: the calibration circle is a true
    ''' circle, so the horizontal extent (in scan lines) and the vertical extent
    ''' (in pixel cells) of its ring must be equal when the pixel clock is correct.
    ''' </summary>
    Public Function MeasureCircle(data As Single(), start As Integer, period As Double, lines As Integer,
                                  cells As Integer, ByRef dx As Integer, ByRef dy As Integer,
                                  Optional cellOffset As Integer = 0) As Boolean

        Dim image As Double()() = DecodeRaster(data, start, period, lines, cells, cellOffset)
        Dim rowFrom As Integer = Math.Min(cells - 1, Math.Max(0, CInt(cells * 0.08)))
        Dim colFrom As Integer = Math.Max(0, CInt(lines * 0.10))
        Dim colTo As Integer = Math.Min(lines - 1, CInt(lines * 0.92))
        Dim values As New List(Of Double)

        For y As Integer = rowFrom To cells - 1
            For x As Integer = colFrom To colTo
                values.Add(image(y)(x))
            Next
        Next

        Dim threshold As Double = Dsp.Percentile(values.ToArray, 0.975)
        Dim minX As Integer = Integer.MaxValue
        Dim maxX As Integer = Integer.MinValue
        Dim minY As Integer = Integer.MaxValue
        Dim maxY As Integer = Integer.MinValue

        For y As Integer = rowFrom To cells - 1
            For x As Integer = colFrom To colTo
                If image(y)(x) >= threshold Then
                    If x < minX Then minX = x
                    If x > maxX Then maxX = x
                    If y < minY Then minY = y
                    If y > maxY Then maxY = y
                End If
            Next
        Next

        If minX > maxX Then
            dx = 0
            dy = 0

            Return False
        End If

        dx = maxX - minX + 1
        dy = maxY - minY + 1

        Return True
    End Function

    ''' <summary>
    ''' the mean scan line waveform, the sync pulse shape is highlighted by the averaging
    ''' while the image payload content is cancelled out
    ''' </summary>
    Public Function MeanLine(data As Single(), start As Integer, period As Double, lines As Integer,
                             Optional samplesPerRow As Integer = 0) As Double()

        If samplesPerRow <= 0 Then
            samplesPerRow = CInt(Math.Floor(period))
        End If

        Dim mean As Double() = New Double(samplesPerRow - 1) {}

        For r As Integer = 0 To lines - 1
            Dim origin As Double = start + r * period

            For c As Integer = 0 To samplesPerRow - 1
                mean(c) += Dsp.Sample(data, origin + c)
            Next
        Next

        For c As Integer = 0 To samplesPerRow - 1
            mean(c) /= lines
        Next

        Return mean
    End Function

    ''' <summary>
    ''' the vertical coherence score of a folded raster: the mean squared difference
    ''' between two adjacent scan lines. the smaller the score is, the better the
    ''' folding period matches the real scan line period of the analog signal.
    ''' </summary>
    Public Function CoherenceScore(data As Single(), start As Integer, period As Double,
                                   lines As Integer, decimate As Integer) As Double

        Dim samplesPerRow As Integer = CInt(Math.Floor(period)) \ decimate
        Dim previous As Double() = Nothing
        Dim total As Double = 0
        Dim scale As Double = 0
        Dim rows As Integer = 0

        For r As Integer = 0 To lines - 1
            Dim origin As Double = start + r * period
            Dim current As Double() = New Double(samplesPerRow - 1) {}

            For c As Integer = 0 To samplesPerRow - 1
                current(c) = Dsp.Sample(data, origin + c * decimate)
                scale += current(c) * current(c)
            Next

            If previous IsNot Nothing Then
                Dim sum As Double = 0

                For c As Integer = 0 To samplesPerRow - 1
                    Dim d As Double = current(c) - previous(c)

                    sum += d * d
                Next

                total += sum / samplesPerRow
            End If

            previous = current
            rows += 1
        Next

        scale /= rows * samplesPerRow

        If scale = 0 Then
            Return Double.MaxValue
        End If

        Return total / (rows - 1) / scale
    End Function

    ''' <summary>
    ''' refine the scan line period by the vertical coherence of the folded raster.
    ''' 
    ''' the analog scan line period of the golden record is not an integer number of
    ''' the sample frames, a tiny period error accumulates as a sample phase drift
    ''' along the scan lines, which smears the decoded image in the horizontal
    ''' direction. the period is refined by searching the minimum of the folded
    ''' raster coherence score.
    ''' </summary>
    Public Function RefinePeriod(data As Single(), start As Integer, guess As Double,
                                 lines As Integer, Optional range As Double = 12,
                                 Optional step_ As Double = 0.05) As Double

        Dim best As Double = guess
        Dim bestScore As Double = Double.MaxValue
        Dim p As Double = guess - range

        While p <= guess + range
            Dim score As Double = CoherenceScore(data, start, p, lines, 8)

            If score < bestScore Then
                bestScore = score
                best = p
            End If

            p += step_
        End While

        ' fine search
        Dim fineBest As Double = best
        Dim fineScore As Double = Double.MaxValue
        Dim q As Double = best - step_

        While q <= best + step_
            Dim score As Double = CoherenceScore(data, start, q, lines * 2, 4)

            If score < fineScore Then
                fineScore = score
                fineBest = q
            End If

            q += 0.002
        End While

        Return fineBest
    End Function

    Public Structure LineStat
        Public index As Integer
        Public mean As Double
        Public rms As Double
        Public peak As Double
        Public [start] As Double
    End Structure

    ''' <summary>
    ''' the amplitude statistics of each scan line, it is used for checking the
    ''' baseline drift of the analog recording
    ''' </summary>
    Public Function LineStats(data As Single(), start As Integer, period As Double, lines As Integer,
                              Optional samplesPerRow As Integer = 0) As LineStat()

        If samplesPerRow <= 0 Then
            samplesPerRow = CInt(Math.Floor(period))
        End If

        Dim result As LineStat() = New LineStat(lines - 1) {}

        For r As Integer = 0 To lines - 1
            Dim origin As Integer = start + CInt(Math.Round(r * period))
            Dim [end] As Integer = Math.Min(data.Length, origin + samplesPerRow)
            Dim sum As Double = 0
            Dim square As Double = 0
            Dim peak As Double = 0
            Dim n As Integer = [end] - origin

            For i As Integer = origin To [end] - 1
                Dim v As Double = data(i)

                sum += v
                square += v * v

                If Math.Abs(v) > peak Then
                    peak = Math.Abs(v)
                End If
            Next

            If n <= 0 Then
                n = 1
            End If

            result(r) = New LineStat With {
                .index = r,
                .mean = sum / n,
                .rms = Math.Sqrt(square / n),
                .peak = peak,
                .start = origin
            }
        Next

        Return result
    End Function

    ''' <summary>
    ''' transpose an image matrix
    ''' </summary>
    Public Function Transpose(image As Double()()) As Double()()
        Dim rows As Integer = image.Length
        Dim cols As Integer = image(0).Length
        Dim t As Double()() = Matrix(cols, rows)

        For r As Integer = 0 To rows - 1
            For c As Integer = 0 To cols - 1
                t(c)(r) = image(r)(c)
            Next
        Next

        Return t
    End Function

    Private Function Matrix(rows As Integer, cols As Integer) As Double()()
        Dim m As Double()() = New Double(rows - 1)() {}

        For i As Integer = 0 To rows - 1
            m(i) = New Double(cols - 1) {}
        Next

        Return m
    End Function
End Module
