'由于包含加解密等安全信息，本文件中的部分代码已被删除

Imports System.Text.RegularExpressions

Friend Module ModSecret

    '标注 PCL 的不同分支，仅用于替换标记
    Public Const VersionBranchMain As String = "OpenSource"
    '在开源版的注册表与常规版的注册表隔离，以防数据冲突
    Public Const RegFolder As String = "PCLDebug"
    '用于微软登录的 ClientId
    Public OAuthClientId As String = If(Environment.GetEnvironmentVariable("PCL_MS_CLIENT_ID"), "")
    'CurseForge API Key
    Public CurseForgeAPIKey As String = If(Environment.GetEnvironmentVariable("PCL_CURSEFORGE_API_KEY"), "")
    '用于匿名数据收集的腾讯云日志服务上报 URL，形如 https://{region}.cls.tencentcs.com/track?topic_id={topic_id}
    Public Const ClsBaseUrl As String = ""

#Region "网络鉴权"

    Public Const RestrictMcDownload As Boolean = False
    Friend Function SecretCdnSign(UrlWithMark As String) As String
        If Not UrlWithMark.EndsWithF("{CDN}") Then Return UrlWithMark
        Return UrlWithMark.Replace("{CDN}", "").Replace(" ", "%20")
    End Function
    ''' <summary>
    ''' 设置 Headers 的 UA、Referer。
    ''' </summary>
    Friend Sub SecretHeadersSign(Url As String, ByRef Req As HttpRequestMessage, Optional SimulateBrowserHeaders As Boolean = False)
        If Not Req.Headers.UserAgent.Any Then
            If Url.Contains("baidupcs.com") OrElse Url.Contains("baidu.com") Then
                Req.Headers.Add("User-Agent", "LogStatistic")  '#4951
            ElseIf SimulateBrowserHeaders Then
                Req.Headers.Add("User-Agent", $"PCL2/{VersionBaseName}.{CInt(BuildType)} Mozilla/5.0 AppleWebKit/537.36 Chrome/63.0.3239.132 Safari/537.36")
            Else
                Req.Headers.Add("User-Agent", $"PCL2/{VersionBaseName}.{CInt(BuildType)}")
            End If
        End If
        If Not SimulateBrowserHeaders Then Req.Headers.Add("Referer", $"http://{VersionCode}.open.pcl2.server/")
        If Url.Contains("api.curseforge.com") OrElse Url.Contains("forgecdn.net") Then Req.Headers.Add("x-api-key", CurseForgeAPIKey)
    End Sub

#End Region

#Region "主题"

    Public Color1 As New MyColor(52, 61, 74)
    Public Color2 As New MyColor(11, 91, 203)
    Public Color3 As New MyColor(19, 112, 243)
    Public Color4 As New MyColor(72, 144, 245)
    Public Color5 As New MyColor(150, 192, 249)
    Public Color6 As New MyColor(213, 230, 253)
    Public Color7 As New MyColor(222, 236, 253)
    Public Color8 As New MyColor(234, 242, 254)
    Public ColorBg0 As New MyColor(150, 192, 249)
    Public ColorBg1 As New MyColor(190, Color7)
    Public ColorGray1 As New MyColor(64, 64, 64)
    Public ColorGray2 As New MyColor(115, 115, 115)
    Public ColorGray3 As New MyColor(140, 140, 140)
    Public ColorGray4 As New MyColor(166, 166, 166)
    Public ColorGray5 As New MyColor(204, 204, 204)
    Public ColorGray6 As New MyColor(235, 235, 235)
    Public ColorGray7 As New MyColor(240, 240, 240)
    Public ColorGray8 As New MyColor(245, 245, 245)
    Public ColorSemiTransparent As New MyColor(1, Color8)

    Public ThemeNow As Integer = -1
    Public ColorHue As Integer = 212, ColorSat As Integer = 75, ColorLightAdjust As Integer = 10, ColorHueTopbarDelta As OneOf(Of Integer, Integer()) = 0
    Public ThemeDontClick As Integer = 0

    Public Sub ThemeRefresh(Optional NewTheme As Integer = -1)
        Try
            ' WCL: 允许重复刷新（用于实时调整HSL参数）
            ' If ThemeNow = NewTheme AndAlso NewTheme >= 0 Then Return
            If NewTheme >= 0 Then ThemeNow = NewTheme

            ' WCL: 从Settings读取最新的HSL参数（关键修复！）
            ColorHue = Settings.Get(Of Integer)("UiLauncherHue")
            ColorSat = Settings.Get(Of Integer)("UiLauncherSat")
            ColorLightAdjust = Settings.Get(Of Integer)("UiLauncherLight")
            ' WCL: 读取顶栏渐变色差（UiLauncherDelta 以 90 为中性点，转换为 ±90 的色偏差值）
            ColorHueTopbarDelta = Settings.Get(Of Integer)("UiLauncherDelta") - 90

            ' WCL: 完整的主题预设（覆盖Settings中的默认值）
            Select Case NewTheme
                Case 0  ' 龙猫蓝 → 海洋蓝
                    ColorHue = 212 : ColorSat = 75 : ColorLightAdjust = 10
                Case 1  ' 甜柠青 → 青绿色
                    ColorHue = 170 : ColorSat = 60 : ColorLightAdjust = 5
                Case 2  ' 小草绿 → 绿色
                    ColorHue = 120 : ColorSat = 50 : ColorLightAdjust = 5
                Case 3  ' 菠萝黄 → 黄色
                    ColorHue = 45 : ColorSat = 85 : ColorLightAdjust = 5
                Case 4  ' 橡木棕 → 棕色
                    ColorHue = 25 : ColorSat = 70 : ColorLightAdjust = 5
                Case 5  ' 玄素黑 → 黑灰
                    ColorHue = 210 : ColorSat = 5 : ColorLightAdjust = 0
                Case 6  ' 铁杆粉 → 粉色
                    ColorHue = 340 : ColorSat = 65 : ColorLightAdjust = 5
                Case 7  ' 神秘紫 → 紫色
                    ColorHue = 270 : ColorSat = 55 : ColorLightAdjust = 5
                Case 8  ' 秋仪金 → 金色
                    ColorHue = 40 : ColorSat = 80 : ColorLightAdjust = 5
                Case 9  ' 活跃橙 → 橙色
                    ColorHue = 22 : ColorSat = 85 : ColorLightAdjust = 5
                Case 10 ' 跳票红 → 红色
                    ColorHue = 2 : ColorSat = 70 : ColorLightAdjust = 5
                Case 11 ' 极客蓝 → 科技蓝
                    ColorHue = 220 : ColorSat = 72 : ColorLightAdjust = 5
                Case 12 ' 滑稽彩 → 彩虹色
                    ColorHue = 300 : ColorSat = 62 : ColorLightAdjust = 5
                Case 13 ' 欧皇彩 → 金橙色
                    ColorHue = 35 : ColorSat = 82 : ColorLightAdjust = 5
                Case 14 ' 自定义 → 使用滑块当前值，不覆盖
            End Select

            Color1 = New MyColor().FromHSL2(ColorHue, ColorSat * 0.2, 25 + ColorLightAdjust * 0.3)
            Color2 = New MyColor().FromHSL2(ColorHue, ColorSat, 45 + ColorLightAdjust)
            Color3 = New MyColor().FromHSL2(ColorHue, ColorSat, 55 + ColorLightAdjust)
            Color4 = New MyColor().FromHSL2(ColorHue, ColorSat, 65 + ColorLightAdjust)
            Color5 = New MyColor().FromHSL2(ColorHue, ColorSat, 80 + ColorLightAdjust * 0.4)
            Color6 = New MyColor().FromHSL2(ColorHue, ColorSat, 91 + ColorLightAdjust * 0.1)
            Color7 = New MyColor().FromHSL2(ColorHue, ColorSat, 95)
            Color8 = New MyColor().FromHSL2(ColorHue, ColorSat, 97)
            ColorBg0 = Color4 * 0.4 + Color5 * 0.4 + ColorGray4 * 0.2
            ColorBg1 = New MyColor(190, Color7)

            ColorSemiTransparent = New MyColor(1, Color8)
            Application.Current.Resources("ColorBrush1") = New SolidColorBrush(Color1)
            Application.Current.Resources("ColorBrush2") = New SolidColorBrush(Color2)
            Application.Current.Resources("ColorBrush3") = New SolidColorBrush(Color3)
            Application.Current.Resources("ColorBrush4") = New SolidColorBrush(Color4)
            Application.Current.Resources("ColorBrush5") = New SolidColorBrush(Color5)
            Application.Current.Resources("ColorBrush6") = New SolidColorBrush(Color6)
            Application.Current.Resources("ColorBrush7") = New SolidColorBrush(Color7)
            Application.Current.Resources("ColorBrush8") = New SolidColorBrush(Color8)
            Application.Current.Resources("ColorBrushBg0") = New SolidColorBrush(ColorBg0)
            Application.Current.Resources("ColorBrushBg1") = New SolidColorBrush(ColorBg1)
            Application.Current.Resources("ColorObject1") = CType(Color1, Color)
            Application.Current.Resources("ColorObject2") = CType(Color2, Color)
            Application.Current.Resources("ColorObject3") = CType(Color3, Color)
            Application.Current.Resources("ColorObject4") = CType(Color4, Color)
            Application.Current.Resources("ColorObject5") = CType(Color5, Color)
            Application.Current.Resources("ColorObject6") = CType(Color6, Color)
            Application.Current.Resources("ColorObject7") = CType(Color7, Color)
            Application.Current.Resources("ColorObject8") = CType(Color8, Color)
            Application.Current.Resources("ColorObjectBg0") = CType(ColorBg0, Color)
            Application.Current.Resources("ColorObjectBg1") = CType(ColorBg1, Color)
            ThemeRefreshMain()
        Catch ex As Exception
            Logger.Error(ex, "刷新主题颜色失败", LogBehavior.Toast)
        End Try
    End Sub
    Public Sub ThemeRefreshMain()
        RunInUi(
        Sub()
            If Not FrmMain.IsLoaded Then Return
            '顶部条背景
            Dim Brush = New LinearGradientBrush With {.EndPoint = New Point(1, 0), .StartPoint = New Point(0, 0)}
            Dim Deltas = ColorHueTopbarDelta.Switch(Function(d) New Integer() {-d, 0, d}, Function(d) d)
            Brush.GradientStops.Add(New GradientStop With {.Offset = 0, .Color = New MyColor().FromHSL2(ColorHue + Deltas(0), ColorSat, 48 + ColorLightAdjust)})
            Brush.GradientStops.Add(New GradientStop With {.Offset = 0.5, .Color = New MyColor().FromHSL2(ColorHue + Deltas(1), ColorSat, 54 + ColorLightAdjust)})
            Brush.GradientStops.Add(New GradientStop With {.Offset = 1, .Color = New MyColor().FromHSL2(ColorHue + Deltas(2), ColorSat, 48 + ColorLightAdjust)})
            FrmMain.PanTitle.Background = Brush
            FrmMain.PanTitle.Background.Freeze()
            '主页面背景
            If Settings.Get(Of Boolean)("UiBackgroundColorful") Then
                Brush = New LinearGradientBrush With {.EndPoint = New Point(0.1, 1), .StartPoint = New Point(0.9, 0)}
                Brush.GradientStops.Add(New GradientStop With {.Offset = -0.1, .Color = New MyColor().FromHSL2(ColorHue - 15, ColorSat * 0.8, 91)})
                Brush.GradientStops.Add(New GradientStop With {.Offset = 0.4, .Color = New MyColor().FromHSL2(ColorHue, ColorSat * 0.8, 91)})
                Brush.GradientStops.Add(New GradientStop With {.Offset = 1.1, .Color = New MyColor().FromHSL2(ColorHue + 15, ColorSat * 0.8, 91)})
                FrmMain.PanForm.Background = Brush
            Else
                FrmMain.PanForm.Background = New MyColor(245, 245, 245)
            End If
            FrmMain.PanForm.Background.Freeze()
        End Sub)
    End Sub
    Friend Sub ThemeCheckAll(EffectSetup As Boolean)
    End Sub
    Friend Function ThemeCheckOne(Id As Integer) As Boolean
        Return True
    End Function
    Friend Function ThemeUnlock(Id As Integer, Optional ShowDoubleHint As Boolean = True, Optional UnlockHint As String = Nothing) As Boolean
        Return True
    End Function

#End Region

#Region "更新"

    'PCL WE 的更新来源仓库（对应 Publish-PCLWE.ps1 推送的仓库）
    Friend Const PclWeRepo As String = "Hedwig207/PCL-WE"
    Friend Const PclWeUserAgent As String = "PCL-WE-Updater/1.0"
    Private IsCheckingUpdate As Boolean = False

    ''' <summary>
    ''' 手动检查更新（联网获取 GitHub 最新发行版）。
    ''' </summary>
    Friend Sub UpdateCheckByButton()
        If IsCheckingUpdate Then
            Hint("正在检查更新，请稍候……", HintType.Blue)
            Return
        End If
        IsCheckingUpdate = True
        Hint("正在联网检查更新……", HintType.Blue)
        RunInNewThread(
        Sub()
            Try
                Logger.Info($"开始联网检查 PCL WE 更新（当前版本：{VersionBaseName}）")
                Dim Latest As JObject = QueryLatestRelease()
                Dim RemoteTag As String = If(Latest("tag_name")?.ToString, "")
                Dim Body As String = If(Latest("body")?.ToString, "")
                Dim AssetUrl As String = Nothing
                Dim AssetName As String = Nothing

                '选择第一个可下载的 exe 发行资产
                If Latest("assets") IsNot Nothing Then
                    For Each Asset In CType(Latest("assets"), JArray)
                        Dim Name As String = If(Asset("name")?.ToString, "")
                        If Name.EndsWithF(".exe") Then
                            AssetUrl = Asset("browser_download_url")?.ToString
                            AssetName = Name
                            Exit For
                        End If
                    Next
                End If

                Dim HasNewer As Boolean = IsRemoteNewer(RemoteTag)
                If Not HasNewer Then
                    RunInUi(Sub() Hint(If(RemoteTag = "", "已是最新版本（未获取到远程版本信息，请检查网络）。", $"已是最新版本：{RemoteTag}"), If(RemoteTag = "", HintType.Red, HintType.Blue)))
                    Return
                End If
                If String.IsNullOrEmpty(AssetUrl) Then
                    RunInUi(Sub() Hint($"发现新版本 {RemoteTag}，但远程未提供可下载的程序文件，请前往 GitHub 手动下载。", HintType.Red))
                    RunInUi(Sub() OpenWebsite($"https://github.com/{PclWeRepo}/releases"))
                    Return
                End If

                Dim PromptBody As String = Body.Trim()
                If PromptBody.Length > 500 Then PromptBody = PromptBody.Substring(0, 500) & "……"
                RunInUi(
                Sub()
                    If MyMsgBox($"发现新版本：{RemoteTag}" & vbCrLf & vbCrLf &
                                If(PromptBody = "", "", "更新内容：" & vbCrLf & PromptBody & vbCrLf & vbCrLf) &
                                $"即将下载并安装：{AssetName}" & vbCrLf &
                                "下载完成后启动器会自动重启以完成更新。当前正在进行的游戏操作可能会被中断。",
                                "发现新版本", "下载并更新", "暂不更新", IsBlue:=True) = 1 Then
                        DownloadAndApplyUpdate(AssetUrl, AssetName)
                    Else
                        Hint("已忽略本次更新。", HintType.Blue)
                    End If
                End Sub)
            Catch ex As Exception
                Logger.Warn(ex, "检查更新失败")
                RunInUi(Sub() Hint("检查更新失败：" & If(ex.Message, "网络错误"), HintType.Red))
            Finally
                IsCheckingUpdate = False
            End Try
        End Sub, "PCLUpdateCheck", ThreadPriority.BelowNormal)
    End Sub

    ''' <summary>
    ''' 从 GitHub 获取最新发行版信息（Release API）。
    ''' </summary>
    Private Function QueryLatestRelease() As JObject
        Dim Url As String = $"https://api.github.com/repos/{PclWeRepo}/releases/latest"
        Dim Headers As String(,) = New String(,) {{"User-Agent", PclWeUserAgent}}
        Dim Response As String = NetRequestByClientRetry(Url, Headers:=Headers, RequireJson:=True, Accept:="application/vnd.github+json")
        Dim Json As JObject = JObject.Parse(Response)
        Return Json
    End Function

    ''' <summary>
    ''' 判断远程发行版本号是否比当前版本更新（纯数字分段比较）。
    ''' </summary>
    Private Function IsRemoteNewer(RemoteTag As String) As Boolean
        If String.IsNullOrEmpty(RemoteTag) Then Return False
        Dim Extract = Function(Txt As String) Regex.Matches(Txt, "\d+").Cast(Of Match).Select(Function(m) Integer.Parse(m.Value)).ToList()
        Dim Remote As List(Of Integer) = Extract(RemoteTag)
        Dim Local As List(Of Integer) = Extract(VersionBaseName & " " & VersionCode)
        For i = 0 To Math.Min(Remote.Count, Local.Count) - 1
            If Remote(i) > Local(i) Then Return True
            If Remote(i) < Local(i) Then Return False
        Next
        Return Remote.Count > Local.Count
    End Function

    ''' <summary>
    ''' 下载更新文件并在确认后自动替换重启。
    ''' </summary>
    Private Sub DownloadAndApplyUpdate(AssetUrl As String, AssetName As String)
        '更新文件放到缓存目录，持久化保存，避免中途关闭丢失
        Dim TempFolder As String = PathTemp & "Update\"
        Dim TargetFolder As String = TempFolder
        DirectoryUtils.Create(TargetFolder)
        Dim NewExe As String = TargetFolder & AssetName

        Hint("正在下载更新文件……", HintType.Blue)
        RunInNewThread(
        Sub()
            Try
                Logger.Info($"开始下载更新文件：{AssetName}")
                NetDownloadByLoader(AssetUrl, NewExe, SimulateBrowserHeaders:=False)
                If Not VerifyPeHeader(NewExe) Then Throw New Exception("下载的文件不是有效的程序文件，已拒绝安装")

                RunInUi(
                Sub()
                    If MyMsgBox("更新文件已下载完成。" & vbCrLf &
                                "是否立即安装？启动器会先退出，安装完成后会自动重新打开。" & vbCrLf &
                                "请确保当前没有正在进行的游戏存档操作。",
                                "安装更新", "立即安装", "稍后安装", IsBlue:=True) = 1 Then
                        ApplyUpdateArrangeRestart(NewExe)
                    Else
                        Hint("已保留更新文件，暂不安装。", HintType.Blue)
                    End If
                End Sub)
            Catch ex As Exception
                Logger.Error(ex, "下载更新失败")
                RunInUi(Sub() Hint("下载更新失败：" & If(ex.Message, "网络错误"), HintType.Red))
            End Try
        End Sub, "PCLUpdateDownload", ThreadPriority.BelowNormal)
    End Sub

    ''' <summary>
    ''' 校验文件是否为 PE 可执行文件（MZ 头）。
    ''' </summary>
    Private Function VerifyPeHeader(FilePath As String) As Boolean
        Try
            Using Stream As New IO.FileStream(FilePath, IO.FileMode.Open, IO.FileAccess.Read)
                If Stream.Length < 2 Then Return False
                Dim Bytes(1) As Byte
                Stream.Read(Bytes, 0, 2)
                Return Bytes(0) = CByte(AscW("M")) AndAlso Bytes(1) = CByte(AscW("Z"))
            End Using
        Catch
            Return False
        End Try
    End Function

    ''' <summary>
    ''' 安排更新：借助 cmd 等待本进程退出后替换 exe 并重新启动。
    ''' </summary>
    Friend Sub ApplyUpdateArrangeRestart(NewExe As String)
        IsUpdateWaitingRestart = True
        Try
            Dim CurrentExe As String = PathExe
            Dim CurrentPid As Integer = Process.GetCurrentProcess().Id
            '旧版需要更新器协作；我们直接使用 cmd 脚本完成替换与重启
            Dim Cmd As String = PathTemp & "WCL Update.cmd"
            Dim Q As String = """" ' 一个英文双引号
            Dim Script As String =
                "@echo off" & vbCrLf &
                "ping 127.0.0.1 -n 3 >nul" & vbCrLf &
                "taskkill /f /pid " & CurrentPid & " >nul 2>&1" & vbCrLf &
                "timeout /t 1 /nobreak >nul" & vbCrLf &
                "move /y " & Q & NewExe & Q & " " & Q & CurrentExe & Q & " >nul 2>&1" & vbCrLf &
                "start " & Q & Q & " " & Q & CurrentExe & Q & vbCrLf &
                "del /f /q " & Q & "%~f0" & Q & " >nul 2>&1"
            IO.File.WriteAllText(Cmd, Script, New Text.UTF8Encoding(True))
            Dim StartInfo As New ProcessStartInfo("cmd.exe", $"/c ""{Cmd}""")
            StartInfo.UseShellExecute = False
            StartInfo.CreateNoWindow = True
            StartInfo.WindowStyle = ProcessWindowStyle.Hidden
            Process.Start(StartInfo)
            '让主窗口尽快退出，由 cmd 完成后续替换与重启
            Application.Current.Dispatcher.BeginInvoke(Sub() Application.Current.Shutdown())
        Catch ex As Exception
            Logger.Error(ex, "安排更新重启失败")
            IsUpdateWaitingRestart = False
            Hint("安排更新失败：" & ex.Message, HintType.Red)
        End Try
    End Sub

    Friend IsUpdateWaitingRestart As Boolean = False
    Public Sub UpdateRestart(TriggerRestartAndByEnd As Boolean)
    End Sub
    Public Sub UpdateReplace(ProcessId As Integer, OldFileName As String, NewFileName As String, TriggerRestart As Boolean)
    End Sub

    ''' <summary>
    ''' 确保 PathTemp/Latest.exe 是最新正式版的 PCL，它会被用于整合包打包。
    ''' 如果不是，则下载一个。
    ''' </summary>
    Friend Sub DownloadLatestPCL(Optional LoaderToSyncProgress As LoaderBase = Nothing)
        '注意：如果要自行实现这个功能，请换用另一个文件路径，以免与官方版本冲突
    End Sub

#End Region

#Region "联网配置"

    'PCL-WE-service 服务仓库：负责 PCL WE 的各项在线服务
    Public Const PclWeServiceRepo As String = "Hedwig207/PCL-WE-service"
    Public Const PclWeServiceConfigBranch As String = "main"
    Private Const ServiceConfigFileName As String = "launcher-config.json"
    Private ReadOnly UserRecordLock As New Object()
    '用户记录器的计数地址（从服务仓库的配置文件中读取，未配置时禁用）
    Private UserRecordHitUrl As String = ""
    Private UserRecordGetUrl As String = ""
    Private UserRecordCountKey As String = "count"

    ''' <summary>
    ''' 联网获取的配置信息。
    ''' 若获取失败或仍在获取中，可能为 Nothing。
    ''' </summary>
    Public ServerConfig As JObject

    Public ServerLoader As New LoaderTask(Of Integer, Integer)("PCL 配置更新", AddressOf LoadServerConfig, Priority:=ThreadPriority.BelowNormal) With
        {.ReloadTimeout = 1000 * 60 * 60} '超时 1 小时

    ''' <summary>
    ''' 联网拉取 PCL-WE-service 提供的启动器配置（含用户记录器等服务信息）。
    ''' </summary>
    Private Sub LoadServerConfig(Loader As LoaderTask(Of Integer, Integer))
        Try
            Logger.Info("开始联网获取 PCL WE 服务配置……")
            Dim Url As String = $"https://raw.githubusercontent.com/{PclWeServiceRepo}/{PclWeServiceConfigBranch}/{ServiceConfigFileName}"
            Dim Headers As String(,) = New String(,) {{"User-Agent", PclWeUserAgent}}
            Dim Response As String = NetRequestByClientRetry(Url, Headers:=Headers, RequireJson:=True)
            ServerConfig = JObject.Parse(Response)
            ParseUserRecordConfig(ServerConfig)
            Logger.Info("PCL WE 服务配置获取成功，用户记录器计数地址：" & If(UserRecordGetUrl = "", "（未设置，用户记录器已禁用）", UserRecordGetUrl))
            If UserRecordGetUrl <> "" Then ReportUserRecord("start")
        Catch ex As Exception
            Logger.Warn(ex, "获取 PCL WE 服务配置失败，相关服务将被禁用")
        End Try
    End Sub

    ''' <summary>
    ''' 解析服务配置中的用户记录器信息。
    ''' 支持两种写法：对象形式（hit-url / get-url / count-key），或单个地址字符串形式。
    ''' </summary>
    Private Sub ParseUserRecordConfig(Config As JObject)
        UserRecordHitUrl = ""
        UserRecordGetUrl = ""
        UserRecordCountKey = "count"
        Dim Node As JToken = Nothing
        If Config.TryGetValue("user-record", Node) AndAlso Node IsNot Nothing AndAlso Node.Type = JTokenType.Object Then
            Dim Obj As JObject = CType(Node, JObject)
            UserRecordHitUrl = Obj.Value(Of String)("hit-url")
            UserRecordGetUrl = Obj.Value(Of String)("get-url")
            Dim CountKey As String = Obj.Value(Of String)("count-key")
            If Not String.IsNullOrEmpty(CountKey) Then UserRecordCountKey = CountKey
        Else
            '兼容旧写法：单个地址，计数与读取都走同一个地址
            For Each Key In {"user-record-endpoint", "user-recorder", "user_record_endpoint"}
                Dim Value As String = Config(Key)?.ToString
                If Not String.IsNullOrEmpty(Value) Then
                    UserRecordHitUrl = Value
                    UserRecordGetUrl = Value
                    Exit For
                End If
            Next
        End If
        If String.IsNullOrEmpty(UserRecordHitUrl) Then UserRecordHitUrl = UserRecordGetUrl
    End Sub

    ''' <summary>
    ''' 用户记录器：向 PCL-WE 服务计数一次，并取回累计用户数。
    ''' 仅在遥测开启、且服务仓库配置了计数地址时才会真正请求；否则静默忽略。
    ''' 同一台设备只在首次成功计数时自增一次，之后仅读取总数，因此统计的是用户数而非启动次数。
    ''' </summary>
    Friend Sub ReportUserRecord([Event] As String)
        If Not Settings.Get(Of Boolean)("SystemSystemTelemetry") Then Return '与普通遥测同步遵循用户的遥测开关
        If String.IsNullOrEmpty(UserRecordGetUrl) Then Return '服务端尚未配置
        SyncLock UserRecordLock
            Try
                Dim IsFirstReport As Boolean = Not Settings.Get(Of Boolean)("SystemWclReported")
                Dim Url As String = If(IsFirstReport AndAlso Not String.IsNullOrEmpty(UserRecordHitUrl), UserRecordHitUrl, UserRecordGetUrl)
                Logger.Info($"PCL WE 用户记录器请求（{[Event]}，{If(IsFirstReport, "计入新用户", "仅读取总数")}）：{Url}")
                Dim Headers As String(,) = New String(,) {{"User-Agent", PclWeUserAgent}}
                Dim Response As String = NetRequestByClient(Url, Headers:=Headers, MakeLog:=False, Timeout:=8000)
                Dim UserCount As Integer = ParseUserCount(Response)
                If UserCount > 0 Then
                    Settings.Set("SystemWclUserCount", UserCount)
                    If IsFirstReport Then Settings.Set("SystemWclReported", True)
                End If
            Catch ex As Exception
                Logger.Warn(ex, "PCL WE 用户记录器请求失败")
            End Try
        End SyncLock
    End Sub

    ''' <summary>
    ''' 从计数接口的响应中提取累计用户数（优先使用配置指定的键名，并兼容常见键名）。
    ''' </summary>
    Private Function ParseUserCount(Response As String) As Integer
        If String.IsNullOrEmpty(Response) Then Return 0
        Try
            Dim Json As JObject = JObject.Parse(Response.Trim)
            For Each Key In {UserRecordCountKey, "count", "value", "users", "user_count", "userCount", "total"}
                If String.IsNullOrEmpty(Key) Then Continue For
                Dim Token As JToken = Nothing
                If Json.TryGetValue(Key, Token) AndAlso Token IsNot Nothing Then
                    Dim Result As Integer
                    If Integer.TryParse(Token.ToString, Result) AndAlso Result > 0 Then Return Result
                End If
            Next
        Catch
            '响应不是 JSON 时忽略
        End Try
        Return 0
    End Function

    ''' <summary>
    ''' 获取设备识别码。首次启动时生成并持久保存，之后保持不变。
    ''' 它同时作为加密配置项的密钥，因此不能在每次启动时改变。
    ''' </summary>
    Friend Function GetIdentify() As String
        Dim Value As String = Settings.Get(Of String)("Identify")
        If String.IsNullOrEmpty(Value) Then
            Value = Guid.NewGuid().ToString("N")
            Settings.Set("Identify", Value)
        End If
        Return Value
    End Function

#End Region

#Region "赞助等级"

    Public ReadOnly Property CurrentRank As DonationRank
        Get
            Return DonationRank.None
        End Get
    End Property

    Public Sub InputPotatoCode(IsUpdating As Boolean)
    End Sub
    Friend Sub GeneratePotatoCode()
    End Sub

#End Region

End Module