Imports System.Globalization
Imports Newtonsoft.Json.Linq

Public Class PageOtherBugReport
    Implements IRefreshable

#Region "初始化"

    '滚动条
    Private Sub PageOther_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        PanBack.ScrollToHome()
    End Sub
    '初始化加载器信息
    Private Sub PageOther_Inited(sender As Object, e As EventArgs) Handles Me.Initialized
        PageLoaderInit(Load, PanLoad, CardIssue, CardSubmit, IssueLoader, AddressOf IssueListShow)
    End Sub

#End Region

#Region "Issue 列表"

    ''' <summary>
    ''' 一条 Bug 反馈（GitHub Issue）的信息。
    ''' </summary>
    Public Class BugIssueEntry
        Public Number As Integer
        Public Title As String
        Public Url As String
        Public IsOpen As Boolean
        Public CommentCount As Integer
        Public CreatedAt As DateTimeOffset = DateTimeOffset.MinValue
    End Class

    ''' <summary>
    ''' 获取 PCL-WE 仓库全部 Issue 的加载器。
    ''' </summary>
    Public Shared IssueLoader As New LoaderTask(Of Integer, List(Of BugIssueEntry))("Bug Report Issues", AddressOf IssueListLoad,, ThreadPriority.BelowNormal)
    ''' <summary>
    ''' 从 GitHub 拉取 PCL-WE 仓库的 Issue 列表。
    ''' </summary>
    Private Shared Sub IssueListLoad(Loader As LoaderTask(Of Integer, List(Of BugIssueEntry)))
        Dim Url As String = $"https://api.github.com/repos/{PclWeRepo}/issues?state=all&per_page=30&sort=created&direction=desc"
        Dim Headers As String(,) = New String(,) {{"User-Agent", PclWeUserAgent}}
        Dim Response As String = NetRequestByClientRetry(Url, Headers:=Headers, RequireJson:=True, Accept:="application/vnd.github+json")
        Dim Items As JArray = JArray.Parse(Response)
        Dim Result As New List(Of BugIssueEntry)
        For Each Raw As JObject In Items
            If Raw("pull_request") IsNot Nothing Then Continue For 'Pull Request 不属于 Issue
            Dim CreatedAt As DateTimeOffset
            DateTimeOffset.TryParse(If(Raw("created_at")?.ToString, ""), CultureInfo.InvariantCulture, DateTimeStyles.None, CreatedAt)
            Result.Add(New BugIssueEntry With {
                .Number = CInt(Val(If(Raw("number")?.ToString, "0"))),
                .Title = If(Raw("title")?.ToString, "（无标题）"),
                .Url = If(Raw("html_url")?.ToString, ""),
                .IsOpen = If(Raw("state")?.ToString, "") = "open",
                .CommentCount = CInt(Val(If(Raw("comments")?.ToString, "0"))),
                .CreatedAt = CreatedAt
            })
        Next
        Loader.Output = Result
    End Sub

    ''' <summary>
    ''' 将 Issue 列表实例化为 UI。
    ''' </summary>
    Private Sub IssueListShow(Loader As LoaderTask(Of Integer, List(Of BugIssueEntry)))
        Try
            PanIssue.Children.Clear()
            PanBack.ScrollToHome()
            Dim Entries = Loader.Output
            If Entries Is Nothing OrElse Not Entries.Any() Then
                LabListTip.Text = "暂时还没有 Bug 反馈，欢迎成为第一个提交的人~"
                Return
            End If
            LabListTip.Text = $"共 {Entries.Count} 条，按提交时间从新到旧排列，点击可打开对应的 GitHub Issue。"
            For Each Entry As BugIssueEntry In Entries
                PanIssue.Children.Add(EntryToListItem(Entry))
            Next
        Catch ex As Exception
            Logger.Error(ex, "加载 Bug 反馈列表 UI 失败")
        End Try
    End Sub

    ''' <summary>
    ''' 将一条 Bug 反馈转化为列表项。
    ''' </summary>
    Private Shared Function EntryToListItem(Entry As BugIssueEntry) As MyListItem
        Dim InfoParts As New List(Of String) From {$"#{Entry.Number}", If(Entry.IsOpen, "开启中", "已关闭")}
        If Entry.CommentCount > 0 Then InfoParts.Add($"{Entry.CommentCount} 条评论")
        If Entry.CreatedAt <> DateTimeOffset.MinValue Then InfoParts.Add(Entry.CreatedAt.LocalDateTime.ToString("yyyy-MM-dd"))
        Dim Item As New MyListItem With {
            .SnapsToDevicePixels = True,
            .Title = Entry.Title,
            .Info = InfoParts.Join(" · "),
            .Logo = PathImage & "Blocks/CommandBlock.png",
            .Height = 42,
            .Type = MyListItem.CheckType.Clickable,
            .Tag = Entry
        }
        AddHandler Item.Click, Sub(sender, e) OpenWebsite(CType(sender.Tag, BugIssueEntry).Url)
        Return Item
    End Function

#End Region

#Region "操作"

    ''' <summary>
    ''' 提交新的 Bug 反馈。
    ''' </summary>
    Private Sub BtnSubmit_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnSubmit.Click
        Select Case MyMsgBox("在提交新反馈前，建议先搜索反馈列表，以避免重复提交。" & vbCrLf & "如果无法打开该网页，请使用加速器改善网络环境。",
                    "Bug 反馈", "提交新反馈", "查看反馈列表", "取消")
            Case 1
                OpenWebsite(NewIssueUrl())
            Case 2
                OpenWebsite(IssueListUrl())
        End Select
    End Sub

    ''' <summary>
    ''' 打开 Issues 列表网页。
    ''' </summary>
    Private Sub BtnIssues_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnIssues.Click
        OpenWebsite(IssueListUrl())
    End Sub

    ''' <summary>
    ''' 打开日志文件夹，方便用户上传日志。
    ''' </summary>
    Private Sub BtnLog_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnLog.Click
        OpenExplorer(Paths.Base & "PCL\")
    End Sub

    ''' <summary>
    ''' 刷新 Bug 反馈列表。
    ''' </summary>
    Private Sub BtnRefresh_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnRefresh.Click
        Refresh()
    End Sub
    Public Sub Refresh() Implements IRefreshable.Refresh
        PageLoaderRestart()
        Hint("正在刷新 Bug 反馈列表……", HintType.Blue, Log:=False)
    End Sub

    ''' <summary>
    ''' Issues 列表网页地址。
    ''' </summary>
    Private Shared Function IssueListUrl() As String
        Return $"https://github.com/{PclWeRepo}/issues"
    End Function

    ''' <summary>
    ''' 新建 Issue 网页地址，预填充版本与系统信息，减少用户的填写负担。
    ''' </summary>
    Private Shared Function NewIssueUrl() As String
        Dim Body As String =
            "### 问题描述" & vbCrLf & vbCrLf &
            "（请描述发生了什么，以及你期望的结果）" & vbCrLf & vbCrLf &
            "### 复现步骤" & vbCrLf & vbCrLf &
            "1. " & vbCrLf & "2. " & vbCrLf & "3. " & vbCrLf & vbCrLf &
            "### 错误信息" & vbCrLf & vbCrLf &
            "（若与崩溃有关，请把日志中包含错误信息的段落粘贴到这里）" & vbCrLf & vbCrLf &
            "### 启动器信息" & vbCrLf & vbCrLf &
            $"- 启动器版本：{VersionBaseName}（{VersionCode}）" & vbCrLf &
            $"- 操作系统：{My.Computer.Info.OSFullName}" & vbCrLf &
            $"- 内存：{Int(My.Computer.Info.AvailablePhysicalMemory / 1024 / 1024)} M / {Int(My.Computer.Info.TotalPhysicalMemory / 1024 / 1024)} M" & vbCrLf &
            $"- DPI：{DPI}（{Math.Round(DPI / 96, 2) * 100}%）"
        Return $"https://github.com/{PclWeRepo}/issues/new?body={StringUtils.UrlEscape(Body)}"
    End Function

#End Region

End Class