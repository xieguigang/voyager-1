Imports System
Imports System.IO
Imports System.Text
Imports voyager_1

''' <summary>
''' the command line interface of the golden record image decoder.
''' 
''' the tool works on the wave file data of the golden record and provides
''' three working modes:
''' 
''' 1. ``--diagnose``: print the empirical analysis report of a given data section,
'''     the scan line period, the pixel clock, the brightness encoding scheme and
'''     the color component detection result are measured from the real waveform.
''' 2. ``--decode``  : decode a single image section and save it as a png file.
''' 3. ``--batch``   : scan the whole wave file and export all of the images.
''' </summary>
Friend Module DecoderCli

    Friend Function Run(args As String()) As Integer
        Dim options As CliOptions

        Try
            options = CliOptions.Parse(args)
        Catch ex As Exception
            Call Console.Error.WriteLine($"[error] {ex.Message}")
            Return 2
        End Try

        If options.help Then
            Call PrintUsage()
            Return 0
        End If

        Try
            If options.diagnose Then
                Return RunDiagnose(options)
            ElseIf options.probe Then
                Return RunProbe(options)
            ElseIf options.compare Then
                Return RunCompare(options)
            ElseIf options.batch Then
                Call Console.Error.WriteLine("[error] the --batch mode is not available yet!")
                Return 9
            Else
                Return RunDecode(options)
            End If
        Catch ex As Exception
            Call Console.Error.WriteLine($"[error] {ex.Message}")
            Call Console.Error.WriteLine(ex.StackTrace)
            Return 3
        End Try
    End Function

    ''' <summary>
    ''' decode a single image data section and save the result as a png image file
    ''' </summary>
    Private Function RunDecode(options As CliOptions) As Integer
        Call Console.WriteLine($"decoding {options.wav} ...")
        Call Console.WriteLine($"data section: start={options.start} frames, length={options.length} frames, channel={options.channelName}")

        Dim sw As Diagnostics.Stopwatch = Diagnostics.Stopwatch.StartNew

        Using source As WaveSource = WaveSource.Open(options.wav)
            Dim data As Single() = source.ReadChannel(options.channel, options.start, options.length)
            Dim args As DecoderArgument = options.DecoderArguments
            Dim image As PixelDecode = ImageDecoder.DecodeSection(data, args)
            Dim render As RenderOptions = options.RenderArguments

            Call Console.WriteLine()
            Call Console.WriteLine($"measured line period : {image.line_period.ToString("F4")} samples ({(source.SampleRate / image.line_period).ToString("F3")} Hz), score={image.line_score.ToString("F4")}")
            Call Console.WriteLine($"measured pixel pitch : {image.pixel_pitch.ToString("F4")} samples, {image.width} scan lines of {image.height} pixels")
            Call Console.WriteLine($"line synchronization : {image.aligned}, residual jitter std={image.jitter_std.ToString("F3")} samples")
            Call Console.WriteLine($"render options       : {render}")
            Call Console.WriteLine()

            Dim png As String = options.out
            Dim dir As String = Path.GetDirectoryName(png)

            If Not String.IsNullOrEmpty(dir) Then
                Call Directory.CreateDirectory(dir)
            End If

            Call ImageRender.SavePng(png, image.pixels, render)
            Call Console.WriteLine($"the decoded image was saved to {png} ({image.width} x {image.height})")

            If Not String.IsNullOrEmpty(options.csv) Then
                Call SaveIntensityCsv(options.csv, image.pixels)
                Call Console.WriteLine($"the intensity matrix was saved to {options.csv}")
            End If

            Dim report As String = options.report

            If Not String.IsNullOrEmpty(report) Then
                Call File.WriteAllText(report, GenerateReport(options, image, render, source.SampleRate).ToJson,
                                       New UTF8Encoding(encoderShouldEmitUTF8Identifier:=False))
                Call Console.WriteLine($"the decoding report was saved to {report}")
            End If
        End Using

        Call Console.WriteLine($"decoding finished in {sw.Elapsed.TotalSeconds.ToString("F2")} seconds.")

        Return 0
    End Function

    Private Function GenerateReport(options As CliOptions, image As PixelDecode, render As RenderOptions,
                                    sampleRate As Integer) As DecodeReport
        Return New DecodeReport With {
            .source = options.wav,
            .channel = options.channelName,
            .start_frame = options.start,
            .length_frame = options.length,
            .sample_rate = sampleRate,
            .line_period = image.line_period,
            .line_period_score = image.line_score,
            .line_count = image.width,
            .samples_per_pixel = image.pixel_pitch,
            .pixel_rate = If(image.pixel_pitch = 0, 0, sampleRate / image.pixel_pitch),
            .brightness_encoding = "amplitude-continuous",
            .notes = {
                $"the decoded image is {image.width} x {image.height} pixels",
                $"the residual jitter after the line synchronization is {image.jitter_std.ToString("F3")} samples",
                $"the render options are {render}"
            }
        }
    End Function

    Private Sub SaveIntensityCsv(path As String, pixels As Single()())
        Using s As Stream = File.Create(path)
            Using writer As New StreamWriter(s, New UTF8Encoding(encoderShouldEmitUTF8Identifier:=False))
                Call writer.WriteLine($"# golden record image intensity matrix, {pixels.Length} x {pixels(0).Length}")

                For Each row As Single() In pixels
                    Call writer.WriteLine(String.Join(",", row.Select(Function(v) CSng(v).ToString("F6"))))
                Next

                Call writer.Flush()
            End Using
        End Using
    End Sub

    Private Function RunDiagnose(options As CliOptions) As Integer
        Call Console.WriteLine($"analyzing {options.wav} ...")
        Call Console.WriteLine($"data section: start={options.start} frames, length={options.length} frames, channel={options.channelName}")
        Call Console.WriteLine()

        Dim sw As Diagnostics.Stopwatch = Diagnostics.Stopwatch.StartNew
        Dim report As DecodeReport = SignalAnalysis.Analyse(
            wavPath:=options.wav,
            startFrame:=options.start,
            lengthFrame:=options.length,
            channel:=options.channel,
            envWindow:=options.envWindow)
        Dim elapsed As Double = sw.Elapsed.TotalSeconds

        Call Console.WriteLine(report.ToText)

        If Not String.IsNullOrEmpty(options.report) Then
            Call File.WriteAllText(options.report, report.ToJson, New UTF8Encoding(encoderShouldEmitUTF8Identifier:=False))
            Call Console.WriteLine($"the json report was saved to {options.report}")
        End If

        Call Console.WriteLine($"analysis finished in {elapsed.ToString("F2")} seconds.")

        Return 0
    End Function

    ''' <summary>
    ''' the objective comparison between the decoded image and the official reference image
    ''' </summary>
    Private Function RunCompare(options As CliOptions) As Integer
        Dim ours As PngImage = PngReader.Load(options.image)
        Dim reference As PngImage = PngReader.Load(options.reference)

        Call Console.WriteLine($"decoded  : {options.image} ({ours.Width} x {ours.Height})")
        Call Console.WriteLine($"reference: {options.reference} ({reference.Width} x {reference.Height})")
        Call Console.WriteLine($"            bit depth={reference.BitDepth}, color type={reference.ColorType}, idat={reference.IdatBytes} bytes, raw={reference.RawBytes} bytes, empty={reference.IsEmpty}")
        Call Console.WriteLine()

        Dim oursGray As Double()() = ours.Gray
        Dim referenceGray As Double()() = reference.Gray
        Dim test As OrientationTest = ImageCompare.OrientationTest(oursGray, referenceGray)

        Call Console.WriteLine(test.ToString)
        Call Console.WriteLine()
        Call PrintFeatureBox("decoded  ", oursGray)
        Call PrintFeatureBox("reference", referenceGray)

        Return 0
    End Function

    ''' <summary>
    ''' print the bounding box of the brightest and the darkest feature of the image,
    ''' this is used for the geometric (aspect ratio) checking of the calibration circle
    ''' </summary>
    Private Sub PrintFeatureBox(name As String, image As Double()())
        Dim height As Integer = image.Length
        Dim width As Integer = image(0).Length
        Dim flat As Double() = image.SelectMany(Function(row) row.AsEnumerable).ToArray
        Dim bright As Integer() = ImageCompare.BoundingBox(image, Dsp.Percentile(flat, 0.9))
        Dim dark As Integer() = ImageCompare.BoundingBox(Negate(image), Dsp.Percentile(Negate(image).SelectMany(Function(row) row.AsEnumerable).ToArray, 0.9))

        Call Console.WriteLine($"{name}: size {width} x {height}, min={flat.Min.ToString("F4")} max={flat.Max.ToString("F4")} mean={flat.Average.ToString("F4")}")
        Call Console.WriteLine($"{name}: bright box x={bright(0)}, y={bright(1)}, w={bright(2)}, h={bright(3)} ({bright(2) * 100.0 / width:F1}% x {bright(3) * 100.0 / height:F1}%)")
        Call Console.WriteLine($"{name}: dark   box x={dark(0)}, y={dark(1)}, w={dark(2)}, h={dark(3)} ({dark(2) * 100.0 / width:F1}% x {dark(3) * 100.0 / height:F1}%)")
    End Sub

    Private Function Negate(image As Double()()) As Double()()
        Dim result As Double()() = New Double(image.Length - 1)() {}

        For y As Integer = 0 To image.Length - 1
            Dim row As Double() = New Double(image(y).Length - 1) {}

            For x As Integer = 0 To image(y).Length - 1
                row(x) = -image(y)(x)
            Next

            result(y) = row
        Next

        Return result
    End Function

    ''' <summary>
    ''' the debug visualization of the raw waveform structure
    ''' </summary>
    Private Function RunProbe(options As CliOptions) As Integer
        Dim dir As String = options.out

        Call Directory.CreateDirectory(dir)
        Call Console.WriteLine($"probing {options.wav} [{options.channelName}] ...")

        Using source As WaveSource = WaveSource.Open(options.wav)
            Dim data As Single() = source.ReadChannel(options.channel, options.start, options.length)
            Dim period As Double = options.period

            If period <= 0 Then
                period = SignalAnalysis.MeasureLinePeriod(data)
                Call Console.WriteLine($"the raw scan line period is {period.ToString("F3")} samples ({(source.SampleRate / period).ToString("F2")} Hz)")

                Dim probeLines As Integer = Math.Min(256, CInt(data.Length / period))
                Dim sw As Diagnostics.Stopwatch = Diagnostics.Stopwatch.StartNew

                period = Probe.RefinePeriod(data, 0, period, probeLines)
                Call Console.WriteLine($"the refined scan line period is {period.ToString("F4")} samples (drift {(period - SignalAnalysis.MeasureLinePeriod(data)).ToString("F4")} samples/line), refined in {sw.Elapsed.TotalSeconds.ToString("F2")} seconds")
            End If

            Call Console.WriteLine($"the scan line period is {period.ToString("F4")} samples ({(source.SampleRate / period).ToString("F2")} Hz), {SignalAnalysis.DefaultPixelCount} pixels per line, {CInt(data.Length / period)} scan lines on this data section")

            Dim render As RenderOptions = RenderOptions.CreateDefault
            Dim lines As Integer = SignalAnalysis.DefaultLineCount

            ' 1. the amplitude oscillogram of the first 32 scan lines
            Dim oscCount As Integer = Math.Min(data.Length, CInt(period * 32))
            Dim osc As Double()() = Probe.Oscillogram(data, 0, oscCount, 1600, 600)

            render.percentile_low = 0
            render.percentile_high = 1
            Call ImageRender.SavePng($"{dir}/oscillogram.png", osc, render)
            Call Console.WriteLine($"  -> {dir}/oscillogram.png ({oscCount} samples)")

            ' 2. the folded scan line raster
            Dim fold As Double()() = Probe.Fold(data, 0, period, lines)
            Dim foldOptions As RenderOptions = RenderOptions.CreateDefault
            foldOptions.percentile_low = 0.02
            foldOptions.percentile_high = 0.98
            Call ImageRender.SavePng($"{dir}/fold.png", fold, foldOptions)
            Call Console.WriteLine($"  -> {dir}/fold.png ({fold(0).Length} x {lines})")

            ' 3. the folded pixel cell preview
            Dim preview As Double()() = Probe.FoldPixel(data, 0, period, lines, SignalAnalysis.DefaultPixelCount)
            Call ImageRender.SavePng($"{dir}/fold-pixel.png", preview, foldOptions)
            Call Console.WriteLine($"  -> {dir}/fold-pixel.png ({SignalAnalysis.DefaultPixelCount} x {lines})")

            ' 3b. the transposed layout, for the scan direction checking
            Call ImageRender.SavePng($"{dir}/fold-pixel-transposed.png", Probe.Transpose(preview), foldOptions)
            Call Console.WriteLine($"  -> {dir}/fold-pixel-transposed.png ({lines} x {SignalAnalysis.DefaultPixelCount})")

            ' 3c. pixel clock calibration via the calibration circle roundness
            Call Console.WriteLine()
            Call Console.WriteLine("pixel clock calibration (the calibration circle must be a round circle):")
            Call Console.WriteLine("   cells    ring dx    ring dy     dy/dx")

            For Each cells As Integer In New Integer() {320, 384, 400, 416, 448, 480, 512, 544, 576, 640}
                Dim dx As Integer = 0
                Dim dy As Integer = 0

                If Probe.MeasureCircle(data, 0, period, lines, cells, dx, dy) Then
                    Call Console.WriteLine($"  {cells,6}    {dx,7}    {dy,7}    {(dy / CDbl(dx)).ToString("F3")}")
                End If
            Next

            ' 4. zoom into the scan line sync region
            Dim zoom As Integer = Math.Min(CInt(period), 900)
            Dim lineStart As Double()() = Probe.Fold(data, 0, period, lines, zoom)
            Call ImageRender.SavePng($"{dir}/line-start.png", lineStart, foldOptions)
            Call Console.WriteLine($"  -> {dir}/line-start.png ({zoom} x {lines})")

            ' 5. the mean scan line waveform
            Dim meanLine As Double() = Probe.MeanLine(data, 0, period, lines, 3200)
            Dim meanPlot As Double()() = Probe.Oscillogram(meanLine.Select(Function(v) CSng(v)).ToArray, 0, meanLine.Length, 3200, 400)

            Call ImageRender.SavePng($"{dir}/mean-line.png", meanPlot, render)
            Call Console.WriteLine($"  -> {dir}/mean-line.png (3200 samples)")

            Call Console.WriteLine()
            Call Console.WriteLine("mean scan line profile:")
            Call PrintProfile(meanLine, 64)

            Call Console.WriteLine()
            Call Console.WriteLine("scan line amplitude statistics (baseline drift check):")
            Call Console.WriteLine("  index      mean        rms       peak")

            For Each stat As Probe.LineStat In Probe.LineStats(data, 0, period, lines)
                If stat.index < 24 Then
                    Call Console.WriteLine($"  {stat.index,5}  {stat.mean.ToString("F6"),10}  {stat.rms.ToString("F6"),9}  {stat.peak.ToString("F6"),9}")
                End If
            Next
        End Using

        Return 0
    End Function

    Private Sub PrintProfile(values As Double(), steps As Integer)
        Dim step_ As Integer = Math.Max(1, values.Length \ steps)

        For i As Integer = 0 To steps - 1
            Dim from As Integer = i * step_
            Dim [to] As Integer = Math.Min(values.Length, from + step_)
            Dim sum As Double = 0

            For j As Integer = from To [to] - 1
                sum += values(j)
            Next

            Dim value As Double = sum / ([to] - from)
            Dim bar As Integer = Math.Min(40, CInt(Math.Abs(value) * 400))

            Call Console.WriteLine($"  {from,5}: {value.ToString("F6").PadLeft(11)} {New String(" "c, If(value < 0, 0, 0))}{New String("-"c, bar)}")
        Next
    End Sub

    Private Sub PrintUsage()
        Dim text As New StringBuilder

        Call text.AppendLine("golden record image decoder")
        Call text.AppendLine()
        Call text.AppendLine("usage:")
        Call text.AppendLine("  test [--diagnose] [--wav <path>] [--start <frame>] [--length <frame>]")
        Call text.AppendLine("       [--channel <left|right>] [--env-window <n>] [--json <path>]")
        Call text.AppendLine()
        Call text.AppendLine("  test --decode [--start <frame>] [--length <frame>] [--channel <left|right>]")
        Call text.AppendLine("       [--out <png path>] [--gamma <value>] [--percentile <low,high>]")
        Call text.AppendLine("       [--white <value>] [--invert] [--palette <name>] [--csv <path>]")
        Call text.AppendLine()
        Call text.AppendLine("  test --batch [--out <directory>] [--first <n>] [--count <n>] [--channel <left|right>]")
        Call text.AppendLine()
        Call text.AppendLine("common options:")
        Call text.AppendLine($"  --wav <path>       the golden record wave file, by default it is searched from the repository docs directory")
        Call text.AppendLine($"  --start <frame>    the start frame of the image data section (default {Program.DefaultStartFrame})")
        Call text.AppendLine($"  --length <frame>   the frame length of the image data section (default {Program.DefaultLengthFrame})")

        Call Console.WriteLine(text.ToString)
    End Sub
End Module

''' <summary>
''' the parsed command line options
''' </summary>
Friend Class CliOptions

    Public Property wav As String
    Public Property start As Long = Program.DefaultStartFrame
    Public Property length As Long = Program.DefaultLengthFrame
    Public Property channel As Integer = 0
    Public Property envWindow As Integer = 96
    Public Property diagnose As Boolean = True
    Public Property probe As Boolean
    Public Property compare As Boolean
    Public Property image As String
    Public Property reference As String
    Public Property period As Double
    Public Property batch As Boolean
    Public Property help As Boolean
    Public Property out As String
    Public Property report As String
    Public Property csv As String
    Public Property gamma As Double = 1.0
    Public Property white As Double = 1.0
    Public Property invert As Boolean
    Public Property palette As String = "gray"
    Public Property percentileLow As Double = 0.005
    Public Property percentileHigh As Double = 0.995
    Public Property firstImage As Integer = 0
    Public Property imageCount As Integer = -1
    Public Property pixels As Integer = SignalAnalysis.DefaultPixelCount
    Public Property lines As Integer = SignalAnalysis.DefaultLineCount
    Public Property guard As Double = 0
    Public Property noAlign As Boolean

    Public ReadOnly Property channelName As String
        Get
            Return SignalAnalysis.ChannelName(channel)
        End Get
    End Property

    ''' <summary>
    ''' the decoder parameters which are measured from the waveform data itself
    ''' </summary>
    Public ReadOnly Property DecoderArguments As DecoderArgument
        Get
            Return New DecoderArgument With {
                .lines = lines,
                .period = period,
                .pixels = pixels,
                .guard = guard,
                .remove_baseline = True,
                .use_rms = True,
                .align = Not noAlign,
                .align_window = 48
            }
        End Get
    End Property

    ''' <summary>
    ''' the image rendering parameters
    ''' </summary>
    Public ReadOnly Property RenderArguments As RenderOptions
        Get
            Return New RenderOptions With {
                .percentile_low = percentileLow,
                .percentile_high = percentileHigh,
                .gamma = gamma,
                .white = white,
                .invert = invert,
                .palette = palette
            }
        End Get
    End Property

    Public Shared Function Parse(args As String()) As CliOptions
        Dim opts As New CliOptions
        Dim i As Integer = 0

        If args.Length = 0 Then
            Return opts
        End If

        opts.diagnose = False

        While i < args.Length
            Dim key As String = args(i).ToLower
            Dim value As String = If(i + 1 < args.Length, args(i + 1), Nothing)

            Select Case key
                Case "--help", "-h", "/?"
                    opts.help = True
                Case "--diagnose", "-d"
                    opts.diagnose = True
                Case "--probe"
                    opts.probe = True
                Case "--compare"
                    opts.compare = True
                    opts.diagnose = False
                Case "--image"
                    opts.image = value
                    i += 1
                Case "--reference"
                    opts.reference = value
                    i += 1
                Case "--period"
                    opts.period = Double.Parse(value)
                    i += 1
                Case "--decode"
                    ' the single image decoding mode
                Case "--batch"
                    opts.batch = True
                Case "--wav", "-w"
                    opts.wav = value
                    i += 1
                Case "--start", "-s"
                    opts.start = Long.Parse(value)
                    i += 1
                Case "--length", "-l"
                    opts.length = Long.Parse(value)
                    i += 1
                Case "--channel", "-c"
                    opts.channel = ParseChannel(value)
                    i += 1
                Case "--env-window"
                    opts.envWindow = Integer.Parse(value)
                    i += 1
                Case "--out", "-o"
                    opts.out = value
                    i += 1
                Case "--json"
                    opts.report = value
                    i += 1
                Case "--csv"
                    opts.csv = value
                    i += 1
                Case "--gamma"
                    opts.gamma = Double.Parse(value)
                    i += 1
                Case "--white"
                    opts.white = Double.Parse(value)
                    i += 1
                Case "--invert"
                    opts.invert = True
                Case "--palette"
                    opts.palette = value
                    i += 1
                Case "--percentile"
                    Dim parts As String() = value.Split(","c)

                    If parts.Length <> 2 Then
                        Throw New ArgumentException($"invalid percentile value '{value}', it should be a pair like 0.005,0.995")
                    End If

                    opts.percentileLow = Double.Parse(parts(0))
                    opts.percentileHigh = Double.Parse(parts(1))
                    i += 1
                Case "--first"
                    opts.firstImage = Integer.Parse(value)
                    i += 1
                Case "--count"
                    opts.imageCount = Integer.Parse(value)
                    i += 1
                Case "--pixels"
                    opts.pixels = Integer.Parse(value)
                    i += 1
                Case "--lines"
                    opts.lines = Integer.Parse(value)
                    i += 1
                Case "--guard"
                    opts.guard = Double.Parse(value)
                    i += 1
                Case "--no-align"
                    opts.noAlign = True
                Case Else
                    Throw New ArgumentException($"unknown command line option '{args(i)}'!")
            End Select

            i += 1
        End While

        If String.IsNullOrEmpty(opts.wav) Then
            opts.wav = FindDefaultWaveFile()
        End If

        If opts.out Is Nothing Then
            opts.out = ResolveOutputPath(opts.batch, opts.probe)
        End If

        If opts.compare Then
            If String.IsNullOrEmpty(opts.image) Then
                opts.image = FindRepositoryRoot() & "/docs/decoded/probe/fold-pixel.png"
            End If

            If String.IsNullOrEmpty(opts.reference) Then
                opts.reference = FindRepositoryRoot() & "/docs/images/Calibration_circle.png"
            End If
        End If

        Return opts
    End Function

    Private Shared Function ParseChannel(value As String) As Integer
        Select Case value.ToLower
            Case "left", "l", "0" : Return 0
            Case "right", "r", "1" : Return 1
            Case Else
                Throw New ArgumentException($"invalid channel value '{value}', it should be left or right!")
        End Select
    End Function

    Private Shared Function ResolveOutputPath(batch As Boolean, probe As Boolean) As String
        If probe Then
            Return FindRepositoryRoot() & "/docs/decoded/probe"
        ElseIf batch Then
            Return FindRepositoryRoot() & "/docs/decoded"
        Else
            Return FindRepositoryRoot() & "/docs/decoded/circle.png"
        End If
    End Function

    ''' <summary>
    ''' locate the golden record wave file from the repository docs directory
    ''' </summary>
    Public Shared Function FindDefaultWaveFile() As String
        Dim path As String = FindRepositoryRoot() & "/docs/384kHzStereo.wav"

        If Not File.Exists(path) Then
            Throw New FileNotFoundException($"can not found the golden record wave file at '{path}', please specify it via the --wav option!")
        End If

        Return path
    End Function

    ''' <summary>
    ''' walk up the directory tree to locate the repository root directory
    ''' </summary>
    Public Shared Function FindRepositoryRoot() As String
        Dim dir As New DirectoryInfo(AppContext.BaseDirectory)

        While dir IsNot Nothing
            If File.Exists(dir.FullName & "/voyager-1.sln") Then
                Return dir.FullName.Replace("\", "/")
            End If

            dir = dir.Parent
        End While

        Return Directory.GetCurrentDirectory.Replace("\", "/")
    End Function
End Class
