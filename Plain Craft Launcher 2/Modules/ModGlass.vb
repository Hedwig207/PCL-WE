Imports System.Windows.Shapes

''' <summary>
''' 磨砂玻璃（亚克力）质感与天气粒子效果的实现。
''' 玻璃层位于背景之上、界面内容之下，所有效果均可在个性化设置中独立关闭。
''' </summary>
Module ModGlass

#Region "状态"

    Private GlassBack As Grid
    Private GlassWallpaper As Image
    Private GlassTint As Rectangle
    Private GlassNoise As Rectangle
    Private GlassHighlight As Rectangle
    Private GlassWeatherHost As Grid
    Private GlassWeatherEffect As GlassWeather
    Private IsApplying As Boolean = False
    Private WallpaperLoaded As Boolean = False

    Private DetectStarted As Boolean = False
    Private AutoWeather As Integer = -1
    Private AutoWeatherTime As Long = 0
    Private AutoCloud As Integer = -1
    Private LastMojiCode As String = ""
    Private LastWeatherCity As String = ""

    ''' <summary>
    ''' 当前是否处于磨砂玻璃模式。
    ''' </summary>
    Public ReadOnly Property IsGlassEnabled As Boolean
        Get
            If FrmMain Is Nothing Then Return False
            Return GlassBack IsNot Nothing AndAlso FrmMain.PanForm.Children.Contains(GlassBack)
        End Get
    End Property

#End Region

#Region "刷新"

    ''' <summary>
    ''' 设置项改变后重新应用磨砂玻璃效果。
    ''' </summary>
    Public Sub OnSettingChanged(Value As Object)
        RunInUi(AddressOf GlassRefresh)
    End Sub

    ''' <summary>
    ''' 背景与日夜模式相关设置改变后，重新应用背景图片与磨砂玻璃。
    ''' </summary>
    Public Sub OnBackgroundSettingChanged(Value As Object)
        If IsSettingBackground Then Return
        RunInUi(
            Sub()
                ApplyBackground(False)
                GlassRefresh()
                '日夜模式可能改变，需要同步刷新深浅主题
                ThemeRefresh()
            End Sub)
    End Sub

    Private IsSettingBackground As Boolean

    ''' <summary>
    ''' 记录手动选中的壁纸并立即应用，避免设置回调重复加载。
    ''' </summary>
    Public Sub SetBackgroundPick(Value As String)
        IsSettingBackground = True
        Try
            Settings.Set("UiBackgroundPick", Value)
        Finally
            IsSettingBackground = False
        End Try
        ApplyBackground(True)
    End Sub

    ''' <summary>
    ''' 根据当前设置重建并刷新磨砂玻璃效果。
    ''' </summary>
    Public Sub GlassRefresh()
        If FrmMain Is Nothing Then Return
        If IsApplying Then Return
        IsApplying = True
        Try
            Dim Enabled As Boolean = Settings.Get(Of Boolean)("UiGlass")
            SetSurfaces(Enabled)
            If Not Enabled Then
                GlassRemove()
                Return
            End If
            GlassBuild()
            If Not WallpaperLoaded Then
                WallpaperLoaded = True
                ApplyWallpaper()
            End If
            ApplyWeather()
            ApplyTint()
        Catch ex As Exception
            Logger.Error(ex, "刷新磨砂玻璃效果失败")
        Finally
            IsApplying = False
        End Try
    End Sub

    ''' <summary>
    ''' 主题色改变后同步玻璃的染色。
    ''' </summary>
    Public Sub ApplyThemeTint()
        SetSurfaces(Settings.Get(Of Boolean)("UiGlass"))
        If GlassTint Is Nothing Then Return
        ApplyTint()
    End Sub

    ''' <summary>
    ''' 关闭窗口时停止所有动画。
    ''' </summary>
    Public Sub GlassStop()
        If GlassWeatherEffect IsNot Nothing Then GlassWeatherEffect.StopEffect()
    End Sub

    Private Sub GlassBuild()
        Dim Pan As Grid = FrmMain.PanForm

        If GlassBack Is Nothing Then
            GlassWallpaper = New Image With {.Stretch = Stretch.UniformToFill, .Visibility = Visibility.Collapsed}
            RenderOptions.SetBitmapScalingMode(GlassWallpaper, BitmapScalingMode.HighQuality)
            GlassTint = New Rectangle()
            GlassNoise = New Rectangle With {.Fill = GetNoiseBrush(), .Opacity = 0.55}
            GlassHighlight = New Rectangle With {.Fill = GetHighlightBrush()}
            GlassBack = New Grid With {.IsHitTestVisible = False, .ClipToBounds = True}
            Grid.SetRow(GlassBack, 1)
            GlassBack.Children.Add(GlassWallpaper)
            GlassBack.Children.Add(GlassTint)
            GlassBack.Children.Add(GlassNoise)
            GlassBack.Children.Add(GlassHighlight)
        End If
        If Not Pan.Children.Contains(GlassBack) Then
            Dim TargetIndex As Integer = Pan.Children.IndexOf(FrmMain.ImgBack)
            Pan.Children.Insert(If(TargetIndex < 0, 0, TargetIndex + 1), GlassBack)
        End If

        If GlassWeatherHost Is Nothing Then
            GlassWeatherEffect = New GlassWeather()
            GlassWeatherHost = New Grid With {.IsHitTestVisible = False, .ClipToBounds = True}
            Grid.SetRow(GlassWeatherHost, 1)
            GlassWeatherHost.Children.Add(GlassWeatherEffect)
        End If
        If Not Pan.Children.Contains(GlassWeatherHost) Then
            Dim TargetIndex As Integer = Pan.Children.IndexOf(FrmMain.PanLeft)
            If TargetIndex < 0 Then TargetIndex = Pan.Children.Count
            Pan.Children.Insert(TargetIndex, GlassWeatherHost)
        End If
    End Sub

    Private Sub GlassRemove()
        If FrmMain Is Nothing Then Return
        If GlassWeatherEffect IsNot Nothing Then GlassWeatherEffect.StopEffect()
        If GlassBack IsNot Nothing Then FrmMain.PanForm.Children.Remove(GlassBack)
        If GlassWeatherHost IsNot Nothing Then FrmMain.PanForm.Children.Remove(GlassWeatherHost)
        GlassBack = Nothing
        GlassWeatherHost = Nothing
        GlassWeatherEffect = Nothing
        GlassWallpaper = Nothing
        GlassTint = Nothing
        GlassNoise = Nothing
        GlassHighlight = Nothing
        WallpaperLoaded = False
    End Sub

#End Region

#Region "外观"

    ''' <summary>
    ''' 磨砂模式下让卡片与侧边栏更通透，以便透出玻璃背后的内容。
    ''' </summary>
    Private Sub SetSurfaces(Enabled As Boolean)
        If Application.Current Is Nothing Then Return
        Dim Dark As Boolean = Settings.Get(Of Boolean)("UiThemeDark") AndAlso DayNightNow = 2
        Dim CardColor As Color
        Dim SideColor As Color
        If Dark Then
            CardColor = If(Enabled, Color.FromArgb(226, 30, 36, 46), Color.FromArgb(246, 25, 30, 39))
            SideColor = If(Enabled, Color.FromArgb(216, 26, 31, 41), Color.FromArgb(242, 21, 26, 34))
        Else
            CardColor = If(Enabled, Color.FromArgb(212, 255, 255, 255), Color.FromArgb(245, 255, 255, 255))
            SideColor = If(Enabled, Color.FromArgb(198, 255, 255, 255), Color.FromArgb(241, 255, 255, 255))
        End If
        Application.Current.Resources("ColorBrushCardBackground") = FrozenBrush(CardColor)
        Application.Current.Resources("ColorBrushBackgroundTransparentSidebar") = FrozenBrush(SideColor)
    End Sub

    Private Sub ApplyTint()
        If GlassTint Is Nothing OrElse GlassBack Is Nothing Then Return
        Dim Mode As Integer = If(GlassWeatherEffect Is Nothing, 2, GlassWeatherEffect.CurrentMode)
        Dim Strength As Double = Math.Max(0, Math.Min(1000, Settings.Get(Of Integer)("UiGlassOpacity"))) / 1000
        Dim Hue As Double = ColorHue
        Dim Sat As Double = ColorSat
        Dim Light As Double
        Dim Alpha As Double
        Select Case Mode
            Case 1 '晴
                Hue += 6
                Sat = Math.Min(Sat, 60) * 0.5
                Light = 92
                Alpha = 150
            Case 3 '雨
                Hue = 210
                Sat = 22
                Light = 80
                Alpha = 136
            Case 4 '雪
                Hue = 200
                Sat = 26
                Light = 93
                Alpha = 160
            Case Else '多云
                Sat = Sat * 0.35
                Light = 88
                Alpha = 152
        End Select
        Alpha *= Strength

        '夜间模式整体压暗，让玻璃更接近夜色
        Dim BaseLight As Double = 96
        If DayNightNow = 2 Then
            Light = Math.Max(0, Light - 14)
            BaseLight = 84
            Alpha = Math.Min(255, Alpha * 1.12)
        End If

        '底色：无系统壁纸时使用的主题色渐变
        Dim Base As New LinearGradientBrush With {.StartPoint = New Point(0.1, 0), .EndPoint = New Point(0.6, 1)}
        Base.GradientStops.Add(New GradientStop(New MyColor().FromHSL2(Hue, Sat, BaseLight), 0))
        Base.GradientStops.Add(New GradientStop(New MyColor().FromHSL2(Hue, Sat, BaseLight - 10), 1))
        Base.Freeze()
        GlassBack.Background = Base

        '染色层：把壁纸压暗或提亮到主题色调，形成玻璃质感
        Dim Tint As New LinearGradientBrush With {.StartPoint = New Point(0, 0), .EndPoint = New Point(0.35, 1)}
        Tint.GradientStops.Add(New GradientStop(WithAlpha(New MyColor().FromHSL2(Hue, Sat, Light), Alpha), 0))
        Tint.GradientStops.Add(New GradientStop(WithAlpha(New MyColor().FromHSL2(Hue, Sat, Math.Max(0, Light - 8)), Alpha * 0.82), 1))
        Tint.Freeze()
        GlassTint.Fill = Tint
    End Sub

    Private Sub ApplyWeather()
        If GlassWeatherEffect Is Nothing Then Return
        GlassWeatherEffect.Density = Settings.Get(Of Integer)("UiGlassParticle") / 1000.0
        GlassWeatherEffect.Speed = Settings.Get(Of Integer)("UiGlassSpeed") / 1000.0
        '天气源改变后重新识别一次
        Dim MojiCode As String = Settings.Get(Of String)("UiGlassMojiCode")
        Dim City As String = Settings.Get(Of String)("UiGlassWeatherCity")
        If MojiCode <> LastMojiCode OrElse City <> LastWeatherCity Then
            LastMojiCode = MojiCode
            LastWeatherCity = City
            AutoWeather = -1
            AutoCloud = -1
        End If
        Dim Beam As Double = Settings.Get(Of Integer)("UiGlassSunbeam") / 1000.0
        Dim Mode As Integer = Settings.Get(Of Integer)("UiGlassWeather")
        If Mode = 0 Then
            '云量越多，阳光越弱，避免“晴天”与“多云”看起来一样
            If AutoCloud >= 0 Then Beam *= 0.35 + 0.65 * (1 - AutoCloud / 100.0)
            If AutoWeather < 0 Then
                GlassWeatherEffect.Mode = 2
                DetectWeather()
            ElseIf GetTimeMs() - AutoWeatherTime > 1800000 Then
                GlassWeatherEffect.Mode = AutoWeather
                DetectWeather()
            Else
                GlassWeatherEffect.Mode = AutoWeather
            End If
        Else
            GlassWeatherEffect.Mode = Mode
        End If
        GlassWeatherEffect.BeamStrength = Beam
    End Sub

    Private Function WithAlpha(C As Color, Alpha As Double) As Color
        Return Color.FromArgb(CByte(Math.Max(0, Math.Min(255, CInt(Alpha)))), C.R, C.G, C.B)
    End Function

    Private Function FrozenBrush(C As Color) As SolidColorBrush
        Dim B As New SolidColorBrush(C)
        B.Freeze()
        Return B
    End Function

    Private Function GetHighlightBrush() As Brush
        Dim B As New LinearGradientBrush With {.StartPoint = New Point(0, 0), .EndPoint = New Point(0.65, 1)}
        B.GradientStops.Add(New GradientStop(Color.FromArgb(52, 255, 255, 255), 0))
        B.GradientStops.Add(New GradientStop(Color.FromArgb(8, 255, 255, 255), 0.42))
        B.GradientStops.Add(New GradientStop(Color.FromArgb(0, 255, 255, 255), 0.72))
        B.GradientStops.Add(New GradientStop(Color.FromArgb(18, 255, 255, 255), 1))
        B.Freeze()
        Return B
    End Function

    ''' <summary>
    ''' 生成用于模拟玻璃表面细微颗粒感的噪点贴图。
    ''' </summary>
    Private Function GetNoiseBrush() As ImageBrush
        Const Size As Integer = 64
        Dim Bmp As New WriteableBitmap(Size, Size, 96, 96, PixelFormats.Bgra32, Nothing)
        Dim Pixels(Size * Size * 4 - 1) As Byte
        Dim R As New Random(233)
        For i As Integer = 0 To Size * Size - 1
            Pixels(i * 4) = 255
            Pixels(i * 4 + 1) = 255
            Pixels(i * 4 + 2) = 255
            Pixels(i * 4 + 3) = CByte(R.Next(0, 24))
        Next
        Bmp.WritePixels(New Int32Rect(0, 0, Size, Size), Pixels, Size * 4, 0)
        Bmp.Freeze()
        Dim B As New ImageBrush(Bmp) With {
            .TileMode = TileMode.Tile,
            .ViewportUnits = BrushMappingMode.Absolute,
            .Viewport = New Rect(0, 0, Size, Size),
            .Stretch = Stretch.None
        }
        B.Freeze()
        Return B
    End Function

#End Region

#Region "背景图片"

    ''' <summary>
    ''' 启动器背景图片改变后刷新玻璃底图。
    ''' </summary>
    Public Sub RefreshWallpaper()
        If GlassWallpaper Is Nothing Then Return
        If Not IsGlassEnabled Then Return
        ApplyWallpaper()
    End Sub

    ''' <summary>
    ''' 获取启动器当前显示的背景图片，未设置背景图片时返回 Nothing。
    ''' </summary>
    Private Function GetLauncherBackground() As BitmapSource
        If FrmMain Is Nothing Then Return Nothing
        If FrmMain.ImgBack.Visibility <> Visibility.Visible Then Return Nothing
        Dim Brush As ImageBrush = TryCast(FrmMain.ImgBack.Background, ImageBrush)
        If Brush Is Nothing Then Return Nothing
        Dim Source As BitmapSource = TryCast(Brush.ImageSource, BitmapSource)
        If Source Is Nothing Then Return Nothing
        If Not Source.IsFrozen Then
            Source = Source.Clone()
            Source.Freeze()
        End If
        Return Source
    End Function

    ''' <summary>
    ''' 使用启动器自身的背景图片作为磨砂玻璃的底图；未设置背景图片时保持主题色底纹。
    ''' </summary>
    Private Sub ApplyWallpaper()
        If GlassWallpaper Is Nothing Then Return
        Dim Source As BitmapSource = GetLauncherBackground()
        If Source Is Nothing Then
            GlassWallpaper.Source = Nothing
            GlassWallpaper.Visibility = Visibility.Collapsed
            Return
        End If
        Try
            Dim Scale As Double = Math.Min(1.0, 320 / Math.Max(1, Source.PixelWidth))
            If Scale < 1 Then
                Dim Scaled As New TransformedBitmap(Source, New ScaleTransform(Scale, Scale))
                Scaled.Freeze()
                Source = Scaled
            End If
            GlassWallpaper.Source = Source
            GlassWallpaper.Opacity = FrmMain.ImgBack.Opacity
            GlassWallpaper.Effect = New Effects.BlurEffect With {.Radius = 11, .RenderingBias = Effects.RenderingBias.Performance}
            GlassWallpaper.Visibility = Visibility.Visible
        Catch ex As Exception
            Logger.Warn(ex, "读取启动器背景图片失败，磨砂玻璃将使用主题色作为底色")
            GlassWallpaper.Source = Nothing
            GlassWallpaper.Visibility = Visibility.Collapsed
        End Try
    End Sub

