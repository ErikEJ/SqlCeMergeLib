using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ErikEJ.SqlCeMergeLib;
using System.Data.SqlServerCe;

namespace TestFormCS
{
    public partial class Form1 : Form
    {
        private EventHandler myStatusEvent;
        private SyncArgs syncArgs;
        private MergeReplication sync = new MergeReplication();
        private SqlCeConnection conn;

        public Form1()
        {
            InitializeComponent();
            this.myStatusEvent = new EventHandler(StatusEvent);
        }

        private void StatusEvent(object sender, EventArgs e)
        {
            textBox1.AppendText(Environment.NewLine + syncArgs.Message);

            switch (syncArgs.SyncStatus)
            {
                case SyncStatus.BeginUpload:
                    textBox1.AppendText(Environment.NewLine + syncArgs.TableName);
                    break;

                case SyncStatus.PercentComplete:
                    textBox1.AppendText(Environment.NewLine + syncArgs.PercentComplete.ToString());
                    break;

                case SyncStatus.BeginDownload:
                    textBox1.AppendText(Environment.NewLine + syncArgs.TableName);
                    break;

                case SyncStatus.SyncComplete:
                    button1.Enabled = true;
                    break;

                case SyncStatus.SyncFailed:
                    if ((syncArgs.Exception != null))
                    {
                        switch (syncArgs.Exception.GetType().Name)
                        {
                            case "PublicationMayHaveExpiredException":
                                //' Inner exception is SqlCeException in this case
                                textBox1.AppendText(Environment.NewLine + sync.ShowErrors((SqlCeException)syncArgs.Exception.InnerException));                                
                                // Here we couldb start doing recovery - reset of local db
                                //sync.GenerateInsertScripts(conn, new List<string> { "test1", "test2" });
                                break;
                            case "SqlCeException":
                                textBox1.AppendText(Environment.NewLine + sync.ShowErrors((SqlCeException)syncArgs.Exception));
                                break;
                            case "Exception":
                                textBox1.AppendText(textBox1.Text + Environment.NewLine + syncArgs.Exception.Message);
                                break;
                        }
                    }
                    button1.Enabled = true;
                    break;
            }
        }

        private void  Button1_Click(System.Object sender, System.EventArgs e)
        {
            try
            {
                button1.Enabled = false;
                textBox1.Text = string.Empty;
                string sdfFile = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MergeTest.sdf");
                conn = new SqlCeConnection(string.Format("Data Source={0}", sdfFile));
                
                //To use a password, use the following syntax
                conn = new SqlCeConnection(string.Format("Data Source={0}", sdfFile));
                sync.DatabasePassword = "secret";

                textBox1.AppendText("Runtime version (must be 3.5.8088 or higher for Merge with SQL 2012): " + Environment.NewLine + conn.ServerVersion.ToString());

                //Optionally specify replication properties in code:
                //sync.ReplicationProperties = new ReplicationProperties();

                DateTime syncDate = sync.GetLastSuccessfulSyncTime(conn);
                textBox1.AppendText(Environment.NewLine + "Last Sync: " + syncDate.ToString());
                
                sync.Completed += SyncCompletedEvent;
                sync.Progress += SyncProgressEvent;
                sync.Synchronize(conn, 1002);
            }
            catch (SqlCeException sqlex)
            {
                MessageBox.Show(sync.ShowErrors(sqlex));
                button1.Enabled = true;
            }
        }

        private void SyncCompletedEvent(object sender, SyncArgs e)
        {
            sync.Completed -= SyncCompletedEvent;
            sync.Progress -= SyncProgressEvent;
            syncArgs = e;
            this.Invoke(myStatusEvent);
        }

        private void SyncProgressEvent(object sender, SyncArgs e)
        {
            syncArgs = e;
            this.Invoke(myStatusEvent);
        }

    }
}
