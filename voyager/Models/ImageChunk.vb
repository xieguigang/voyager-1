Imports System.Drawing
Imports Microsoft.VisualBasic.Data.Wave

Public Class ImageChunk

    Public Property channel As ChannelPositions
    Public Property start As Integer
    Public Property length As Integer
    ''' <summary>
    ''' it is always [364,540] pixels?
    ''' </summary>
    ''' <returns></returns>
    Public Property size As Size

    Public Function GetSampleData(samples As IEnumerable(Of Sample)) As Single()
        If channel = ChannelPositions.None OrElse channel = ChannelPositions.Left Then
            Return samples.Select(Function(d) d.left).ToArray
        Else
            Return samples.Select(Function(d) d.right).ToArray
        End If
    End Function

    Public Overrides Function ToString() As String
        Return $"[{start}] {length} pixels on {channel} channel."
    End Function

End Class