#End Region

#Region "日夜模式"

    Private DayNightTimer As Threading.DispatcherTimer
    Private LastDayNight As Integer = 0

    ''' <summary>
    ''' 当前生效的日夜模式：1 为日间，2 为夜间。
    ''' 手动模式下直接返回设置值，自动模式下按设定的时间段判断。
    ''' </summary>
    Public ReadOnly Property DayNightNow As Integer
        Get
            Dim Mode As Integer = Settings.Get(Of Integer)("UiThemeMode")
            If Mode = 1 Then Return 1
            If Mode = 2 Then Return 2
            Dim DayStart As Integer = Settings.Get(Of Integer)("UiDayStart")
            Dim NightStart As Integer = Settings.Get(Of Integer)("UiNightStart")
            DayStart = Math.Max(0, Math.Min(23, DayStart))
            NightStart = Math.Max(0, Math.Min(23, NightStart))
            If DayStart = NightStart Then Return 1
            Dim Hour As Integer = Date.Now.Hour
            If DayStart < NightStart Then
                Return If(Hour >= DayStart AndAlso Hour < NightStart, 1, 2)
            Else
                Return If(Hour >= DayStart OrElse Hour < NightStart, 1, 2)
            End If
        End Get
    End Property

    ''' <summary>
    ''' 当前日夜模式的显示文本。
    ''' </summary>
    Public ReadOnly Property DayNightText As String
        Get
            Return If(DayNightNow = 1, "日间", "夜间")
        End Get
    End Property

    ''' <summary>
    ''' 当前日夜模式对应的内置壁纸资源路径。
    ''' </summary>
    Private Function DefaultWallpaperPath() As String
        Return PathImage & If(DayNightNow = 1, "DayBackground.png", "NightBackground.png")
    End Function

    ''' <summary>
    ''' 启动日夜模式的定时检查，跨过时间段边界时自动切换壁纸。
    ''' </summary>
    Public Sub StartDayNightWatch()
        If FrmMain Is Nothing Then Return
        LastDayNight = DayNightNow
        If DayNightTimer IsNot Nothing Then Return
        DayNightTimer = New Threading.DispatcherTimer With {.Interval = New TimeSpan(0, 0, 30)}
        AddHandler DayNightTimer.Tick,
            Sub()
                If FrmMain Is Nothing Then Return
                Dim Current As Integer = DayNightNow
                If Current = LastDayNight Then Return
                LastDayNight = Current
                Logger.Info($"已切换到{DayNightText}模式，自动更换壁纸")
                If Settings.Get(Of Integer)("UiBackgroundSwitch") = 0 Then ApplyBackground(False)
                ThemeRefresh()
                If IsGlassEnabled Then
                    ApplyWallpaper()
                    ApplyTint()
                End If
                If FrmSetupUI IsNot Nothing Then FrmSetupUI.RefreshDayNightUI()
            End Sub
        DayNightTimer.Start()
    End Sub

    ''' <summary>
    ''' 停止日夜模式的定时检查。
    ''' </summary>
    Public Sub StopDayNightWatch()
        If DayNightTimer Is Nothing Then Return
        DayNightTimer.Stop()
        DayNightTimer = Nothing
    End Sub

    ''' <summary>
    ''' 读取 PCL\Pictures 下可用的图片文件，按需包含子文件夹。
    ''' </summary>
    Public Function GetPictures(Optional IncludeSubFolder As Boolean = True) As List(Of String)
        Dim Result As New List(Of String)
        DirectoryUtils.Create(Paths.Base & "PCL\Pictures\")
        For Each File In DirectoryUtils.EnumerateFiles(Paths.Base & "PCL\Pictures\", IncludeSubFolder)
            Dim Extension As String = PathUtils.GetExtension(File)
            If Extension <> "ini" AndAlso Extension <> "db" Then Result.Add(File) '文件夹可能会被加入 .ini 和 thumbs.db
        Next
        Return Result
    End Function

    ''' <summary>
    ''' 计算当前应当显示的背景图片路径；返回空字符串表示不显示背景图片。
    ''' </summary>
    Public Function ResolveBackgroundPath() As String
        Dim Source As Integer = Settings.Get(Of Integer)("UiBackgroundSource")
        If Source = 2 Then Return "" '不使用壁纸
        Dim Switch As Integer = Settings.Get(Of Integer)("UiBackgroundSwitch")
        Dim Pick As String = Settings.Get(Of String)("UiBackgroundPick")
        If Switch = 2 Then
            '手动固定：内置壁纸按上次手动选择的日夜版本，自定义壁纸按上次选中的文件
            If Source = 0 Then
                If Pick = "Night" Then Return PathImage & "NightBackground.png"
                If Pick = "Day" Then Return PathImage & "DayBackground.png"
                Return DefaultWallpaperPath()
            End If
            If Not String.IsNullOrWhiteSpace(Pick) AndAlso File.Exists(Pick) Then Return Pick
            Dim ManualPics As List(Of String) = GetPictures()
            If ManualPics.Any() Then Return RandomOne(ManualPics)
            Return DefaultWallpaperPath()
        End If
        Dim All As List(Of String) = GetPictures()
        If Switch = 0 AndAlso Source = 1 Then
            '跟随日夜：优先取 Day、Night 子文件夹中的图片
            Dim Folder As String = Paths.Base & "PCL\Pictures\" & If(DayNightNow = 1, "Day", "Night") & "\"
            If Directory.Exists(Folder) Then
                Dim SubPics As New List(Of String)
                For Each File In DirectoryUtils.EnumerateFiles(Folder, False)
                    Dim Extension As String = PathUtils.GetExtension(File)
                    If Extension <> "ini" AndAlso Extension <> "db" Then SubPics.Add(File)
                Next
                If SubPics.Any() Then Return RandomOne(SubPics)
            End If
        End If
        If Source = 1 AndAlso All.Any() Then Return RandomOne(All)
        '内置默认壁纸：随机模式下在日间与夜间两张图之间随机，否则按当前模式取图
        If Switch = 1 Then Return PathImage & If(New Random().Next(0, 2) = 0, "DayBackground.png", "NightBackground.png")
        Return DefaultWallpaperPath()
    End Function

    ''' <summary>
    ''' 按当前设置应用背景图片，并在有磨砂玻璃时同步刷新玻璃底图。
    ''' </summary>
    ''' <param name="IsHint">是否显示提示。</param>
    Public Sub ApplyBackground(IsHint As Boolean)
        If FrmMain Is Nothing Then Return
        Dim Address As String = ResolveBackgroundPath()
        Try
            If String.IsNullOrEmpty(Address) Then
                If FrmMain.ImgBack.Visibility = Visibility.Collapsed Then
                    If IsHint Then Hint("未检测到可用背景图片！", HintType.Red)
                Else
                    FrmMain.ImgBack.Visibility = Visibility.Collapsed
                    If IsHint Then Hint("背景图片已清除！", HintType.Green)
                End If
            Else
                Logger.Info($"加载背景图片：{Address}")
                FrmMain.ImgBack.Background = New MyBitmap(Address)
                FrmMain.ImgBack.Visibility = Visibility.Visible
                If IsHint Then Hint($"已切换到{DayNightText}背景：{PathUtils.GetLastPart(Address)}", HintType.Green, False)
            End If
            FrmMain.UpdateBackgroundAndTitleBar()
        Catch ex As Exception
            If ex.Message.Contains("参数无效") Then
                Logger.Error($"刷新背景图片失败，该图片文件可能并非标准格式。{vbCrLf}你可以尝试使用画图打开该文件并重新保存，这会让图片变为标准格式。{vbCrLf}文件：{Address}", LogBehavior.Alert)
            Else
                Logger.Error(ex, $"刷新背景图片失败（{Address}）", LogBehavior.Alert)
            End If
            FrmMain.ImgBack.Visibility = Visibility.Collapsed
        End Try
        If IsGlassEnabled Then
            ApplyWallpaper()
            ApplyTint()
        End If
        If FrmSetupUI IsNot Nothing Then FrmSetupUI.RefreshDayNightUI()
    End Sub

