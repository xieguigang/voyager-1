Imports System.Runtime.CompilerServices

Module WaveProcessor

    <Extension>
    Public Function Invert(data As Single()) As Single()
        For i = 0 To data.Length - 1
            data(i) = -data(i)
        Next

        Return data
    End Function
End Module
