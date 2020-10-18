Imports Microsoft.VisualBasic.Serialization.JSON

Public Class DecoderArgument

    Public Property windowSize As Integer
    Public Property offset As Integer

    Public Overrides Function ToString() As String
        Return Me.GetJson
    End Function

End Class
