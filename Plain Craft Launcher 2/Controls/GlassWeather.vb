Imports System.Windows.Threading

''' <summary>
''' 磨砂玻璃层的天气粒子效果，绘制在背景之上、界面内容之下。
''' 支持晴天（暖光与浮尘）、多云（云影流动）、雨天（雨丝与挂壁水珠滚落）、雪天（积雪与飘雪）。
''' </summary>
Public Class GlassWeather
    Inherits FrameworkElement

#Region "属性"

    Private _Mode As Integer = 2
    ''' <summary>
    ''' 天气模式：1 晴，2 多云，3 雨，4 雪。
    ''' </summary>
    Public Property Mode As Integer
        Get
            Return _Mode
        End Get
        Set(value As Integer)
            Dim V As Integer = If(value >= 1 AndAlso value <= 4, value, 2)
            If _Mode = V Then Return
            _Mode = V
            Reseed()
        End Set
    End Property

    ''' <summary>
    ''' 当前实际参与运算的天气模式，未初始化时为 0。
    ''' </summary>
    Public ReadOnly Property CurrentMode As Integer
        Get
            Return _Mode
        End Get
    End Property

    Private _Density As Double = 0.7
    ''' <summary>
    ''' 粒子浓度，0 到 1。
    ''' </summary>
    Public Property Density As Double
        Get
            Return _Density
        End Get
        Set(value As Double)
            Dim V As Double = Math.Max(0, Math.Min(1, value))
            If Math.Abs(_Density - V) < 0.01 Then Return
            _Density = V
            Reseed()
        End Set
    End Property

    Private _BeamStrength As Double = 0.7
    ''' <summary>
    ''' 晴天阳光反射（光带）的强度，0 到 1。
    ''' </summary>
    Public Property BeamStrength As Double
        Get
            Return _BeamStrength
        End Get
        Set(value As Double)
            _BeamStrength = Math.Max(0, Math.Min(1, value))
        End Set
    End Property

    Private _Speed As Double = 0.5
    ''' <summary>
    ''' 动效速度，0 到 1，0.5 为默认速度。
    ''' </summary>
    Public Property Speed As Double
        Get
            Return _Speed
        End Get
        Set(value As Double)
            _Speed = Math.Max(0, Math.Min(1, value))
        End Set
    End Property

#End Region

#Region "生命周期"

    Private ReadOnly Rand As New Random()
    Private ReadOnly Timer As DispatcherTimer
    Private LastTick As Long = 0
    Private Elapsed As Double = 0
    Private IsLoadedOnce As Boolean = False

    Public Sub New()
        IsHitTestVisible = False
        Focusable = False
        Timer = New DispatcherTimer With {.Interval = New TimeSpan(0, 0, 0, 0, 33)}
        AddHandler Timer.Tick, AddressOf Tick
        AddHandler Loaded, Sub()
                               IsLoadedOnce = True
                               LastTick = 0
                               Timer.Start()
                           End Sub
        AddHandler Unloaded, Sub() Timer.Stop()
        AddHandler SizeChanged, Sub() Reseed()
    End Sub

    ''' <summary>
    ''' 停止动画并释放粒子数据。
    ''' </summary>
    Public Sub StopEffect()
        Timer.Stop()
        Droplets.Clear()
        Trails.Clear()
        Streaks.Clear()
        Flakes.Clear()
        Motes.Clear()
        Clouds.Clear()
    End Sub

    Private Sub Tick(sender As Object, e As EventArgs)
        If Not IsLoadedOnce OrElse Not IsVisible Then Return
        Dim NowMs As Long = GetTimeMs()
        Dim Dt As Double = If(LastTick = 0, 33, Math.Min(NowMs - LastTick, 120)) / 1000.0
        Dt *= 0.2 + _Speed * 1.8
        LastTick = NowMs
        Elapsed += Dt
        Select Case _Mode
            Case 1 : UpdateSunny(Dt)
            Case 3 : UpdateRain(Dt)
            Case 4 : UpdateSnow(Dt)
            Case Else : UpdateCloudy(Dt)
        End Select
        InvalidateVisual()
    End Sub

    Protected Overrides Sub OnRender(dc As DrawingContext)
        MyBase.OnRender(dc)
        Dim W As Double = ActualWidth
        Dim H As Double = ActualHeight
        If W <= 1 OrElse H <= 1 Then Return
        Select Case _Mode
            Case 1 : DrawSunny(dc, W, H)
            Case 3 : DrawRain(dc, W, H)
            Case 4 : DrawSnow(dc, W, H)
            Case Else : DrawCloudy(dc, W, H)
        End Select
    End Sub

