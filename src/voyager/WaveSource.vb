' /********************************************************************************/
'
' Summary:
'
'   random access reader of the raw sample frames on a wave file,
'   it bypasses the Microsoft.VisualBasic.Data.Wave.Sample object allocation
'   for make the batch decoding of the whole golden record wave file possible.
'
'   the byte offset of the sample data section is resolved by the wav parser
'   library (Microsoft.VisualBasic.Data.Wave), and then the raw float/pcm samples
'   are read directly from the file stream for performance.
'
' /********************************************************************************/

Imports System.IO
Imports Microsoft.VisualBasic.Data.IO
Imports Microsoft.VisualBasic.Data.Wave

''' <summary>
''' random access reader for a single channel sample data on a wave file
''' </summary>
Public Class WaveSource : Implements IDisposable

    ''' <summary>
    ''' the file path of current wave file
    ''' </summary>
    ''' <returns></returns>
    Public ReadOnly Property Path As String

    ''' <summary>
    ''' the number of the audio channels on current wave file
    ''' </summary>
    ''' <returns></returns>
    Public ReadOnly Property Channels As Integer

    ''' <summary>
    ''' samples per second
    ''' </summary>
    ''' <returns></returns>
    Public ReadOnly Property SampleRate As Integer

    ''' <summary>
    ''' 32/64 for the IEEE float wave data, or 8/16/24/32 for the PCM data
    ''' </summary>
    ''' <returns></returns>
    Public ReadOnly Property BitsPerSample As Integer

    ''' <summary>
    ''' True for the IEEE float wave data, False for the integer PCM data
    ''' </summary>
    ''' <returns></returns>
    Public ReadOnly Property IsFloat As Boolean

    ''' <summary>
    ''' the total number of the sample frames on current wave file
    ''' </summary>
    ''' <returns></returns>
    Public ReadOnly Property FrameCount As Long

    ''' <summary>
    ''' the duration of the wave file in seconds
    ''' </summary>
    ''' <returns></returns>
    Public ReadOnly Property Duration As Double
        Get
            Return FrameCount / SampleRate
        End Get
    End Property

    Dim fs As FileStream
    Dim frameSize As Integer
    Dim baseOffset As Long

    Private Sub New(path As String, channels As Integer, sampleRate As Integer, bits As Integer,
                    isFloat As Boolean, frameCount As Long, baseOffset As Long)

        Me.Path = path
        Me.Channels = channels
        Me.SampleRate = sampleRate
        Me.BitsPerSample = bits
        Me.IsFloat = isFloat
        Me.FrameCount = frameCount
        Me.baseOffset = baseOffset
        Me.frameSize = channels * (bits \ 8)
        Me.fs = File.OpenRead(path)
    End Sub

    ''' <summary>
    ''' open a given wave file for the random access of the raw sample frame data
    ''' </summary>
    ''' <param name="path"></param>
    ''' <returns></returns>
    Public Shared Function Open(path As String) As WaveSource
        Dim baseOffset As Long
        Dim channels As Integer
        Dim sampleRate As Integer
        Dim bits As Integer
        Dim isFloat As Boolean
        Dim fmt As FMTSubChunk

        ' resolve the byte position of the sample data section
        ' via the wav parser library
        Using reader As BinaryDataReader = New BinaryDataReader(File.OpenRead(path))
            Dim wav As WaveFile = WaveFile.Open(reader, lazy:=True)
            fmt = wav.fmt
            baseOffset = DirectCast(wav.data, LazyDataChunk).CalculateOffset(0, scan0:=8)
            Call wav.Dispose()
        End Using

        channels = fmt.channels
        sampleRate = fmt.SampleRate
        bits = fmt.BitsPerSample
        isFloat = Not fmt.isPCM

        Dim fileSize As Long = New FileInfo(path).Length
        Dim frameSize As Integer = channels * (bits \ 8)
        Dim frameCount As Long = (fileSize - baseOffset) \ frameSize

        Return New WaveSource(path, channels, sampleRate, bits, isFloat, frameCount, baseOffset)
    End Function

    ''' <summary>
    ''' read a consecutive block of the sample frames on a given channel
    ''' </summary>
    ''' <param name="channelIndex">the zero based channel index</param>
    ''' <param name="startFrame">the zero based start frame position</param>
    ''' <param name="count">the number of the sample frames for read</param>
    ''' <returns></returns>
    Public Function ReadChannel(channelIndex As Integer, startFrame As Long, count As Long) As Single()
        Dim data As Single() = New Single(count - 1) {}

        Call ReadChannel(channelIndex, startFrame, data, 0, count)

        Return data
    End Function

    ''' <summary>
    ''' read a consecutive block of the sample frames on a given channel into a pre-allocated buffer
    ''' </summary>
    Public Sub ReadChannel(channelIndex As Integer, startFrame As Long, buffer As Single(),
                           offset As Integer, count As Long)

        Dim raw As Byte() = New Byte(count * frameSize - 1) {}

        Call fs.Seek(baseOffset + startFrame * frameSize, SeekOrigin.Begin)
        Call ReadFully(raw)

        Dim p As Integer = channelIndex * (BitsPerSample \ 8)

        If IsFloat Then
            For i As Integer = 0 To count - 1
                buffer(offset + i) = BitConverter.ToSingle(raw, p)
                p += frameSize
            Next
        Else
            For i As Integer = 0 To count - 1
                buffer(offset + i) = DecodePcm(raw, p)
                p += frameSize
            Next
        End If
    End Sub

    ''' <summary>
    ''' read a consecutive block of the sample frames on multiple channels in one pass
    ''' </summary>
    ''' <param name="channels">the zero based channel indices for read</param>
    Public Function ReadChannels(startFrame As Long, count As Long, channels As Integer()) As Single()()
        Dim raw As Byte() = New Byte(count * frameSize - 1) {}

        Call fs.Seek(baseOffset + startFrame * frameSize, SeekOrigin.Begin)
        Call ReadFully(raw)

        Dim result As Single()() = New Single(channels.Length - 1)() {}

        For c As Integer = 0 To channels.Length - 1
            result(c) = New Single(count - 1) {}
        Next

        Dim positions As Integer() = New Integer(channels.Length - 1) {}

        For c As Integer = 0 To channels.Length - 1
            positions(c) = channels(c) * (BitsPerSample \ 8)
        Next

        For i As Integer = 0 To count - 1
            For c As Integer = 0 To channels.Length - 1
                If IsFloat Then
                    result(c)(i) = BitConverter.ToSingle(raw, positions(c))
                Else
                    result(c)(i) = DecodePcm(raw, positions(c))
                End If

                positions(c) += frameSize
            Next
        Next

        Return result
    End Function

    Private Shared Function DecodePcm(raw As Byte(), p As Integer) As Single
        Return raw(p) / 128.0
    End Function

    Private Sub ReadFully(raw As Byte())
        Dim offset As Integer = 0

        While offset < raw.Length
            Dim n As Integer = fs.Read(raw, offset, raw.Length - offset)

            If n <= 0 Then
                Throw New EndOfStreamException($"unexpected end of the wave file stream [{offset}]!")
            End If

            offset += n
        End While
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        If fs IsNot Nothing Then
            Call fs.Dispose()
            fs = Nothing
        End If
    End Sub
End Class
