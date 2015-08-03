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
        List<Producer> producerObjects = new List<Producer>();
        List<Consumer> consumerObjects = new List<Consumer>();
        List<Thread> workerThreads = new List<Thread>();
        string basePath = "C:\\arma2oa\\dayz_2\\BattlEye";
        //string basePath = "testlogs\\BattlEye";
        ConcurrentQueue<string> debugLogQueue = new ConcurrentQueue<string>();
        ConcurrentQueue<string> outputLogQueue = new ConcurrentQueue<string>();
        Dictionary<string, ConcurrentQueue<string>> lineQueues = new Dictionary<string, ConcurrentQueue<string>>();
        Dictionary<string, Regex[]> fileRegexes = new Dictionary<string, Regex[]>();

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

            // regexes
            // TODO: move this to config file
            // 12:54:01 BattlEye Server: Player #1 ZBuffet (174.26.147.224:23204) connected
            // 12:54:03 BattlEye Server: Verified GUID (0f09332d84ea4d1cd6bcd7332ae81d24) of player #1 ZBuffet

            Regex[] server_console = new Regex[2];
            server_console[0] = new Regex(@"BattlEye Server: Player #[0-9]+ (.*) \(([0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}):[0-9]+\) connected");
            server_console[1] = new Regex(@"BattlEye Server: Verified GUID \(([0-9a-z]+)\) of player #[0-9]+ (.*)");
            fileRegexes.Add("server_console.log", server_console);

            Run();
        }

        private void Run()
        {
            // create and start UI threads
            Thread tDebug = new Thread(doDebugLog);
            tDebug.IsBackground = true;
            tDebug.Start();

            Thread tOutput = new Thread(doOutputLog);
            tOutput.IsBackground = true;
            tOutput.Start();

            Thread tQueues = new Thread(viewQueues);
            tQueues.IsBackground = true;
            tQueues.Start();

            // create and start producer threads
            foreach (String file in filesToWatch)
            {
                string fileName = Path.GetFileName(file);
                ConcurrentQueue<string> lineQueue = new ConcurrentQueue<string>();
                lineQueues.Add(fileName, lineQueue);
                Producer w = new Producer(this, basePath + "\\" + file, ref lineQueue);
                producerObjects.Add(w);
                Thread t = new Thread(w.DoWork);
                workerThreads.Add(t);
                t.IsBackground = true;
                t.Start();
            }

            // create and start consumer threads
            foreach (var lineQueue in lineQueues)
            {
                ConcurrentQueue<string> lq = lineQueues[lineQueue.Key];
                Consumer c = new Consumer(this, lineQueue.Key, ref lq, fileRegexes[lineQueue.Key]);
                consumerObjects.Add(c);
                Thread t = new Thread(c.DoWork);
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

        private void viewQueues()
        {
            for (;;)
            {
                var keys = new List<string>(lineQueues.Keys);
                foreach (string key in keys)
                {
                    if (!lineQueues[key].IsEmpty)
                    {
                        logDebug("Queue " + key + " has " + lineQueues[key].Count + " entries");
                    }
                }
                Thread.Sleep(2000);
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
            foreach (Thread t in workerThreads)
            {
                if (!t.Join(0))
                {
                    threadsRunning++;
                }
            }

            // if worker threads are running tell them to stop and wait
            if (threadsRunning > 0)
            {
                e.Cancel = true;
                foreach (Producer w in producerObjects)
                {
                    w.RequestStop();
                }
                foreach (Consumer c in consumerObjects)
                {
                    c.RequestStop();
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
                        if (threadsRunning == 0)
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

    public class GenericWorkerThread
    {
        public volatile bool _shouldStop;
        public frmMain _parentForm;

        public void threadLogDebug(String s)
        {
            _parentForm.logDebug("Thread " + System.Threading.Thread.CurrentThread.ManagedThreadId + " " + s);
        }

        public void RequestStop()
        {
            _shouldStop = true;
        }

        public void SpinAndWait(int ms)
        {
            for (int i = 0; i < ms; i += 10)
            {
                while (!_shouldStop)
                {
                    Thread.Sleep(10);
                }
            }
        }
    }

    public class Producer : GenericWorkerThread
    {
        private String _filePath;
        private String _fileName;
        private FileStream _fs;
        private StreamReader _sr;
        private Int64 _linesRead = 0;
        private ConcurrentQueue<string> _lineQueue;

        public Producer(frmMain parentForm, String filePath, ref ConcurrentQueue<string> lineQueue)
        {
            this._parentForm = parentForm;
            this._filePath = filePath;
            this._fileName = Path.GetFileName(_filePath);
            this._lineQueue = lineQueue;
        }

        public void DoWork()
        {
            threadLogDebug("Producer starting for file " + _fileName);

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
                        if ((line = _sr.ReadLine()) != null)
                        {
                            _linesRead++;
                            if ((_linesRead % 100) == 0)
                            {
                                threadLogDebug(_fileName + " " + _linesRead + " lines read");
                            }
                            _lineQueue.Enqueue(line);
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
            threadLogDebug("Producer exiting");
        }

        public Int64 GetFileSize()
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

    }

    public class Consumer : GenericWorkerThread
    {
        private ConcurrentQueue<string> _lineQueue;
        private string _fileName;
        private Regex[] _regexes;
        private Match match;

        public Consumer(frmMain parentForm, string fileName, ref ConcurrentQueue<string> lineQueue, Regex[] regexes)
        {
            this._parentForm = parentForm;
            this._fileName = fileName;
            this._lineQueue = lineQueue;
            this._regexes = regexes;
        }

        public void DoWork()
        {
            threadLogDebug("Consumer starting for file " + _fileName);
            string line;

            while (!_shouldStop)
            {
                while (_lineQueue.TryDequeue(out line))
                {
                    foreach (var regex in _regexes)
                    {
                        match = regex.Match(line);
                        if (match.Success)
                        {
                            StringBuilder sb = new StringBuilder();
                            sb.Append("MATCH:");
                            for (int i = 1; i < match.Groups.Count; i++)
                            {
                                sb.Append(" " + match.Groups[i].Value);
                            }
                            threadLogOutput(sb.ToString());
                        }
                    }
                }
            }

            threadLogDebug("Consumer exiting");
        }

        public void threadLogOutput(String s)
        {
            _parentForm.logOutput(s);
        }
    }
}