#End Region

#Region "画笔"

    Private Shared ReadOnly BrushDropFill As SolidColorBrush
    Private Shared ReadOnly BrushDropShine As SolidColorBrush
    Private Shared ReadOnly PenDropEdge As Pen
    Private Shared ReadOnly BrushTrail As SolidColorBrush
    Private Shared ReadOnly PensStreak() As Pen
    Private Shared ReadOnly BrushesFlake() As SolidColorBrush
    Private Shared ReadOnly BrushFog As LinearGradientBrush
    Private Shared ReadOnly BrushFrostBottom As LinearGradientBrush
    Private Shared ReadOnly BrushFrostTop As LinearGradientBrush
    Private Shared ReadOnly BrushSun As RadialGradientBrush
    Private Shared ReadOnly BrushCloud As RadialGradientBrush
    Private Shared ReadOnly BrushFlakeSoft As RadialGradientBrush
    Private Shared ReadOnly BrushBeam As LinearGradientBrush
    Private Shared ReadOnly BrushBeamBright As LinearGradientBrush
    Private Shared ReadOnly BrushCloudVeil As LinearGradientBrush

    Shared Sub New()
        BrushDropFill = Frozen(Color.FromArgb(52, 92, 112, 138))
        BrushDropShine = Frozen(Color.FromArgb(230, 255, 255, 255))
        PenDropEdge = FrozenPen(Color.FromArgb(74, 70, 88, 112), 1)
        BrushTrail = Frozen(Color.FromArgb(26, 88, 108, 132))
        PensStreak = {
            FrozenPen(Color.FromArgb(34, 84, 104, 130), 1),
            FrozenPen(Color.FromArgb(52, 84, 104, 130), 1),
            FrozenPen(Color.FromArgb(72, 84, 104, 130), 1)
        }
        BrushesFlake = {
            Frozen(Color.FromArgb(132, 206, 220, 240)),
            Frozen(Color.FromArgb(186, 222, 232, 246)),
            Frozen(Color.FromArgb(236, 240, 246, 255))
        }
        BrushFog = FrozenLinear({New GradientStop(Color.FromArgb(44, 255, 255, 255), 0),
                                  New GradientStop(Color.FromArgb(0, 255, 255, 255), 1)}, New Point(0.5, 1), New Point(0.5, 0.45))
        BrushFrostBottom = FrozenLinear({New GradientStop(Color.FromArgb(62, 255, 255, 255), 0),
                                          New GradientStop(Color.FromArgb(0, 255, 255, 255), 1)}, New Point(0.5, 1), New Point(0.5, 0.72))
        BrushFrostTop = FrozenLinear({New GradientStop(Color.FromArgb(38, 255, 255, 255), 0),
                                       New GradientStop(Color.FromArgb(0, 255, 255, 255), 1)}, New Point(0.5, 0), New Point(0.5, 0.13))
        BrushSun = FrozenRadial(Color.FromArgb(92, 255, 234, 170), Color.FromArgb(0, 255, 234, 170))
        BrushCloud = FrozenRadial(Color.FromArgb(30, 118, 138, 168), Color.FromArgb(0, 118, 138, 168))
        BrushFlakeSoft = FrozenRadial(Color.FromArgb(160, 255, 255, 255), Color.FromArgb(0, 255, 255, 255))
        '阳光斜射产生的柔和光带
        BrushBeam = FrozenLinear({New GradientStop(Color.FromArgb(0, 255, 246, 214), 0),
                                  New GradientStop(Color.FromArgb(28, 255, 246, 214), 0.38),
                                  New GradientStop(Color.FromArgb(56, 255, 249, 222), 0.5),
                                  New GradientStop(Color.FromArgb(28, 255, 246, 214), 0.62),
                                  New GradientStop(Color.FromArgb(0, 255, 246, 214), 1)}, New Point(0, 0), New Point(1, 0))
        '最亮的一道反射高光
        BrushBeamBright = FrozenLinear({New GradientStop(Color.FromArgb(0, 255, 252, 236), 0),
                                        New GradientStop(Color.FromArgb(0, 255, 252, 236), 0.44),
                                        New GradientStop(Color.FromArgb(92, 255, 252, 236), 0.5),
                                        New GradientStop(Color.FromArgb(0, 255, 252, 236), 0.56),
                                        New GradientStop(Color.FromArgb(0, 255, 252, 236), 1)}, New Point(0, 0), New Point(1, 0))
        '阴天的冷色雾气，用来与晴天拉开差距
        BrushCloudVeil = FrozenLinear({New GradientStop(Color.FromArgb(34, 116, 134, 162), 0),
                                       New GradientStop(Color.FromArgb(16, 116, 134, 162), 0.55),
                                       New GradientStop(Color.FromArgb(4, 116, 134, 162), 1)}, New Point(0, 0), New Point(0, 1))
    End Sub

    Private Shared Function Frozen(C As Color) As SolidColorBrush
        Dim B As New SolidColorBrush(C)
        B.Freeze()
        Return B
    End Function
    Private Shared Function FrozenPen(C As Color, Thickness As Double) As Pen
        Dim P As New Pen(Frozen(C), Thickness)
        P.Freeze()
        Return P
    End Function
    Private Shared Function FrozenLinear(Stops As GradientStop(), StartPoint As Point, EndPoint As Point) As LinearGradientBrush
        Dim B As New LinearGradientBrush With {.StartPoint = StartPoint, .EndPoint = EndPoint}
        For Each Item As GradientStop In Stops
            B.GradientStops.Add(Item)
        Next
        B.Freeze()
        Return B
    End Function
    Private Shared Function FrozenRadial(Center As Color, Edge As Color) As RadialGradientBrush
        Dim B As New RadialGradientBrush()
        B.GradientStops.Add(New GradientStop(Center, 0))
        B.GradientStops.Add(New GradientStop(Edge, 1))
        B.Freeze()
        Return B
    End Function

