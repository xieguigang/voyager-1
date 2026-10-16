' /********************************************************************************/
'
' Summary:
'
'   Module BatchExporter
'
'     Function: DefaultSections, Run, SaveManifest
'
'   the batch export of the golden record images.
'
'   the image data sections on the golden record wave file are located by the
'   pre-defined section table (the same offsets as the ``R/chunks.R`` script),
'   and every section is decoded and exported as a png image file. a failed
'   section does not interrupt the whole batch, the error is recorded in the
'   section result and the summary report.
'
' /********************************************************************************/

Imports System.IO
Imports Microsoft.VisualBasic.Serialization.JSON

''' <summary>
''' an image data section on the golden record wave file
''' </summary>
Public Class ImageSection

    ''' <summary>
    ''' the name of the image, it is also the exported file name
    ''' </summary>
    ''' <returns></returns>
    Public Property name As String

    ''' <summary>
    ''' the zero based start frame position of the section
    ''' </summary>
    ''' <returns></returns>
    Public Property start As Long

    ''' <summary>
    ''' the frame length of the section, zero for the default section length
    ''' </summary>
    ''' <returns></returns>
    Public Property length As Long

    Public Overrides Function ToString() As String
        Return $"{name} @ {start}"
    End Function
End Class

''' <summary>
''' the decoding result of a single image section
''' </summary>
Public Class SectionResult

    Public Property name As String
    Public Property start As Long
    Public Property length As Long
    Public Property success As Boolean
    Public Property file As String
    Public Property line_period As Double
    Public Property pixel_pitch As Double
    Public Property line_count As Integer
    Public Property pixel_count As Integer
    Public Property jitter_std As Double
    Public Property error As String

End Class

''' <summary>
''' the summary report of a batch export
''' </summary>
Public Class BatchResult

    Public Property source As String
    Public Property channel As String
    Public Property output As String
    Public Property exported As Integer
    Public Property failed As Integer

    Public Property sections As SectionResult()

    Public Overrides Function ToString() As String
        Return $"batch export: {exported} exported, {failed} failed"
    End Function
End Class

Public Module BatchExporter

    ''' <summary>
    ''' the data length of an image section on the golden record wave file
    ''' </summary>
    Public Const DefaultSectionLength As Long = 1928181

    ''' <summary>
    ''' the pre-defined image data sections on the golden record wave file,
    ''' they are the same offsets as the ``R/chunks.R`` script
    ''' </summary>
    Public Function DefaultSections() As ImageSection()
        Return {
            New ImageSection With {.name = "01-Calibration_circle", .start = 6000208, .length = DefaultSectionLength},
            New ImageSection With {.name = "02-Solar_location_map", .start = 8465560, .length = DefaultSectionLength},
            New ImageSection With {.name = "03-Mathematical_definitions", .start = 10686671, .length = DefaultSectionLength},
            New ImageSection With {.name = "04-Physical_unit_definitions", .start = 13001000, .length = DefaultSectionLength}
        }
    End Function

    ''' <summary>
    ''' load the image data section table from a text file
    ''' 
    ''' the file contains one section per line in the format of ``name[,start[,length]]``,
    ''' the line which is started with the ``#`` character is a comment.
    ''' </summary>
    Public Function LoadSections(path As String) As ImageSection()
        Dim sections As New List(Of ImageSection)

        For Each line As String In File.ReadAllLines(path)
            Dim text As String = line.Trim

            If text = "" OrElse text.StartsWith("#") Then
                Continue For
            End If

            Dim parts As String() = text.Split(","c)
            Dim section As New ImageSection With {
                .name = parts(0).Trim,
                .start = Long.Parse(parts(If(parts.Length > 1, 1, 0)).Trim)
            }

            If parts.Length > 2 Then
                section.length = Long.Parse(parts(2).Trim)
            Else
                section.length = DefaultSectionLength
            End If

            sections.Add(section)
        Next

        Return sections.ToArray
    End Function

    ''' <summary>
    ''' decode and export the given image data sections
    ''' </summary>
    ''' <param name="wavPath">the file path of the golden record wave file</param>
    ''' <param name="outputDir">the directory for the exported image files</param>
    ''' <param name="sections">the image data sections for export</param>
    ''' <param name="args">the decoder parameters</param>
    ''' <param name="render">the render parameters</param>
    ''' <param name="channel">the zero based channel index</param>
    Public Function Run(wavPath As String, outputDir As String, sections As ImageSection(),
                        args As DecoderArgument, render As RenderOptions,
                        Optional channel As Integer = 0) As BatchResult

        Dim result As New BatchResult With {
            .source = wavPath,
            .channel = SignalAnalysis.ChannelName(channel),
            .output = outputDir,
            .sections = {}
        }

        Call Directory.CreateDirectory(outputDir)

        Dim list As New List(Of SectionResult)

        Using source As WaveSource = WaveSource.Open(wavPath)
            For Each section As ImageSection In sections
                Dim item As New SectionResult With {
                    .name = section.name,
                    .start = section.start,
                    .length = section.length
                }

                Try
                    Dim length As Long = If(section.length > 0, section.length, DefaultSectionLength)
                    Dim available As Long = source.FrameCount - section.start

                    If section.start < 0 OrElse available <= 0 Then
                        Throw New ArgumentOutOfRangeException($"the section '{section.name}' is outside the wave file data!")
                    End If

                    If length > available Then
                        length = available
                    End If

                    Dim data As Single() = source.ReadChannel(channel, section.start, length)
                    Dim image As PixelDecode = ImageDecoder.DecodeSection(data, args)
                    Dim png As String = Path.Combine(outputDir, $"{section.name}.png")

                    Call ImageRender.SavePng(png, image.pixels, render)

                    item.success = True
                    item.file = png
                    item.line_period = image.line_period
                    item.pixel_pitch = image.pixel_pitch
                    item.line_count = image.width
                    item.pixel_count = image.height
                    item.jitter_std = image.jitter_std

                    result.exported += 1
                Catch ex As Exception
                    item.success = False
                    item.error = ex.Message

                    result.failed += 1
                End Try

                list.Add(item)
            Next
        End Using

        result.sections = list.ToArray

        Return result
    End Function

    ''' <summary>
    ''' save the batch report as a json document
    ''' </summary>
    Public Sub SaveManifest(path As String, result As BatchResult)
        Call File.WriteAllText(path, result.GetJson)
    End Sub
End Module
