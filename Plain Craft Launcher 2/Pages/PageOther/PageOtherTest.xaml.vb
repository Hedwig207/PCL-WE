Imports System.Runtime.InteropServices

Public Class PageOtherTest

    Private Shadows IsLoaded As Boolean = False
    Private Sub PageTest_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        If IsLoaded Then Return
        IsLoaded = True
        TextDownloadFolder.Text = Paths.Base & "PCL\Downloads\"
        SettingService.RefreshSettings(CheckImeFix)
        PanBack.ScrollToHome()
    End Sub

    ''' <summary>
    ''' 正在进行的操作名称。同一个操作未结束时重复触发会被忽略，避免重复弹窗或重复执行。
    ''' </summary>
    Private Shared BusyOperations As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
    Private Shared BusySync As New Object()

    ''' <summary>
    ''' 尝试开始一个操作。若同名操作正在进行中，则返回 False。
    ''' </summary>
    Private Shared Function TryBeginOperation(Name As String) As Boolean
        SyncLock BusySync
            If BusyOperations.Contains(Name) Then Return False
            BusyOperations.Add(Name)
            Return True
        End SyncLock
    End Function

    ''' <summary>
    ''' 结束一个操作，释放占用。
    ''' </summary>
    Private Shared Sub EndOperation(Name As String)
        SyncLock BusySync
            BusyOperations.Remove(Name)
        End SyncLock
    End Sub

    ''' <summary>
    ''' 百宝箱 · 今日人品。
    ''' </summary>
    Private Sub BtnLuck_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnLuck.Click
        Jrrp()
    End Sub

    ''' <summary>
    ''' 百宝箱 · 内存优化。
    ''' </summary>
    Private Sub BtnMemory_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnMemory.Click
        RunInThread(Sub() MemoryOptimize(True))
    End Sub

    ''' <summary>
    ''' 百宝箱 · 清理游戏垃圾。
    ''' </summary>
    Private Sub BtnClear_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnClear.Click
        RubbishClear()
    End Sub

    ''' <summary>
    ''' 百宝箱 · MC 输入法修复开关。
    ''' 具体的锁定与解除逻辑由 ModImeFix 在设置项改变时执行，这里只负责提示。
    ''' </summary>
    Private Sub CheckImeFix_Change(sender As Object, user As Boolean) Handles CheckImeFix.Change
        If Not user Then Return
        If CheckImeFix.Checked Then
            Hint("已开启 MC 输入法修复", HintType.Blue)
        Else
            Hint("已关闭 MC 输入法修复", HintType.Blue)
        End If
    End Sub

    ''' <summary>
    ''' 奇妙小工具 · 搜索文件夹（手动触发一次全盘静默检测）。
    ''' </summary>
    Private Sub BtnSearch_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnSearch.Click
        ScanAndMigrate(Force:=True)
    End Sub

    ''' <summary>
    ''' 奇妙小工具 · 一键打包。
    ''' </summary>
    Private Sub BtnPack_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnPack.Click
        PackGameFiles()
    End Sub

    ''' <summary>
    ''' 奇妙小工具 · 一键导入。
    ''' </summary>
    Private Sub BtnImport_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnImport.Click
        ImportGameFiles()
    End Sub

    ''' <summary>
    ''' 百宝箱 · 千万别点。
    ''' </summary>
    Private Sub BtnDontClick_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnDontClick.Click
        If Not TryBeginOperation("千万别点") Then
            Hint("已经弹过一次啦，先把这个看完吧~", HintType.Blue)
            Return
        End If
        Try
            MyMsgBox(GetRandomCave(), "千万别点", "我知道了")
        Finally
            EndOperation("千万别点")
        End Try
    End Sub

    ''' <summary>
    ''' 下载地址变化时自动填充文件名。
    ''' </summary>
    Private Sub TextDownloadUrl_TextChanged(sender As Object, e As TextChangedEventArgs) Handles TextDownloadUrl.TextChanged
        Dim Url As String = TextDownloadUrl.Text.Trim()
        If String.IsNullOrEmpty(Url) Then Return
        Dim Name As String = PathUtils.GetLastPart(Url)
        If String.IsNullOrEmpty(Name) Then Return
        TextDownloadName.Text = Name
    End Sub

    ''' <summary>
    ''' 选择文件保存到的文件夹。
    ''' </summary>
    Private Sub BtnSelectFolder_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnSelectFolder.Click
        Dim NewFolder = Dialogs.SelectFolder("选择文件保存到的文件夹", False).FirstOrDefault
        If String.IsNullOrEmpty(NewFolder) Then Return
        TextDownloadFolder.Text = NewFolder
    End Sub

    ''' <summary>
    ''' 开始下载。
    ''' </summary>
    Private Sub BtnDownload_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnDownload.Click
        Dim Url As String = TextDownloadUrl.Text.Trim()
        Dim Folder As String = TextDownloadFolder.Text.Trim()
        Dim Name As String = TextDownloadName.Text.Trim()
        If String.IsNullOrEmpty(Url) Then
            Hint("请先输入下载地址！", HintType.Red)
            Return
        End If
        If Not (Url.StartsWithF("http://", True) OrElse Url.StartsWithF("https://", True)) Then
            Hint("下载地址必须以 http:// 或 https:// 开头！", HintType.Red)
            Return
        End If
        If String.IsNullOrEmpty(Folder) Then Folder = Paths.Base & "PCL\Downloads\"
        If String.IsNullOrEmpty(Name) Then Name = PathUtils.GetLastPart(Url)
        If String.IsNullOrEmpty(Name) Then
            Hint("无法从下载地址中获取文件名，请手动填写！", HintType.Red)
            Return
        End If
        StartCustomDownload(Url, Name, Folder)
    End Sub

    ''' <summary>
    ''' 打开下载文件夹。
    ''' </summary>
    Private Sub BtnOpenFolder_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnOpenFolder.Click
        Dim Folder As String = TextDownloadFolder.Text.Trim()
        If String.IsNullOrEmpty(Folder) Then Folder = Paths.Base & "PCL\Downloads\"
        Try
            DirectoryUtils.Create(Folder)
            OpenExplorer(Folder)
        Catch ex As Exception
            Hint("打开文件夹失败：" & ex.Message, HintType.Red)
        End Try
    End Sub

    ''' <summary>
    ''' 百宝箱 · 下载自定义文件。
    ''' </summary>
    Public Shared Sub StartCustomDownload(Url As String, FileName As String, Optional Folder As String = Nothing)
        Try
            If String.IsNullOrEmpty(Folder) Then Folder = Paths.Base & "PCL\Downloads\"
            DirectoryUtils.Create(Folder)
            Dim TargetAddress As String = Folder & PathUtils.GetLastPart(FileName)
            ModNet.NetDownloadByClient(Url, TargetAddress)
            Hint("文件下载成功：" & TargetAddress, HintType.Green)
            If Path.GetExtension(TargetAddress).ToLower() = ".zip" Then
                MyMsgBox("文件下载完成。" & vbCrLf & "若需安装整合包，请前往 下载 → 导入整合包 进行安装。", "下载完成")
            End If
        Catch ex As OperationCanceledException
        Catch ex As Exception
            Hint("文件下载失败：" & If(ex.Message, "未知错误"), HintType.Red)
        End Try
    End Sub

    ''' <summary>
    ''' 百宝箱 · 今日人品。
    ''' 每天只能抽取一次，当天重复点击会显示相同的结果。
    ''' </summary>
    Public Shared Sub Jrrp()
        '重复触发时直接忽略，避免同时弹出多个人品窗口
        If Not TryBeginOperation("今日人品") Then Return
        Try
            Dim Today As String = Date.Now.ToString("yyyyMMdd")
            Dim StoredDate As String = Settings.Get(Of String)("SystemJrrpDate")
            Dim StoredValue As Integer = Settings.Get(Of Integer)("SystemJrrpValue")
            Dim IsFirstTime As Boolean = StoredDate <> Today OrElse StoredValue < 1 OrElse StoredValue > 100
            If IsFirstTime Then
                '每天第一次点击时生成并保存结果，之后当天不再变化
                StoredValue = RandomInteger(1, 100)
                Settings.Set("SystemJrrpDate", Today)
                Settings.Set("SystemJrrpValue", StoredValue)
            End If
            Dim Comment As String
            Select Case StoredValue
                Case Is >= 95
                    Comment = "运气爆棚！今天一定要去买张彩票！"
                Case Is >= 80
                    Comment = "今天运气不错，适合做点大事！"
                Case Is >= 60
                    Comment = "还行啦，还行啦。"
                Case Is >= 40
                    Comment = "普普通通的一天，平平安安就好。"
                Case Is >= 20
                    Comment = "运气有点背，万事小心为妙。"
                Case Else
                    Comment = "今天还是宅在家里吧……"
            End Select
            Dim Tip As String = If(IsFirstTime, "", vbCrLf & "今天的人品已经抽过啦，明天再来吧~")
            MyMsgBox("你今天的人品值是：" & StoredValue & "！" & Comment & Tip, "今日人品 - " & Date.Now.ToString("yyyy/MM/dd"), "我知道了")
        Finally
            EndOperation("今日人品")
        End Try
    End Sub

    ''' <summary>
    ''' 百宝箱 · 清理游戏垃圾。
    ''' </summary>
    Public Shared Sub RubbishClear()
        If Not TryBeginOperation("清理垃圾") Then
            Hint("垃圾清理正在进行中，请稍候……", HintType.Blue)
            Return
        End If
        Hint("正在清理游戏垃圾，请稍候……", HintType.Blue)
        RunInThread(
        Sub()
            Dim Released As Long = 0
            Try
                Dim CacheFolders As New List(Of String) From {
                    PathTemp & "Cache\",
                    PathTemp & "Download\",
                    PathTemp & "Message\",
                    PathTemp & "Pictures_Ani\",
                    PathTemp & "TaskTemp\",
                    PathTemp & "Help\"
                }
                For Each Folder In CacheFolders
                    Try
                        If Not DirectoryUtils.Exists(Folder) Then Continue For
                        Released += GetFolderSize(Folder)
                        DirectoryUtils.Delete(Folder)
                    Catch
                    End Try
                Next
            Catch ex As Exception
                Logger.Warn(ex, "清理游戏垃圾失败")
            End Try
            RunInUi(
            Sub()
                Dim ReleasedMb As String = (Released / 1024 / 1024).ToString("0.0")
                Hint("垃圾清理完成，共释放 " & ReleasedMb & " MB，可以继续正常使用启动器！", HintType.Green)
            End Sub)
            EndOperation("清理垃圾")
        End Sub)
    End Sub

    ''' <summary>
    ''' 计算文件夹总大小（字节）。
    ''' </summary>
    Private Shared Function GetFolderSize(Folder As String) As Long
        Dim Total As Long = 0
        For Each File In DirectoryUtils.EnumerateFiles(Folder, True)
            Try
                Total += FileUtils.GetInfo(File).Length
            Catch
            End Try
        Next
        Return Total
    End Function

    ''' <summary>
    ''' 百宝箱 · 内存优化。
    ''' 会尝试以管理员权限重新运行启动器进行优化，以获得最彻底的效果。
    ''' </summary>
    Public Shared Sub MemoryOptimize(ShowHint As Boolean)
        If Not ShowHint Then
            '启动游戏等场景下静默优化，避免弹出权限请求
            MemoryOptimizeInternal(False)
            Return
        End If
        '重复触发时直接忽略，避免重复执行与重复提示
        If Not TryBeginOperation("内存优化") Then
            Hint("内存优化正在进行中，请稍候……", HintType.Blue)
            Return
        End If
        Try
            Hint("正在进行内存优化……", HintType.Blue)
            Dim RamBefore As Long = CLng(My.Computer.Info.AvailablePhysicalMemory)
            Dim FreedK As Integer = -1
            Try
                '以管理员权限重新运行本程序并执行优化，通过退出码返回释放的内存量（K）
                FreedK = RunAsAdmin("--memory")
            Catch ex As Exception
                Logger.Warn(ex, "内存优化未能获取管理员权限，将使用普通权限继续优化")
            End Try
            If FreedK < 0 Then
                '未获得管理员权限，退回到当前进程内的优化
                Try
                    MemoryOptimizeInternal(False)
                Catch ex As Exception
                    Logger.Warn(ex, "内存优化失败")
                End Try
                Dim RamAfter As Long = CLng(My.Computer.Info.AvailablePhysicalMemory)
                Dim FreedG As Double = Math.Max(0.0, (RamAfter - RamBefore) / 1024 / 1024)
                Hint("内存优化完成，可用内存增加了 " & FreedG.ToString("0.00") & " G，目前剩余内存 " & Math.Round(RamAfter / 1024 / 1024 / 1024, 1) & " G！" & vbCrLf & "以管理员权限运行可获得更彻底的优化效果。", HintType.Green)
                Return
            End If
            Dim Freed As Double = FreedK / 1024 / 1024
            Dim RamNow As Long = CLng(My.Computer.Info.AvailablePhysicalMemory)
            Hint("内存优化完成，可用内存增加了 " & Freed.ToString("0.00") & " G，目前剩余内存 " & Math.Round(RamNow / 1024 / 1024 / 1024, 1) & " G！", HintType.Green)
        Finally
            EndOperation("内存优化")
        End Try
    End Sub

    ''' <summary>
    ''' 尽可能激进地释放闲置内存：清理启动器自身的托管内存与工作集，
    ''' 清空所有进程的工作集，并尝试清空系统文件缓存、刷新修改页列表与备用内存列表。
    ''' </summary>
    Public Shared Sub MemoryOptimizeInternal(ShowHint As Boolean)
        '清理启动器自身的托管内存
        GC.Collect()
        GC.WaitForPendingFinalizers()
        GC.Collect()
        '启用系统级优化所需的权限（仅管理员身份有效）
        EnableMemoryPrivileges()
        '清空当前进程的工作集
        Try
            EmptyWorkingSet(Process.GetCurrentProcess().Handle)
        Catch
        End Try
        '清空系统中所有可访问进程的工作集
        For Each Proc In Process.GetProcesses()
            Try
                EmptyWorkingSet(Proc.Handle)
            Catch
            End Try
            Try
                Proc.Dispose()
            Catch
            End Try
        Next
        '清空系统文件缓存
        Try
            SetSystemFileCacheSize(New IntPtr(-1), New IntPtr(-1), 0)
        Catch
        End Try
        '清空所有进程的工作集、刷新修改页列表、清空备用内存列表
        Try
            Dim Cmd As Integer = MemoryEmptyWorkingSets
            NtSetSystemInformation(SystemMemoryListInformation, Cmd, 4)
            Cmd = MemoryFlushModifiedList
            NtSetSystemInformation(SystemMemoryListInformation, Cmd, 4)
            Cmd = MemoryPurgeStandbyList
            NtSetSystemInformation(SystemMemoryListInformation, Cmd, 4)
        Catch
        End Try
        If ShowHint Then Hint("内存优化完成！", HintType.Green)
    End Sub

    ''' <summary>
    ''' 启用内存优化所需的系统权限（需要管理员身份，否则静默失败）。
    ''' </summary>
    Private Shared Sub EnableMemoryPrivileges()
        Dim Token As IntPtr = IntPtr.Zero
        Try
            If Not OpenProcessToken(Process.GetCurrentProcess().Handle, TOKEN_ADJUST_PRIVILEGES Or TOKEN_QUERY, Token) Then Return
            Dim PrivilegeNames As String() = {"SeProfileSingleProcessPrivilege", "SeIncreaseQuotaPrivilege"}
            For Each PrivilegeName In PrivilegeNames
                Dim Luid As New LUID
                If Not LookupPrivilegeValue(Nothing, PrivilegeName, Luid) Then Continue For
                Dim State As New TOKEN_PRIVILEGES With {
                    .PrivilegeCount = 1,
                    .Privileges = New LUID_AND_ATTRIBUTES With {.Luid = Luid, .Attributes = SE_PRIVILEGE_ENABLED}
                }
                Dim Prev As New TOKEN_PRIVILEGES
                Dim RetLen As Integer = 0
                AdjustTokenPrivileges(Token, False, State, Marshal.SizeOf(GetType(TOKEN_PRIVILEGES)), Prev, RetLen)
            Next
        Catch ex As Exception
            Logger.Warn(ex, "启用内存优化权限失败")
        Finally
            If Token <> IntPtr.Zero Then CloseHandle(Token)
        End Try
    End Sub

    ''' <summary>
    ''' 获取随机彩蛋文本。
    ''' </summary>
    Public Shared Function GetRandomCave() As String
        Dim List As New List(Of String) From {
            "你点了？都说了千万别点！",
            "恭喜你发现了这个隐藏按钮，奖励是……什么都没有！",
            "据说点了这个按钮的人，第二天人品都会 +1。",
            "其实这里本来应该放一个很好玩的功能，但是被砍掉了。",
            "别点了别点了，真的没有彩蛋。"
        }
        Return RandomOne(List)
    End Function

    ''' <summary>
    ''' 获取加载时的随机提示文本。
    ''' </summary>
    Public Shared Function GetRandomHint() As String
        Return GetRandomPresetHint()
    End Function

    ''' <summary>
    ''' 从预设列表中随机选取一条提示文本。
    ''' </summary>
    Public Shared Function GetRandomPresetHint() As String
        Dim List As New List(Of String) From {
            "WCL beta1.0-intest",
            "欢迎使用 Warm Ocean Craft 启动器！",
            "百宝箱功能已恢复！",
            "享受游戏的乐趣吧~",
            "清理垃圾，从我做起！",
            "内存优化，流畅游戏！"
        }
        Return RandomOne(List)
    End Function

#Region "一键打包 / 一键导入"

    ''' <summary>
    ''' 整合包内的配置文件名。
    ''' </summary>
    Private Const GamePackConfigName As String = ".wclconfig"

    ''' <summary>
    ''' 整合包文件名。
    ''' </summary>
    Private Const GamePackFileName As String = "gamefile_for_wcl.7z"

    ''' <summary>
    ''' 一键打包：把当前所有 Minecraft 文件夹与全部启动器配置打包为 gamefile_for_wcl.7z。
    ''' </summary>
    Public Shared Sub PackGameFiles()
        '重复触发时直接忽略，避免同时打包两次
        If Not TryBeginOperation("一键打包") Then
            Hint("正在打包中，请稍候……", HintType.Blue)
            Return
        End If
        Dim IsStarted As Boolean = False
        Try
            Dim Output As String = Paths.Base & GamePackFileName
            If MyMsgBox("即将把当前所有 Minecraft 文件夹与全部启动器配置打包为：" & vbCrLf & Output & vbCrLf & vbCrLf &
                        "配置文件包含登录数据、邮箱数据等敏感信息，请妥善保管生成的整合包。" & vbCrLf &
                        "文件夹较大时打包可能需要数分钟，期间可以正常使用启动器。是否继续？",
                        "一键打包", "开始打包", "取消", IsBlue:=True) = 2 Then Return

            Hint("正在打包游戏文件夹，请稍候……", HintType.Blue)
            IsStarted = True
            RunInThread(Sub() PackGameFilesInternal(Output))
        Finally
            If Not IsStarted Then EndOperation("一键打包")
        End Try
    End Sub

    ''' <summary>
    ''' 一键打包的后台执行体。
    ''' </summary>
    Private Shared Sub PackGameFilesInternal(Output As String)
        Try
                '收集需要打包的文件夹：列表中的自定义文件夹 + 启动器自带的 .minecraft
                Dim Folders As New List(Of String())
                Dim UsedNames As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
                For Each Entry In Settings.Get(Of String)("LaunchFolders").Split("|"c)
                    If String.IsNullOrEmpty(Entry) OrElse Not Entry.Contains(">") Then Continue For
                    Dim FolderPath As String = PathUtils.AddSlashSuffix(Entry.AfterFirst(">"))
                    If Not DirectoryUtils.Exists(FolderPath) Then Continue For
                    If Folders.Any(Function(F) PathUtils.IsSame(F(1), FolderPath)) Then Continue For
                    Dim FolderName As String = Entry.BeforeFirst(">")
                    Folders.Add(New String() {FolderName, FolderPath, GetPackEntryName(FolderName, UsedNames)})
                Next
                Dim MainFolder As String = Paths.Base & ".minecraft\"
                If DirectoryUtils.Exists(MainFolder) AndAlso Not Folders.Any(Function(F) PathUtils.IsSame(F(1), MainFolder)) Then
                    Folders.Add(New String() {".minecraft", MainFolder, GetPackEntryName(".minecraft", UsedNames)})
                End If
                If Folders.Count = 0 Then
                    RunInUi(Sub() Hint("没有找到可以打包的 Minecraft 文件夹！", HintType.Red))
                    Return
                End If

                '生成配置文件内容：全部设置项（含登录、邮箱等数据）、文件夹列表、离线账号与登录历史
                Dim Config As New JObject
                Dim SettingsNode As New JObject
                For Each Pair In Settings.Entries
                    'Identify 与当前电脑绑定，版本独立设置无法脱离版本，二者都不参与导出
                    If Pair.Key = "Identify" OrElse Pair.Value.Source = Settings.Sources.Instance Then Continue For
                    Try
                        SettingsNode(Pair.Key) = CStr(Settings.Get(Pair.Key))
                    Catch ex As Exception
                        Logger.Warn(ex, $"读取设置项 {Pair.Key} 失败，已跳过导出")
                    End Try
                Next
                Config("Settings") = SettingsNode
                Dim FoldersNode As New JArray
                For Each Folder In Folders
                    FoldersNode.Add(New JObject From {
                        {"Name", Folder(0)}, {"Path", Folder(1)}, {"Entry", Folder(2)}
                    })
                Next
                Config("Folders") = FoldersNode
                '离线账号文件与登录历史不在设置中，转成 Base64 一并写入配置
                Dim AccountsNode As New JObject
                Dim AccountsFolder As String = Paths.AppDataThenName & "Accounts\"
                If DirectoryUtils.Exists(AccountsFolder) Then
                    For Each AccountFile In DirectoryUtils.EnumerateFiles(AccountsFolder, True)
                        Try
                            Dim Relative As String = AccountFile.Substring(AccountsFolder.Length).Replace("\", "/")
                            AccountsNode(Relative) = Convert.ToBase64String(FileUtils.ReadAsBytes(AccountFile))
                        Catch ex As Exception
                            Logger.Warn(ex, "读取离线账号数据失败：" & AccountFile)
                        End Try
                    Next
                End If
                Config("Accounts") = AccountsNode
                Dim LoginHistoryFile As String = Paths.AppDataThenName & "LoginHistory.dat"
                If FileUtils.Exists(LoginHistoryFile) Then
                    Try
                        Config("LoginHistory") = Convert.ToBase64String(FileUtils.ReadAsBytes(LoginHistoryFile))
                    Catch ex As Exception
                        Logger.Warn(ex, "读取登录历史失败")
                    End Try
                End If

                '写入压缩包
                DirectoryUtils.Create(Paths.Base)
                FileUtils.Delete(Output)
                Dim FileCount As Integer = 0
                Using Archive = ZipFile.Open(Output, ZipArchiveMode.Create)
                    Dim ConfigEntry = Archive.CreateEntry(GamePackConfigName, CompressionLevel.Optimal)
                    Using EntryStream = ConfigEntry.Open()
                        Using Writer As New StreamWriter(EntryStream, New UTF8Encoding(False))
                            Writer.Write(Config.ToString())
                        End Using
                    End Using
                    For Each Folder In Folders
                        Dim FolderPath As String = Folder(1)
                        Dim EntryRoot As String = Folder(2)
                        For Each SourceFile In DirectoryUtils.EnumerateFiles(FolderPath, True)
                            Try
                                Dim Relative As String = SourceFile.Substring(FolderPath.Length).Replace("\", "/")
                                Archive.CreateEntryFromFile(SourceFile, EntryRoot & "/" & Relative, CompressionLevel.Optimal)
                                FileCount += 1
                                If FileCount Mod 200 = 0 Then RunInUi(Sub() Hint($"正在打包…… 已处理 {FileCount} 个文件", HintType.Blue))
                            Catch ex As Exception
                                Logger.Warn(ex, "打包文件失败：" & SourceFile)
                            End Try
                        Next
                    Next
                End Using

                RunInUi(
                Sub()
                    Hint($"打包完成！共打包 {Folders.Count} 个文件夹、{FileCount} 个文件，已保存到：{Output}", HintType.Green)
                    If MyMsgBox("打包完成！" & vbCrLf & Output & vbCrLf & vbCrLf & "是否打开所在文件夹？",
                                "一键打包", "打开文件夹", "关闭", IsBlue:=True) = 1 Then OpenExplorer(Paths.Base)
                End Sub)
            Catch ex As Exception
                Logger.Error(ex, "一键打包失败")
                RunInUi(Sub() Hint("打包失败：" & If(ex.Message, "未知错误"), HintType.Red))
            Finally
                EndOperation("一键打包")
            End Try
    End Sub

    ''' <summary>
    ''' 一键导入：从 gamefile_for_wcl.7z 恢复全部游戏文件夹与启动器配置。
    ''' </summary>
    Public Shared Sub ImportGameFiles()
        '重复触发时直接忽略，避免同时导入两次
        If Not TryBeginOperation("一键导入") Then
            Hint("正在导入中，请稍候……", HintType.Blue)
            Return
        End If
        Dim IsStarted As Boolean = False
        Try
            Dim FileAddress As String = Dialogs.SelectFile("选择要导入的 " & GamePackFileName, False, filter:={({"7z", "zip"}, "WCL 游戏整合包")}).FirstOrDefault()
            If String.IsNullOrEmpty(FileAddress) Then Return

            Dim TempFolder As String = PathTemp & "GamePackImport\"
            Hint("正在读取整合包，请稍候……", HintType.Blue)
            IsStarted = True
            RunInThread(Sub() ImportGameFilesInternal(FileAddress, TempFolder))
        Finally
            If Not IsStarted Then EndOperation("一键导入")
        End Try
    End Sub

    ''' <summary>
    ''' 一键导入的后台执行体。
    ''' </summary>
    Private Shared Sub ImportGameFilesInternal(FileAddress As String, TempFolder As String)
        Try
            '解压到临时文件夹
            Try
                DirectoryUtils.Delete(TempFolder)
            Catch ex As Exception
                Logger.Warn(ex, "清理导入临时文件夹失败")
            End Try
                DirectoryUtils.Create(TempFolder)
                If Not ExtractGamePack(FileAddress, TempFolder) Then
                    RunInUi(Sub() Hint("无法解压该文件，请确认它是有效的 " & GamePackFileName & "！", HintType.Red))
                    Return
                End If

                '读取配置文件
                Dim ConfigFile As String = TempFolder & GamePackConfigName
                If Not FileUtils.Exists(ConfigFile) Then
                    RunInUi(Sub() Hint("该文件中没有 " & GamePackConfigName & " 配置文件，可能不是 WCL 导出的整合包！", HintType.Red))
                    Return
                End If
                Dim Config As JObject = JObject.Parse(FileUtils.ReadAsString(ConfigFile))
                Dim FoldersNode = TryCast(Config("Folders"), JArray)
                Dim FolderCount As Integer = If(FoldersNode Is Nothing, 0, FoldersNode.Count)

                '确认后再开始覆盖
                Dim Message As String = "即将导入该整合包：" & vbCrLf &
                                        $"· {FolderCount} 个 Minecraft 文件夹（恢复到打包时的位置，同名文件会被覆盖）" & vbCrLf &
                                        "· 全部启动器配置（含登录数据、邮箱数据、个性化设置等）" & vbCrLf & vbCrLf &
                                        "是否继续？"
                If RunInUiWait(Function() MyMsgBox(Message, "一键导入", "开始导入", "取消", IsWarn:=True)) <> 1 Then Return

                '恢复全部配置（导入时会用本机密钥重新加密，因此可以跨电脑迁移）
                Dim SettingsNode = TryCast(Config("Settings"), JObject)
                Dim RestoredSettings As Integer = 0
                If SettingsNode IsNot Nothing Then
                    For Each Item In SettingsNode.Properties
                        If Item.Name = "Identify" OrElse Not Settings.Entries.ContainsKey(Item.Name) Then Continue For
                        Try
                            Settings.Set(Item.Name, If(Item.Value Is Nothing, "", Item.Value.ToString()))
                            RestoredSettings += 1
                        Catch ex As Exception
                            Logger.Warn(ex, $"恢复设置项 {Item.Name} 失败")
                        End Try
                    Next
                End If

                '恢复游戏文件夹
                Dim RestoredFolders As New List(Of String)
                Dim FallbackNames As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
                If FoldersNode IsNot Nothing Then
                    For Each Item In FoldersNode.OfType(Of JObject)()
                        Try
                            If Item("Path") Is Nothing OrElse String.IsNullOrWhiteSpace(Item("Path").ToString()) Then Continue For
                            Dim FolderName As String = If(Item("Name") Is Nothing, ".minecraft", Item("Name").ToString())
                            Dim SourceFolder As String = PathUtils.AddSlashSuffix(TempFolder & If(Item("Entry") Is Nothing, FolderName, Item("Entry").ToString()))
                            Dim TargetFolder As String = PathUtils.AddSlashSuffix(Item("Path").ToString())
                            If Not DirectoryUtils.Exists(SourceFolder) Then Continue For
                            '原盘符不存在时（例如换了一台电脑），改放到启动器目录下
                            If String.IsNullOrEmpty(IO.Path.GetPathRoot(TargetFolder)) OrElse Not DirectoryUtils.Exists(IO.Path.GetPathRoot(TargetFolder)) Then
                                TargetFolder = Paths.Base & GetPackEntryName(FolderName, FallbackNames) & "\"
                            End If
                            DirectoryUtils.Create(TargetFolder)
                            For Each SourceFile In DirectoryUtils.EnumerateFiles(SourceFolder, True)
                                Try
                                    FileUtils.Copy(SourceFile, TargetFolder & SourceFile.Substring(SourceFolder.Length))
                                Catch ex As Exception
                                    Logger.Warn(ex, "恢复文件失败：" & SourceFile)
                                End Try
                            Next
                            RestoredFolders.Add(FolderName & ">" & TargetFolder)
                        Catch ex As Exception
                            Logger.Warn(ex, "恢复游戏文件夹失败：" & Item.ToString())
                        End Try
                    Next
                End If

                '同步文件夹列表：保留原有条目，并把导入的文件夹路径更新为实际路径
                Dim FolderList As New List(Of String)
                For Each Entry In Settings.Get(Of String)("LaunchFolders").Split("|"c)
                    If String.IsNullOrEmpty(Entry) Then Continue For
                    FolderList.Add(Entry)
                Next
                For Each RestoredFolder In RestoredFolders
                    Dim FolderName As String = RestoredFolder.BeforeFirst(">")
                    Dim FolderPath As String = RestoredFolder.AfterFirst(">")
                    Dim IsExists As Boolean = False
                    For i = 0 To FolderList.Count - 1
                        If FolderList(i).Contains(">") AndAlso PathUtils.IsSame(FolderList(i).AfterFirst(">"), FolderPath) Then
                            FolderList(i) = FolderName & ">" & FolderPath
                            IsExists = True
                            Exit For
                        End If
                    Next
                    If Not IsExists Then FolderList.Add(RestoredFolder)
                Next
                If FolderList.Count = 0 Then FolderList.Add("")
                Settings.Set("LaunchFolders", FolderList.Join("|"c))

                '恢复离线账号与登录历史
                Dim AccountsNode = TryCast(Config("Accounts"), JObject)
                If AccountsNode IsNot Nothing Then
                    For Each Item In AccountsNode.Properties
                        Try
                            FileUtils.Write(Paths.AppDataThenName & "Accounts\" & Item.Name.Replace("/", "\"),
                                            Convert.FromBase64String(Item.Value.ToString()))
                        Catch ex As Exception
                            Logger.Warn(ex, "恢复离线账号数据失败：" & Item.Name)
                        End Try
                    Next
                End If
                Dim LoginHistoryNode = Config("LoginHistory")
                If LoginHistoryNode IsNot Nothing AndAlso Not String.IsNullOrEmpty(LoginHistoryNode.ToString()) Then
                    Try
                        FileUtils.Write(Paths.AppDataThenName & "LoginHistory.dat", Convert.FromBase64String(LoginHistoryNode.ToString()))
                    Catch ex As Exception
                        Logger.Warn(ex, "恢复登录历史失败")
                    End Try
                End If

                '清理临时文件
                Try
                    DirectoryUtils.Delete(TempFolder)
                Catch ex As Exception
                    Logger.Warn(ex, "清理导入临时文件夹失败")
                End Try

                RunInUi(
                Sub()
                    McFolderListLoader.Start(IsForceRestart:=True)
                    Hint($"导入完成！已恢复 {RestoredFolders.Count} 个游戏文件夹与 {RestoredSettings} 项配置，部分设置需重启启动器后生效。", HintType.Green)
                End Sub)
            Catch ex As Exception
                Logger.Error(ex, "一键导入失败")
                RunInUi(Sub() Hint("导入失败：" & If(ex.Message, "未知错误"), HintType.Red))
            Finally
                EndOperation("一键导入")
            End Try
    End Sub

    ''' <summary>
    ''' 解压整合包：优先使用启动器内置引擎；若失败，则尝试调用系统自带的 tar 读取 7z 格式。
    ''' </summary>
    Private Shared Function ExtractGamePack(FileAddress As String, TargetFolder As String) As Boolean
        Try
            FileUtils.ExtractToDirectory(FileAddress, TargetFolder)
            Return True
        Catch ex As Exception
            Logger.Warn(ex, "内置引擎解压失败，尝试使用系统 tar 解压")
        End Try
        Try
            Dim TarPath As String = IO.Path.Combine(Environment.SystemDirectory, "tar.exe")
            If Not FileUtils.Exists(TarPath) Then Return False
            Dim Proc As New Process With {
                .StartInfo = New ProcessStartInfo With {
                    .FileName = TarPath,
                    .Arguments = $"-xf ""{FileAddress}"" -C ""{PathUtils.RemoveSlashSuffix(TargetFolder)}""",
                    .CreateNoWindow = True,
                    .UseShellExecute = False
                }
            }
            Proc.Start()
            Proc.WaitForExit()
            Proc.Dispose()
            Return DirectoryUtils.Exists(TargetFolder)
        Catch ex As Exception
            Logger.Warn(ex, "使用系统 tar 解压失败")
            Return False
        End Try
    End Function

    ''' <summary>
    ''' 生成压缩包内的目录名：去掉非法字符，并保证互不重名。
    ''' </summary>
    Private Shared Function GetPackEntryName(Name As String, UsedNames As HashSet(Of String)) As String
        Dim Result As String = If(String.IsNullOrWhiteSpace(Name), ".minecraft", Name.Trim())
        For Each InvalidChar In IO.Path.GetInvalidFileNameChars()
            Result = Result.Replace(InvalidChar.ToString(), "_")
        Next
        Result = Result.Replace("/", "_").Replace("\", "_")
        If Result = "" Then Result = ".minecraft"
        Dim Origin As String = Result
        Dim Index As Integer = 1
        While UsedNames.Contains(Result)
            Index += 1
            Result = Origin & "_" & Index
        End While
        UsedNames.Add(Result)
        Return Result
    End Function

#End Region

#Region "内存优化相关的系统调用"

    Private Const TOKEN_ADJUST_PRIVILEGES As Integer = &H20
    Private Const TOKEN_QUERY As Integer = &H8
    Private Const SE_PRIVILEGE_ENABLED As Integer = &H2
    Private Const SystemMemoryListInformation As Integer = 80
    Private Const MemoryEmptyWorkingSets As Integer = 2
    Private Const MemoryFlushModifiedList As Integer = 3
    Private Const MemoryPurgeStandbyList As Integer = 4

    <StructLayout(LayoutKind.Sequential)>
    Private Structure LUID
        Public LowPart As Integer
        Public HighPart As Integer
    End Structure

    <StructLayout(LayoutKind.Sequential)>
    Private Structure LUID_AND_ATTRIBUTES
        Public Luid As LUID
        Public Attributes As Integer
    End Structure

    <StructLayout(LayoutKind.Sequential)>
    Private Structure TOKEN_PRIVILEGES
        Public PrivilegeCount As Integer
        Public Privileges As LUID_AND_ATTRIBUTES
    End Structure

    Private Declare Function EmptyWorkingSet Lib "psapi.dll" (hProcess As IntPtr) As Boolean
    Private Declare Function SetSystemFileCacheSize Lib "kernel32" (MinimumFileCacheSize As IntPtr, MaximumFileCacheSize As IntPtr, Flags As Integer) As Boolean
    Private Declare Function NtSetSystemInformation Lib "ntdll.dll" (SystemInformationClass As Integer, ByRef SystemInformation As Integer, SystemInformationLength As Integer) As Integer
    Private Declare Function OpenProcessToken Lib "advapi32.dll" (ProcessHandle As IntPtr, DesiredAccess As Integer, ByRef TokenHandle As IntPtr) As Boolean
    Private Declare Function LookupPrivilegeValue Lib "advapi32.dll" (lpSystemName As String, lpName As String, ByRef lpLuid As LUID) As Boolean
    Private Declare Function AdjustTokenPrivileges Lib "advapi32.dll" (TokenHandle As IntPtr, DisableAllPrivileges As Boolean, ByRef NewState As TOKEN_PRIVILEGES, BufferLength As Integer, ByRef PreviousState As TOKEN_PRIVILEGES, ByRef ReturnLength As Integer) As Boolean
    Private Declare Function CloseHandle Lib "kernel32" (hObject As IntPtr) As Boolean

#End Region

End Class