#End Region

#Region "天气识别"

    Private AutoWeatherText As String = ""
    Private AutoWeatherCity As String = ""
    Private AutoWeatherTemp As String = ""

    ''' <summary>
    ''' 当前自动识别到的天气摘要，用于在设置页展示。
    ''' </summary>
    Public ReadOnly Property WeatherSummary As String
        Get
            If String.IsNullOrWhiteSpace(AutoWeatherText) Then Return "尚未获取天气信息"
            Dim Parts As New List(Of String)
            If Not String.IsNullOrWhiteSpace(AutoWeatherCity) Then Parts.Add(AutoWeatherCity)
            Parts.Add(AutoWeatherText)
            If Not String.IsNullOrWhiteSpace(AutoWeatherTemp) Then Parts.Add(AutoWeatherTemp & "℃")
            Return String.Join(" · ", Parts)
        End Get
    End Property

    ''' <summary>
    ''' 立即重新获取位置与天气信息。
    ''' </summary>
    Public Sub RefreshWeatherNow()
        AutoWeather = -1
        AutoWeatherTime = 0
        AutoWeatherText = ""
        AutoCloud = -1
        DetectStarted = False
        ApplyWeather()
    End Sub

    ''' <summary>
    ''' 在后台识别本地天气，仅用于“自动”模式，失败时回退为多云。
    ''' 优先使用 open-meteo（无需密钥，可精确到手动指定的城市），失败时回退到墨迹天气。
    ''' </summary>
    Private Sub DetectWeather()
        If DetectStarted Then Return
        DetectStarted = True
        RunInNewThread(
            Sub()
                Try
                    Dim City As String = ""
                    Dim Latitude As String = ""
                    Dim Longitude As String = ""
                    Dim Result As Integer = -1
                    Dim Desc As String = ""
                    Dim Temp As String = ""
                    Dim Cloud As Integer = -1
                    '优先按手动指定的城市定位，避免 IP 定位偏差
                    Dim ManualCity As String = Settings.Get(Of String)("UiGlassWeatherCity").Trim()
                    If Not String.IsNullOrWhiteSpace(ManualCity) Then
                        Try
                            Dim R As (Latitude As String, Longitude As String, Name As String) = QueryCityLocation(ManualCity)
                            Latitude = R.Latitude
                            Longitude = R.Longitude
                            City = R.Name
                        Catch ex As Exception
                            Logger.Warn(ex, $"通过 open-meteo 定位城市（{ManualCity}）失败，回退到 IP 定位")
                        End Try
                    End If
                    '获取位置信息
                    If String.IsNullOrWhiteSpace(Latitude) Then
                        Try
                            Dim Text As String = NetRequestByClient("https://ipapi.co/json/", Timeout:=8000, RequireJson:=True, MakeLog:=False)
                            Dim Json As JObject = JObject.Parse(Text)
                            Latitude = GetJsonText(Json, {"latitude"})
                            Longitude = GetJsonText(Json, {"longitude"})
                            City = GetJsonText(Json, {"city"})
                        Catch ex As Exception
                            Logger.Warn(ex, "通过 ipapi.co 获取位置失败，尝试备用接口")
                        End Try
                    End If
                    If String.IsNullOrWhiteSpace(Latitude) Then
                        Try
                            Dim Text As String = NetRequestByClient("https://ipwho.is/", Timeout:=8000, RequireJson:=True, MakeLog:=False)
                            Dim Json As JObject = JObject.Parse(Text)
                            Latitude = GetJsonText(Json, {"latitude"})
                            Longitude = GetJsonText(Json, {"longitude"})
                            City = GetJsonText(Json, {"city"})
                        Catch ex As Exception
                            Logger.Warn(ex, "通过 ipwho.is 获取位置失败")
                        End Try
                    End If
                    If String.IsNullOrWhiteSpace(Latitude) OrElse String.IsNullOrWhiteSpace(Longitude) Then
                        Throw New Exception("无法获取位置信息")
                    End If
                    '主天气源：open-meteo
                    Try
                        Dim R As (Mode As Integer, Desc As String, Temp As String, Cloud As Integer) = QueryOpenMeteo(Latitude, Longitude)
                        Result = R.Mode
                        Desc = R.Desc
                        Temp = R.Temp
                        Cloud = R.Cloud
                    Catch ex As Exception
                        Logger.Warn(ex, "通过 open-meteo 获取天气失败，尝试墨迹天气")
                    End Try
                    '备用天气源：墨迹天气（需在设置中填写 AppCode）
                    If Result < 0 Then
                        Dim AppCode As String = Settings.Get(Of String)("UiGlassMojiCode")
                        If Not String.IsNullOrWhiteSpace(AppCode) Then
                            Try
                                Dim R As (Mode As Integer, Desc As String, Temp As String) = QueryMojiWeather(AppCode.Trim(), Latitude, Longitude)
                                Result = R.Mode
                                Desc = R.Desc
                                Temp = R.Temp
                            Catch ex As Exception
                                Logger.Warn(ex, "通过墨迹天气获取天气失败")
                            End Try
                        End If
                    End If
                    If Result < 0 Then Result = 2
                    If String.IsNullOrWhiteSpace(Desc) Then Desc = ModeToText(Result)
                    AutoWeather = Result
                    AutoWeatherTime = GetTimeMs()
                    AutoWeatherCity = City
                    AutoWeatherText = Desc
                    AutoWeatherTemp = Temp
                    AutoCloud = Cloud
                    RunInUi(Sub()
                                If GlassWeatherEffect IsNot Nothing AndAlso Settings.Get(Of Integer)("UiGlassWeather") = 0 Then
                                    GlassWeatherEffect.Mode = AutoWeather
                                    ApplyWeather()
                                    ApplyTint()
                                End If
                                If FrmSetupUI IsNot Nothing Then FrmSetupUI.RefreshGlassWeatherUI()
                            End Sub)
                Catch ex As Exception
                    Logger.Warn(ex, "自动识别天气失败")
                    AutoWeather = 2
                    AutoWeatherTime = GetTimeMs()
                    AutoWeatherText = ModeToText(AutoWeather)
                    AutoWeatherTemp = ""
                    AutoCloud = -1
                Finally
                    DetectStarted = False
                End Try
            End Sub, "GlassWeatherDetect")
    End Sub

    ''' <summary>
    ''' 通过 open-meteo 的地理编码接口把城市名解析为经纬度。
    ''' </summary>
    Private Function QueryCityLocation(City As String) As (Latitude As String, Longitude As String, Name As String)
        Dim Url As String = $"https://geocoding-api.open-meteo.com/v1/search?name={WebUtility.UrlEncode(City)}&count=1&language=zh&format=json"
        Dim Text As String = NetRequestByClient(Url, Timeout:=8000, RequireJson:=True, MakeLog:=False)
        Dim Json As JObject = JObject.Parse(Text)
        Dim Results As JArray = TryCast(Json("results"), JArray)
        If Results Is Nothing OrElse Results.Count = 0 Then Throw New Exception($"找不到城市：{City}")
        Dim Item As JObject = CType(Results(0), JObject)
        Dim Latitude As String = GetJsonText(Item, {"latitude"})
        Dim Longitude As String = GetJsonText(Item, {"longitude"})
        If String.IsNullOrWhiteSpace(Latitude) OrElse String.IsNullOrWhiteSpace(Longitude) Then
            Throw New Exception($"城市 {City} 缺少经纬度信息")
        End If
        Dim Name As String = GetJsonText(Item, {"name"})
        If String.IsNullOrWhiteSpace(Name) Then Name = City
        Return (Latitude, Longitude, Name)
    End Function

    ''' <summary>
    ''' 调用 open-meteo 实况接口，返回天气模式、描述、温度与云量。
    ''' </summary>
    Private Function QueryOpenMeteo(Latitude As String, Longitude As String) As (Mode As Integer, Desc As String, Temp As String, Cloud As Integer)
        Dim Url As String = $"https://api.open-meteo.com/v1/forecast?latitude={Latitude}&longitude={Longitude}&current=weather_code,cloud_cover,precipitation,temperature_2m,is_day&timezone=auto&forecast_days=1"
        Dim Text As String = NetRequestByClient(Url, Timeout:=8000, RequireJson:=True, MakeLog:=False)
        Dim Json As JObject = JObject.Parse(Text)
        Dim Current As JObject = TryCast(Json("current"), JObject)
        If Current Is Nothing Then Throw New Exception("open-meteo 返回内容缺少 current 字段")
        Dim Code As Integer = CInt(Current("weather_code"))
        Dim Cloud As Integer = -1
        If Current("cloud_cover") IsNot Nothing Then Cloud = CInt(Current("cloud_cover"))
        Dim Temp As String = ""
        If Current("temperature_2m") IsNot Nothing Then
            Dim T As Double = CDbl(Current("temperature_2m"))
            Temp = Math.Round(T).ToString()
        End If
        Dim Mode As Integer = WeatherCodeToMode(Code)
        Dim Desc As String = WeatherCodeToText(Code)
        '天气代码只写“晴”时，用云量进一步区分晴天与多云
        If Mode = 1 AndAlso Cloud >= 0 Then
            If Cloud >= 70 Then
                Mode = 2
                Desc = "阴"
            ElseIf Cloud >= 35 Then
                Mode = 2
                Desc = "多云"
            End If
        End If
        Return (Mode, Desc, Temp, Cloud)
    End Function

    ''' <summary>
    ''' 调用墨迹天气实况接口，返回天气模式、天气描述与温度。
    ''' </summary>
    Private Function QueryMojiWeather(AppCode As String, Latitude As String, Longitude As String) As (Mode As Integer, Desc As String, Temp As String)
        Dim Url As String = $"https://mjweather.market.alicloudapi.com/whapi/json/aliweather/briefcondition?lat={Latitude}&lon={Longitude}"
        Dim Text As String = NetRequestByClient(Url, Timeout:=8000, RequireJson:=True, MakeLog:=False,
                Headers:=New String(,) {{"Authorization", "APPCODE " & AppCode}})
        Dim Json As JObject = JObject.Parse(Text)
        Dim Data As JObject = TryCast(Json("data"), JObject)
        If Data Is Nothing Then Throw New Exception("墨迹天气返回内容缺少 data 字段")
        Dim Desc As String = GetJsonText(Data, {"condition", "weather", "cond", "realCondition"})
        Dim Temp As String = GetJsonText(Data, {"temp", "temperature", "realFeel"})
        If String.IsNullOrWhiteSpace(Desc) Then Throw New Exception("墨迹天气返回内容缺少天气描述")
        Return (WeatherTextToMode(Desc), Desc, Temp)
    End Function

    ''' <summary>
    ''' 读取 JSON 中第一个存在的文本字段。
    ''' </summary>
    Private Function GetJsonText(J As JObject, Keys As String()) As String
        For Each K As String In Keys
            Dim T As JToken = J(K)
            If T IsNot Nothing AndAlso T.Type <> JTokenType.Null Then
                Dim S As String = T.ToString().Trim()
                If S <> "" Then Return S
            End If
        Next
        Return ""
    End Function

    ''' <summary>
    ''' 将墨迹天气的天气描述转换为 1 晴、2 多云、3 雨、4 雪。
    ''' </summary>
    Private Function WeatherTextToMode(Desc As String) As Integer
        If Desc.Contains("雪") OrElse Desc.Contains("冰雹") Then Return 4
        If Desc.Contains("雨") OrElse Desc.Contains("雷") OrElse Desc.Contains("暴") Then Return 3
        If Desc.Contains("晴") AndAlso Not Desc.Contains("云") AndAlso Not Desc.Contains("阴") Then Return 1
        Return 2
    End Function

    Private Function ModeToText(Mode As Integer) As String
        Select Case Mode
            Case 1
                Return "晴"
            Case 3
                Return "雨"
            Case 4
                Return "雪"
            Case Else
                Return "多云"
        End Select
    End Function

    ''' <summary>
    ''' 将 WMO 天气代码转换为 1 晴、2 多云、3 雨、4 雪。
    ''' </summary>
    Private Function WeatherCodeToMode(Code As Integer) As Integer
        Select Case Code
            Case 0
                Return 1
            Case 51, 53, 55, 56, 57, 61, 63, 65, 66, 67, 80, 81, 82, 95, 96, 99
                Return 3
            Case 71, 73, 75, 77, 85, 86
                Return 4
            Case Else
                Return 2
        End Select
    End Function

    ''' <summary>
    ''' 将 WMO 天气代码转换为中文描述，用于设置页展示。
    ''' </summary>
    Private Function WeatherCodeToText(Code As Integer) As String
        Select Case Code
            Case 0
                Return "晴"
            Case 1
                Return "晴间多云"
            Case 2
                Return "多云"
            Case 3
                Return "阴"
            Case 45, 48
                Return "雾"
            Case 51, 53, 55
                Return "毛毛雨"
            Case 56, 57
                Return "冻毛毛雨"
            Case 61
                Return "小雨"
            Case 63
                Return "中雨"
            Case 65
                Return "大雨"
            Case 66, 67
                Return "冻雨"
            Case 71
                Return "小雪"
            Case 73
                Return "中雪"
            Case 75
                Return "大雪"
            Case 77
                Return "米雪"
            Case 80
                Return "小阵雨"
            Case 81
                Return "阵雨"
            Case 82
                Return "强阵雨"
            Case 85, 86
                Return "阵雪"
            Case 95
                Return "雷阵雨"
            Case 96, 99
                Return "雷阵雨伴冰雹"
            Case Else
                Return "多云"
        End Select
    End Function

#End Region

End Module