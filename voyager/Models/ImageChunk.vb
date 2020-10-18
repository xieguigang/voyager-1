#Region "Microsoft.VisualBasic::aebc540544f62eef1a4ef4eb7a3296d9, voyager\Models\ImageChunk.vb"

    ' Author:
    ' 
    '       xieguigang (i@xieguigang.me)
    ' 
    ' Copyright (c) 2020 i@xieguigang.me
    ' 
    ' 
    ' MIT License
    ' 
    ' Permission is hereby granted, free of charge, to any person obtaining a copy
    ' of this software and associated documentation files (the "Software"), to deal
    ' in the Software without restriction, including without limitation the rights
    ' to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    ' copies of the Software, and to permit persons to whom the Software is
    ' furnished to do so, subject to the following conditions:
    ' 
    ' The above copyright notice and this permission notice shall be included in all
    ' copies or substantial portions of the Software.
    ' 
    ' THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    ' IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    ' FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    ' AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    ' LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    ' OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
    ' SOFTWARE.



    ' /********************************************************************************/

    ' Summaries:

    ' Class ImageChunk
    ' 
    '     Properties: channel, length, size, start
    ' 
    '     Function: GetSampleData, ToString
    ' 
    ' /********************************************************************************/

#End Region

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

