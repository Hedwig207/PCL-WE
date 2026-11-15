Public Class PageOtherJukebox

#Region "图标"

    Private Const IconPause As String =
        "M256 128h160v768h-160z M608 128h160v768h-160z"
    Private Const IconPrev As String =
        "M768 96v832a32 32 0 0 1-48 28L224 540a32 32 0 0 1 0-56l496-416a32 32 0 0 1 48 28z M160 128h96v768h-96z"
    Private Const IconNext As String =
        "M256 96v832a32 32 0 0 0 48 28L800 540a32 32 0 0 0 0-56L304 68a32 32 0 0 0-48 28z M768 128h96v768h-96z"

#End Region

#Region "初始化"

    Private IsLoad As Boolean = False

    Private Sub PageOther_Loaded() Handles Me.Loaded
        '注册回调
        JukeboxUiCallback = AddressOf RefreshUI
        JukeboxProgressCallback = AddressOf RefreshProgress
        '图标与按钮
        BtnPrev.Logo = IconPrev
        BtnNext.Logo = IconNext
        '调整单选按钮边距
        For Each Btn As MyRadioButton In PanMode.Children
            Btn.LabText.Margin = New Thickness(-2, 0, 8, 0)
        Next
        For Each Btn As MyRadioButton In PanFilter.Children
            Btn.LabText.Margin = New Thickness(-2, 0, 8, 0)
        Next
        '非重复加载部分
        If Not IsLoad Then
            IsLoad = True
            '初始化音量
            AniControlEnabled += 1
            SliderVolume.Value = Settings.Get(Of Integer)("UiJukeboxVolume")
            AniControlEnabled -= 1
            '扫描唱片
            RunInNewThread(Sub()
                               JukeboxScan()
                               RunInUi(Sub() RefreshUI())
                           End Sub, "Jukebox Scan")
        End If
        RefreshUI()
    End Sub

    Private Sub PageOther_Unloaded() Handles Me.Unloaded
        JukeboxUiCallback = Nothing
        JukeboxProgressCallback = Nothing
    End Sub

#End Region