#End Region

#Region "粒子数据"

    Private Class RainDrop
        Public X As Double
        Public Y As Double
        Public R As Double
        Public VY As Double
        Public Moving As Boolean
        Public Delay As Double
    End Class
    Private Class RainTrail
        Public X As Double
        Public Y1 As Double
        Public Y2 As Double
        Public R As Double
        Public Life As Double
    End Class
    Private Class RainStreak
        Public X As Double
        Public Y As Double
        Public VY As Double
        Public Len As Double
        Public Style As Integer
    End Class
    Private Class SnowFlake
        Public X As Double
        Public Y As Double
        Public R As Double
        Public VY As Double
        Public Sway As Double
        Public Phase As Double
        Public Style As Integer
        Public Soft As Boolean
    End Class
    Private Class DustMote
        Public X As Double
        Public Y As Double
        Public R As Double
        Public VY As Double
        Public Phase As Double
        Public Style As Integer
    End Class
    Private Class CloudBlob
        Public X As Double
        Public Y As Double
        Public W As Double
        Public H As Double
        Public VX As Double
    End Class

    Private ReadOnly Droplets As New List(Of RainDrop)
    Private ReadOnly Trails As New List(Of RainTrail)
    Private ReadOnly Streaks As New List(Of RainStreak)
    Private ReadOnly Flakes As New List(Of SnowFlake)
    Private ReadOnly Motes As New List(Of DustMote)
    Private ReadOnly Clouds As New List(Of CloudBlob)

    Private Sub Reseed()
        Dim W As Double = ActualWidth
        Dim H As Double = ActualHeight
        If W < 1 OrElse H < 1 Then Return
        Elapsed = 0
        Droplets.Clear()
        Trails.Clear()
        Streaks.Clear()
        Flakes.Clear()
        Motes.Clear()
        Clouds.Clear()
        Dim DensityScale As Double = 0.15 + _Density * 0.85
        Select Case _Mode
            Case 1
                For i As Integer = 1 To Math.Max(4, CInt(30 * DensityScale))
                    Motes.Add(NewMote(W, H))
                Next
            Case 3
                Dim DropCount As Integer = Math.Max(10, CInt(Math.Max(36, Math.Min(120, W * H / 9000)) * DensityScale))
                For i As Integer = 1 To DropCount
                    Droplets.Add(NewDrop(W, H))
                Next
                For i As Integer = 1 To Math.Max(6, CInt(34 * DensityScale))
                    Streaks.Add(NewStreak(W, H))
                Next
            Case 4
                Dim FlakeCount As Integer = Math.Max(8, CInt(Math.Max(28, Math.Min(110, W * H / 11000)) * DensityScale))
                For i As Integer = 1 To FlakeCount
                    Flakes.Add(NewFlake(W, H))
                Next
            Case Else
                For i As Integer = 1 To Math.Max(2, CInt(6 * DensityScale))
                    Clouds.Add(NewCloud(W, H, i))
                Next
        End Select
        InvalidateVisual()
    End Sub

    Private Function NewDrop(W As Double, H As Double) As RainDrop
        Return New RainDrop With {
            .X = Rand.NextDouble() * W,
            .Y = Rand.NextDouble() * H,
            .R = 1.4 + Rand.NextDouble() * 2.8,
            .Moving = False,
            .Delay = 1.2 + Rand.NextDouble() * 9,
            .VY = 0
        }
    End Function
    Private Function NewStreak(W As Double, H As Double) As RainStreak
        Dim Len As Double = 12 + Rand.NextDouble() * 26
        Return New RainStreak With {
            .X = Rand.NextDouble() * W,
            .Y = -Rand.NextDouble() * H,
            .VY = 700 + Rand.NextDouble() * 760,
            .Len = Len,
            .Style = Rand.Next(0, PensStreak.Length)
        }
    End Function
    Private Function NewFlake(W As Double, H As Double) As SnowFlake
        Dim Soft As Boolean = Rand.NextDouble() < 0.16
        Return New SnowFlake With {
            .X = Rand.NextDouble() * W,
            .Y = Rand.NextDouble() * H,
            .R = If(Soft, 3.5 + Rand.NextDouble() * 5.5, 0.8 + Rand.NextDouble() * 1.8),
            .VY = If(Soft, 5 + Rand.NextDouble() * 9, 15 + Rand.NextDouble() * 34),
            .Sway = 5 + Rand.NextDouble() * 18,
            .Phase = Rand.NextDouble() * 6.283,
            .Style = Rand.Next(0, BrushesFlake.Length),
            .Soft = Soft
        }
    End Function
    Private Function NewMote(W As Double, H As Double) As DustMote
        Return New DustMote With {
            .X = Rand.NextDouble() * W,
            .Y = Rand.NextDouble() * H,
            .R = 0.7 + Rand.NextDouble() * 1.6,
            .VY = 6 + Rand.NextDouble() * 14,
            .Phase = Rand.NextDouble() * 6.283,
            .Style = Rand.Next(0, BrushesFlake.Length)
        }
    End Function
    Private Function NewCloud(W As Double, H As Double, Index As Integer) As CloudBlob
        Return New CloudBlob With {
            .X = Rand.NextDouble() * (W + 400) - 200,
            .Y = H * (0.12 + Index * 0.16) + Rand.NextDouble() * 40,
            .W = W * (0.45 + Rand.NextDouble() * 0.35),
            .H = H * (0.22 + Rand.NextDouble() * 0.16),
            .VX = 6 + Rand.NextDouble() * 14
        }
    End Function

