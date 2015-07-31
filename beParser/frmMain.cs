using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace beParser
{
    public partial class frmMain : Form
    {
        List<String> filesToWatch = new List<String>();
        List<Worker> workerObjects = new List<Worker>();
        List<Thread> workerThreads = new List<Thread>();
        String basePath = "testlogs";

        public frmMain()
        {
            InitializeComponent();
        }

        private void fmrMain_Load(object sender, EventArgs e)
        {
            filesToWatch.Add("scripts.log");
            filesToWatch.Add("arma2oaserver.RPT");
            filesToWatch.Add("attachto.log");
            filesToWatch.Add("publicvariable.log");
            filesToWatch.Add("setvariable.log");

            logDebug("LOADED");

            Run();
        }

        private void Run()
        {
            foreach(String file in filesToWatch)
            {
                Worker w = new Worker(this, basePath + "\\" + file);
                workerObjects.Add(w);
                Thread t = new Thread(w.DoWork);
                workerThreads.Add(t);
                t.IsBackground = true;
                t.Start();
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            foreach(Worker w in workerObjects)
            {
                w.RequestStop();
            }

            base.OnFormClosing(e);
        }

        delegate void logDebugDelegate(String s);

        public void logDebug(String s)
        {
            if (InvokeRequired)
            {
                logDebugDelegate del = logDebug;
                Invoke(del, new object[]{s});
            }
            else
            {
                rtbOutput.Text += addDateString(s);
            }
        }

        private String getDateString()
        {
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        private String addDateString(String s)
        {
            return getDateString() + " " + s + "\n";
        }
    }

    public class Worker
    {
        private volatile bool _shouldStop;
        private frmMain _parentForm;
        private String _filePath;
        private FileStream _fileStream;

        public Worker(frmMain parentForm, String filePath)
        {
            this._parentForm = parentForm;
            this._filePath = filePath;
        }

        public void DoWork()
        {
            threadLogDebug("starting");


            while(!_shouldStop)
            {
                threadLogDebug("sleeping");
                Thread.Sleep(1000);
            }
            threadLogDebug("exiting");
        }

        private void threadLogDebug(String s)
        {
            _parentForm.logDebug("Thread " + System.Threading.Thread.CurrentThread.ManagedThreadId.ToString() + " " + s);
        }

        public void RequestStop()
        {
            _shouldStop = true;
        }
    }
}
