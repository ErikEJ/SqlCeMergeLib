Imports ErikEJ.SqlCeMergeLib
Imports System.Data.SqlServerCe

Public Class Form1

    Private myStatusEvent As EventHandler
    Private syncArgs As SyncArgs
    Private sync As New MergeReplication()

    Public Sub New()

        ' This call is required by the designer.
        InitializeComponent()

        ' Add any initialization after the InitializeComponent() call.
        Me.myStatusEvent = New EventHandler(AddressOf StatusEvent)
    End Sub

    Private Sub StatusEvent(ByVal sender As Object, ByVal e As EventArgs)

        TextBox1.AppendText(Environment.NewLine & syncArgs.Message)

        Select Case syncArgs.SyncStatus

            Case SyncStatus.BeginUpload
                TextBox1.AppendText(Environment.NewLine & syncArgs.TableName)

            Case SyncStatus.PercentComplete
                TextBox1.AppendText(Environment.NewLine & syncArgs.PercentComplete.ToString())

            Case SyncStatus.BeginDownload
                TextBox1.AppendText(Environment.NewLine & syncArgs.TableName)

            Case SyncStatus.SyncComplete

            Case SyncStatus.SyncFailed
                If Not (syncArgs.Exception Is Nothing) Then

                    Select Case syncArgs.Exception.GetType().Name
                        Case GetType(PublicationMayHaveExpiredException).Name
                            '' Inner exception is SqlCeException in this case
                            TextBox1.AppendText(Environment.NewLine & sync.ShowErrors(syncArgs.Exception.InnerException))
                            ' Here we can start doing recovery - reset of local db
                        Case GetType(SqlCeException).Name
                            TextBox1.AppendText(Environment.NewLine & sync.ShowErrors(syncArgs.Exception))
                        Case GetType(Exception).Name
                            TextBox1.AppendText(TextBox1.Text & Environment.NewLine & syncArgs.Exception.Message)
                    End Select

                End If

        End Select
    End Sub


    Private Sub Button1_Click(sender As System.Object, e As System.EventArgs) Handles Button1.Click

        Try

            TextBox1.Clear()

            Dim sdfFile = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "GBPSTest.sdf")

            Dim conn As New SqlCeConnection(String.Format("Data Source={0}", sdfFile))

            Dim syncDate = sync.GetLastSuccessfulSyncTime(conn)
            TextBox1.Text = "Last Sync: " & syncDate.ToString()

            AddHandler sync.Completed, AddressOf SyncCompletedEvent

            AddHandler sync.Progress, AddressOf SyncProgressEvent

            sync.Synchronize(conn, 1002, 1)

        Catch sqlex As SqlCeException
            MessageBox.Show(sync.ShowErrors(sqlex))
        End Try

    End Sub

    Private Sub SyncCompletedEvent(sender As Object, e As SyncArgs)
        syncArgs = e
        Me.Invoke(myStatusEvent)
    End Sub

    Private Sub SyncProgressEvent(sender As Object, e As SyncArgs)
        syncArgs = e
        Me.Invoke(myStatusEvent)
    End Sub

End Class
