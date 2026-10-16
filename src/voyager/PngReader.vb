' /********************************************************************************/
'
' Summary:
'
'   Module PngReader
'
'     Function: Load, Inflate, Unfilter
'
'   a minimal managed PNG decoder for the 8/16 bit gray, true color and alpha
'   images. it is used for loading the official decoded golden record images
'   for the decoder verification, because the sciBASIC imaging runtime does not
'   provides a png decoder on the net10.0 target framework.
'
' /********************************************************************************/

Imports System.IO
Imports System.IO.Compression

''' <summary>
''' the loaded png raster image data
''' </summary>
Public Class PngImage

Public Property Width As Integer
    Public Property Height As Integer

    ''' <summary>
    ''' the number of the color channels: 1 = gray, 2 = gray+alpha, 3 = rgb, 4 = rgba
    ''' </summary>
    ''' <returns></returns>
    Public Property Channels As Integer

    ''' <summary>
    ''' the row major raw pixel data, the row padding is already removed
    ''' </summary>
    ''' <returns></returns>
    Public Property Pixels As Byte()

    ''' <summary>
    ''' the bit depth and the color type of the source png file
    ''' </summary>
    ''' <returns></returns>
    Public Property BitDepth As Integer
    Public Property ColorType As Integer

    ''' <summary>
    ''' the byte length of the compressed IDAT section and the inflated raw pixel section
    ''' </summary>
    ''' <returns></returns>
    Public Property IdatBytes As Integer
    Public Property RawBytes As Integer

    ''' <summary>
    ''' true when the raw pixel section is fully transparent black
    ''' </summary>
    ''' <returns></returns>
    Public Property IsEmpty As Boolean

    ''' <summary>
    ''' the gray scale intensity of some reference image is stored on the alpha channel
    ''' with the rgb color all filled with zero (this is the output of the
    ''' ``Color.FromArgb(alpha, 0, 0, 0)`` based gray scale renderer of the
    ''' Voyager-Golden-Record-Decoder project), in this case the intensity value is
    ''' evaluated from the alpha channel just like the image is composited on a white canvas
    ''' </summary>
    ''' <returns></returns>
    Public Property GrayInAlpha As Boolean

    ''' <summary>
    ''' the gray scale intensity matrix of current image, the values are scaled into the range [0,1]
    ''' </summary>
    Public Function Gray() As Double()()
        Dim matrix As Double()() = New Double(Height - 1)() {}

        For y As Integer = 0 To Height - 1
            Dim row As Double() = New Double(Width - 1) {}
            Dim p As Integer = y * Width * Channels

            For x As Integer = 0 To Width - 1
                Select Case Channels
                    Case 1
                        row(x) = Pixels(p) / 255.0
                    Case 2
                        row(x) = If(GrayInAlpha, 1.0 - Pixels(p + 1) / 255.0, Pixels(p) / 255.0)
                    Case Else
                        If GrayInAlpha Then
                            row(x) = 1.0 - Pixels(p + 3) / 255.0
                        Else
                            row(x) = (0.299 * Pixels(p + 2) + 0.587 * Pixels(p + 1) + 0.114 * Pixels(p)) / 255.0
                        End If
                End Select

                p += Channels
            Next

            matrix(y) = row
        Next

        Return matrix
    End Function
End Class

