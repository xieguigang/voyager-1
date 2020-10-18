Imports System.Runtime.CompilerServices

Module WaveProcessor

    <Extension>
    Public Function PreProcessing(data As Single()) As Single()
        For i = 0 To data.Length - 1
            data(i) = -data(i)
        Next

        Dim base As Single = Math.Abs(data.Min)

        For i = 0 To data.Length - 1
            data(i) += base
        Next

        Return data
    End Function
End Module
