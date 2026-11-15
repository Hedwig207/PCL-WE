Imports NAudio.Wave

''' <summary>
''' 唱片机模块：负责唱片的扫描、播放控制与状态回调。
''' <para/> 内置唱片位于 musicassets 的 music 与 disc 目录，自定义唱片位于 PCL\Jukebox 下同名的 music 与 disc 目录。
''' </summary>
Public Module ModJukebox

#Region "数据"

    ''' <summary>
    ''' 一张唱片。
    ''' </summary>
    Public Class JukeboxDisc
        ''' <summary>
        ''' 显示名称。
        ''' </summary>
        Public Property Name As String = ""
        ''' <summary>
        ''' 音频文件路径。
        ''' </summary>
        Public Property MusicPath As String = ""
        ''' <summary>
        ''' 唱片图标路径。找不到对应图标时为空字符串。
        ''' </summary>
        Public Property IconPath As String = ""
        ''' <summary>
        ''' 是否为用户导入的唱片。
        ''' </summary>
        Public Property IsCustom As Boolean = False
    End Class

    ''' <summary>
    ''' 播放模式。
    ''' </summary>
    Public Enum JukeboxModes
        Random = 0
        List = 1
        Repeat = 2
    End Enum

    ''' <summary>
    ''' 当前扫描到的全部唱片。
    ''' </summary>
    Public JukeboxDiscs As New List(Of JukeboxDisc)
    ''' <summary>
    ''' 当前选中（或正在播放）的唱片。
    ''' </summary>
    Public JukeboxCurrent As JukeboxDisc = Nothing
    ''' <summary>
    ''' 当前播放进度，范围为 0~1。
    ''' </summary>
    Public JukeboxProgress As Double = 0
    ''' <summary>
    ''' 当前播放位置。
    ''' </summary>
    Public JukeboxPosition As TimeSpan = TimeSpan.Zero
    ''' <summary>
    ''' 当前唱片总时长。
    ''' </summary>
    Public JukeboxDuration As TimeSpan = TimeSpan.Zero
    ''' <summary>
    ''' 当前是否处于播放状态（暂停时为 False）。
    ''' </summary>
    Public JukeboxPlaying As Boolean = False
    ''' <summary>
    ''' 页面整体刷新回调，由唱片编辑页注册。
    ''' </summary>
    Public JukeboxUiCallback As Action = Nothing
    ''' <summary>
    ''' 页面进度刷新回调，由唱片编辑页注册。
    ''' </summary>
    Public JukeboxProgressCallback As Action = Nothing

    Private JukeboxWave As WaveOutEvent = Nothing
    Private JukeboxReader As WaveStream = Nothing
    Private JukeboxPlayId As Integer = 0

#End Region

