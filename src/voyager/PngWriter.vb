' /********************************************************************************/
'
' Summary:
'
'   Module PngWriter
'
'     Function: Save, WriteChunk, ZlibCompress, Adler32, Crc32
'
'   a minimal managed PNG encoder.
'
'   the sciBASIC imaging runtime only provides a GDI+ based png encoder on the
'   windows target framework, and only a BMP encoder on the net10.0 target,
'   so a dependency free png encoder is implemented here for the command line
'   decoding tool and the batch image export.
'
' /********************************************************************************/

Imports System.IO
Imports System.IO.Compression

''' <summary>
''' a minimal dependency free PNG encoder which supports
''' the gray scale, the true color and the gray/true color with alpha images
''' </summary>
Public Module PngWriter

    ReadOnly CrcTable As UInteger() = Crc32Table()

    ''' <summary>
    ''' save a raw pixel buffer as a png file
    ''' </summary>
    ''' <param name="path">the output png file path</param>
    ''' <param name="pixels">the row major raw pixel data</param>
    ''' <param name="width"></param>
    ''' <param name="height"></param>
    ''' <param name="channels">1 = gray, 2 = gray+alpha, 3 = rgb, 4 = rgba</param>
    Public Sub Save(path As String, pixels As Byte(), width As Integer, height As Integer, channels As Integer)
        Using s As Stream = File.Create(path)
            Call Save(s, pixels, width, height, channels)
            Call s.Flush()
        End Using
    End Sub

    Public Sub Save(s As Stream, pixels As Byte(), width As Integer, height As Integer, channels As Integer)
        Dim colorType As Byte

        Select Case channels
            Case 1 : colorType = 0
            Case 2 : colorType = 4
            Case 3 : colorType = 2
            Case 4 : colorType = 6
            Case Else
                Throw New NotSupportedException($"unsupported png channel number {channels}!")
        End Select

        ' png signature
        Call s.Write({&H89, &H50, &H4E, &H47, &HD, &HA, &H1A, &HA}, 0, 8)

        ' IHDR
        Dim ihdr As Byte() = New Byte(12) {}
        Call WriteInt32(ihdr, 0, width)
        Call WriteInt32(ihdr, 4, height)
        ihdr(8) = 8                ' bit depth
        ihdr(9) = colorType
        ihdr(10) = 0               ' compression
        ihdr(11) = 0               ' filter
        ihdr(12) = 0               ' interlace
        Call WriteChunk(s, "IHDR", ihdr)

        ' IDAT: the scan lines with the filter byte
        Dim rowSize As Integer = width * channels
        Dim raw As Byte() = New Byte(height * (rowSize + 1) - 1) {}
        Dim offset As Integer = 0

        For y As Integer = 0 To height - 1
            raw(offset) = 0        ' filter type: none
            offset += 1
            Array.Copy(pixels, y * rowSize, raw, offset, rowSize)
            offset += rowSize
        Next

        Call WriteChunk(s, "IDAT", ZlibCompress(raw))
        Call WriteChunk(s, "IEND", Nothing)
    End Sub

    Private Sub WriteInt32(buffer As Byte(), offset As Integer, value As Integer)
        buffer(offset) = CByte((value >> 24) And &HFF)
        buffer(offset + 1) = CByte((value >> 16) And &HFF)
        buffer(offset + 2) = CByte((value >> 8) And &HFF)
        buffer(offset + 3) = CByte(value And &HFF)
    End Sub

    Private Sub WriteChunk(s As Stream, type As String, data As Byte())
        Dim dataLen As Integer = If(data Is Nothing, 0, data.Length)
        Dim header As Byte() = New Byte(3) {}

        Call WriteInt32(header, 0, dataLen)
        Call s.Write(header, 0, 4)

        Dim typeBytes As Byte() = System.Text.Encoding.ASCII.GetBytes(type)
        Call s.Write(typeBytes, 0, 4)

        If dataLen > 0 Then
            Call s.Write(data, 0, dataLen)
        End If

        Dim crcData As Byte()

        If dataLen > 0 Then
            crcData = New Byte(3 + dataLen) {}
            Call Array.Copy(typeBytes, crcData, 4)
            Call Array.Copy(data, 0, crcData, 4, dataLen)
        Else
            crcData = typeBytes
        End If

        Dim crc As UInteger = Crc32(crcData)
        Dim crcBytes As Byte() = New Byte(3) {}

        Call WriteInt32(crcBytes, 0, CInt(crc And &HFFFFFFFFL))
        Call s.Write(crcBytes, 0, 4)
    End Sub

    Private Function ZlibCompress(data As Byte()) As Byte()
        Using ms As New MemoryStream
            ms.WriteByte(&H78)
            ms.WriteByte(&H1)

            Using ds As New DeflateStream(ms, CompressionMode.Compress, leaveOpen:=True)
                Call ds.Write(data, 0, data.Length)
            End Using

            Dim adler As UInteger = Adler32(data)
            ms.WriteByte(CByte((adler >> 24) And &HFF))
            ms.WriteByte(CByte((adler >> 16) And &HFF))
            ms.WriteByte(CByte((adler >> 8) And &HFF))
            ms.WriteByte(CByte(adler And &HFF))

            Return ms.ToArray
        End Using
    End Function

    Private Function Adler32(data As Byte()) As UInteger
        Dim a As UInteger = 1
        Dim b As UInteger = 0

        For Each c As Byte In data
            a = (a + c) Mod 65521
            b = (b + a) Mod 65521
        Next

        Return (b << 16) Or a
    End Function

    Private Function Crc32(data As Byte()) As UInteger
        Dim crc As UInteger = &HFFFFFFFFUI

        For Each c As Byte In data
            crc = CrcTable((crc Xor c) And &HFF) Xor (crc >> 8)
        Next

        Return crc Xor &HFFFFFFFFUI
    End Function

    Private Function Crc32Table() As UInteger()
        Dim table As UInteger() = New UInteger(255) {}

        For i As UInteger = 0 To 255
            Dim c As UInteger = i

            For j As Integer = 0 To 7
                If (c And 1UI) = 1UI Then
                    c = &HEDB88320UI Xor (c >> 1)
                Else
                    c >>= 1
                End If
            Next

            table(CInt(i)) = c
        Next

        Return table
    End Function
End Module
