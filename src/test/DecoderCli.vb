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
            ElseIf options.ring Then
                Return RunRing(options)
            ElseIf options.batch Then
                Return RunBatch(options)
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
    ''' decode and export all of the image data sections on the whole wave file
    ''' </summary>
    Private Function RunBatch(options As CliOptions) As Integer
        Dim sections As ImageSection()

        If Not String.IsNullOrEmpty(options.chunks) Then
            sections = BatchExporter.LoadSections(options.chunks)
            Call Console.WriteLine($"loaded {sections.Length} image sections from {options.chunks}")
        Else
            sections = BatchExporter.DefaultSections()
        End If

        If options.firstImage > 0 OrElse options.imageCount >= 0 Then
            Dim from As Integer = Math.Max(0, options.firstImage)
            Dim count As Integer = If(options.imageCount >= 0, options.imageCount, sections.Length - from)

            sections = sections.Skip(from).Take(count).ToArray
        End If

        If sections.Length = 0 Then
            Call Console.Error.WriteLine("[error] no image section is selected for the batch export!")
            Return 2
        End If

        Call Console.WriteLine($"batch decoding {sections.Length} image sections from {options.wav} ...")
        Call Console.WriteLine($"the image files will be exported to {options.out}")
        Call Console.WriteLine()

        Dim sw As Diagnostics.Stopwatch = Diagnostics.Stopwatch.StartNew
        Dim result As BatchResult = BatchExporter.Run(
            wavPath:=options.wav,
            outputDir:=options.out,
            sections:=sections,
            args:=options.DecoderArguments,
            render:=options.RenderArguments,
            channel:=options.channel
        )
        Dim manifest As String = Path.Combine(options.out, "manifest.json")

        Call BatchExporter.SaveManifest(manifest, result)

        Call Console.WriteLine("  name                              success     lines   pixels    period    pitch   jitter")
        Call Console.WriteLine("  -------------------------------------------------------------------------------------")

        For Each item As SectionResult In result.sections
            If item.success Then
                Call Console.WriteLine($"  {item.name,-32} {"ok",7} {item.line_count,9} {item.pixel_count,8} {item.line_period,9:F3} {item.pixel_pitch,8:F4} {item.jitter_std,8:F3}")
            Else
                Call Console.WriteLine($"  {item.name,-32} {"failed",7}  {item.message}")
            End If
        Next

        Call Console.WriteLine()
        Call Console.WriteLine($"{result} in {sw.Elapsed.TotalSeconds.ToString("F2")} seconds.")
        Call Console.WriteLine($"the batch manifest was saved to {manifest}")

        Return If(result.failed = 0, 0, 1)
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
    ''' the pixel clock calibration: the calibration circle of the golden record is a
    ''' true circle, so the ring of the decoded calibration circle image must have the
    ''' same physical width (in scan lines) and height (in pixel cells).
    ''' 
    ''' the relation of the pixel clock calibration is:
    ''' 
    ''' ```
    ''' N = pixelClock * dx / dy
    ''' ```
    ''' 
    ''' where the ``N`` is the number of the pixel cells per scan line which makes the
    ''' calibration circle round again, the ``pixelClock`` is the number of the cell
    ''' slots which are used for one scan line period, and the ``dx``/``dy`` are the
    ''' measured extents of the ring on the scan line axis and the pixel cell axis.
    ''' </summary>
    Private Function RunRing(options As CliOptions) As Integer
        If Not String.IsNullOrEmpty(options.image) Then
            Return MeasureRingOnImage(options.image)
        End If

        Using source As WaveSource = WaveSource.Open(options.wav)
            Dim data As Single() = source.ReadChannel(options.channel, options.start, options.length)
            Dim grid As LineGrid = LineSync.Sync(data, options.lines, options.period, Not options.noAlign, options.alignWindow)
            Dim pitch As Double = If(options.pitch > 0, options.pitch, LineDemodulator.PixelPitch(grid.period))
            Dim opts As New DemodulateOptions With {
                .pixels = options.pixels,
                .pixel_pitch = pitch,
                .guard = options.guard,
                .remove_baseline = True,
                .use_rms = True
            }
            Dim image As Single()() = LineDemodulator.Demodulate(data, grid, opts)
            Dim cells As Integer = image.Length
            Dim lines As Integer = image(0).Length
            Dim skip As Integer = Math.Max(0, Math.Min(cells - 8, options.skipCells))
            Dim used As Integer = cells - skip

            Call Console.WriteLine($"scan line period : {grid.period.ToString("F5")} samples, sync offset {grid.sync_offset.ToString("F2")}")
            Call Console.WriteLine($"pixel clock      : {pitch.ToString("F5")} samples x {options.pixels} cells, guard {options.guard.ToString("F2")} samples")
            Call Console.WriteLine($"raster           : {lines} scan lines x {cells} cells (the last {skip} cells are excluded from the analysis)")
            Call Console.WriteLine()

            Dim flat As Double() = MatrixValues(image, used, lines)
            Dim residual As Double()() = BackgroundRemoved(image, used, lines, 12)

            Call Console.WriteLine($"intensity range  : {flat.Min.ToString("F4")} .. {flat.Max.ToString("F4")}, mean {flat.Average.ToString("F4")}")
            Call Console.WriteLine()

            Dim crossings As Integer() = RingCrossings(residual, used, lines)
            Dim dx As Integer = crossings(1)
            Dim dy As Integer = crossings(3)

            Call Console.WriteLine($"the ring crossings: dx={dx} scan lines, dy={dy} pixel cells")
            Call Console.WriteLine($"the ring roundness: dy/dx={(dy / CDbl(dx)).ToString("F4")}")

            If dy > 0 Then
                Call Console.WriteLine($"the square pixel pitch: {pitch.ToString("F5")} * dx / dy = {(pitch * dx / CDbl(dy)).ToString("F5")} samples per pixel")
                Call Console.WriteLine($"the square pixel clock: {options.pixels * dx / CDbl(dy):F1} cell slots per scan line period")
            End If

            Call Console.WriteLine()
            Call PrintRowVariance(image, used, lines, 48)

            Return 0
        End Using
    End Function

    ''' <summary>
    ''' print the across scan line standard deviation of each pixel cell row.
    ''' 
    ''' the sync pulse and the blanking level are constant on every scan line, so
    ''' their rows have a much smaller across line deviation than the image data rows,
    ''' this profile locates the blanking region of the scan line.
    ''' </summary>
    Private Sub PrintRowVariance(image As Single()(), rows As Integer, cols As Integer, bins As Integer)
        Dim sigma As Double() = New Double(rows - 1) {}

        For r As Integer = 0 To rows - 1
            Dim sum As Double = 0

            For c As Integer = 0 To cols - 1
                sum += image(r)(c)
            Next

            Dim mean As Double = sum / cols
            Dim square As Double = 0

            For c As Integer = 0 To cols - 1
                square += (image(r)(c) - mean) ^ 2
            Next

            sigma(r) = Math.Sqrt(square / cols)
        Next

        Dim max As Double = sigma.Max
        Dim step_ As Integer = Math.Max(1, rows \ bins)

        Call Console.WriteLine("across scan line deviation profile (low value = the blanking region):")

        For i As Integer = 0 To bins - 1
            Dim from As Integer = i * step_
            Dim [to] As Integer = Math.Min(rows, from + step_)

            If [to] <= from Then
                Continue For
            End If

            Dim sum As Double = 0

            For r As Integer = from To [to] - 1
                sum += sigma(r)
            Next

            Dim value As Double = sum / ([to] - from)
            Dim bar As Integer = If(max = 0, 0, CInt(value / max * 48))

            Call Console.WriteLine($"  {from,4}-{[to] - 1,-4}: {value.ToString("F5").PadLeft(9)} {New String("#"c, bar)}")
        Next
    End Sub

    ''' <summary>
    ''' measure the calibration ring of a decoded raster or of an image file
    ''' </summary>
    Private Function MeasureRingOnImage(path As String) As Integer
        Dim png As PngImage = PngReader.Load(path)
        Dim gray As Double()() = png.Gray
        Dim rows As Integer = gray.Length
        Dim cols As Integer = gray(0).Length
        Dim residual As Double()() = BackgroundRemoved(gray, rows, cols, 12)

        Call Console.WriteLine($"image            : {path} ({cols} x {rows})")
        Call Console.WriteLine()

        Return ReportRing(residual, rows, cols)
    End Function

    Private Function ReportRing(residual As Double()(), rows As Integer, cols As Integer) As Integer
        Dim crossings As Integer() = RingCrossings(residual, rows, cols)
        Dim dx As Integer = crossings(1) - crossings(0)
        Dim dy As Integer = crossings(3) - crossings(2)

        Call Console.WriteLine($"the ring crossings: left x={crossings(0)}, right x={crossings(1)} (of {cols}); top y={crossings(2)}, bottom y={crossings(3)} (of {rows})")
        Call Console.WriteLine($"the ring extents  : dx={dx} pixels, dy={dy} pixels, dy/dx={(dy / CDbl(dx)).ToString("F4")}")

        Return 0
    End Function

    ''' <summary>
    ''' locate the calibration ring by the crossings of a straight line through the
    ''' raster centre: the ring is a closed curve, so the line crosses it exactly twice.
    ''' the median of several cut lines is used for the robustness.
    ''' </summary>
    Private Function RingCrossings(residual As Double()(), rows As Integer, cols As Integer) As Integer()
        Dim margin As Integer = 6
        Dim dxs As New List(Of Integer)
        Dim dys As New List(Of Integer)

        ' the horizontal cuts give the ring width on the scan line axis
        For Each offset As Integer In {-cols \ 12, -cols \ 24, 0, cols \ 24, cols \ 12}
            Dim row As Integer = rows \ 2 + offset
            Dim x1 As Integer = 0
            Dim x2 As Integer = 0

            If TryScan(cols, Function(i) residual(row)(i), margin, x1, x2) Then
                dxs.Add(x2 - x1)
            End If
        Next

        ' the vertical cuts give the ring height on the pixel cell axis
        For Each offset As Integer In {-rows \ 12, -rows \ 24, 0, rows \ 24, rows \ 12}
            Dim col As Integer = cols \ 2 + offset
            Dim y1 As Integer = 0
            Dim y2 As Integer = 0

            If TryScan(rows, Function(i) residual(i)(col), margin, y1, y2) Then
                dys.Add(y2 - y1)
            End If
        Next

        Dim dx As Integer = Median(dxs.ToArray)
        Dim dy As Integer = Median(dys.ToArray)

        Return {0, dx, 0, dy}
    End Function

    ''' <summary>
    ''' scan a line profile and return the two strongest peaks on the left and the
    ''' right part of the profile
    ''' </summary>
    Private Function TryScan(n As Integer, profile As Func(Of Integer, Double),
                             margin As Integer, ByRef left As Integer, ByRef right As Integer) As Boolean
        Dim middle As Integer = n \ 2
        Dim best As Double = Double.MinValue
        Dim i As Integer

        left = 0
        right = 0

        For i = margin To middle - 1
            If Math.Abs(profile(i)) > best Then
                best = Math.Abs(profile(i))
                left = i
            End If
        Next

        best = Double.MinValue

        For i = middle + 1 To n - margin - 1
            If Math.Abs(profile(i)) > best Then
                best = Math.Abs(profile(i))
                right = i
            End If
        Next

        Return right > left
    End Function

    Private Function Median(values As Integer()) As Integer
        If values.Length = 0 Then
            Return 0
        End If

        Dim sorted As Integer() = values.ToArray

        Array.Sort(sorted)

        Return sorted(sorted.Length \ 2)
    End Function

    ''' <summary>
    ''' remove the smooth background of the raster by a box blur, the thin ink curve
    ''' of the calibration ring is highlighted by this filter
    ''' </summary>
    Private Function BackgroundRemoved(image As Single()(), rows As Integer, cols As Integer,
                                       radius As Integer) As Double()()
        Return BackgroundRemoved(ImageRender.ToDouble(image), rows, cols, radius)
    End Function

    Private Function BackgroundRemoved(image As Double()(), rows As Integer, cols As Integer,
                                       radius As Integer) As Double()()
        Dim smooth As Double()() = New Double(rows - 1)() {}
        Dim result As Double()() = New Double(rows - 1)() {}
        Dim r As Integer
        Dim c As Integer

        For r = 0 To rows - 1
            smooth(r) = New Double(cols - 1) {}
            result(r) = New Double(cols - 1) {}
        Next

        For r = 0 To rows - 1
            For c = 0 To cols - 1
                Dim sum As Double = 0
                Dim n As Integer = 0
                Dim r0 As Integer = Math.Max(0, r - radius)
                Dim r1 As Integer = Math.Min(rows - 1, r + radius)
                Dim c0 As Integer = Math.Max(0, c - radius)
                Dim c1 As Integer = Math.Min(cols - 1, c + radius)

                For y As Integer = r0 To r1
                    For x As Integer = c0 To c1
                        sum += image(y)(x)
                        n += 1
                    Next
                Next

                smooth(r)(c) = sum / n
            Next
        Next

        For r = 0 To rows - 1
            For c = 0 To cols - 1
                result(r)(c) = image(r)(c) - smooth(r)(c)
            Next
        Next

        Return result
    End Function

    Private Sub PrintRing(name As String, box As Integer(), pixelClock As Integer, lines As Integer, used As Integer)
        Dim w As Integer = box(2)
        Dim h As Integer = box(3)

        If w <= 0 OrElse h <= 0 Then
            Call Console.WriteLine($" {name,-10}  not detected")
            Return
        End If

        Call Console.WriteLine($" {name,-10} {w,9} {h,11} {(h / CDbl(w)).ToString("F3"),8} {(pixelClock * w / CDbl(h)).ToString("F1"),14}")
        Call Console.WriteLine($"            box: x={box(0)}..{box(0) + w - 1} of {lines}, y={box(1)}..{box(1) + h - 1} of {used}")
    End Sub

    Private Function MatrixValues(image As Single()(), rows As Integer, cols As Integer) As Double()
        Dim values As Double() = New Double(rows * cols - 1) {}
        Dim p As Integer = 0

        For r As Integer = 0 To rows - 1
            For c As Integer = 0 To cols - 1
                values(p) = image(r)(c)
                p += 1
            Next
        Next

        Return values
    End Function

    Private Function MatrixValues(image As Double()(), rows As Integer, cols As Integer) As Double()
        Dim values As Double() = New Double(rows * cols - 1) {}
        Dim p As Integer = 0

        For r As Integer = 0 To rows - 1
            For c As Integer = 0 To cols - 1
                values(p) = image(r)(c)
                p += 1
            Next
        Next

        Return values
    End Function
    Private Function RingBox(image As Double()(), rows As Integer, cols As Integer,
                             [select] As Func(Of Double, Boolean)) As Integer()
        Dim minX As Integer = Integer.MaxValue
        Dim maxX As Integer = Integer.MinValue
        Dim minY As Integer = Integer.MaxValue
        Dim maxY As Integer = Integer.MinValue

        For r As Integer = 0 To rows - 1
            For c As Integer = 0 To cols - 1
                If [select](image(r)(c)) Then
                    If c < minX Then minX = c
                    If c > maxX Then maxX = c
                    If r < minY Then minY = r
                    If r > maxY Then maxY = r
                End If
            Next
        Next

        If minX > maxX Then
            Return {0, 0, 0, 0}
        End If

        Return {minX, minY, maxX - minX + 1, maxY - minY + 1}
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

            ' 3c. the aligned decoding with the measured pixel clock
            Call Console.WriteLine()

            Dim grid As LineGrid = LineSync.Sync(data, lines, period, Not options.noAlign, options.alignWindow)
            Dim opts As New DemodulateOptions With {
                .pixels = options.pixels,
                .pixel_pitch = If(options.pitch > 0, options.pitch, LineDemodulator.PixelPitch(grid.period)),
                .guard = options.guard,
                .remove_baseline = True,
                .use_rms = True
            }
            Dim decoded As Single()() = LineDemodulator.Demodulate(data, grid, opts)
            Dim decodedOptions As RenderOptions = RenderOptions.CreateDefault

            decodedOptions.percentile_low = 0.01
            decodedOptions.percentile_high = 0.99
            Call ImageRender.SavePng($"{dir}/decoded.png", decoded, decodedOptions)
            Call Console.WriteLine($"  -> {dir}/decoded.png ({decoded(0).Length} x {decoded.Length})")

            Call Console.WriteLine()
            Call Console.WriteLine("pixel clock calibration (the calibration circle must be a round circle):")
            Call Console.WriteLine($"  pixel clock    : {opts.pixel_pitch.ToString("F5")} samples per pixel cell")

            Dim crossings As Integer() = RingCrossings(BackgroundRemoved(decoded, decoded.Length, decoded(0).Length, 12), decoded.Length, decoded(0).Length)

            If crossings(3) > 0 AndAlso crossings(1) > 0 Then
                Call Console.WriteLine($"  the ring       : dx={crossings(1)} scan lines, dy={crossings(3)} pixel cells, dy/dx={(crossings(3) / CDbl(crossings(1))).ToString("F4")}")
                Call Console.WriteLine($"  the round pitch: {(opts.pixel_pitch * crossings(1) / CDbl(crossings(3))).ToString("F5")} samples per pixel cell")
                Call Console.WriteLine($"  the round clock: {(grid.period / (opts.pixel_pitch * crossings(1) / CDbl(crossings(3)))).ToString("F1")} cell slots per scan line period")
            End If

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
        Call text.AppendLine("  test --probe [--out <directory>] [--period <samples>]")
        Call text.AppendLine()
        Call text.AppendLine("  test --ring [--pixels <n>] [--pitch <samples>] [--guard <samples>] [--skip-cells <n>]")
        Call text.AppendLine("       [--image <png path>]    the pixel clock calibration via the calibration circle roundness")
        Call text.AppendLine()
        Call text.AppendLine("  test --batch [--out <directory>] [--first <n>] [--count <n>] [--channel <left|right>]")
        Call text.AppendLine()
        Call text.AppendLine("common options:")
        Call text.AppendLine($"  --wav <path>       the golden record wave file, by default it is searched from the repository docs directory")
        Call text.AppendLine($"  --start <frame>    the start frame of the image data section (default {Program.DefaultStartFrame})")
        Call text.AppendLine($"  --length <frame>   the frame length of the image data section (default {Program.DefaultLengthFrame})")
        Call text.AppendLine($"  --pixels <n>       the image pixel cells per scan line (default {SignalAnalysis.DefaultPixelCount})")
        Call text.AppendLine($"  --lines <n>        the number of the scan lines of the image (default {SignalAnalysis.DefaultLineCount}, 0 for auto detect)")
        Call text.AppendLine($"  --period <samples> the scan line period (default 0, measured from the waveform data)")
        Call text.AppendLine($"  --guard <samples>  the samples which are skipped after the sync pulse position")
        Call text.AppendLine($"  --no-align         disable the per scan line synchronization")
        Call text.AppendLine($"  --align-window <n> the maximal per scan line alignment shift (default 48 samples)")

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
    Public Property invert As Boolean = True
    Public Property smooth As Integer = 2
    Public Property palette As String = "gray"
    Public Property percentileLow As Double = 0.005
    Public Property percentileHigh As Double = 0.995
    Public Property firstImage As Integer = 0
    Public Property imageCount As Integer = -1
    Public Property pixels As Integer = SignalAnalysis.DefaultPixelCount
    Public Property lines As Integer = SignalAnalysis.DefaultLineCount
    Public Property guard As Double = 0
    Public Property noAlign As Boolean
    Public Property noRms As Boolean
    Public Property ring As Boolean
    Public Property pitch As Double
    Public Property skipCells As Integer
    Public Property alignWindow As Integer = 48
    Public Property chunks As String

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
                .use_rms = Not noRms,
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
                .smooth = smooth,
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
                Case "--chunks"
                    opts.chunks = value
                    i += 1
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
                Case "--positive", "--no-invert"
                    opts.invert = False
                Case "--smooth"
                    opts.smooth = Integer.Parse(value)
                    i += 1
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
                Case "--no-rms"
                    opts.noRms = True
                Case "--align-window"
                    opts.alignWindow = Integer.Parse(value)
                    i += 1
                Case "--ring"
                    opts.ring = True
                    opts.diagnose = False
                Case "--pitch"
                    opts.pitch = Double.Parse(value)
                    i += 1
                Case "--skip-cells"
                    opts.skipCells = Integer.Parse(value)
                    i += 1
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
