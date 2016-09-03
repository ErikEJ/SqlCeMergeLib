using System;
using System.Windows.Forms;
using ErikEJ.SqlCeMergeLib;
using System.Data.SqlServerCe;

namespace TestFormCS
{
    public partial class Form1 : Form
    {
        private readonly EventHandler _myStatusEvent;
        private SyncArgs _syncArgs;
        private readonly MergeReplication _sync = new MergeReplication();
        private SqlCeConnection _conn;

        public Form1()
        {
            InitializeComponent();
            _myStatusEvent = StatusEvent;
        }

        private void StatusEvent(object sender, EventArgs e)
        {
            textBox1.AppendText(Environment.NewLine + _syncArgs.Message);

            switch (_syncArgs.SyncStatus)
            {
                case SyncStatus.BeginUpload:
                    textBox1.AppendText(Environment.NewLine + _syncArgs.TableName);
                    break;

                case SyncStatus.PercentComplete:
                    textBox1.AppendText(Environment.NewLine + _syncArgs.PercentComplete.ToString());
                    break;

                case SyncStatus.BeginDownload:
                    textBox1.AppendText(Environment.NewLine + _syncArgs.TableName);
                    break;

                case SyncStatus.SyncComplete:
                    button1.Enabled = true;
                    //Optionally validate that the database has been properly replicated
                    //sync.Validate(conn);
                    break;

                case SyncStatus.SyncFailed:
                    if ((_syncArgs.Exception != null))
                    {
                        switch (_syncArgs.Exception.GetType().Name)
                        {
                            case "PublicationMayHaveExpiredException":
                                //' Inner exception is SqlCeException in this case
                                textBox1.AppendText(Environment.NewLine + _sync.ShowErrors((SqlCeException)_syncArgs.Exception.InnerException));                                
                                // Here we couldb start doing recovery - reset of local db
                                //sync.GenerateInsertScripts(conn, new List<string> { "test1", "test2" });
                                break;
                            case "SqlCeException":
                                textBox1.AppendText(Environment.NewLine + _sync.ShowErrors((SqlCeException)_syncArgs.Exception));
                                break;
                            case "Exception":
                                textBox1.AppendText(textBox1.Text + Environment.NewLine + _syncArgs.Exception.Message);
                                break;
                        }
                    }
                    button1.Enabled = true;
                    break;
            }
        }

        private void  Button1_Click(object sender, EventArgs e)
        {
            try
            {
                button1.Enabled = false;
                textBox1.Text = string.Empty;
                string sdfFile = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Merge.sdf");
                _conn = new SqlCeConnection(string.Format("Data Source={0}", sdfFile));
                
                //To use a password, use the following syntax
                //conn = new SqlCeConnection(string.Format("Data Source={0}", sdfFile));
                //sync.DatabasePassword = "secret";

                if (_conn.ServerVersion != null)
                    textBox1.AppendText("Runtime version (must be 3.5.8088 or higher for Merge with SQL 2012): " + Environment.NewLine + _conn.ServerVersion);

                //Optionally specify replication properties in code:
                //sync.ReplicationProperties = new ReplicationProperties();

                DateTime syncDate = _sync.GetLastSuccessfulSyncTime(_conn);
                textBox1.AppendText(Environment.NewLine + "Last Sync: " + syncDate);
                
                _sync.Completed += SyncCompletedEvent;
                _sync.Progress += SyncProgressEvent;
                _sync.Synchronize(_conn, 1002);
            }
            catch (SqlCeException sqlex)
            {
                MessageBox.Show(_sync.ShowErrors(sqlex));
                button1.Enabled = true;
            }
        }

        private void SyncCompletedEvent(object sender, SyncArgs e)
        {
            _sync.Completed -= SyncCompletedEvent;
            _sync.Progress -= SyncProgressEvent;
            _syncArgs = e;
            Invoke(_myStatusEvent);
        }

        private void SyncProgressEvent(object sender, SyncArgs e)
        {
            _syncArgs = e;
            Invoke(_myStatusEvent);
        }
    }
}