#Region "路径与扫描"

    ''' <summary>
    ''' 内置音乐素材根目录（含 music 与 disc 子目录）。
    ''' <para/> 返回的路径必须是不含 .. 的绝对路径，否则会被 ForApi 转成 \\?\ 格式而报错。
    ''' </summary>
    Public Function JukeboxFolderGet() As String
        '优先使用启动器数据目录（PCL\musicassets）中的素材
        Dim Local As String = PathUtils.AddSlashSuffix(Paths.Base & "PCL\musicassets")
        If Directory.Exists(Local & "music") Then Return Local
        '其次在上级目录中查找开发目录里的素材
        Dim Current As New DirectoryInfo(Paths.Base)
        While Current IsNot Nothing
            Dim Candidate As String = PathUtils.AddSlashSuffix(Path.Combine(Current.FullName, "musicassets"))
            If Directory.Exists(Path.Combine(Candidate, "music")) Then Return Candidate
            Current = Current.Parent
        End While
        '都没有则创建空目录，供用户自行放置素材
        DirectoryUtils.Create(Local & "music\")
        DirectoryUtils.Create(Local & "disc\")
        Return Local
    End Function

    ''' <summary>
    ''' 唱片机按钮图标路径（musicassets 下的 Jukebox.png）。
    ''' </summary>
    Public Function JukeboxIconPath() As String
        Dim Folder As String = JukeboxFolderGet()
        Dim UpperPath As String = Folder & "Jukebox.png"
        If File.Exists(UpperPath) Then Return UpperPath
        Dim LowerPath As String = Folder & "jukebox.png"
        Return If(File.Exists(LowerPath), LowerPath, UpperPath)
    End Function

    ''' <summary>
    ''' 自定义唱片根目录（PCL\Jukebox），其中包含 music 与 disc 子目录。
    ''' </summary>
    Public Function JukeboxCustomFolderGet() As String
        Dim Folder As String = PathUtils.AddSlashSuffix(Paths.Base & "PCL\Jukebox")
        DirectoryUtils.Create(Folder & "music\")
        DirectoryUtils.Create(Folder & "disc\")
        Return Folder
    End Function

    Private Function JukeboxIsAudio(File As String) As Boolean
        Return {"mp3", "wav", "flac", "ogg", "m4a", "aac", "wma"}.Contains(PathUtils.GetExtension(File))
    End Function

    Private Function JukeboxIsImage(File As String) As Boolean
        Return {"png", "jpg", "jpeg", "webp"}.Contains(PathUtils.GetExtension(File))
    End Function

    ''' <summary>
    ''' 将名称规范化为可用于模糊比对的字符串。
    ''' </summary>
    Private Function JukeboxNormalize(Name As String) As String
        Dim Result As String = Name.ToLowerInvariant()
        Result = Result.Replace("music_disc", "").Replace("唱片", "")
        Result = Result.Replace("八音盒", "musicbox").Replace("音乐盒", "musicbox")
        For Each Item As String In {"_", "-", "·", " ", "（", "）", "(", ")", "．", ".", "[", "]", "　"}
            Result = Result.Replace(Item, "")
        Next
        Return Result
    End Function

    ''' <summary>
    ''' 在内置图标目录中为音乐寻找最匹配的唱片图标。
    ''' </summary>
    Private Function JukeboxMatchIcon(IconFiles As List(Of String), MusicName As String) As String
        Dim Target As String = JukeboxNormalize(MusicName)
        Dim Best As String = ""
        Dim BestLength As Integer = 0
        For Each File In IconFiles
            Dim Key As String = JukeboxNormalize(PathUtils.GetFileNameWithoutExtension(File))
            If Key.Length < 1 Then Continue For
            If Target.Contains(Key) AndAlso Key.Length > BestLength Then
                Best = File
                BestLength = Key.Length
            End If
        Next
        Return Best
    End Function

    ''' <summary>
    ''' 默认唱片图标，用于找不到图标的自定义唱片。
    ''' </summary>
    Public Function JukeboxDefaultIcon() As String
        Dim DiscFolder As String = JukeboxFolderGet() & "disc\"
        If Directory.Exists(DiscFolder) Then
            Dim First As String = DirectoryUtils.EnumerateFiles(DiscFolder, False).FirstOrDefault(Function(F) JukeboxIsImage(F))
            If First IsNot Nothing Then Return First
        End If
        Return ""
    End Function

    ''' <summary>
    ''' 重新扫描全部唱片。
    ''' </summary>
    Public Sub JukeboxScan()
        Dim Result As New List(Of JukeboxDisc)
        Try
            '内置唱片
            Dim Root As String = JukeboxFolderGet()
            Dim MusicFolder As String = Root & "music\"
            Dim DiscFolder As String = Root & "disc\"
            Dim Icons As New List(Of String)
            If Directory.Exists(DiscFolder) Then
                Icons = DirectoryUtils.EnumerateFiles(DiscFolder, False).Where(Function(F) JukeboxIsImage(F)).ToList()
            End If
            If Directory.Exists(MusicFolder) Then
                For Each File In DirectoryUtils.EnumerateFiles(MusicFolder, False)
                    If Not JukeboxIsAudio(File) Then Continue For
                    Dim Key As String = PathUtils.GetFileNameWithoutExtension(File)
                    Result.Add(New JukeboxDisc With {
                        .Name = JukeboxPrettyName(Key),
                        .MusicPath = File,
                        .IconPath = JukeboxMatchIcon(Icons, Key),
                        .IsCustom = False
                    })
                Next
            End If
            '自定义唱片
            Dim CustomRoot As String = JukeboxCustomFolderGet()
            Dim CustomMusic As String = CustomRoot & "music\"
            Dim CustomDisc As String = CustomRoot & "disc\"
            If Directory.Exists(CustomMusic) Then
                For Each File In DirectoryUtils.EnumerateFiles(CustomMusic, False)
                    If Not JukeboxIsAudio(File) Then Continue For
                    Dim Key As String = PathUtils.GetFileNameWithoutExtension(File)
                    Dim Icon As String = ""
                    If Directory.Exists(CustomDisc) Then
                        Icon = DirectoryUtils.EnumerateFiles(CustomDisc, False).
                            Where(Function(F) JukeboxIsImage(F) AndAlso
                                  PathUtils.GetFileNameWithoutExtension(F).ToLowerInvariant() = Key.ToLowerInvariant()).
                            FirstOrDefault()
                    End If
                    If Icon = "" Then Icon = JukeboxDefaultIcon()
                    Result.Add(New JukeboxDisc With {
                        .Name = Key,
                        .MusicPath = File,
                        .IconPath = Icon,
                        .IsCustom = True
                    })
                Next
            End If
        Catch ex As Exception
            Logger.Error(ex, "扫描唱片列表失败")
        End Try
        JukeboxDiscs = Result
    End Sub

    ''' <summary>
    ''' 去掉内置唱片文件名中的冗余前缀，得到更友好的显示名称。
    ''' </summary>
    Private Function JukeboxPrettyName(Name As String) As String
        Dim Result As String = Name
        For Each Prefix As String In {"唱片_·_", "唱片·", "唱片_"}
            If Result.StartsWithF(Prefix, True) Then
                Result = Result.Substring(Prefix.Length)
                Exit For
            End If
        Next
        Result = Result.Replace("_-_", " - ").Replace("_", " ")
        Return Result
    End Function

#End Region

#Region "播放控制"

    ''' <summary>
    ''' 播放模式，与设置项同步。
    ''' </summary>
    Public Property JukeboxMode As JukeboxModes
        Get
            Return CType(Settings.Get(Of Integer)("UiJukeboxMode"), JukeboxModes)
        End Get
        Set(value As JukeboxModes)
            Settings.Set("UiJukeboxMode", CInt(value))
            JukeboxRefreshUI()
        End Set
    End Property

    Public Function JukeboxVolumeGet() As Double
        Return Math.Max(0, Math.Min(1000, Settings.Get(Of Integer)("UiJukeboxVolume"))) / 1000.0
    End Function

    Private Function JukeboxIndexOf(Disc As JukeboxDisc) As Integer
        If Disc Is Nothing Then Return -1
        Return JukeboxDiscs.FindIndex(Function(D) D.MusicPath = Disc.MusicPath)
    End Function

    ''' <summary>
    ''' 播放指定唱片。
    ''' </summary>
    Public Sub JukeboxPlay(Disc As JukeboxDisc)
        If Disc Is Nothing Then Return
        If Not File.Exists(Disc.MusicPath) Then
            Hint("唱片文件不存在：" & Disc.Name, HintType.Red)
            Return
        End If
        JukeboxPlayId += 1
        Dim MyId As Integer = JukeboxPlayId
        JukeboxCurrent = Disc
        JukeboxProgress = 0
        JukeboxStopWave()
        Settings.Set("UiJukeboxDisc", Disc.MusicPath)
        JukeboxRefreshUI()
        RunInNewThread(Sub() JukeboxPlayThread(Disc, MyId), "Jukebox " & MyId, ThreadPriority.BelowNormal)
    End Sub

    ''' <summary>
    ''' 按列表序号播放唱片，序号会自动取模。
    ''' </summary>
    Public Sub JukeboxPlayIndex(Index As Integer)
        If Not JukeboxDiscs.Any() Then Return
        Dim Count As Integer = JukeboxDiscs.Count
        Index = ((Index Mod Count) + Count) Mod Count
        JukeboxPlay(JukeboxDiscs(Index))
    End Sub

    ''' <summary>
    ''' 播放下一曲。自动切曲时，循环模式会重播当前唱片。
    ''' </summary>
    Public Sub JukeboxPlayNext(Optional AutoNext As Boolean = False)
        If Not JukeboxDiscs.Any() Then Return
        Dim Index As Integer = JukeboxIndexOf(JukeboxCurrent)
        Select Case JukeboxMode
            Case JukeboxModes.Repeat
                If AutoNext AndAlso JukeboxCurrent IsNot Nothing Then
                    JukeboxPlay(JukeboxCurrent)
                Else
                    JukeboxPlayIndex(Index + 1)
                End If
            Case JukeboxModes.Random
                JukeboxPlay(JukeboxRandomDisc())
            Case Else
                JukeboxPlayIndex(Index + 1)
        End Select
    End Sub

    ''' <summary>
    ''' 播放上一曲。
    ''' </summary>
    Public Sub JukeboxPlayPrev()
        If Not JukeboxDiscs.Any() Then Return
        JukeboxPlayIndex(JukeboxIndexOf(JukeboxCurrent) - 1)
    End Sub

    Private Function JukeboxRandomDisc() As JukeboxDisc
        If JukeboxDiscs.Count <= 1 Then Return JukeboxDiscs.FirstOrDefault()
        Dim Pool As List(Of JukeboxDisc) = JukeboxDiscs.Where(Function(D) D IsNot JukeboxCurrent).ToList()
        Return Pool(New Random().Next(0, Pool.Count))
    End Function

    ''' <summary>
    ''' 暂停 / 继续播放。未开始播放时会从上次的唱片或第一张开始。
    ''' </summary>
    Public Sub JukeboxToggle()
        If JukeboxWave IsNot Nothing Then
            If JukeboxWave.PlaybackState = PlaybackState.Playing Then
                JukeboxWave.Pause()
                JukeboxPlaying = False
                JukeboxRefreshUI()
                Return
            End If
            If JukeboxWave.PlaybackState = PlaybackState.Paused Then
                JukeboxWave.Play()
                JukeboxPlaying = True
                JukeboxRefreshUI()
                Return
            End If
        End If
        '尚未播放
        If JukeboxCurrent IsNot Nothing Then
            JukeboxPlay(JukeboxCurrent)
        ElseIf JukeboxDiscs.Any() Then
            Dim LastPath As String = Settings.Get(Of String)("UiJukeboxDisc")
            Dim Last As JukeboxDisc = JukeboxDiscs.FirstOrDefault(Function(D) D.MusicPath = LastPath)
            JukeboxPlay(If(Last IsNot Nothing, Last, JukeboxDiscs(0)))
        Else
            Hint("没有找到任何唱片！请先在唱片编辑中导入歌曲。", HintType.Red)
        End If
    End Sub

    ''' <summary>
    ''' 停止播放并清空当前唱片。
    ''' </summary>
    Public Sub JukeboxStop()
        JukeboxPlayId += 1
        JukeboxStopWave()
        JukeboxProgress = 0
        JukeboxRefreshUI()
    End Sub

    ''' <summary>
    ''' 跳转到指定进度，范围为 0~1。
    ''' </summary>
    Public Sub JukeboxSeek(Percent As Double)
        Dim Reader As WaveStream = JukeboxReader
        If Reader Is Nothing Then Return
        Try
            Percent = Math.Max(0, Math.Min(1, Percent))
            Reader.CurrentTime = TimeSpan.FromMilliseconds(Reader.TotalTime.TotalMilliseconds * Percent)
            JukeboxProgress = Percent
            JukeboxRefreshProgress()
        Catch ex As Exception
            Logger.Warn(ex, "跳转唱片进度失败")
        End Try
    End Sub

    Private Sub JukeboxStopWave()
        JukeboxPlaying = False
        Dim OldWave As WaveOutEvent = JukeboxWave
        JukeboxWave = Nothing
        If OldWave Is Nothing Then Return
        Try
            OldWave.Stop()
        Catch ex As Exception
            Logger.Warn(ex, "停止唱片播放失败")
        End Try
    End Sub

    Private Function JukeboxReaderCreate(Path As String) As WaveStream
        Try
            Return New AudioFileReader(Path)
        Catch ex As Exception
            Logger.Warn(ex, "AudioFileReader 打开失败，改用 MediaFoundation 读取")
            Return New MediaFoundationReader(Path)
        End Try
    End Function

    Private Sub JukeboxPlayThread(Disc As JukeboxDisc, Id As Integer)
        Dim Wave As WaveOutEvent = Nothing
        Dim Reader As WaveStream = Nothing
        Try
            Wave = New WaveOutEvent With {.DesiredLatency = 300}
            Reader = JukeboxReaderCreate(Disc.MusicPath)
            Wave.Init(Reader)
            If Id <> JukeboxPlayId Then Return
            JukeboxWave = Wave
            JukeboxReader = Reader
            Wave.Volume = JukeboxVolumeGet()
            Wave.Play()
            JukeboxPlaying = True
            JukeboxDuration = Reader.TotalTime
            JukeboxRefreshUI()
            While Id = JukeboxPlayId AndAlso Wave.PlaybackState <> PlaybackState.Stopped
                Try
                    Wave.Volume = JukeboxVolumeGet()
                Catch
                End Try
                If Reader.TotalTime.TotalMilliseconds > 0 Then
                    JukeboxPosition = Reader.CurrentTime
                    JukeboxProgress = Reader.CurrentTime.TotalMilliseconds / Reader.TotalTime.TotalMilliseconds
                    RunInUi(Sub() JukeboxRefreshProgress())
                End If
                Thread.Sleep(120)
            End While
            If Id = JukeboxPlayId Then
                '自然播放结束，自动切曲
                JukeboxProgress = 0
                JukeboxPosition = TimeSpan.Zero
                JukeboxPlaying = False
                JukeboxPlayNext(True)
            End If
        Catch ex As Exception
            Logger.Error(ex, $"播放唱片失败（{Disc.Name}）")
            Hint("播放唱片失败：" & Disc.Name & "，可能是不受支持的音频格式", HintType.Red)
        Finally
            Try
                Wave?.Dispose()
            Catch
            End Try
            Try
                Reader?.Dispose()
            Catch
            End Try
        End Try
    End Sub

#End Region

#Region "导入"

    ''' <summary>
    ''' 导入歌曲到自定义唱片目录。
    ''' </summary>
    Public Function JukeboxImportMusic() As Integer
        Dim Files As List(Of String) = Dialogs.SelectFile("选择要导入的歌曲", True,
            filter:={({"mp3", "wav", "flac", "ogg", "m4a", "aac", "wma"}, "音频文件")})
        If Not Files.Any() Then Return 0
        Dim TargetFolder As String = JukeboxCustomFolderGet() & "music\"
        Dim Success As Integer = 0
        For Each FilePath As String In Files
            Try
                Dim Name As String = PathUtils.GetFileNameWithoutExtension(FilePath)
                Dim Target As String = TargetFolder & Name & "." & PathUtils.GetExtension(FilePath)
                If FileUtils.Exists(Target) AndAlso
                   New FileInfo(FilePath).Length = New FileInfo(Target).Length Then
                    '同名同大小，视为已导入
                    Success += 1
                    Continue For
                End If
                If FileUtils.Exists(Target) Then
                    '重名，追加序号
                    Dim I As Integer = 1
                    Do
                        Target = TargetFolder & Name & " (" & I & ")." & PathUtils.GetExtension(FilePath)
                        I += 1
                    Loop While FileUtils.Exists(Target)
                End If
                FileUtils.Copy(FilePath, Target)
                Success += 1
            Catch ex As Exception
                Logger.Warn(ex, "导入歌曲失败（" & FilePath & "）")
            End Try
        Next
        JukeboxScan()
        If Success > 0 Then Hint("成功导入 " & Success & " 首歌曲！", HintType.Green)
        Return Success
    End Function

    ''' <summary>
    ''' 为指定唱片设置自定义图标。
    ''' </summary>
    Public Sub JukeboxImportIcon(Disc As JukeboxDisc)
        If Disc Is Nothing Then Return
        Dim IconFile As String = Dialogs.SelectFile("选择唱片图标", False,
            filter:={({"png", "jpg", "jpeg", "webp"}, "常用图片文件")}).FirstOrDefault()
        If IconFile = "" Then Return
        Try
            Dim Target As String = JukeboxCustomFolderGet() & "disc\" &
                                   PathUtils.GetFileNameWithoutExtension(Disc.MusicPath) & "." & PathUtils.GetExtension(IconFile)
            FileUtils.Copy(IconFile, Target)
            JukeboxScan()
            Dim NewDisc As JukeboxDisc = JukeboxDiscs.FirstOrDefault(Function(D) D.MusicPath = Disc.MusicPath)
            If NewDisc IsNot Nothing Then
                Disc.IconPath = NewDisc.IconPath
                If JukeboxCurrent IsNot Nothing AndAlso JukeboxCurrent.MusicPath = Disc.MusicPath Then JukeboxCurrent.IconPath = NewDisc.IconPath
            End If
            Hint("已更新唱片图标！", HintType.Green)
        Catch ex As Exception
            Logger.Error(ex, "设置唱片图标失败")
            Hint("设置唱片图标失败：" & ex.Message, HintType.Red)
        End Try
        JukeboxRefreshUI()
    End Sub

#End Region

#Region "界面刷新"

    ''' <summary>
    ''' 刷新唱片机的全部界面（唱片编辑页与右下角按钮）。
    ''' </summary>
    Public Sub JukeboxRefreshUI()
        JukeboxRefreshProgress()
        Dim Callback As Action = JukeboxUiCallback
        If Callback IsNot Nothing Then
            Try
                RunInUi(Sub() Callback.Invoke())
            Catch ex As Exception
                Logger.Warn(ex, "刷新唱片编辑页失败")
            End Try
        End If
    End Sub

    ''' <summary>
    ''' 只刷新进度显示。
    ''' </summary>
    Public Sub JukeboxRefreshProgress()
        Dim Callback As Action = JukeboxProgressCallback
        RunInUi(Sub()
                    If FrmMain IsNot Nothing AndAlso FrmMain.BtnExtraJukebox IsNot Nothing Then
                        Dim Btn As MyExtraButton = FrmMain.BtnExtraJukebox
                        Btn.Progress = If(JukeboxPlaying OrElse JukeboxWave IsNot Nothing, JukeboxProgress, 0)
                        If JukeboxCurrent Is Nothing Then
                            Btn.ToolTip = "唱片机"
                        Else
                            Btn.ToolTip = "唱片机：" & JukeboxCurrent.Name & If(JukeboxPlaying, "（右键暂停）", "（右键播放）")
                        End If
                    End If
                    If Callback IsNot Nothing Then Callback.Invoke()
                End Sub)
    End Sub

#End Region

End Module