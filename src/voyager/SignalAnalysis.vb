' /********************************************************************************/
'
' Summary:
'
'   Module SignalAnalysis
'
'     Function: Analyse, EstimateLinePeriod, DetectSyncPulses, AnalysePayload,
'               AnalyseChannels, AnalyseChroma, BandPower, IntervalModes
'
' /********************************************************************************/

Imports System.Runtime.CompilerServices
Imports Microsoft.VisualBasic.Data.Wave
Imports Microsoft.VisualBasic.Linq

''' <summary>
''' the empirical analysis of the golden record image data.
''' 
''' the image data of the golden record is an analog scan line signal: a sync pulse
''' train marks the start position of each scan line, and the image brightness data
''' is modulated between the sync pulses. this module measures all of the decoder
''' parameters directly from the waveform instead of using guessed constants.
''' </summary>
Public Module SignalAnalysis

    ''' <summary>
    ''' the number of the image pixel cells on a single scan line.
    ''' 
    ''' this value is measured from the official calibration circle image of the golden
    ''' record (the reference image is 529 x 364 pixels): the scan line of the analog
    ''' signal is one image column, and the image rows are the pixel cells inside the
    ''' scan line, so the image is 364 pixel cells high.
    ''' </summary>
    Public Const DefaultPixelCount As Integer = 364

    ''' <summary>
    ''' the number of the scan lines of a single image (the value 512 on the cover of
    ''' the golden record)
    ''' </summary>
    Public Const DefaultLineCount As Integer = 512

    ''' <summary>
    ''' the number of the pixel cell slots inside a single scan line period.
    ''' 
    ''' the golden record image data has a leading blanking/sync region on each scan
    ''' line, so the scan line period contains more pixel cell slots than the image
    ''' pixel number. this value is measured from the calibration circle image of the
    ''' golden record: the ring of the calibration image is a true circle only at this
    ''' pixel clock, that is about 7.5 samples per pixel cell (a 51.2 kHz pixel clock)
    ''' on the 384kHz wave file.
    ''' </summary>
    Public Const CellSlotsPerPeriod As Double = 426.3

    ''' <summary>
    ''' the measured samples per pixel cell of the golden record image data on the
    ''' 384kHz wave file
    ''' </summary>
    Public Const NominalPixelPitch As Double = 7.5

    ''' <summary>
    ''' run the empirical analysis of a given data section on the golden record wave file
    ''' </summary>
    ''' <param name="wavPath">the file path of the golden record wave file</param>
    ''' <param name="startFrame">the start position of the image data section</param>
    ''' <param name="lengthFrame">the data length of the image data section</param>
    ''' <param name="channel">the zero based channel index for the decoding</param>
    ''' <param name="envWindow">the window size of the envelope low pass filter</param>
    Public Function Analyse(wavPath As String, startFrame As Long, lengthFrame As Long,
                            Optional channel As Integer = 0,
                            Optional envWindow As Integer = 96) As DecodeReport

        Using source As WaveSource = WaveSource.Open(wavPath)
            If startFrame < 0 OrElse lengthFrame <= 0 Then
                lengthFrame = source.FrameCount - startFrame
            End If

            If startFrame + lengthFrame > source.FrameCount Then
                lengthFrame = source.FrameCount - startFrame
            End If

            Dim chSel As Integer()

            If source.Channels >= 2 Then
                chSel = {0, 1}
            Else
                chSel = {0}
            End If

            Dim raw As Single()() = source.ReadChannels(startFrame, lengthFrame, chSel)
            Dim data As Single() = raw(channel)

            Dim report As New DecodeReport With {
                .source = wavPath,
                .channel = ChannelName(channel),
                .start_frame = startFrame,
                .length_frame = lengthFrame,
                .sample_rate = source.SampleRate,
                .duration = lengthFrame / source.SampleRate,
                .min_value = data.Min,
                .max_value = data.Max,
                .dc_offset = data.Mean,
                .rms = data.Rms,
                .amplitude_p90 = Dsp.Percentile(DoubleArray(data), 0.9),
                .amplitude_p99 = Dsp.Percentile(DoubleArray(data), 0.99)
            }

            ' ------------------------------------------------------------------
            ' scan line structure
            ' ------------------------------------------------------------------
            Dim envelope As Double() = data.Envelope(envWindow)
            Dim period As New PeriodEstimation

            Call EstimateLinePeriod(envelope, period)

            report.line_period = period.period
            report.line_period_score = period.score
            report.samples_per_pixel = period.period / CellSlotsPerPeriod
            report.pixel_rate = source.SampleRate / report.samples_per_pixel

            ' sync pulse positions and their interval statistics
            Dim pulses As Double() = DetectSyncPulses(envelope, period.period)
            Dim intervals As Double() = PulseIntervals(pulses)

            report.sync_pulse_count = pulses.Length
            report.line_count = pulses.Length
            report.line_period_std = StdError(intervals)
            report.line_period_drift = IntervalDrift(pulses)
            report.pulse_interval_mean = If(intervals.Length > 0, intervals.MeanArray, 0)
            report.pulse_interval_std = StdError(intervals)
            report.pulse_interval_modes = IntervalModes(intervals, 8, 3)

            ' ------------------------------------------------------------------
            ' payload (brightness) modulation analysis
            ' ------------------------------------------------------------------
            Dim payload As PayloadAnalysis = AnalysePayload(data, pulses, report.samples_per_pixel)

            report.envelope_peaks_per_pixel = payload.peaks_per_pixel
            report.spectral_peaks = payload.spectral_peaks
            report.high_band_ratio = payload.high_band_ratio
            report.brightness_encoding = payload.encoding

            ' ------------------------------------------------------------------
            ' stereo channel relationship
            ' ------------------------------------------------------------------
            If raw.Length >= 2 Then
                Call AnalyseChannels(raw(0), raw(1), report)
            End If

            ' ------------------------------------------------------------------
            ' color (chroma) component detection
            ' ------------------------------------------------------------------
            Call AnalyseChroma(raw, report)

            report.notes = payload.notes

            Return report
        End Using
    End Function

    ''' <summary>
    ''' measure the scan line period (in samples) of a given channel data
    ''' </summary>
    Public Function MeasureLinePeriod(data As Single(), Optional envWindow As Integer = 96) As Double
        Dim envelope As Double() = data.Envelope(envWindow)
        Dim result As New PeriodEstimation

        Call EstimateLinePeriod(envelope, result)

        Return result.period
    End Function

    Public Structure PeriodEstimation
        Public period As Double
        Public score As Double
    End Structure

    ''' <summary>
    ''' estimate the scan line period via the auto correlation of the signal envelope
    ''' </summary>
    ''' <param name="envelope"></param>
    ''' <param name="result"></param>
    ''' <param name="k">the decimation factor for the auto correlation computation</param>
    Friend Sub EstimateLinePeriod(envelope As Double(), ByRef result As PeriodEstimation,
                                  Optional k As Integer = 8)

        ' the line period is expect to be inside the range of [1500, 6000] samples
        Dim minLag As Integer = 1500 \ k
        Dim maxLag As Integer = 6000 \ k
        Dim dec As Double() = envelope.Decimate(k)
        Dim corr As Double() = dec.AutoCorrelation(minLag, maxLag, maxCount:=65536)

        Dim best As Integer = 0

        For i As Integer = 1 To corr.Length - 1
            If corr(i) > corr(best) Then
                best = i
            End If
        Next

        Dim coarse As Double = (minLag + best) * k

        ' refine the period on the full resolution envelope
        Dim fine As Double() = envelope.AutoCorrelation(Math.Max(1, CInt(coarse) - 32), CInt(coarse) + 32, maxCount:=200000)
        Dim fineBest As Integer = 0

        For i As Integer = 1 To fine.Length - 1
            If fine(i) > fine(fineBest) Then
                fineBest = i
            End If
        Next

        result.period = coarse - 32 + fineBest
        result.score = fine(fineBest)
    End Sub

    ''' <summary>
    ''' detect the sub sample positions of the sync pulses on the signal envelope
    ''' </summary>
    ''' <param name="envelope"></param>
    ''' <param name="linePeriod"></param>
    Friend Function DetectSyncPulses(envelope As Double(), linePeriod As Double) As Double()
        Dim median As Double = Dsp.Percentile(envelope, 0.5)
        Dim top As Double = Dsp.Percentile(envelope, 0.995)
        Dim threshold As Double = median + (top - median) * 0.35
        Dim peaks As Integer() = Dsp.FindPeaks(envelope, threshold, Math.Max(16, CInt(linePeriod * 0.5)))
        Dim positions As Double() = New Double(peaks.Length - 1) {}

        For i As Integer = 0 To peaks.Length - 1
            positions(i) = Dsp.ParabolicPeak(envelope, peaks(i))
        Next

        Return positions
    End Function

    Private Function PulseIntervals(pulses As Double()) As Double()
        If pulses.Length < 2 Then
            Return New Double() {}
        End If

        Dim d As Double() = New Double(pulses.Length - 2) {}

        For i As Integer = 1 To pulses.Length - 1
            d(i - 1) = pulses(i) - pulses(i - 1)
        Next

        Return d
    End Function

    Friend Function StdError(values As Double()) As Double
        If values.Length < 2 Then
            Return 0
        End If

        Dim mean As Double = values.MeanArray
        Dim sum As Double = 0

        For i As Integer = 0 To values.Length - 1
            sum += (values(i) - mean) ^ 2
        Next

        Return Math.Sqrt(sum / (values.Length - 1))
    End Function

    ''' <summary>
    ''' the period drift: the mean interval of the second half data minus the first half data
    ''' </summary>
    Private Function IntervalDrift(pulses As Double()) As Double
        If pulses.Length < 8 Then
            Return 0
        End If

        Dim half As Integer = pulses.Length \ 2

        Return (pulses(pulses.Length - 1) - pulses(half)) / (pulses.Length - half) -
               (pulses(half) - pulses(0)) / half
    End Function

    ''' <summary>
    ''' the dominate values on the interval histogram
    ''' </summary>
    Friend Function IntervalModes(intervals As Double(), binSize As Integer, top As Integer) As Double()
        If intervals.Length = 0 Then
            Return New Double() {}
        End If

        Dim histogram As New Dictionary(Of Integer, Integer)

        For Each interval As Double In intervals
            Dim bin As Integer = CInt(Math.Round(interval / binSize)) * binSize
            Dim count As Integer = 0

            If histogram.TryGetValue(bin, count) Then
                histogram(bin) = count + 1
            Else
                histogram(bin) = 1
            End If
        Next

        Return histogram _
            .OrderByDescending(Function(x) x.Value) _
            .Take(top) _
            .Select(Function(x) CDbl(x.Key)) _
            .ToArray
    End Function

#Region "payload modulation analysis"

    Public Structure PayloadAnalysis
        Public peaks_per_pixel As Double
        Public spectral_peaks As Double()
        Public high_band_ratio As Double
        Public encoding As String
        Public notes As String()
    End Structure

    ''' <summary>
    ''' determine how the brightness of the image pixel is modulated on the carrier
    ''' </summary>
    Friend Function AnalysePayload(data As Single(), pulses As Double(), samplesPerPixel As Double) As PayloadAnalysis
        Dim result As New PayloadAnalysis
        Dim notes As New List(Of String)
        Dim start As Integer
        Dim [end] As Integer

        If pulses.Length >= 2 AndAlso samplesPerPixel > 0 Then
            ' take 8 scan lines from the middle of the data section
            Dim mid As Integer = pulses.Length \ 2
            start = CInt(Math.Floor(pulses(mid)))
            [end] = CInt(Math.Floor(pulses(Math.Min(pulses.Length - 1, mid + 8))))
        Else
            start = 0
            [end] = Math.Min(data.Length, 4096 * 8)
        End If

        If [end] - start < 128 Then
            result.encoding = "unknown"
            result.notes = {"not enough data for the payload analysis!"}
            result.spectral_peaks = New Double() {}
            Return result
        End If

        Dim block As Single() = data.Skip(start).Take([end] - start).ToArray
        Dim window As Integer = Math.Min(block.Length, 8192)
        Dim spectrumInput As Double() = New Double(window - 1) {}

        For i As Integer = 0 To window - 1
            spectrumInput(i) = block(i + (block.Length - window) \ 2)
        Next

        result.spectral_peaks = TopSpectralPeaks(spectrumInput, 384000, 12000) ' only report the low frequency peaks

        ' narrow band peak counting: the mean number of the signal peaks inside
        ' a single pixel cell
        Dim cell As Integer = Math.Max(1, CInt(Math.Round(samplesPerPixel)))
        Dim pixelCount As Integer = block.Length \ cell
        Dim peakCount As Double = 0
        Dim cells As Integer = 0
        Dim energy0 As Double = 0
        Dim energy1 As Double = 0

        For p As Integer = 0 To pixelCount - 1
            Dim cellStart As Integer = p * cell
            Dim peak As Double = 0
            Dim energy As Double = 0

            For i As Integer = cellStart To cellStart + cell - 1
                Dim v As Double = Math.Abs(block(i))

                If v > peak Then
                    peak = v
                End If

                energy += block(i) * block(i)
            Next

            ' a "pulse" inside the cell: the peak magnitude is far above the cell mean
            If energy > 0 AndAlso peak / Math.Sqrt(energy / cell) > 2.5 Then
                peakCount += 1
            End If

            cells += 1
        Next

        If cells > 0 Then
            result.peaks_per_pixel = peakCount / cells
        End If

        ' the high band energy ratio: the energy above the pixel clock nyquist
        Dim spec As Double() = spectrumInput.PowerSpectrum
        Dim nyquistBin As Double = (spec.Length - 1) / samplesPerPixel

        For i As Integer = 1 To spec.Length - 1
            If i > nyquistBin Then
                energy1 += spec(i) * spec(i)
            Else
                energy0 += spec(i) * spec(i)
            End If
        Next

        Dim total As Double = energy0 + energy1

        result.high_band_ratio = If(total = 0, 0, energy1 / total)

        ' ------------------------------------------------------------------
        ' determine the brightness encoding scheme
        ' ------------------------------------------------------------------
        If result.peaks_per_pixel > 1.35 Then
            result.encoding = "pulse-density"
        ElseIf result.peaks_per_pixel > 0.65 Then
            result.encoding = "pulse-amplitude"
        Else
            result.encoding = "amplitude-continuous"
        End If

        Call notes.Add($"payload block: {block.Length} samples, {pixelCount} pixel cells of {cell} samples")

        result.notes = notes.ToArray
        Return result
    End Function

    ''' <summary>
    ''' the top magnitude positions (hertz) on the given signal spectrum
    ''' </summary>
    Friend Function TopSpectralPeaks(signal As Double(), sampleRate As Integer,
                                     Optional maxHz As Double = 100000) As Double()

        Dim spectrum As Double() = signal.PowerSpectrum
        Dim binHz As Double = sampleRate / (2.0 * (spectrum.Length - 1))
        Dim bins As List(Of Double) = spectrum _
            .Skip(2) _
            .Take(Math.Min(spectrum.Length - 2, CInt(maxHz / binHz))) _
            .Select(Function(x, i) New KeyValuePair(Of Integer, Double)(i + 2, x)) _
            .OrderByDescending(Function(x) x.Value) _
            .Take(12) _
            .OrderBy(Function(x) x.Key) _
            .Select(Function(x) (x.Key - 2) * binHz) _
            .ToList

        Return bins.ToArray
    End Function

#End Region

#Region "stereo channel relationship"

    Friend Sub AnalyseChannels(left As Single(), right As Single(), report As DecodeReport)
        report.channel_energy_left = left.Rms
        report.channel_energy_right = right.Rms

        Dim k As Integer = 16
        Dim l As Double() = DoubleArray(left)
        Dim r As Double() = DoubleArray(right)
        Dim decL As Double() = l.Decimate(k)
        Dim decR As Double() = r.Decimate(k)
        Dim n As Integer = Math.Min(Math.Min(decL.Length, decR.Length), 32768)
        Dim maxLag As Integer = Math.Min(400, n \ 4)
        Dim bestScore As Double = Double.MinValue
        Dim bestLag As Integer = 0
        Dim meanL As Double = 0
        Dim meanR As Double = 0

        For i As Integer = 0 To n - 1
            meanL += decL(i)
            meanR += decR(i)
        Next

        meanL /= n
        meanR /= n

        For lag As Integer = -maxLag To maxLag
            Dim sum As Double = 0

            For i As Integer = 0 To n - 1
                Dim j As Integer = i + lag

                If j < 0 OrElse j >= n Then
                    Continue For
                End If

                sum += (decL(i) - meanL) * (decR(j) - meanR)
            Next

            If sum > bestScore Then
                bestScore = sum
                bestLag = lag
            End If
        Next

        Dim normL As Double = 0
        Dim normR As Double = 0

        For i As Integer = 0 To n - 1
            normL += (decL(i) - meanL) ^ 2
            normR += (decR(i) - meanR) ^ 2
        Next

        report.channel_best_lag = bestLag * k
        report.channel_correlation = If(normL = 0 OrElse normR = 0, 0, bestScore / Math.Sqrt(normL * normR))
    End Sub

#End Region

#Region "color component detection"

    ''' <summary>
    ''' the frequency bands for the energy distribution analysis
    ''' </summary>
    ReadOnly BandEdges As Double() = {0, 5000, 10000, 20000, 30000, 44700, 60000, 80000, 100000, 150000, 192000}

    ''' <summary>
    ''' detect the narrow band chroma carrier (if any) which is independent from the luminance data
    ''' </summary>
    Friend Sub AnalyseChroma(raw As Single()(), report As DecodeReport)
        Dim bands As New Dictionary(Of String, Double)
        Dim frameSize As Integer = 4096
        Dim hop As Integer = 2048
        Dim accum As Double() = Nothing
        Dim binHz As Double

        For c As Integer = 0 To raw.Length - 1
            Dim data As Single() = raw(c)
            Dim power As Double() = Nothing
            Dim frames As Integer = 0

            If accum Is Nothing Then
                Dim spec As Double() = DoubleArray(data.Take(frameSize).ToArray).PowerSpectrum
                accum = New Double(spec.Length - 1) {}
                binHz = report.sample_rate / (2.0 * (spec.Length - 1))
            End If

            Dim pos As Integer = 0

            While pos + frameSize < data.Length AndAlso frames < 256
                Dim block As Double() = New Double(frameSize - 1) {}

                For i As Integer = 0 To frameSize - 1
                    block(i) = data(pos + i)
                Next

                Dim spec As Double() = block.PowerSpectrum

                If power Is Nothing Then
                    power = New Double(spec.Length - 1) {}
                End If

                For i As Integer = 0 To spec.Length - 1
                    power(i) += spec(i)
                Next

                pos += hop
                frames += 1
            End While

            If power IsNot Nothing Then
                For i As Integer = 0 To Math.Min(accum.Length, power.Length) - 1
                    accum(i) += power(i) / frames
                Next
            End If
        Next

        If accum Is Nothing Then
            report.band_power = bands
            report.notes = {"no enough data for the spectrum analysis!"}
            Return
        End If

        ' energy distribution over the frequency bands
        For i As Integer = 0 To BandEdges.Length - 2
            Dim sum As Double = 0
            Dim name As String = $"{BandEdges(i) / 1000:F0}-{BandEdges(i + 1) / 1000:F0} kHz"

            For b As Integer = 0 To accum.Length - 1
                Dim f As Double = b * binHz

                If f >= BandEdges(i) AndAlso f < BandEdges(i + 1) Then
                    sum += accum(b) * accum(b)
                End If
            Next

            bands(name) = sum
        Next

        Dim total As Double = bands.Values.Sum
        Dim relative As New Dictionary(Of String, Double)

        For Each band In bands
            relative(band.Key) = If(total = 0, 0, band.Value / total)
        Next

        report.band_power = relative

        ' narrow band chroma carrier detection: a strong peak above the luminance band
        Dim minBin As Integer = CInt(60000 / binHz)
        Dim maxBin As Integer = Math.Min(accum.Length - 1, CInt(150000 / binHz))
        Dim peakBin As Integer = -1
        Dim peakValue As Double = 0

        For b As Integer = Math.Max(2, minBin) To maxBin
            Dim local As Double = LocalMedian(accum, b, 32)

            If local <= 0 Then
                Continue For
            End If

            Dim score As Double = accum(b) / local

            If score > peakValue Then
                peakValue = score
                peakBin = b
            End If
        Next

        If peakBin > 0 Then
            report.chroma_candidate_hz = peakBin * binHz
            report.chroma_band_ratio = peakValue
            report.has_chroma_component = peakValue > 6.0
        End If
    End Sub

    Private Function LocalMedian(spectrum As Double(), i As Integer, radius As Integer) As Double
        Dim from As Integer = Math.Max(0, i - radius)
        Dim [to] As Integer = Math.Min(spectrum.Length - 1, i + radius)
        Dim buffer As Double() = New Double([to] - from) {}

        Array.Copy(spectrum, from, buffer, 0, buffer.Length)
        Array.Sort(buffer)

        Return buffer(buffer.Length \ 2)
    End Function

#End Region

#Region "small helpers"

    Friend Function DoubleArray(x As Single()) As Double()
        Dim d As Double() = New Double(x.Length - 1) {}

        For i As Integer = 0 To x.Length - 1
            d(i) = x(i)
        Next

        Return d
    End Function

    Public Function ChannelName(channel As Integer) As String
        Select Case channel
            Case 0 : Return "Left"
            Case 1 : Return "Right"
            Case Else : Return $"Channel {channel}"
        End Select
    End Function

    <Extension>
    Friend Function MeanArray(x As Double()) As Double
        If x.Length = 0 Then
            Return 0
        End If

        Dim sum As Double = 0

        For i As Integer = 0 To x.Length - 1
            sum += x(i)
        Next

        Return sum / x.Length
    End Function

#End Region
End Module
