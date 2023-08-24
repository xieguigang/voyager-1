Public Class PixelDecode

    Public Property size As Integer = 364
    Public Property aligns As Integer()
    Public Property pixels As Single()()

    Public ReadOnly Property length As Integer
        Get
            Return pixels.Length
        End Get
    End Property

End Class
