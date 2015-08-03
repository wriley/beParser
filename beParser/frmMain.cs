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
            //filesToWatch.Add("arma2oaserver.RPT");
            //filesToWatch.Add("attachto.log");
            //filesToWatch.Add("publicvariable.log");
            //filesToWatch.Add("setvariable.log");

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
            int threadsRunning = 0;
            foreach(Thread t in workerThreads)
            {
                if(!t.Join(0))
                {
                    threadsRunning++;
                }
            }
            if (threadsRunning > 0)
            {
                e.Cancel = true;
                foreach (Worker w in workerObjects)
                {
                    w.RequestStop();
                }
                var timer = new System.Timers.Timer();
                timer.AutoReset = false;
                timer.SynchronizingObject = this;
                timer.Interval = 1000;
                timer.Elapsed +=
                    (sender, args) =>
                    {
                        threadsRunning = 0;
                        foreach (Thread t in workerThreads)
                        {
                            if (!t.Join(0))
                            {
                                threadsRunning++;
                            }
                        }
                        if(threadsRunning == 0)
                        {
                            Close();
                        }
                        else
                        {
                            timer.Start();
                        }
                    };
                timer.Start();
            }

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
                rtbOutput.SelectionStart = rtbOutput.Text.Length;
                rtbOutput.ScrollToCaret();
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
        private String _fileName;

        public Worker(frmMain parentForm, String filePath)
        {
            this._parentForm = parentForm;
            this._filePath = filePath;
            this._fileName = Path.GetFileName(_filePath);
        }

        public void DoWork()
        {
            threadLogDebug("starting");

            while(!_shouldStop)
            {
                try
                {
                    FileStream fs = File.Open(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    StreamReader sr = new StreamReader(fs);
                    Int64 lastSize = GetFileSize();
                    sr.BaseStream.Seek(lastSize, SeekOrigin.Begin);
                    string line;
                    Int64 currentSize;
                    while (!_shouldStop)
                    {
                        currentSize = GetFileSize();
                        if (currentSize < lastSize)
                        {
                            threadLogDebug("File size reduced, starting at beginning of file");
                            sr.DiscardBufferedData();
                            sr.BaseStream.Seek(0, SeekOrigin.Begin);
                            sr.BaseStream.Position = 0;
                        }
                        if((line = sr.ReadLine()) != null)
                        {
                            threadLogDebug(line);
                        }
                        lastSize = currentSize;
                    }
                    if (fs != null) { fs.Close(); }
                }
                catch (Exception)
                {
                    threadLogDebug("Error opening file, sleeping for a bit");
                    Thread.Sleep(2000);
                }
            }
            threadLogDebug("exiting");
        }

        private Int64 GetFileSize()
        {
            FileStream fs = null;
            Int64 length;
            try
            {
                fs = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                length = fs.Length;
                return length;
            }
            finally
            {
                fs.Close();
            }
        }

        private void threadLogDebug(String s)
        {
            _parentForm.logDebug(_fileName + ": " + s);
        }

        public void RequestStop()
        {
            _shouldStop = true;
        }
    }
}
