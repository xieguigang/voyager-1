' /********************************************************************************/
'
' Module GoldenRecord
'
'   the measured calibration constants of the golden record image data.
'
'   those values are the properties of the concrete data source (the analog recording
'   on the 384kHz wave file of the voyager golden record), they are NOT the generic
'   signal processing parameters, so they are kept on this decoder project instead of
'   the shared signal processing library.
'
' /********************************************************************************/

''' <summary>
''' the measured calibration constants of the golden record image data
''' </summary>
Public Module GoldenRecord

    ''' <summary>
    ''' the number of the image pixel cells on a single scan line.
    ''' 
    ''' this value is measured from the official calibration circle image of the golden
    ''' record (the reference image is 529 x 364 pixels): the scan line of the analog
    ''' signal is one image column, and the image rows are the pixel cells inside the
    ''' scan line, so the image is 364 pixel cells high.
    ''' </summary>
    Public Const DefaultPixelCount As Integer = 364

    ''' <summary>
    ''' the number of the scan lines of a single image (the value 512 on the cover of
    ''' the golden record)
    ''' </summary>
    Public Const DefaultLineCount As Integer = 512

    ''' <summary>
    ''' the number of the pixel cell slots inside a single scan line period.
    ''' 
    ''' the golden record image data has a leading blanking/sync region on each scan
    ''' line, so the scan line period contains more pixel cell slots than the image
    ''' pixel number. this value is measured from the calibration circle image of the
    ''' golden record: the ring of the calibration image is a true circle only at this
    ''' pixel clock, that is about 7.5 samples per pixel cell (a 51.2 kHz pixel clock)
    ''' on the 384kHz wave file.
    ''' </summary>
    Public Const CellSlotsPerPeriod As Double = 426.3

    ''' <summary>
    ''' the measured samples per pixel cell of the golden record image data on the
    ''' 384kHz wave file
    ''' </summary>
    Public Const NominalPixelPitch As Double = 7.5

    ''' <summary>
    ''' the nominal scan line period of the golden record image data on the 384kHz wave file
    ''' </summary>
    Public Const NominalLinePeriod As Double = 3197.34

End Module
