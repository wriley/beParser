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
using System.Collections.Concurrent;

namespace beParser
{
    public partial class frmMain : Form
    {
        List<String> filesToWatch = new List<String>();
        List<Worker> workerObjects = new List<Worker>();
        List<Thread> workerThreads = new List<Thread>();
        string basePath = "C:\\arma2oa\\dayz_2\\BattlEye";
        //string basePath = "testlogs";
        ConcurrentQueue<string> debugLogQueue = new ConcurrentQueue<string>();
        ConcurrentQueue<string> outputLogQueue = new ConcurrentQueue<string>();

        public frmMain()
        {
            InitializeComponent();
        }

        private void fmrMain_Load(object sender, EventArgs e)
        {
            filesToWatch.Add("..\\server_console.log");
            filesToWatch.Add("addweaponcargo.log");
            filesToWatch.Add("addmagazinecargo.log");
            filesToWatch.Add("remoteexec.log");
            filesToWatch.Add("setpos.log");
            filesToWatch.Add("setvariable.log");
            filesToWatch.Add("deletevehicle.log");
            filesToWatch.Add("createvehicle.log");
            filesToWatch.Add("publicvariable.log");
            filesToWatch.Add("attachto.log");
            filesToWatch.Add("waypointstatements.log");
            filesToWatch.Add("..\\arma2oaserver.RPT");
            filesToWatch.Add("scripts.log");
            
            this.Run();
        }

        private void Run()
        {
            var timerDebugLog = new System.Timers.Timer();
            timerDebugLog.AutoReset = true;
            timerDebugLog.Interval = 1000;
            timerDebugLog.Elapsed += TimerDebugLog_Elapsed;
            timerDebugLog.Start();

            var timerOutputLog = new System.Timers.Timer();
            timerOutputLog.AutoReset = true;
            timerOutputLog.Interval = 1000;
            timerOutputLog.Elapsed += TimerOutputLog_Elapsed;
            timerOutputLog.Start();

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

        private void TimerDebugLog_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            string s;
            while(debugLogQueue.TryDequeue(out s))
            {
                updateDebugText(s);
            }
        }

        private void TimerOutputLog_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            string s;
            while(outputLogQueue.TryDequeue(out s))
            {
                updateOutputText(s);
            }
        }

        delegate void updateDebugTextCallback(string s);

        private void updateDebugText(string s)
        {
            if (this.rtbDebug.InvokeRequired)
            {
                updateDebugTextCallback d = new updateDebugTextCallback(updateDebugText);
                this.Invoke(d, new object[] { s });
            }
            else
            {
                rtbDebug.Text += s;
                rtbDebug.Text += "\n";
                rtbDebug.SelectionStart = rtbDebug.Text.Length;
                rtbDebug.ScrollToCaret();
            }
        }

        delegate void updateOutputTextCallback(string s);

        private void updateOutputText(string s)
        {
            if (this.rtbOutput.InvokeRequired)
            {
                updateOutputTextCallback d = new updateOutputTextCallback(updateOutputText);
                this.Invoke(d, new object[] { s });
            }
            else
            {
                rtbOutput.Text += s;
                rtbOutput.Text += "\n";
                rtbOutput.SelectionStart = rtbOutput.Text.Length;
                rtbOutput.ScrollToCaret();
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

        public void logDebug(String s)
        {
            debugLogQueue.Enqueue(addDateString(s));
        }

        public void logOutput(String s)
        {
            outputLogQueue.Enqueue(addDateString(s));
        }

        private String getDateString()
        {
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        private String addDateString(String s)
        {
            return getDateString() + " " + s;
        }

        private void btnRCON_Click(object sender, EventArgs e)
        {
            MessageBox.Show("not yet implemented");
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
            threadLogDebug("starting for file " + _fileName);

            while (!_shouldStop)
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
                            threadLogOutput(line);
                        }
                        lastSize = currentSize;
                        Thread.Sleep(5);
                    }
                    if (fs != null) { fs.Close(); }
                }
                catch (Exception ex)
                {
                    threadLogDebug("Error opening file: " + ex.Message);
                    _shouldStop = true;
                    //TODO: have main form re-check?
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
            _parentForm.logDebug("Thread " + System.Threading.Thread.CurrentThread.ManagedThreadId + " " + s);
        }

        private void threadLogOutput(String s)
        {
            _parentForm.logOutput(_fileName + ": " + s);
        }

        public void RequestStop()
        {
            _shouldStop = true;
        }
    }
}
