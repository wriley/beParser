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
using System.Text.RegularExpressions;

namespace beParser
{
    public partial class frmMain : Form
    {
        List<String> filesToWatch = new List<String>();
        List<Worker> workerObjects = new List<Worker>();
        List<Thread> workerThreads = new List<Thread>();
        string basePath = "C:\\arma2oa\\dayz_2\\BattlEye";
        //string basePath = "testlogs\\BattlEye";
        ConcurrentQueue<string> debugLogQueue = new ConcurrentQueue<string>();
        ConcurrentQueue<string> outputLogQueue = new ConcurrentQueue<string>();

        public frmMain()
        {
            InitializeComponent();
        }

        private void fmrMain_Load(object sender, EventArgs e)
        {
            // files to monitor
            // TODO: move this to config file
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

            Run();
        }

        private void Run()
        {
            Thread tDebug = new Thread(doDebugLog);
            tDebug.IsBackground = true;
            tDebug.Start();

            Thread tOutput = new Thread(doOutputLog);
            tOutput.IsBackground = true;
            tOutput.Start();

            // create and start worker threads
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

        private void doDebugLog()
        {
            string s;
            for (;;)
            {
                while (debugLogQueue.TryDequeue(out s))
                {
                    updateDebugText(s);
                }
            }
        }

        private void doOutputLog()
        {
            string s;
            for (;;)
            {
                while (outputLogQueue.TryDequeue(out s))
                {
                    updateOutputText(s);
                }
            }
        }

        delegate void updateDebugTextCallback(string s);
        private void updateDebugText(string s)
        {
            if (this.rtbDebug.InvokeRequired)
            {
                updateDebugTextCallback d = new updateDebugTextCallback(updateDebugText);
                Invoke(d, new object[] { s });
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
                Invoke(d, new object[] { s });
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
            // Check if any worker threads are running
            int threadsRunning = 0;
            foreach(Thread t in workerThreads)
            {
                if(!t.Join(0))
                {
                    threadsRunning++;
                }
            }

            // if worker threads are running tell them to stop and wait
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
            outputLogQueue.Enqueue(s);
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
            MessageBox.Show("Not yet implemented");
        }

    }

    public class Worker
    {
        private volatile bool _shouldStop;
        private frmMain _parentForm;
        private String _filePath;
        private String _fileName;
        private FileStream _fs;
        private StreamReader _sr;
        private Int64 _linesRead = 0;
        private Regex _regex = new Regex(@"Verified GUID \(([0-9a-z]+)\) of player #[0-9]+ (.*)");
        private Match match;

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
                    _fs = File.Open(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    _sr = new StreamReader(_fs);
                    Int64 lastSize = GetFileSize();
                    //sr.BaseStream.Seek(lastSize, SeekOrigin.Begin);
                    string line;
                    Int64 currentSize;
                    while (!_shouldStop)
                    {
                        currentSize = GetFileSize();
                        if (currentSize < lastSize)
                        {
                            threadLogDebug("File size reduced, starting at beginning of file");
                            _sr.DiscardBufferedData();
                            _sr.BaseStream.Seek(0, SeekOrigin.Begin);
                            _sr.BaseStream.Position = 0;
                        }
                        if((line = _sr.ReadLine()) != null)
                        {
                            _linesRead++;
                            if((_linesRead % 100) == 0)
                            {
                                threadLogDebug(_fileName + " " + _linesRead + " lines read");
                            }
                            if(_fileName == "server_console.log")
                            {
                                match = _regex.Match(line);
                                if(match.Success)
                                {
                                    threadLogOutput("GUID " + match.Groups[1].Value + " " + match.Groups[2].Value);
                                }
                            }
                            //threadLogOutput(line);
                        }
                        lastSize = currentSize;
                        //Thread.Sleep(1);
                    }
                }
                catch (Exception ex)
                {
                    threadLogDebug("Error opening file: " + ex.Message + " will retry in 5 seconds");
                    SpinAndWait(5000);
                }
                finally
                {
                    if (_sr != null) { _sr.Close(); }
                    if (_fs != null) { _fs.Close(); }
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
                if (fs != null) { fs.Close(); }
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

        private void SpinAndWait(int ms)
        {
            for (int i = 0; i < ms; i+= 10)
            {
                while (!_shouldStop)
                {
                    Thread.Sleep(10);
                }
            }
        }
    }
}