#End Region

#Region "更新"

    Private Sub UpdateRain(Dt As Double)
        Dim W As Double = ActualWidth
        Dim H As Double = ActualHeight
        If W < 1 OrElse H < 1 Then Return
        For i As Integer = Droplets.Count - 1 To 0 Step -1
            Dim D As RainDrop = Droplets(i)
            If D.Moving Then
                D.VY = Math.Min(D.VY + 260 * Dt, 240)
                D.Y += D.VY * Dt
                '与附近静止水珠合并
                For j As Integer = Droplets.Count - 1 To 0 Step -1
                    If j = i Then Continue For
                    Dim O As RainDrop = Droplets(j)
                    If Not O.Moving AndAlso Math.Abs(O.X - D.X) < D.R + O.R AndAlso Math.Abs(O.Y - D.Y) < D.R + O.R Then
                        D.R = Math.Min(D.R + O.R * 0.45, 7.5)
                        Droplets(j) = NewDrop(W, H)
                        Exit For
                    End If
                Next
                If D.Y > H + D.R * 2 Then
                    Trails.Add(New RainTrail With {.X = D.X, .Y1 = D.Y - 60, .Y2 = H, .R = D.R, .Life = 3 + Rand.NextDouble() * 3})
                    If Trails.Count > 60 Then Trails.RemoveAt(0)
                    Droplets(i) = NewDrop(W, H)
                End If
            Else
                D.Delay -= Dt
                If D.Delay <= 0 Then
                    D.Moving = True
                    D.VY = 4
                End If
            End If
        Next
        For i As Integer = Trails.Count - 1 To 0 Step -1
            Trails(i).Life -= Dt
            If Trails(i).Life <= 0 Then Trails.RemoveAt(i)
        Next
        For Each S As RainStreak In Streaks
            S.Y += S.VY * Dt
            If S.Y > H + S.Len Then
                S.Y = -S.Len - Rand.NextDouble() * 240
                S.X = Rand.NextDouble() * W
            End If
        Next
    End Sub

    Private Sub UpdateSnow(Dt As Double)
        Dim W As Double = ActualWidth
        Dim H As Double = ActualHeight
        If W < 1 OrElse H < 1 Then Return
        For Each F As SnowFlake In Flakes
            F.Y += F.VY * Dt
            If F.Y > H + F.R Then
                F.Y = -F.R - Rand.NextDouble() * 40
                F.X = Rand.NextDouble() * W
            End If
            If F.X < -20 Then F.X = W + 10
            If F.X > W + 20 Then F.X = -10
        Next
    End Sub

    Private Sub UpdateSunny(Dt As Double)
        Dim W As Double = ActualWidth
        Dim H As Double = ActualHeight
        If W < 1 OrElse H < 1 Then Return
        For Each M As DustMote In Motes
            M.Y -= M.VY * Dt
            M.X += Math.Sin(M.Phase + Elapsed * 0.6) * 6 * Dt
            If M.Y < -8 Then
                M.Y = H + 8
                M.X = Rand.NextDouble() * W
            End If
        Next
    End Sub

    Private Sub UpdateCloudy(Dt As Double)
        Dim W As Double = ActualWidth
        If W < 1 Then Return
        For Each C As CloudBlob In Clouds
            C.X += C.VX * Dt
            If C.X > W + C.W * 0.5 Then C.X = -C.W * 0.9
        Next
    End Sub

