' /********************************************************************************/
'
' Summary:
'
'   Module SignalAnalysis
'
'     Function: Analyse, AnalyseFrames, ChannelName
'
'   the empirical analysis of the golden record image data.
'
'   the image data of the golden record is an analog scan line signal: a sync pulse
'   train marks the start position of each scan line, and the image brightness data
'   is modulated between the sync pulses. this module measures all of the decoder
'   parameters directly from the waveform instead of using guessed constants.
'
'   the measuring algorithms themselves are the generic signal processing functions
'   of the sciBASIC# signal processing library
'   (Microsoft.VisualBasic.Math.SignalProcessing), this module is only the adapter
'   which fills the measured values into the golden record decode report.
'
' /********************************************************************************/

Imports Microsoft.VisualBasic.Data.Wave
Imports Microsoft.VisualBasic.Math.SignalProcessing
Imports Microsoft.VisualBasic.Math.SignalProcessing.Waves

''' <summary>
''' the empirical analysis of the golden record image data
''' </summary>
Public Module SignalAnalysis

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

        Using source As WaveSampleReader = WaveSampleReader.Open(wavPath)
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

            Return AnalyseFrames(raw, source.SampleRate, wavPath, startFrame, lengthFrame, channel, envWindow)
        End Using
    End Function

    ''' <summary>
    ''' run the empirical analysis on the already loaded channel sample data
    ''' </summary>
    ''' <param name="raw">the sample data of the stereo channels, ``raw(0)`` is the left channel</param>
    ''' <param name="sampleRate"></param>
    ''' <param name="source">the description of the data source, it is only used for the report</param>
    Public Function AnalyseFrames(raw As Single()(), sampleRate As Integer, Optional source As String = Nothing,
                                  Optional startFrame As Long = 0, Optional lengthFrame As Long = 0,
                                  Optional channel As Integer = 0,
                                  Optional envWindow As Integer = 96) As DecodeReport

        Dim data As Single() = raw(channel)
        Dim report As New DecodeReport With {
            .source = source,
            .channel = ChannelName(channel),
            .start_frame = startFrame,
            .length_frame = If(lengthFrame > 0, lengthFrame, data.Length),
            .sample_rate = sampleRate,
            .duration = data.Length / sampleRate,
            .min_value = data.Min,
            .max_value = data.Max,
            .dc_offset = MathUtils.Mean(data),
            .rms = MathUtils.Rms(data),
            .amplitude_p90 = MathUtils.Percentile(data, 0.9),
            .amplitude_p99 = MathUtils.Percentile(data, 0.99)
        }

        ' ------------------------------------------------------------------
        ' scan line structure
        ' ------------------------------------------------------------------
        Dim envelope As Double() = MathUtils.Envelope(data, envWindow)
        Dim period As New PeriodEstimation

        Call PeriodAnalysis.EstimateLinePeriod(envelope, period)

        report.line_period = period.period
        report.line_period_score = period.score
        report.samples_per_pixel = period.period / GoldenRecord.CellSlotsPerPeriod
        report.pixel_rate = sampleRate / report.samples_per_pixel

        ' sync pulse positions and their interval statistics
        Dim pulses As Double() = PeriodAnalysis.DetectSyncPulses(envelope, period.period)
        Dim intervals As Double() = PeriodAnalysis.PulseIntervals(pulses)

        report.sync_pulse_count = pulses.Length
        report.line_count = pulses.Length
        report.line_period_std = PeriodAnalysis.StdError(intervals)
        report.line_period_drift = PeriodAnalysis.IntervalDrift(pulses)
        report.pulse_interval_mean = If(intervals.Length > 0, MathUtils.Mean(intervals), 0)
        report.pulse_interval_std = PeriodAnalysis.StdError(intervals)
        report.pulse_interval_modes = PeriodAnalysis.IntervalModes(intervals, 8, 3)

        ' ------------------------------------------------------------------
        ' payload (brightness) modulation analysis
        ' ------------------------------------------------------------------
        Dim payload As PayloadAnalysis = ModulationAnalysis.AnalysePayload(
            data:=data,
            pulses:=pulses,
            samplesPerPixel:=report.samples_per_pixel,
            sampleRate:=sampleRate
        )

        report.envelope_peaks_per_pixel = payload.peaks_per_pixel
        report.spectral_peaks = payload.spectral_peaks
        report.high_band_ratio = payload.high_band_ratio
        report.brightness_encoding = payload.encoding

        ' ------------------------------------------------------------------
        ' stereo channel relationship
        ' ------------------------------------------------------------------
        If raw.Length >= 2 Then
            Dim channels As ChannelCorrelation = ChannelAnalysis.AnalyseChannels(raw(0), raw(1))

            report.channel_energy_left = channels.energy_left
            report.channel_energy_right = channels.energy_right
            report.channel_best_lag = channels.best_lag
            report.channel_correlation = channels.correlation
        End If

        ' ------------------------------------------------------------------
        ' color (chroma) component detection
        ' ------------------------------------------------------------------
        Dim bands As SpectrumBands = BandAnalysis.AnalyseSpectrumBands(raw, sampleRate)

        report.band_power = bands.band_power
        report.has_chroma_component = bands.has_chroma_component
        report.chroma_candidate_hz = bands.chroma_candidate_hz
        report.chroma_band_ratio = bands.chroma_band_ratio

        report.notes = payload.notes

        Return report
    End Function

    ''' <summary>
    ''' the display name of the given audio channel
    ''' </summary>
    Public Function ChannelName(channel As Integer) As String
        Select Case channel
            Case 0 : Return "Left"
            Case 1 : Return "Right"
            Case Else : Return $"Channel {channel}"
        End Select
    End Function
End Module