Public Module PngReader

    ''' <summary>
    ''' load a png image file
    ''' </summary>
    ''' <remarks>
    ''' only the non interlaced 8/16 bit gray/true color images are supported
    ''' </remarks>
    Public Function Load(path As String) As PngImage
        Dim bytes As Byte() = File.ReadAllBytes(path)
        Dim signature As Byte() = {&H89, &H50, &H4E, &H47, &HD, &HA, &H1A, &HA}

        For i As Integer = 0 To 7
            If bytes(i) <> signature(i) Then
                Throw New InvalidDataException($"'{path}' is not a valid png image file!")
            End If
        Next

        Dim offset As Integer = 8
        Dim width As Integer = 0
        Dim height As Integer = 0
        Dim bitDepth As Integer = 0
        Dim colorType As Integer = 0
        Dim interlace As Integer = 0
        Dim idat As New MemoryStream
        Dim palette As Byte() = Nothing

        While offset + 8 <= bytes.Length
            Dim length As Integer = ReadInt32(bytes, offset)
            Dim type As String = System.Text.Encoding.ASCII.GetString(bytes, offset + 4, 4)
            Dim data As Integer = offset + 8

            Select Case type
                Case "IHDR"
                    width = ReadInt32(bytes, data)
                    height = ReadInt32(bytes, data + 4)
                    bitDepth = bytes(data + 8)
                    colorType = bytes(data + 9)
                    interlace = bytes(data + 12)
                Case "PLTE"
                    palette = New Byte(length - 1) {}
                    Call Array.Copy(bytes, data, palette, 0, length)
                Case "IDAT"
                    Call idat.Write(bytes, data, length)
                Case "IEND"
                    Exit While
            End Select

            offset = data + length + 4
        End While

        If interlace <> 0 Then
            Throw New NotSupportedException($"the interlaced png image '{path}' is not supported!")
        End If

        If bitDepth <> 8 Then
            Throw New NotSupportedException($"the {bitDepth} bit png image '{path}' is not supported!")
        End If

        Dim channels As Integer

        Select Case colorType
            Case 0 : channels = 1
            Case 2 : channels = 3
            Case 3 : channels = 3 ' the palette image is expanded into the true color image
            Case 4 : channels = 2
            Case 6 : channels = 4
            Case Else
                Throw New NotSupportedException($"the png color type {colorType} is not supported!")
        End Select

        Dim raw As Byte() = Inflate(idat.ToArray)
        Dim sourceChannels As Integer = If(colorType = 3, 1, channels)
        Dim rowBytes As Integer = width * sourceChannels
        Dim decoded As Byte() = New Byte(width * height * channels - 1) {}
        Dim previous As Byte() = New Byte(rowBytes - 1) {}
        Dim position As Integer = 0

        For y As Integer = 0 To height - 1
            Dim filter As Integer = raw(position)
            Dim current As Byte() = New Byte(rowBytes - 1) {}

            position += 1
            Call Array.Copy(raw, position, current, 0, rowBytes)
            position += rowBytes
            Call Unfilter(filter, current, previous, sourceChannels)

            If colorType = 3 Then
                For x As Integer = 0 To width - 1
                    Dim index As Integer = current(x) * 3

                    decoded((y * width + x) * 3) = palette(index)
                    decoded((y * width + x) * 3 + 1) = palette(index + 1)
                    decoded((y * width + x) * 3 + 2) = palette(index + 2)
                Next
            Else
                Call Array.Copy(current, 0, decoded, y * rowBytes, rowBytes)
            End If

            previous = current
        Next

        Dim isEmpty As Boolean = True

        For Each b As Byte In decoded
            If b <> 0 Then
                isEmpty = False
                Exit For
            End If
        Next

        Return New PngImage With {
            .Width = width,
            .Height = height,
            .Channels = channels,
            .Pixels = decoded,
            .BitDepth = bitDepth,
            .ColorType = colorType,
            .IdatBytes = CInt(idat.Length),
            .RawBytes = raw.Length,
            .IsEmpty = isEmpty,
            .GrayInAlpha = IsBlackRgb(decoded, channels)
        }
    End Function

    ''' <summary>
    ''' checks that the rgb color of almost all of the pixels are black
    ''' </summary>
    Private Function IsBlackRgb(pixels As Byte(), channels As Integer) As Boolean
        If channels < 4 Then
            Return False
        End If

        Dim black As Integer = 0
        Dim total As Integer = 0

        For p As Integer = 0 To pixels.Length - channels Step channels
            If pixels(p) = 0 AndAlso pixels(p + 1) = 0 AndAlso pixels(p + 2) = 0 Then
                black += 1
            End If

            total += 1
        Next

        Return total > 0 AndAlso black / total > 0.9
    End Function

    ''' <summary>
    ''' the png scan line filters
    ''' </summary>
    Private Sub Unfilter(filter As Integer, current As Byte(), previous As Byte(), bpp As Integer)
        Select Case filter
            Case 0
                ' none
            Case 1
                For i As Integer = bpp To current.Length - 1
                    current(i) += current(i - bpp)
                Next
            Case 2
                For i As Integer = 0 To current.Length - 1
                    current(i) += previous(i)
                Next
            Case 3
                For i As Integer = 0 To current.Length - 1
                    Dim left As Integer = If(i >= bpp, current(i - bpp), 0)

                    current(i) += CByte((left + previous(i)) \ 2)
                Next
            Case 4
                For i As Integer = 0 To current.Length - 1
                    Dim a As Integer = If(i >= bpp, current(i - bpp), 0)
                    Dim b As Integer = previous(i)
                    Dim c As Integer = If(i >= bpp, previous(i - bpp), 0)
                    Dim p As Integer = a + b - c
                    Dim pa As Integer = Math.Abs(p - a)
                    Dim pb As Integer = Math.Abs(p - b)
                    Dim pc As Integer = Math.Abs(p - c)
                    Dim predictor As Integer

                    If pa <= pb AndAlso pa <= pc Then
                        predictor = a
                    ElseIf pb <= pc Then
                        predictor = b
                    Else
                        predictor = c
                    End If

                    current(i) += CByte(predictor)
                Next
            Case Else
                Throw New NotSupportedException($"unknown png scan line filter {filter}!")
        End Select
    End Sub

    Private Function Inflate(data As Byte()) As Byte()
        Using ms As New MemoryStream(data)
            ' skip the 2 bytes zlib header
            Call ms.Seek(2, SeekOrigin.Begin)

            Using ds As New DeflateStream(ms, CompressionMode.Decompress)
                Using out As New MemoryStream
                    Call ds.CopyTo(out)

                    Return out.ToArray
                End Using
            End Using
        End Using
    End Function

    Private Function ReadInt32(bytes As Byte(), offset As Integer) As Integer
        Return (CInt(bytes(offset)) << 24) Or (CInt(bytes(offset + 1)) << 16) Or (CInt(bytes(offset + 2)) << 8) Or CInt(bytes(offset + 3))
    End Function
End Module
