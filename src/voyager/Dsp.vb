' /********************************************************************************/
'
' Summary:
'
'   Module Dsp
'
'     Function: Fft, PowerSpectrum, Percentile, Median, Mean, MovingAverage,
'               Rectify, AutoCorrelation, FindPeaks, ParabolicPeak, Decimate
'
' /********************************************************************************/

Imports System.Runtime.CompilerServices
Imports Microsoft.VisualBasic.Linq

''' <summary>
''' the digital signal processing helpers for the golden record image decoder.
''' 
''' the `FFT` module of the sciBASIC runtime is not included in the reference closure
''' of current project, so a compact radix-2 implementation is provided here.
''' </summary>
Public Module Dsp

    ''' <summary>
    ''' in-place radix-2 complex FFT, the length of the data must be a power of two
    ''' </summary>
    Public Sub Fft(re As Double(), im As Double(), Optional inverse As Boolean = False)
        Dim n As Integer = re.Length

        If n <= 1 Then
            Return
        End If

        If (n And (n - 1)) <> 0 Then
            Throw New ArgumentException($"the data length {n} of the FFT is not a power of two!")
        End If

        ' bit reversal permutation
        Dim j As Integer = 0
        For i As Integer = 0 To n - 1
            If i < j Then
                Dim tmp As Double = re(i)
                re(i) = re(j)
                re(j) = tmp
                tmp = im(i)
                im(i) = im(j)
                im(j) = tmp
            End If

            Dim m As Integer = n >> 1

            While j >= m AndAlso m > 0
                j -= m
                m >>= 1
            End While

            j += m
        Next

        ' butterfly
        Dim len As Integer = 2
        Dim sign As Double = If(inverse, 1.0, -1.0)

        While len <= n
            Dim ang As Double = 2 * Math.PI / len * sign
            Dim wr As Double = Math.Cos(ang)
            Dim wi As Double = Math.Sin(ang)
            Dim step_ As Integer = len \ 2
            Dim i As Integer = 0

            While i < n
                Dim cr As Double = 1.0
                Dim ci As Double = 0.0

                For k As Integer = 0 To step_ - 1
                    Dim xr As Double = re(i + k + step_)
                    Dim xi As Double = im(i + k + step_)
                    Dim vr As Double = xr * cr - xi * ci
                    Dim vi As Double = xr * ci + xi * cr
                    Dim ur As Double = re(i + k)
                    Dim ui As Double = im(i + k)

                    re(i + k) = ur + vr
                    im(i + k) = ui + vi
                    re(i + k + step_) = ur - vr
                    im(i + k + step_) = ui - vi

                    Dim ncr As Double = cr * wr - ci * wi
                    ci = cr * wi + ci * wr
                    cr = ncr
                Next

                i += len
            End While

            len <<= 1
        End While

        If inverse Then
            For i As Integer = 0 To n - 1
                re(i) /= n
                im(i) /= n
            Next
        End If
    End Sub

    ''' <summary>
    ''' complex spectrum of a real valued signal, the length of the result is ``n/2+1``
    ''' </summary>
    ''' <param name="x"></param>
    ''' <param name="window">apply the hann window on the signal before the transform</param>
    <Extension>
    Public Function PowerSpectrum(x As Double(), Optional window As Boolean = True) As Double()
        Dim n As Integer = RoundupPow2(x.Length)
        Dim re As Double() = New Double(n - 1) {}
        Dim im As Double() = New Double(n - 1) {}

        For i As Integer = 0 To x.Length - 1
            Dim w As Double = If(window, 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / (x.Length - 1)), 1.0)

            re(i) = x(i) * w
        Next

        Call Fft(re, im)

        Dim spec As Double() = New Double(n \ 2) {}

        For i As Integer = 0 To spec.Length - 1
            spec(i) = Math.Sqrt(re(i) * re(i) + im(i) * im(i)) / (x.Length / 2)
        Next

        Return spec
    End Function

    ''' <summary>
    ''' round the input value up to the nearest power of two
    ''' </summary>
    Public Function RoundupPow2(n As Integer) As Integer
        Dim p As Integer = 1

        While p < n
            p <<= 1
        End While

        Return p
    End Function

    ''' <summary>
    ''' the value at the given percentile position ``p`` in range ``[0,1]``,
    ''' the input data does not need to be sorted.
    ''' </summary>
    Public Function Percentile(data As Double(), p As Double) As Double
        If data.Length = 0 Then
            Return Double.NaN
        End If

        Dim sorted As Double() = data.ToArray
        Array.Sort(sorted)

        Return Quantile(sorted, p)
    End Function

    ''' <summary>
    ''' the quantile value on an already sorted data array, ``p`` in range ``[0,1]``
    ''' </summary>
    Public Function Quantile(sorted As Double(), p As Double) As Double
        If sorted.Length = 0 Then
            Return Double.NaN
        End If

        If p <= 0 Then
            Return sorted(Scan0)
        ElseIf p >= 1 Then
            Return sorted(sorted.Length - 1)
        End If

        Dim pos As Double = p * (sorted.Length - 1)
        Dim i As Integer = CInt(Math.Floor(pos))
        Dim f As Double = pos - i

        If i >= sorted.Length - 1 Then
            Return sorted(sorted.Length - 1)
        End If

        Return sorted(i) * (1 - f) + sorted(i + 1) * f
    End Function

    <Extension>
    Public Function Median(x As Double()) As Double
        Return Percentile(x, 0.5)
    End Function

    <Extension>
    Public Function Mean(x As Single()) As Double
        If x.Length = 0 Then
            Return Double.NaN
        End If

        Dim sum As Double = 0

        For i As Integer = 0 To x.Length - 1
            sum += x(i)
        Next

        Return sum / x.Length
    End Function

    ''' <summary>
    ''' the root mean square of the given signal
    ''' </summary>
    <Extension>
    Public Function Rms(x As Single()) As Double
        If x.Length = 0 Then
            Return Double.NaN
        End If

        Dim sum As Double = 0

        For i As Integer = 0 To x.Length - 1
            sum += x(i) * x(i)
        Next

        Return Math.Sqrt(sum / x.Length)
    End Function

    ''' <summary>
    ''' rectify the signal and then smooth it by a moving average window,
    ''' this is the amplitude envelope of the given analog signal.
    ''' </summary>
    <Extension>
    Public Function Envelope(x As Single(), Optional window As Integer = 64) As Double()
        Dim n As Integer = x.Length
        Dim rect As Double() = New Double(n - 1) {}

        For i As Integer = 0 To n - 1
            rect(i) = Math.Abs(x(i))
        Next

        Return MovingAverage(rect, window)
    End Function

    ''' <summary>
    ''' centered moving average with a edge clamping
    ''' </summary>
    Public Function MovingAverage(x As Double(), window As Integer) As Double()
        Dim n As Integer = x.Length
        Dim result As Double() = New Double(n - 1) {}
        Dim half As Integer = Math.Max(1, window \ 2)
        Dim sum As Double = 0
        Dim count As Integer = 0

        ' running sum initialization
        For i As Integer = 0 To Math.Min(half, n - 1)
            sum += x(i)
            count += 1
        Next

        For i As Integer = 0 To n - 1
            result(i) = sum / count

            ' advance the window
            Dim drop As Integer = i - half
            Dim add As Integer = i + half + 1

            If drop >= 0 Then
                sum -= x(drop)
                count -= 1
            End If

            If add < n Then
                sum += x(add)
                count += 1
            End If
        Next

        Return result
    End Function

    ''' <summary>
    ''' decimate the signal by the factor ``k`` using a simple averaging low pass filter
    ''' </summary>
    <Extension>
    Public Function Decimate(x As Double(), k As Integer) As Double()
        Dim n As Integer = x.Length \ k
        Dim result As Double() = New Double(n - 1) {}

        For i As Integer = 0 To n - 1
            Dim sum As Double = 0

            For j As Integer = 0 To k - 1
                sum += x(i * k + j)
            Next

            result(i) = sum / k
        Next

        Return result
    End Function

    ''' <summary>
    ''' normalized auto correlation of the given signal on the lag range ``[minLag, maxLag]``
    ''' </summary>
    ''' <param name="x">the input signal, should be already decimated by the caller</param>
    <Extension>
    Public Function AutoCorrelation(x As Double(), minLag As Integer, maxLag As Integer,
                                    Optional maxCount As Integer = 65536) As Double()

        Dim n As Integer = Math.Min(x.Length, maxCount)
        Dim mean As Double = 0

        For i As Integer = 0 To n - 1
            mean += x(i)
        Next

        mean /= n

        Dim variance As Double = 0

        For i As Integer = 0 To n - 1
            Dim d As Double = x(i) - mean

            variance += d * d
        Next

        Dim result As Double() = New Double(maxLag - minLag) {}

        For lag As Integer = minLag To maxLag
            Dim sum As Double = 0

            For i As Integer = 0 To n - lag - 1
                sum += (x(i) - mean) * (x(i + lag) - mean)
            Next

            result(lag - minLag) = If(variance = 0, 0, sum / variance)
        Next

        Return result
    End Function

    ''' <summary>
    ''' the local maxima positions <see cref="Integer"/> index on the given signal,
    ''' a peak is reported when its magnitude is above <paramref name="threshold"/>
    ''' and it is the maximum value inside the ``minDistance`` neighbor region.
    ''' </summary>
    ''' <remarks>
    ''' the peaks are detected via a small window local maxima scan and then a
    ''' greedy non maximum suppression pass, so that the complexity stays linear
    ''' even when the minimal peak distance is large.
    ''' </remarks>
    Public Function FindPeaks(x As Double(), threshold As Double, minDistance As Integer) As Integer()
        Dim candidates As New List(Of Integer)

        For i As Integer = 1 To x.Length - 2
            If x(i) < threshold Then
                Continue For
            End If

            If x(i) < x(i - 1) OrElse x(i) < x(i + 1) Then
                Continue For
            End If

            candidates.Add(i)
        Next

        If candidates.Count = 0 Then
            Return New Integer() {}
        End If

        ' greedy non maximum suppression by the peak magnitude
        Dim ordered As Integer() = candidates _
            .OrderByDescending(Function(i) x(i)) _
            .ToArray
        Dim accepted As New List(Of Integer)
        Dim blocked As Boolean() = New Boolean(x.Length - 1) {}

        For Each i As Integer In ordered
            Dim from As Integer = Math.Max(0, i - minDistance)
            Dim [to] As Integer = Math.Min(x.Length - 1, i + minDistance)
            Dim suppressed As Boolean = False

            For j As Integer = from To [to]
                If blocked(j) Then
                    suppressed = True
                    Exit For
                End If
            Next

            If Not suppressed Then
                accepted.Add(i)

                For j As Integer = from To [to]
                    blocked(j) = True
                Next
            End If
        Next

        Return accepted.OrderBy(Function(i) i).ToArray
    End Function

    ''' <summary>
    ''' sub sample position of a local maximum via the parabolic interpolation
    ''' </summary>
    Public Function ParabolicPeak(x As Double(), i As Integer) As Double
        If i <= 0 OrElse i >= x.Length - 1 Then
            Return i
        End If

        Dim y1 As Double = x(i - 1)
        Dim y2 As Double = x(i)
        Dim y3 As Double = x(i + 1)
        Dim denom As Double = y1 - 2 * y2 + y3

        If denom = 0 Then
            Return i
        End If

        Dim delta As Double = 0.5 * (y1 - y3) / denom

        If Math.Abs(delta) > 1 Then
            Return i
        End If

        Return i + delta
    End Function

    ''' <summary>
    ''' linear interpolation sample of a signal at a fractional position
    ''' </summary>
    Public Function Sample(x As Single(), pos As Double) As Double
        If pos <= 0 Then
            Return x(Scan0)
        End If

        If pos >= x.Length - 1 Then
            Return x(x.Length - 1)
        End If

        Dim i As Integer = CInt(Math.Floor(pos))
        Dim f As Double = pos - i

        Return x(i) * (1 - f) + x(i + 1) * f
    End Function

    ''' <summary>
    ''' mean of the square root amplitudes, a robust intensity measurement of the signal block
    ''' </summary>
    Public Function BlockPower(x As Single(), start As Integer, [end] As Integer) As Double
        [end] = Math.Min([end], x.Length)
        start = Math.Max(0, start)

        Dim sum As Double = 0
        Dim n As Integer = [end] - start

        If n <= 0 Then
            Return 0
        End If

        For i As Integer = start To [end] - 1
            sum += x(i) * x(i)
        Next

        Return Math.Sqrt(sum / n)
    End Function
End Module
