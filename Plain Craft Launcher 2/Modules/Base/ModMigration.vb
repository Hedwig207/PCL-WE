Imports System.IO

Public Module ModMigration

    ''' <summary>
    ''' 获取"本次编译"的唯一标识。每次重新编译程序集都会重新生成该值，
    ''' 因此可用于在重新编译后自动重置弹窗/迁移的"已显示"标记。
    ''' </summary>
    Public Function GetCompileStamp() As String
        Try
            '使用程序集文件的最后写入时间（精确到刻度）作为"本次编译"的唯一标识。
            '每次重新编译都会生成新的 exe，其写入时间必然不同，从而自动重置已显示的弹窗/迁移标记。
            '（不采用 ModuleVersionId：.NET Framework 的 VB 编译器对相同源码会生成确定性的 MVID，重新编译后不变）
            Return File.GetLastWriteTimeUtc(System.Reflection.Assembly.GetExecutingAssembly().Location).Ticks.ToString
        Catch ex As Exception
            Logger.Warn(ex, "获取编译标识失败")
            Return ""
        End Try
    End Function

    ''' <summary>
    ''' 扫描所有磁盘递归寻找 .minecraft 文件夹，并向用户询问是否迁移。
    ''' 仅在同一版本构建下执行一次，且会处理找到的全部文件夹。
    ''' </summary>
    Public Sub ScanAndMigrate()
        Try
            '仅在该版本构建下扫描一次（用编译标识，重编译后自动重新检测）
            If Settings.Get(Of String)("SystemMigrationShown") = GetCompileStamp() Then Return
            Settings.Set("SystemMigrationShown", GetCompileStamp())

            '递归遍历所有磁盘，寻找 .minecraft 文件夹（限深度防止卡死）
            Dim FoundFolders As New List(Of String)
            For Each Drive In Environment.GetLogicalDrives()
                Try
                    If Not DirectoryUtils.Exists(Drive) Then Continue For
                    ScanDriveRecursively(Drive, 0, FoundFolders)
                Catch ex As Exception
                    Logger.Warn(ex, $"扫描磁盘 {Drive} 寻找 Minecraft 文件夹失败")
                End Try
            Next

            '遍历每个找到的 .minecraft，分别询问是否迁移
            Dim Index As Integer = 1
            Dim AnyMigrated As Boolean = False
            For Each McFolderToMigrate As String In FoundFolders
                '若该文件夹已经在列表中，则跳过
                If IsFolderInList(McFolderToMigrate) Then Continue For
                Dim Result = MyMsgBox(
                    $"检测到磁盘上存在 Minecraft 文件夹：{vbCrLf}{McFolderToMigrate}{vbCrLf}{vbCrLf}是否将其加入启动器的版本列表，并迁移其中可用的 PCL 设置与个性化数据？",
                    "检测到 Minecraft 文件夹",
                    "加入并迁移", "忽略")
                If Result = 1 Then
                    AddFolderToList(McFolderToMigrate, "迁移文件夹" & Index)
                    TryMigratePclData(McFolderToMigrate)
                    AnyMigrated = True
                    Index += 1
                End If
                If MyMsgBox("是否继续检查其余目录？", "继续检查", "继续", "不再提示") = 2 Then Exit For
            Next

            '若有目录迁移成功，则刷新 Minecraft 文件夹列表并提示
            If AnyMigrated Then
                McFolderListLoader.Start(IsForceRestart:=True)
                MyMsgBox("已成功迁移 Minecraft 文件夹与相关设置！", "迁移完成", IsBlue:=True)
            End If
        Catch ex As Exception
            Logger.Error(ex, "执行 Minecraft 文件夹迁移检查失败")
        End Try
    End Sub

    ''' <summary>
    ''' 递归扫描目录，寻找包含 versions 子目录的 .minecraft 文件夹。
    ''' </summary>
    Private Sub ScanDriveRecursively(Folder As String, Depth As Integer, FoundFolders As List(Of String))
        If Depth > 8 Then Return
        Try
            Dim DirInfo = DirectoryUtils.GetInfo(PathUtils.AddSlashSuffix(Folder))
            If Not DirInfo.Exists Then Return
            '先物化全部子目录，避免延迟枚举到受保护目录（如 System Volume Information）时抛异常中断整盘扫描
            Dim SubDirectories As IO.DirectoryInfo() = Nothing
            Try
                SubDirectories = DirInfo.GetDirectories().ToArray
            Catch ex As Exception
                Logger.Warn(ex, $"枚举 {Folder} 的子目录失败（可能是权限不足），跳过该层")
                SubDirectories = Array.Empty(Of IO.DirectoryInfo)()
            End Try
            '检查当前目录下的 .minecraft 子目录
            Dim McRoot As String = PathUtils.AddSlashSuffix(PathUtils.AddSlashSuffix(Folder) & ".minecraft")
            If DirectoryUtils.Exists(McRoot & "versions") Then
                '去重后保存原始大小写路径
                If Not FoundFolders.Any(Function(x) PathUtils.IsSame(PathUtils.AddSlashSuffix(x), McRoot)) Then
                    FoundFolders.Add(McRoot)
                End If
            End If
            '递归进入子目录
            For Each SubDir As IO.DirectoryInfo In SubDirectories
                Try
                    If SubDir.Name = "Windows" OrElse SubDir.Name = "Windows.old" OrElse
                       SubDir.Name = "System Volume Information" OrElse SubDir.Name = "$Recycle.Bin" OrElse
                       SubDir.Name = "$Windows.~BT" OrElse SubDir.Name = "$Windows.~WS" OrElse
                       SubDir.Name = "$RECYCLE.BIN" Then Continue For
                    ScanDriveRecursively(SubDir.FullName & "\", Depth + 1, FoundFolders)
                Catch ex As Exception
                    Logger.Warn(ex, "扫描子目录寻找 Minecraft 文件夹失败")
                End Try
            Next
        Catch ex As Exception
            Logger.Warn(ex, $"递归扫描 {Folder} 失败")
        End Try
    End Sub

    ''' <summary>
    ''' 判断该 Minecraft 文件夹是否已经加入了列表。
    ''' </summary>
    Private Function IsFolderInList(McFolderPath As String) As Boolean
        Try
            For Each Entry In Settings.Get(Of String)("LaunchFolders").Split("|"c)
                Dim FolderPath As String = If(Entry.Contains(">"), Entry.AfterFirst(">"), Entry)
                If PathUtils.IsSame(PathUtils.AddSlashSuffix(FolderPath), PathUtils.AddSlashSuffix(McFolderPath)) Then Return True
            Next
        Catch ex As Exception
            Logger.Warn(ex, "读取 Minecraft 文件夹列表失败")
        End Try
        Return False
    End Function

    ''' <summary>
    ''' 将 Minecraft 文件夹加入启动器的版本列表。
    ''' </summary>
    Private Sub AddFolderToList(McFolderPath As String, FolderName As String)
        Try
            Dim NewSetup As New List(Of String)
            '保留已有项
            For Each Entry In Settings.Get(Of String)("LaunchFolders").Split("|"c)
                If Entry <> "" Then NewSetup.Add(Entry)
            Next
            '添加新文件夹，确保路径以反斜杠结尾、大小写与列表一致
            Dim FinalPath As String = PathUtils.AddSlashSuffix(McFolderPath)
            NewSetup.Add(FolderName & ">" & FinalPath)
            Settings.Set("LaunchFolders", NewSetup.Join("|"c))
            Logger.Info($"已将 Minecraft 文件夹加入列表：{FolderName} → {FinalPath}")
        Catch ex As Exception
            Logger.Error(ex, "将 Minecraft 文件夹加入列表失败")
        End Try
    End Sub

    ''' <summary>
    ''' 尝试从源 Minecraft 文件夹迁移 PCL 配置、个性化与皮肤等数据。
    ''' </summary>
    Private Sub TryMigratePclData(McFolderPath As String)
        Dim TargetRoot As String = Paths.Base & ".minecraft\"
        DtMigratePclData(McFolderPath, TargetRoot)
    End Sub

    ''' <summary>
    ''' 将旧 Minecraft 文件夹中可用的 PCL 数据迁移到当前 Minecraft 文件夹。
    ''' </summary>
    Private Sub DtMigratePclData(SourceMinecraft As String, TargetMinecraft As String)
        Try
            Dim SourceRoot As String = PathUtils.AddSlashSuffix(SourceMinecraft)
            Dim TargetRoot As String = PathUtils.AddSlashSuffix(TargetMinecraft)
            '自定义个性化主页
            MigrationFileCheck(SourceRoot & "PCL\Custom.xaml", TargetRoot & "PCL\Custom.xaml", "自定义个性化主页 Custom.xaml")
            '自定义皮肤
            MigrationFileCheck(SourceRoot & "PCL\CustomSkin.png", Paths.AppDataThenName & "CustomSkin.png", "自定义皮肤 CustomSkin.png")
            '登录用户数据
            MigrationFileCheck(SourceRoot & "PCL\LoginHistory.dat", Paths.AppDataThenName & "LoginHistory.dat", "登录用户数据")
            MigrationFileCheck(SourceRoot & "PCL\Accounts\", Paths.AppDataThenName & "Accounts\", "离线账号数据")
        Catch ex As Exception
            Logger.Error(ex, "迁移 PCL 数据失败")
        End Try
    End Sub

    ''' <summary>
    ''' 检查源文件/目录存在且目标不存在时复制。
    ''' </summary>
    Private Sub MigrationFileCheck(Source As String, Target As String, Description As String)
        Try
            Source = PathUtils.AddSlashSuffix(Source)
            If DirectoryUtils.Exists(Source) Then
                '迁移目录：确保目标父目录存在
                EnsuredFolder(PathUtils.RemoveLastPart(PathUtils.AddSlashSuffix(Target)))
                If Not DirectoryUtils.Exists(PathUtils.AddSlashSuffix(Target)) Then
                    DirectoryUtils.Copy(Source, PathUtils.AddSlashSuffix(Target))
                    Logger.Info($"已迁移 {Description}：{Source} → {Target}")
                End If
            ElseIf FileUtils.Exists(Source) Then
                '迁移文件：确保目标父目录存在
                EnsuredFolder(PathUtils.RemoveLastPart(PathUtils.AddSlashSuffix(Target)))
                If Not FileUtils.Exists(Target) Then
                    FileUtils.Copy(Source, Target)
                    Logger.Info($"已迁移 {Description}：{Source} → {Target}")
                End If
            End If
        Catch ex As Exception
            Logger.Warn(ex, $"迁移 {Description} 失败")
        End Try
    End Sub

    ''' <summary>
    ''' 确保指定文件夹存在，不存在则创建。
    ''' </summary>
    Private Sub EnsuredFolder(Folder As String)
        Try
            If Folder <> "" AndAlso Not DirectoryUtils.Exists(Folder) Then DirectoryUtils.Create(Folder)
        Catch ex As Exception
            Logger.Warn(ex, "创建目标文件夹失败")
        End Try
    End Sub

End Module