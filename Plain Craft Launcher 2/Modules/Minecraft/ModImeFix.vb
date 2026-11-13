''' <summary>
''' MC 输入法修复。
''' 游戏窗口位于前台时，把输入法强制切换为英文，避免按 Shift 潜行时触发中英文切换，导致游戏输入卡住；
''' 玩家在游戏中打开聊天框（默认按 T 或 /）后会暂停强制切换，此时可以照常切换到中文输入。
''' </summary>
Public Module ModImeFix

#Region "声明"

    '输入法转换状态
    Private Const ImeCmodeNative As Integer = &H1 '中文输入模式
    '轮询间隔（毫秒）
    Private Const PollInterval As Integer = 50
    '进入输入状态后，多久没有按键就自动退出（毫秒）
    Private Const TypeModeTimeout As Long = 20000

    '虚拟键码
    Private Const VkBack As Integer = &H8
    Private Const VkReturn As Integer = &HD
    Private Const VkEscape As Integer = &H1B
    Private Const VkSpace As Integer = &H20
    Private Const VkT As Integer = &H54
    Private Const VkOem2 As Integer = &HBF '/'

    Private Declare Function ImmGetContext Lib "imm32.dll" (hWnd As IntPtr) As IntPtr
    Private Declare Function ImmReleaseContext Lib "imm32.dll" (hWnd As IntPtr, hIMC As IntPtr) As Boolean
    Private Declare Function ImmGetConversionStatus Lib "imm32.dll" (hIMC As IntPtr, ByRef Conversion As Integer, ByRef Sentence As Integer) As Boolean
    Private Declare Function ImmSetConversionStatus Lib "imm32.dll" (hIMC As IntPtr, Conversion As Integer, Sentence As Integer) As Boolean
    Private Declare Function GetForegroundWindow Lib "user32" () As IntPtr
    Private Declare Function IsWindow Lib "user32" (hWnd As IntPtr) As Boolean
    Private Declare Function GetAsyncKeyState Lib "user32" (vKey As Integer) As Short

    '会延长输入状态的按键（字母、数字、空格、退格）
    Private ReadOnly TypingKeys As Integer() =
        Enumerable.Range(&H41, 26).Concat(Enumerable.Range(&H30, 10)).Concat({VkSpace, VkBack}).ToArray()

#End Region

#Region "任务"

    ''' <summary>
    ''' 对一个游戏窗口的输入法修复任务。
    ''' </summary>
    Private Class FixTask
        Public Handle As IntPtr
        Public GameProcess As Process
        Public Stopped As Boolean
        Public IsTyping As Boolean
        Public LastTypeTick As Long
        Public LastKeys As New Dictionary(Of Integer, Boolean)
        Public HasReported As Boolean
    End Class
    Private ReadOnly Tasks As New List(Of FixTask)
    Private ReadOnly TaskLock As New Object()

    ''' <summary>
    ''' 开始对指定游戏窗口应用输入法修复。若该窗口已在修复中则忽略。
    ''' </summary>
    Public Sub ImeFixStart(Handle As IntPtr, GameProcess As Process)
        If Handle = IntPtr.Zero Then Return
        If Not Settings.Get(Of Boolean)("ToolImeFix") Then Return
        SyncLock TaskLock
            For Each Existing As FixTask In Tasks
                If Existing.Handle = Handle Then Return
            Next
            Dim Task As New FixTask With {.Handle = Handle, .GameProcess = GameProcess}
            Tasks.Add(Task)
            RunInNewThread(Sub() ImeFixLoop(Task), "MC Ime Fix " & Handle.ToInt64())
        End SyncLock
        Logger.Info($"已开始 MC 输入法修复：{Handle.ToInt64()}")
    End Sub

    ''' <summary>
    ''' 对当前所有已找到窗口的游戏应用输入法修复。
    ''' </summary>
    Public Sub ImeFixStartAllRunning()
        Dim Watches As Watcher()
        Try
            Watches = McWatcherList.ToArray() '取快照，避免与其它线程同时操作列表
        Catch ex As Exception
            Logger.Warn(ex, "获取游戏窗口列表失败，无法应用 MC 输入法修复")
            Return
        End Try
        For Each Watch As Watcher In Watches
            If Watch.WindowHandle = IntPtr.Zero Then Continue For
            ImeFixStart(Watch.WindowHandle, Watch.GameProcess)
        Next
    End Sub

    ''' <summary>
    ''' 停止全部输入法修复任务。
    ''' </summary>
    Public Sub ImeFixStop()
        SyncLock TaskLock
            For Each Task As FixTask In Tasks
                Task.Stopped = True
            Next
            Tasks.Clear()
        End SyncLock
    End Sub

    ''' <summary>
    ''' 设置项变化时同步修复任务的启停。
    ''' </summary>
    Public Sub OnSettingChanged()
        If Settings.Get(Of Boolean)("ToolImeFix") Then
            ImeFixStartAllRunning()
        Else
            ImeFixStop()
        End If
    End Sub

    ''' <summary>
    ''' 单个窗口的修复主循环。窗口关闭、游戏结束或功能被关闭时自动退出。
    ''' </summary>
    Private Sub ImeFixLoop(Task As FixTask)
        Try
            Do
                If Task.Stopped Then Exit Do
                If Not Settings.Get(Of Boolean)("ToolImeFix") Then Exit Do
                If Task.GameProcess IsNot Nothing AndAlso Task.GameProcess.HasExited Then Exit Do
                If Not IsWindow(Task.Handle) Then Exit Do
                '只在游戏窗口位于前台时生效，避免影响在启动器等其它窗口中输入
                If GetForegroundWindow() = Task.Handle Then
                    UpdateTypingState(Task)
                    If Not Task.IsTyping Then ForceEnglish(Task)
                Else
                    '窗口不在前台时重置状态，避免切回游戏时把旧按键误判为刚刚按下
                    Task.IsTyping = False
                    Task.LastKeys.Clear()
                End If
                Thread.Sleep(PollInterval)
            Loop
        Catch ex As Exception
            Logger.Error(ex, "MC 输入法修复主循环出错")
        Finally
            SyncLock TaskLock
                Tasks.RemoveAll(Function(t) t Is Task)
            End SyncLock
        End Try
    End Sub

    ''' <summary>
    ''' 根据按键情况更新「玩家正在输入文字」的状态。
    ''' 按下 T 或 / 视为打开了聊天框，按下回车或 Esc 视为关闭了聊天框。
    ''' </summary>
    Private Sub UpdateTypingState(Task As FixTask)
        If KeyPressed(Task, VkT) OrElse KeyPressed(Task, VkOem2) Then
            Task.IsTyping = True
            Task.LastTypeTick = GetTimeMs()
        ElseIf KeyPressed(Task, VkReturn) OrElse KeyPressed(Task, VkEscape) Then
            Task.IsTyping = False
        End If
        If Not Task.IsTyping Then Return
        '输入过程中不断续期，避免打字打到一半又被重新锁定
        For Each Key As Integer In TypingKeys
            If KeyPressed(Task, Key) Then
                Task.LastTypeTick = GetTimeMs()
                Exit For
            End If
        Next
        If GetTimeMs() - Task.LastTypeTick > TypeModeTimeout Then Task.IsTyping = False
    End Sub

    ''' <summary>
    ''' 检查某个按键是否刚被按下（仅在按下的那一次轮询中返回 True）。
    ''' </summary>
    Private Function KeyPressed(Task As FixTask, Key As Integer) As Boolean
        Dim IsDown As Boolean = (GetAsyncKeyState(Key) And &H8000) <> 0
        Dim WasDown As Boolean = False
        Task.LastKeys.TryGetValue(Key, WasDown)
        Task.LastKeys(Key) = IsDown
        Return IsDown AndAlso Not WasDown
    End Function

    ''' <summary>
    ''' 把游戏窗口的输入法切换为英文。
    ''' </summary>
    Private Sub ForceEnglish(Task As FixTask)
        Dim Himc As IntPtr = ImmGetContext(Task.Handle)
        If Himc = IntPtr.Zero Then
            If Not Task.HasReported Then
                Task.HasReported = True
                Logger.Warn("MC 输入法修复无法获取游戏窗口的输入法上下文，本次游戏无法锁定输入法")
            End If
            Return
        End If
        Try
            Dim Conversion As Integer = 0, Sentence As Integer = 0
            If Not ImmGetConversionStatus(Himc, Conversion, Sentence) Then Return
            If (Conversion And ImeCmodeNative) = 0 Then Return '已经是英文，无需切换
            ImmSetConversionStatus(Himc, Conversion And Not ImeCmodeNative, Sentence)
        Finally
            ImmReleaseContext(Task.Handle, Himc)
        End Try
    End Sub

#End Region

End Module