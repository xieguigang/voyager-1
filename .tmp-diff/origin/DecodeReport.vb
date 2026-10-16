' /********************************************************************************/
'
' Summary:
'
'   Class DecodeReport
'
'     Properties: source, channel, start_frame, length_frame, sample_rate, duration,
'                 min_value, max_value, dc_offset, rms,
'                 line_period, line_period_std, line_period_score, line_count,
'                 samples_per_pixel, pixel_rate,
'                 pulse_count, pulse_interval_mean, pulse_interval_std,
'                 envelope_peaks_per_line, brightness_encoding,
'                 channel_correlation, channel_best_lag, channel_energy_ratio,
'                 has_chroma_component, chroma_candidate_hz, chroma_band_ratio, band_power
'
'     Function: ToString, ToText
'
' /********************************************************************************/

Imports System.Text
Imports Microsoft.VisualBasic.Linq
Imports Microsoft.VisualBasic.Serialization.JSON

''' <summary>
''' the empirical analysis report of a given image data section on the golden record wave file.
''' 
''' all of the decoder parameters (scan line period, pixel clock, brightness encoding,
''' color component) are measured from the real waveform data instead of guessing.
''' </summary>
Public Class DecodeReport

    Public Property source As String
    Public Property channel As String
    Public Property start_frame As Long
    Public Property length_frame As Long
    Public Property sample_rate As Integer
    Public Property duration As Double

#Region "wave amplitude statistics"

    Public Property min_value As Double
    Public Property max_value As Double
    Public Property dc_offset As Double
    Public Property rms As Double
    Public Property amplitude_p90 As Double
    Public Property amplitude_p99 As Double

#End Region

#Region "scan line structure"

    ''' <summary>
    ''' the measured period (in samples) of the scan line sync pulse train
    ''' </summary>
    ''' <returns></returns>
    Public Property line_period As Double

    ''' <summary>
    ''' standard error of the scan line period
    ''' </summary>
    ''' <returns></returns>
    Public Property line_period_std As Double

    ''' <summary>
    ''' the normalized auto correlation score at the best <see cref="line_period"/> lag,
    ''' a value close to 1.0 indicates a strong and stable line structure.
    ''' </summary>
    ''' <returns></returns>
    Public Property line_period_score As Double

    ''' <summary>
    ''' the drift of the line period, measured as the interval between the first half
    ''' and the second half of the data (in samples per line).
    ''' </summary>
    ''' <returns></returns>
    Public Property line_period_drift As Double

    ''' <summary>
    ''' numbers of the scan lines that are detected on current section
    ''' </summary>
    ''' <returns></returns>
    Public Property line_count As Integer

    ''' <summary>
    ''' the number of the samples per image pixel
    ''' </summary>
    ''' <returns></returns>
    Public Property samples_per_pixel As Double

    ''' <summary>
    ''' the pixel sampling clock (hertz) of the image data
    ''' </summary>
    ''' <returns></returns>
    Public Property pixel_rate As Double

#End Region

#Region "sync pulse statistics"

    Public Property sync_pulse_count As Integer
    Public Property pulse_interval_mean As Double
    Public Property pulse_interval_std As Double

    ''' <summary>
    ''' the top frequency components (hertz) of the pulse interval distribution
    ''' </summary>
    ''' <returns></returns>
    Public Property pulse_interval_modes As Double()

    ''' <summary>
    ''' the mean number of the signal peaks inside a single pixel cell,
    ''' value near one indicates a pulse train encoding (one pulse per pixel),
    ''' value near zero indicates a continuous amplitude signal.
    ''' </summary>
    ''' <returns></returns>
    Public Property envelope_peaks_per_pixel As Double

    ''' <summary>
    ''' the dominant spectral peak positions (hertz) of the image data section
    ''' </summary>
    ''' <returns></returns>
    Public Property spectral_peaks As Double()

    ''' <summary>
    ''' the fraction of the total energy which is located above the pixel clock nyquist
    ''' </summary>
    ''' <returns></returns>
    Public Property high_band_ratio As Double

#End Region

#Region "left/right channel relationship"

    Public Property channel_correlation As Double
    Public Property channel_best_lag As Integer
    Public Property channel_energy_left As Double
    Public Property channel_energy_right As Double

#End Region

#Region "color component"

    ''' <summary>
    ''' does the analysis detects any independent chroma (color) component carrier
    ''' </summary>
    ''' <returns></returns>
    Public Property has_chroma_component As Boolean

    ''' <summary>
    ''' the candidate frequency (hertz) of the chroma carrier
    ''' </summary>
    ''' <returns></returns>
    Public Property chroma_candidate_hz As Double

    ''' <summary>
    ''' the narrow band energy ratio of the detected chroma carrier,
    ''' value much greater than 1 indicates a real color sub carrier.
    ''' </summary>
    ''' <returns></returns>
    Public Property chroma_band_ratio As Double

    ''' <summary>
    ''' the energy distribution over the frequency bands
    ''' </summary>
    ''' <returns></returns>
    Public Property band_power As Dictionary(Of String, Double)

#End Region

#Region "decoder conclusion"

    ''' <summary>
    ''' the brightness encoding scheme of the image data:
    ''' ``amplitude``, ``pulse-position`` or ``unknown``
    ''' </summary>
    ''' <returns></returns>
    Public Property brightness_encoding As String

    ''' <summary>
    ''' the number of the anomalous scan lines which are dropped or interpolated during the decoding
    ''' </summary>
    ''' <returns></returns>
    Public Property anomalous_lines As Integer

    Public Property notes As String()

#End Region

    Public Overrides Function ToString() As String
        Return ToText()
    End Function

    ''' <summary>
    ''' the human readable text report
    ''' </summary>
    Public Function ToText() As String
        Dim s As New StringBuilder

        Call s.AppendLine("======================================================================")
        Call s.AppendLine(" golden record image decoding :: empirical analysis report")
        Call s.AppendLine("======================================================================")
        Call s.AppendLine($"data source      : {source}")
        Call s.AppendLine($"channel          : {channel}")
        Call s.AppendLine($"data section     : start={start_frame} frames, length={length_frame} frames ({duration.ToString("F3")} seconds)")
        Call s.AppendLine($"sample rate      : {sample_rate} Hz")
        Call s.AppendLine()

        Call s.AppendLine("[wave amplitude]")
        Call s.AppendLine($"  min/max        : {min_value.ToString("G6")} / {max_value.ToString("G6")}")
        Call s.AppendLine($"  dc offset      : {dc_offset.ToString("G6")}")
        Call s.AppendLine($"  rms            : {rms.ToString("G6")}")
        Call s.AppendLine($"  p90/p99        : {amplitude_p90.ToString("G6")} / {amplitude_p99.ToString("G6")}")
        Call s.AppendLine()

        Call s.AppendLine("[scan line structure]")
        Call s.AppendLine($"  line period    : {line_period.ToString("F3")} samples ({(sample_rate / line_period).ToString("F2")} Hz)")
        Call s.AppendLine($"  period std     : {line_period_std.ToString("F3")} samples")
        Call s.AppendLine($"  period drift   : {line_period_drift.ToString("F3")} samples/line")
        Call s.AppendLine($"  period score   : {line_period_score.ToString("F4")}")
        Call s.AppendLine($"  detected lines : {line_count}")
        Call s.AppendLine($"  samples / pixel: {samples_per_pixel.ToString("F4")}")
        Call s.AppendLine($"  pixel clock    : {pixel_rate.ToString("F2")} Hz")
        Call s.AppendLine()

        Call s.AppendLine("[sync pulse]")
        Call s.AppendLine($"  pulse count    : {sync_pulse_count}")
        Call s.AppendLine($"  pulse interval : {pulse_interval_mean.ToString("F3")} +/- {pulse_interval_std.ToString("F3")} samples")
        Call s.AppendLine($"  interval modes : {pulse_interval_modes.Select(Function(x) x.ToString("F1")).JoinBy(", ")}")
        Call s.AppendLine($"  peaks / pixel  : {envelope_peaks_per_pixel.ToString("F4")}")
        Call s.AppendLine($"  spectral peaks : {spectral_peaks.Select(Function(x) x.ToString("F1")).JoinBy(", ")} Hz")
        Call s.AppendLine($"  high band ratio: {high_band_ratio.ToString("F4")}")
        Call s.AppendLine()

        Call s.AppendLine("[left / right channel]")
        Call s.AppendLine($"  correlation    : {channel_correlation.ToString("F4")} at lag {channel_best_lag} samples")
        Call s.AppendLine($"  energy L/R     : {channel_energy_left.ToString("G6")} / {channel_energy_right.ToString("G6")}")
        Call s.AppendLine()

        Call s.AppendLine("[color component]")
        Call s.AppendLine($"  chroma carrier : {has_chroma_component} (candidate {chroma_candidate_hz.ToString("F1")} Hz, band ratio {chroma_band_ratio.ToString("F3")})")

        For Each band In band_power
            Call s.AppendLine($"    {band.Key,-12}: {band.Value.ToString("G6")}")
        Next

        Call s.AppendLine()
        Call s.AppendLine("[conclusion]")
        Call s.AppendLine($"  brightness is encoded as: {brightness_encoding}")
        Call s.AppendLine($"  anomalous lines         : {anomalous_lines}")

        For Each note As String In notes.SafeQuery
            Call s.AppendLine($"  * {note}")
        Next

        Return s.ToString
    End Function

    ''' <summary>
    ''' export the analysis report as a json document
    ''' </summary>
    Public Function ToJson() As String
        Return Me.GetJson
    End Function
End Class
