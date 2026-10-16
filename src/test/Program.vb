Imports System

''' <summary>
''' the command line tool for the golden record image decoding.
''' 
''' ```
''' test --diagnose
''' test --decode --start 6000208 --length 1928181 --out .\out\circle.png
''' test --batch --out .\out\images
''' ```
''' </summary>
Module Program

    ''' <summary>
    ''' the default data section of the first calibration circle image on the golden record
    ''' </summary>
    Public Const DefaultStartFrame As Long = 6000208
    Public Const DefaultLengthFrame As Long = 1928181

    Sub Main(args As String())
        Dim exitCode As Integer = DecoderCli.Run(args)

        Environment.Exit(exitCode)
    End Sub
End Module