#End Region

#Region "绘制"

    Private Sub DrawRain(dc As DrawingContext, W As Double, H As Double)
        '玻璃内侧的水汽
        dc.DrawRectangle(BrushFog, Nothing, New Rect(0, 0, W, H))
        '玻璃外侧的雨丝
        For Each S As RainStreak In Streaks
            dc.DrawLine(PensStreak(S.Style), New Point(S.X, S.Y), New Point(S.X + S.Len * 0.12, S.Y + S.Len))
        Next
        '水珠滚落后留下的水痕
        For Each T As RainTrail In Trails
            dc.PushOpacity(Math.Min(1.0, T.Life / 3) * 0.9)
            dc.DrawRectangle(BrushTrail, Nothing, New Rect(T.X - T.R * 0.9, T.Y1, T.R * 1.8, Math.Max(1, T.Y2 - T.Y1)))
            dc.Pop()
        Next
        '挂在玻璃上的水珠
        For Each D As RainDrop In Droplets
            Dim Ry As Double = If(D.Moving, D.R * 1.5, D.R)
            dc.DrawEllipse(BrushDropFill, PenDropEdge, New Point(D.X, D.Y), D.R, Ry)
            dc.DrawEllipse(BrushDropShine, Nothing, New Point(D.X - D.R * 0.32, D.Y - D.R * 0.38), D.R * 0.3, D.R * 0.26)
        Next
    End Sub

    Private Sub DrawSnow(dc As DrawingContext, W As Double, H As Double)
        '窗沿与顶部的积雪
        dc.DrawRectangle(BrushFrostBottom, Nothing, New Rect(0, H * 0.72, W, H * 0.28))
        dc.DrawRectangle(BrushFrostTop, Nothing, New Rect(0, 0, W, H * 0.13))
        For Each F As SnowFlake In Flakes
            Dim Px As Double = F.X + Math.Sin(F.Phase + Elapsed * 0.9) * F.Sway
            If F.Soft Then
                dc.DrawEllipse(BrushFlakeSoft, Nothing, New Point(Px, F.Y), F.R, F.R)
            Else
                dc.DrawEllipse(BrushesFlake(F.Style), Nothing, New Point(Px, F.Y), F.R, F.R)
            End If
        Next
    End Sub

    Private Sub DrawSunny(dc As DrawingContext, W As Double, H As Double)
        '左上角的暖色天光
        Dim Cx As Double = W * 0.16 + Math.Sin(Elapsed * 0.12) * 14
        Dim Cy As Double = H * 0.08 + Math.Cos(Elapsed * 0.1) * 10
        Dim R As Double = Math.Max(W, H) * 0.85
        dc.PushOpacity(0.86 + Math.Sin(Elapsed * 0.45) * 0.14)
        dc.DrawEllipse(BrushSun, Nothing, New Point(Cx, Cy), R, R)
        dc.Pop()
        '阳光在玻璃上的斜射反射
        DrawSunbeams(dc, W, H)
        '缓慢上升的光尘
        For Each M As DustMote In Motes
            dc.DrawEllipse(BrushesFlake(M.Style), Nothing, New Point(M.X, M.Y), M.R, M.R)
        Next
    End Sub

    Private Sub DrawSunbeams(dc As DrawingContext, W As Double, H As Double)
        Dim S As Double = _BeamStrength
        If S < 0.03 Then Return
        Dim Band As Double = Math.Max(W, H) * 0.24
        Dim Period As Double = W + Band * 4
        Dim Shift As Double = ((Elapsed * 32 + W) Mod Period) - Band * 2
        '三道柔和的斜向光带
        For i As Integer = 0 To 2
            Dim X As Double = Shift + i * Band * 1.7
            dc.PushTransform(New RotateTransform(-16 + i * 3, W * 0.5, H * 0.5))
            dc.PushOpacity(0.45 + S * 0.55)
            dc.DrawRectangle(BrushBeam, Nothing, New Rect(X, -H, Band, H * 3))
            dc.Pop()
            dc.Pop()
        Next
        '一道更亮的高光反射，随光带一起移动
        Dim X2 As Double = ((Elapsed * 52 + W * 0.5) Mod (W + Band * 3)) - Band * 1.5
        dc.PushTransform(New RotateTransform(-18, W * 0.5, H * 0.5))
        dc.PushOpacity(S)
        dc.DrawRectangle(BrushBeamBright, Nothing, New Rect(X2, -H, Band * 0.55, H * 3))
        dc.Pop()
        dc.Pop()
    End Sub

    Private Sub DrawCloudy(dc As DrawingContext, W As Double, H As Double)
        '阴天的冷色雾气，与晴天的暖色天光形成明显对比
        dc.DrawRectangle(BrushCloudVeil, Nothing, New Rect(0, 0, W, H))
        For Each C As CloudBlob In Clouds
            dc.DrawEllipse(BrushCloud, Nothing, New Point(C.X + C.W * 0.5, C.Y + C.H * 0.5), C.W * 0.5, C.H * 0.5)
        Next
    End Sub

#End Region

End Class