#Region "界面刷新"

    ''' <summary>
    ''' 整体刷新，包括播放模式、唱片列表与正在播放。
    ''' </summary>
    Private Sub RefreshUI()
        If Not IsLoaded Then Return
        AniControlEnabled += 1
        Try
            '播放模式
            Select Case JukeboxMode
                Case JukeboxModes.List
                    BtnModeList.Checked = True
                Case JukeboxModes.Repeat
                    BtnModeLoop.Checked = True
                Case Else
                    BtnModeRandom.Checked = True
            End Select
            RefreshList()
            RefreshNow()
        Catch ex As Exception
            Logger.Error(ex, "刷新唱片编辑页失败")
        Finally
            AniControlEnabled -= 1
        End Try
        RefreshProgress()
    End Sub

    ''' <summary>
    ''' 刷新唱片列表。
    ''' </summary>
    Private Sub RefreshList()
        Dim Keyword As String = SearchBox.Text.Trim().ToLowerInvariant()
        Dim Shows As New List(Of JukeboxDisc)
        For Each Disc As JukeboxDisc In JukeboxDiscs
            If Filter = 1 AndAlso Disc.IsCustom Then Continue For
            If Filter = 2 AndAlso Not Disc.IsCustom Then Continue For
            If Keyword <> "" AndAlso Not Disc.Name.ToLowerInvariant().Contains(Keyword) Then Continue For
            Shows.Add(Disc)
        Next
        '空状态
        PanList.Children.Clear()
        If Shows.Count = 0 Then
            RefreshEmpty(Keyword)
            PanEmpty.Visibility = Visibility.Visible
            PanList.Visibility = Visibility.Collapsed
            Return
        Else
            PanEmpty.Visibility = Visibility.Collapsed
            PanList.Visibility = Visibility.Visible
        End If
        '生成列表
        For Each Disc As JukeboxDisc In Shows
            Dim Item As New MyListItem With {
                .Type = MyListItem.CheckType.RadioBox,
                .Height = 44,
                .Title = Disc.Name,
                .Info = If(Disc.IsCustom, "自定义唱片", "内置唱片"),
                .LogoScale = 0.95,
                .Tag = Disc
            }
            Item.Logo = Disc.IconPath
            '图标按钮
            Dim BtnIcon As New MyIconButton With {.LogoScale = 1, .Logo = Logo.IconButtonEdit, .Tag = Disc}
            BtnIcon.ToolTip = "设置唱片图标"
            AddHandler BtnIcon.Click, AddressOf Icon_Click
            Item.Buttons = New List(Of MyIconButton) From {BtnIcon}
            AddHandler Item.Check, AddressOf Item_Check
            PanList.Children.Add(Item)
            '标记当前唱片
            If JukeboxCurrent IsNot Nothing AndAlso JukeboxCurrent.MusicPath = Disc.MusicPath Then Item.SetChecked(True, False, False)
        Next
    End Sub

    ''' <summary>
    ''' 根据当前筛选与搜索条件刷新空状态提示。
    ''' </summary>
    Private Sub RefreshEmpty(Keyword As String)
        If Keyword <> "" Then
            LabEmptyTitle.Text = "没有匹配的唱片"
            LabEmptyInfo.Text = "没有名称包含「" & SearchBox.Text.Trim() & "」的唱片，试试别的关键词"
            BtnOpenFolder.Text = "清空搜索"
            Return
        End If
        Select Case Filter
            Case 2
                LabEmptyTitle.Text = "还没有自定义唱片"
                LabEmptyInfo.Text = "点击右上角「导入歌曲」即可添加，或把音乐放入" & vbCrLf &
                                    JukeboxCustomFolderGet() & "music\"
                BtnOpenFolder.Text = "打开自定义唱片目录"
            Case 1
                LabEmptyTitle.Text = "没有找到内置唱片"
                LabEmptyInfo.Text = "请把音乐放入" & vbCrLf & JukeboxFolderGet() & "music\，" &
                                    vbCrLf & "唱片图标放入 disc\"
                BtnOpenFolder.Text = "打开音乐目录"
            Case Else
                LabEmptyTitle.Text = "没有找到任何唱片"
                LabEmptyInfo.Text = "点击右上角「导入歌曲」，或把音乐放入" & vbCrLf & JukeboxFolderGet() & "music\"
                BtnOpenFolder.Text = "打开音乐目录"
        End Select
    End Sub

    ''' <summary>
    ''' 刷新正在播放卡片。
    ''' </summary>
    Private Sub RefreshNow()
        Dim Disc As JukeboxDisc = JukeboxCurrent
        If Disc Is Nothing Then
            ImgNow.Source = Nothing
            LabNowTitle.Text = "尚未播放"
            LabNowInfo.Text = "在下方列表中选择一张唱片开始播放"
            Return
        End If
        ImgNow.Source = If(Disc.IconPath <> "", Disc.IconPath, JukeboxDefaultIcon())
        LabNowTitle.Text = Disc.Name
        Dim StateText As String = If(JukeboxPlaying, "正在播放", "已暂停")
        LabNowInfo.Text = StateText & " · " & If(Disc.IsCustom, "自定义唱片", "内置唱片")
    End Sub

    ''' <summary>
    ''' 刷新进度与播放按钮，会由播放线程高频调用，必须保持轻量。
    ''' </summary>
    Private Sub RefreshProgress()
        If Not IsLoaded Then Return
        '进度条与时间
        If Not ReferenceEquals(DragControl, SliderProgress) Then
            AniControlEnabled += 1
            SliderProgress.Value = CInt(Math.Round(JukeboxProgress * 1000))
            AniControlEnabled -= 1
        End If
        LabTime.Text = JukeboxTimeText(JukeboxPosition) & " / " & JukeboxTimeText(JukeboxDuration)
        '播放按钮
        BtnToggle.Logo = If(JukeboxPlaying, IconPause, Logo.IconPlay)
        BtnToggle.ToolTip = If(JukeboxPlaying, "暂停", "播放")
        '状态文字
        If JukeboxCurrent IsNot Nothing Then
            LabNowInfo.Text = If(JukeboxPlaying, "正在播放", "已暂停") & " · " &
                              If(JukeboxCurrent.IsCustom, "自定义唱片", "内置唱片")
        End If
    End Sub

    Private Shared Function JukeboxTimeText(Time As TimeSpan) As String
        If Time.TotalSeconds < 1 Then Return "0:00"
        Return CInt(Math.Floor(Time.TotalMinutes)) & ":" & Time.Seconds.ToString("00")
    End Function

#End Region

#Region "事件"

    Private Filter As Integer = 0

    Private Sub SearchBox_TextChanged(sender As Object, e As EventArgs) Handles SearchBox.TextChanged
        RefreshList()
    End Sub

    Private Sub Filter_Check(sender As MyRadioButton, raiseByMouse As Boolean) _
        Handles BtnFilterAll.Check, BtnFilterBuiltin.Check, BtnFilterCustom.Check
        If Not raiseByMouse Then Return
        Filter = CInt(Val(sender.Tag))
        RefreshList()
    End Sub

    Private Sub Mode_Check(sender As MyRadioButton, raiseByMouse As Boolean) _
        Handles BtnModeRandom.Check, BtnModeList.Check, BtnModeLoop.Check
        If Not raiseByMouse Then Return
        JukeboxMode = CType(CInt(Val(sender.Tag)), JukeboxModes)
    End Sub

    Private Sub Item_Check(sender As MyListItem, e As RouteEventArgs)
        If Not sender.Checked Then Return
        Dim Disc As JukeboxDisc = CType(sender.Tag, JukeboxDisc)
        '已经在播放这张唱片时不重新开始
        If JukeboxCurrent IsNot Nothing AndAlso JukeboxCurrent.MusicPath = Disc.MusicPath Then Return
        JukeboxPlay(Disc)
    End Sub

    Private Sub Icon_Click(sender As Object, e As EventArgs)
        Dim Btn As MyIconButton = CType(sender, MyIconButton)
        Dim Disc As JukeboxDisc = CType(Btn.Tag, JukeboxDisc)
        JukeboxImportIcon(Disc)
    End Sub

    Private Sub BtnImport_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnImport.Click
        If JukeboxImportMusic() > 0 Then
            Filter = 0
            BtnFilterAll.Checked = True
            RefreshUI()
        End If
    End Sub

    Private Sub BtnOpenFolder_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnOpenFolder.Click
        If SearchBox.Text.Trim() <> "" Then
            SearchBox.Text = ""
            Return
        End If
        Try
            Dim Target As String = If(Filter = 2, JukeboxCustomFolderGet() & "music\", JukeboxFolderGet() & "music\")
            DirectoryUtils.Create(Target)
            OpenExplorer(Target)
        Catch ex As Exception
            Logger.Warn(ex, "打开唱片目录失败")
        End Try
    End Sub

    Private Sub BtnRefresh_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnRefresh.Click
        Hint("正在刷新唱片列表……", Log:=False)
        RunInNewThread(Sub()
                           JukeboxScan()
                           RunInUi(Sub() RefreshUI())
                       End Sub, "Jukebox Scan")
    End Sub

    Private Sub BtnToggle_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnToggle.Click
        If JukeboxCurrent Is Nothing AndAlso JukeboxDiscs.Any() Then
            '先选中列表中的唱片
            Dim Last As JukeboxDisc = JukeboxDiscs.FirstOrDefault(Function(D) D.MusicPath = Settings.Get(Of String)("UiJukeboxDisc"))
            JukeboxPlay(If(Last IsNot Nothing, Last, JukeboxDiscs(0)))
            Return
        End If
        JukeboxToggle()
    End Sub

    Private Sub BtnPrev_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnPrev.Click
        If Not JukeboxDiscs.Any() Then Return
        JukeboxPlayPrev()
    End Sub

    Private Sub BtnNext_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnNext.Click
        If Not JukeboxDiscs.Any() Then Return
        JukeboxPlayNext()
    End Sub

    Private Sub SliderProgress_Change(sender As Object, user As Boolean) Handles SliderProgress.Change
        '拖动时实时更新提示时间
        If user Then
            LabTime.Text = JukeboxTimeText(TimeSpan.FromMilliseconds(JukeboxDuration.TotalMilliseconds * SliderProgress.Value / 1000)) &
                          " / " & JukeboxTimeText(JukeboxDuration)
            JukeboxSeek(SliderProgress.Value / 1000.0)
        End If
    End Sub

    Private Sub SliderVolume_Change(sender As Object, user As Boolean) Handles SliderVolume.Change
        If Not user Then Return
        Settings.Set("UiJukeboxVolume", SliderVolume.Value)
    End Sub

#End Region

End Class