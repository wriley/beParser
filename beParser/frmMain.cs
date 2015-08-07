﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Linq;
using System.Xml.Linq;
using System.Xml.Serialization;
using System.Text;

namespace beParser
{
    public partial class frmMain : Form
    {
        // private
        private bool started = false;
        private List<Producer> producerObjects = new List<Producer>();
        private List<Consumer> consumerObjects = new List<Consumer>();
        private List<Thread> workerThreads = new List<Thread>();
        private ConcurrentQueue<string> debugLogQueue = new ConcurrentQueue<string>();
        private ConcurrentQueue<string> outputLogQueue = new ConcurrentQueue<string>();
        private ConcurrentQueue<string> rconLogQueue = new ConcurrentQueue<string>();

        // public
        public bool rewindOn = false;
        public string basePath;
        public Dictionary<string, string> playerToGuid = new Dictionary<string, string>();
        public Dictionary<string, string> uidToPlayer = new Dictionary<string, string>();
        public Dictionary<int, string> slotToIP = new Dictionary<int, string>();
        public Dictionary<string, int> playerToSlot = new Dictionary<string, int>();
        public List<string> alreadyBannedGuids = new List<string>();
        public Dictionary<string, ConcurrentQueue<string>> lineQueues = new Dictionary<string, ConcurrentQueue<string>>();
        public Dictionary<string, List<FileCheck>> fileChecks = new Dictionary<string, List<FileCheck>>();
        public Dictionary<string, int> ruleCounts = new Dictionary<string, int>();
        public Dictionary<string, string> bufRE = new Dictionary<string, string>();

        #region Classes/Structs

        [Serializable]
        public class FileCheckData
        {
            public string FileName;
            public List<FileCheck> FileChecks;

            public FileCheckData(string fileName, List<FileCheck> fileChecks)
            {
                this.FileName = fileName;
                this.FileChecks = fileChecks;
            }

            private FileCheckData()
            {

            }
        }

        public struct FileCheck
        {
            public int count;
            public int seconds;
            public string regex_match;
            public string regex_nomatch;
            public string command;

            public FileCheck(int count, int seconds, string regex_match = null, string regex_nomatch = null, string command = null)
            {
                this.count = count;
                this.seconds = seconds;
                this.regex_match = regex_match;
                this.regex_nomatch = regex_nomatch;
                this.command = command;
            }
        }

        public struct ruleCount
        {
            public string rule;
            public int count;

            public ruleCount(string rule, int count = 0)
            {
                this.rule = rule;
                this.count = count;
            }
        }

        #endregion

        public frmMain()
        {
            InitializeComponent();
        }

        private void fmrMain_Load(object sender, EventArgs e)
        {
            if (Properties.Settings.Default.basePath == "")
            {
                MessageBox.Show("You need to set the path to your BattlEye folder");
                frmOptions frmOptions = new frmOptions(this);
                frmOptions.Show(this);
            }
            LoadSettings();
            Run();
        }

        #region Main program flow functions

        private bool Start()
        {
            if (basePath.Length == 0 || !Directory.Exists(basePath))
            {
                MessageBox.Show("You need to set the correct path to your BattlEye folder\r\nFile->Options");
                return false;
            }
            // clear things
            fileChecks.Clear();
            lineQueues.Clear();
            producerObjects.Clear();
            consumerObjects.Clear();
            workerThreads.Clear();
            ruleCounts.Clear();

            LogDebug("Loading fileChecks.xml");
            // Load files to monitor from fileChecks.xml
            if (LoadFileChecks())
            {

                // check for rewind option (read files from beginning)
                rewindOn = cbRewindOn.Checked;

                // create and start producer threads
                foreach (String file in fileChecks.Keys)
                {
                    ConcurrentQueue<string> lineQueue = new ConcurrentQueue<string>();
                    lineQueues.Add(file, lineQueue);
                    string fullPath = basePath;
                    if (file == "server_console")
                    {
                        fullPath += "//..//" + file + ".log";
                    }
                    else if (file == "arma2oaserver")
                    {
                        fullPath += "//..//" + file + ".RPT";
                    }
                    else
                    {
                        fullPath += "//" + file + ".log";
                    }
                    Producer w = new Producer(this, fullPath, file);
                    producerObjects.Add(w);
                    Thread t = new Thread(w.DoWork);
                    workerThreads.Add(t);
                    t.IsBackground = true;
                    t.Start();
                }

                LogDebug("Starting producer and consumer threads");
                // create and start consumer threads
                foreach (var lineQueue in lineQueues)
                {
                    ConcurrentQueue<string> lq = lineQueues[lineQueue.Key];
                    Consumer c = new Consumer(this, lineQueue.Key);
                    consumerObjects.Add(c);
                    Thread t = new Thread(c.DoWork);
                    workerThreads.Add(t);
                    t.IsBackground = true;
                    t.Start();
                }

                return true;
            }
            else
            {
                return false;
            }
        }

        private bool Stop()
        {
            LogDebug("Stopping producer and consumer threads");

            // Check if any worker threads are running
            int threadsRunning = 0;
            foreach (Thread t in workerThreads)
            {
                if (!t.Join(0))
                {
                    threadsRunning++;
                }
            }

            // if worker threads are running tell them to stop and then wait for them
            if (threadsRunning > 0)
            {
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
                            return;
                        }
                        else
                        {
                            timer.Start();
                        }
                    };
                timer.Start();
            }
            lineQueues.Clear();
            return true;
        }

        private void Run()
        {
            // create and start UI threads
            Thread tDebug = new Thread(DoDebugLog);
            tDebug.IsBackground = true;
            tDebug.Start();

            Thread tOutput = new Thread(DoOutputLog);
            tOutput.IsBackground = true;
            tOutput.Start();

            Thread tRcon = new Thread(DoRconLog);
            tRcon.IsBackground = true;
            tRcon.Start();

            Thread tLinesQueued = new Thread(DoLinesQueued);
            tLinesQueued.IsBackground = true;
            tLinesQueued.Start();
        }

        private bool LoadFileChecks()
        {
            try
            {
                StreamReader sr = new StreamReader("fileChecks.xml");
                DeserializeFileCheckData(sr.ReadToEnd());
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading fileChecks.xml: " + ex.Message);
                return false;
            }
        }

        private string SerializeFileCheckData()
        {
            List<FileCheckData> tempList = new List<FileCheckData>(fileChecks.Count);
            foreach (string key in fileChecks.Keys)
            {
                tempList.Add(new FileCheckData(key, fileChecks[key]));
            }
            XmlSerializer serializer = new XmlSerializer(typeof(List<FileCheckData>));
            StringWriter sw = new StringWriter();
            XmlSerializerNamespaces ns = new XmlSerializerNamespaces();
            ns.Add("", "");
            serializer.Serialize(sw, tempList, ns);

            return sw.ToString();
        }

        private void DeserializeFileCheckData(string RawData)
        {
            fileChecks.Clear();
            fileChecks = new Dictionary<string, List<FileCheck>>();
            XmlSerializer xs = new XmlSerializer(typeof(List<FileCheckData>));
            StringReader sr = new StringReader(RawData);
            List<FileCheckData> tempList = (List<FileCheckData>)xs.Deserialize(sr);
            foreach (FileCheckData fcd in tempList)
            {
                fileChecks.Add(fcd.FileName, fcd.FileChecks);
            }
        }

        #endregion

        #region Delegates
        delegate void UpdateDebugTextCallback(string s);
        private void UpdateDebugText(string s)
        {
            try
            {
                if (this.rtbDebug.InvokeRequired)
                {

                    UpdateDebugTextCallback d = new UpdateDebugTextCallback(UpdateDebugText);

                    Invoke(d, new object[] { s });

                }
                else
                {
                    rtbDebug.Text += s + Environment.NewLine;
                    rtbDebug.SelectionStart = rtbDebug.Text.Length;
                    rtbDebug.ScrollToCaret();
                }
            }
            catch (Exception ex)
            {
                LogDebug(MethodBase.GetCurrentMethod().Name + " " + ex.Message);
            }
        }

        delegate void UpdateOutputTextCallback(string s);
        private void UpdateOutputText(string s)
        {
            try
            {
                if (this.rtbOutput.InvokeRequired)
                {
                    UpdateOutputTextCallback d = new UpdateOutputTextCallback(UpdateOutputText);

                    Invoke(d, new object[] { s });

                }
                else
                {
                    rtbOutput.Text += s + Environment.NewLine;
                    rtbOutput.SelectionStart = rtbOutput.Text.Length;
                    rtbOutput.ScrollToCaret();
                }
            }
            catch (Exception ex)
            {
                LogDebug(MethodBase.GetCurrentMethod().Name + " " + ex.Message);
            }
        }

        delegate void UpdateRconTextCallback(string s);
        private void UpdateRconText(string s)
        {
            try
            {
                if (this.rtbRcon.InvokeRequired)
                {
                    UpdateRconTextCallback d = new UpdateRconTextCallback(UpdateRconText);

                    Invoke(d, new object[] { s });

                }
                else
                {
                    rtbRcon.Text += s + Environment.NewLine;
                    rtbRcon.SelectionStart = rtbOutput.Text.Length;
                    rtbRcon.ScrollToCaret();
                }
            }
            catch (Exception ex)
            {
                LogRcon(MethodBase.GetCurrentMethod().Name + " " + ex.Message);
            }
        }

        delegate void UpdateLinesQueuedCallback(int n);
        private void UpdateLinesQueued(int n)
        {
            try
            {
                if (this.lblLinesQueued.InvokeRequired)
                {
                    UpdateLinesQueuedCallback d = new UpdateLinesQueuedCallback(UpdateLinesQueued);

                    Invoke(d, new object[] { n });

                }
                else
                {
                    lblLinesQueued.Text = n.ToString();
                }
            }
            catch (Exception ex)
            {
                LogRcon(MethodBase.GetCurrentMethod().Name + " " + ex.Message);
            }
        }
        #endregion

        #region overrides

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            btnStartStop.Enabled = false;
            LogDebug("Stopping producer and consumer threads");

            // Check if any worker threads are running
            int threadsRunning = 0;
            foreach (Thread t in workerThreads)
            {
                if (!t.Join(0))
                {
                    threadsRunning++;
                }
            }

            // if worker threads are running tell them to stop and then wait for them
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

        #endregion

        #region Logging

        internal void LogDebug(String s)
        {
            debugLogQueue.Enqueue(AddDateString(s));
        }

        internal void LogOutput(String s)
        {
            outputLogQueue.Enqueue(s);
        }

        internal void LogRcon(String s)
        {
            rconLogQueue.Enqueue(s);
        }

        private void DoDebugLog()
        {
            string s;
            for (;;)
            {
                if (debugLogQueue.TryDequeue(out s))
                {
                    UpdateDebugText(s);
                }
                else
                {
                    Thread.Sleep(500);
                }
            }
        }

        private void DoOutputLog()
        {
            string s;
            for (;;)
            {
                if (outputLogQueue.TryDequeue(out s))
                {
                    UpdateOutputText(s);
                }
                else
                {
                    Thread.Sleep(500);
                }
            }
        }

        private void DoRconLog()
        {
            string s;
            for (;;)
            {
                if (rconLogQueue.TryDequeue(out s))
                {
                    UpdateRconText(s);
                }
                else
                {
                    Thread.Sleep(500);
                }
            }
        }

        private void DoLinesQueued()
        {
            int linesCount;

            for (;;)
            {
                linesCount = 0;
                foreach (var lq in lineQueues)
                {
                    linesCount += lineQueues[lq.Key].Count;
                }
                UpdateLinesQueued(linesCount);
                //LogDebug("linesQueued: " + linesCount);
                Thread.Sleep(200);
            }
        }

        #endregion

        #region UI event functions

        private void btnStartStop_Click(object sender, EventArgs e)
        {
            btnStartStop.Enabled = false;

            if (started)
            {
                if (Stop())
                {
                    started = false;
                    btnStartStop.Text = "Start";
                    cbRewindOn.Enabled = true;
                }
            }
            else
            {
                if (Start())
                {
                    started = true;
                    btnStartStop.Text = "Stop";
                    cbRewindOn.Enabled = false;
                }
            }

            btnStartStop.Enabled = true;
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            frmAboutBox frmAboutBox = new frmAboutBox();
            frmAboutBox.ShowDialog(this);
        }

        private void optionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (started)
            {
                MessageBox.Show("Please stop before changing options");
            }
            else
            {
                frmOptions frmOptions = new frmOptions(this);
                frmOptions.Show(this);
            }
        }

        private void cbRewindOn_CheckedChanged(object sender, EventArgs e)
        {
            rewindOn = cbRewindOn.Checked;
            SaveSettings();
        }

        #endregion

        #region Internal helper functions

        internal String GetDateString()
        {
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        internal String AddDateString(String s)
        {
            return GetDateString() + " " + s;
        }

        internal bool GetLineQueueLine(string fileName, out string line)
        {
            bool res = false;
            try
            {
                res = lineQueues[fileName].TryDequeue(out line);
                return res;
            }
            catch (Exception ex)
            {
                LogDebug(MethodBase.GetCurrentMethod().Name + " " + ex.Message);
                line = "";
                return false;
            }

        }

        internal void AddLineQueueLine(string fileName, string s)
        {
            try
            {
                lineQueues[fileName].Enqueue(s);
            }
            catch (Exception ex)
            {
                LogDebug(MethodBase.GetCurrentMethod().Name + "() " + ex.Message);
            }
        }

        internal List<FileCheck> GetFileChecks(string fileName)
        {
            try
            {
                return fileChecks[fileName];
            }
            catch (Exception ex)
            {
                LogDebug(MethodBase.GetCurrentMethod().Name + "() " + ex.Message);
                return null;
            }
        }

        internal void PlayerToGuidAdd(string player, string guid)
        {
            if (!playerToGuid.ContainsKey(player))
            {
                playerToGuid.Add(player, guid);
            }
        }

        internal string PlayerToGuidGet(string player)
        {
            if (playerToGuid.ContainsKey(player))
            {
                return playerToGuid[player];
            }
            else
            {
                return null;
            }
        }

        internal void UidToPlayerAdd(string uid, string player)
        {
            if (!uidToPlayer.ContainsKey(uid))
            {
                uidToPlayer.Add(uid, player);
            }
        }

        internal string UidToPlayerGet(string uid)
        {
            if (uidToPlayer.ContainsKey(uid))
            {
                return uidToPlayer[uid];
            }
            else
            {
                return null;
            }
        }

        internal void SlotToIPAdd(int slot, string ip)
        {
            if (!slotToIP.ContainsKey(slot))
            {
                slotToIP.Add(slot, ip);
            }
        }

        internal string SlotToIPGet(int slot)
        {
            if (slotToIP.ContainsKey(slot))
            {
                return slotToIP[slot];
            }
            else
            {
                return null;
            }
        }

        internal void PlayerToSlotAdd(string player, int slot)
        {
            if (!playerToSlot.ContainsKey(player))
            {
                playerToSlot.Add(player, slot);
            }
        }

        internal int PlayerToSlotGet(string player)
        {
            if (playerToSlot.ContainsKey(player))
            {
                return playerToSlot[player];
            }
            else
            {
                return -1;
            }
        }

        internal bool IsNewPlayer(string player, string guid)
        {
            return !playerToGuid.ContainsKey(player);
        }

        internal void UpdateRuleCount(string key, int count = 1)
        {
            if (!ruleCounts.ContainsKey(key))
            {
                ruleCounts.Add(key, 1);
            }
            else
            {
                ruleCounts[key] += count;
            }
        }

        internal int GetRuleCount(string key)
        {
            if (ruleCounts.ContainsKey(key))
            {
                return ruleCounts[key];
            }
            else
            {
                return 0;
            }
        }

        internal void Ban(string guid = null, string ip = null, string player = null, int slot = -1, string date = null, string rule = null, string action = null)
        {
            /*
               0 - guid
               1 - ip
               2 - player
               3 - date
               4 - rule
           */

            // TODO: make rcon connection and commands available like rcon.pl

            String[] cmdArgs = new String[] { guid, ip, player, date, rule };

            if (guid != null || slot != 999 || player != null)
            {
                if (action == null && guid != null && !alreadyBannedGuids.Contains(guid))
                {
                    alreadyBannedGuids.Add(guid);
                    string cmd = String.Format("kickbyguid {0};!sleep 1;addban {0} 0 BP-{4} {2} {3};!sleep 1;addban {1} 0 BP-{4} {2} {3}", cmdArgs);
                    LogRcon(cmd);

                    LogOutput(String.Format("{0} Trigger for GUID:{1} NAME:\"{2}\" SOURCE:{3} command={4}", date, guid, player, rule, cmd));
                }
                else if (action != null)
                {
                    string cmd = String.Format(action, cmdArgs);
                    LogRcon(cmd);

                    LogOutput(String.Format("{0} Trigger for GUID:{1} NAME:\"{2}\" SOURCE:{3} command={4}", date, guid, player, rule, cmd));
                }
                else
                {
                    LogOutput(String.Format("{0} No trigger for GUID:{1} NAME:\"{2}\" SOURCE:{3} already banned", date, guid, player, rule));
                }
            }
        }

        internal void SaveSettings()
        {
            Properties.Settings.Default.basePath = basePath;
            Properties.Settings.Default.rewindOn = rewindOn;
            Properties.Settings.Default.Save();

            LoadSettings();
        }

        internal void LoadSettings()
        {
            basePath = Properties.Settings.Default.basePath;
            rewindOn = Properties.Settings.Default.rewindOn;
            cbRewindOn.Checked = rewindOn;
        }

        #endregion

    }

    #region Thread classes

    public class GenericWorkerThread
    {
        public volatile bool _shouldStop;
        public frmMain _parentForm;

        public void ThreadLogDebug(String s)
        {
            _parentForm.LogDebug("Thread " + System.Threading.Thread.CurrentThread.ManagedThreadId + " " + s);
        }

        public void RequestStop()
        {
            _shouldStop = true;
        }

        public void SpinAndWait(int ms)
        {
            // sleep but check periodically if we should quit
            if (ms >= 10)
            {
                for (int i = 0; i < ms && !_shouldStop; i += 10)
                {
                    Thread.Sleep(10);
                }
            }
            else
            {
                Thread.Sleep(1);
            }
        }
    }

    #region Producer thread

    public class Producer : GenericWorkerThread
    {
        private String _filePath;
        private String _fileName;
        private FileStream _fs;
        private StreamReader _sr;
        private bool _isMultiline = false;
        private StringBuilder _stringBuilder;
        private Regex _lineStartRegex;
        private Match _lineStartMatch;
        private FileSystemWatcher _watcher;
        bool _fileRotated = false;

        public Producer(frmMain parentForm, String filePath, string fileName)
        {
            //threadLogDebug("Producer filePath " + filePath);
            this._parentForm = parentForm;
            this._filePath = filePath;
            this._fileName = fileName;
            if (_fileName == "scripts" || _fileName == "remoteexec" || _fileName == "mpeventhandler" || _fileName == "remotecontrol")
            {
                this._isMultiline = true;
                this._stringBuilder = new StringBuilder();
                // 03.08.2015 18:33:13:
                this._lineStartRegex = new Regex(@"[0-9]{2}\.[0-9]{2}\.[0-9]{4} [0-9]{2}:[0-9]{2}:[0-9]{2}: ");
            }
        }

        private void OnChanged(object source, FileSystemEventArgs e)
        {
            _fileRotated = true;
        }

        public void DoWork()
        {
            //ThreadLogDebug("Producer starting for file " + _fileName);

            _watcher = new FileSystemWatcher();
            _watcher.Path = Path.GetDirectoryName(_filePath);
            _watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size;
            _watcher.Filter = Path.GetFileName(_filePath);
            _watcher.Deleted += new FileSystemEventHandler(OnChanged);
            _watcher.EnableRaisingEvents = true;

            Thread.Sleep(1000);

            while (!_shouldStop)
            {
                try
                {
                    _fs = File.Open(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    _sr = new StreamReader(_fs);
                    Int64 fileSize = GetFileSize();
                    if (!_parentForm.rewindOn)
                    {
                        _sr.BaseStream.Seek(fileSize, SeekOrigin.Begin);
                    }
                    string line;
                    ThreadLogDebug(Path.GetFileName(_filePath) + " is now open");
                    while (!_shouldStop && !_fileRotated)
                    {
                        if ((line = _sr.ReadLine()) != null)
                        {
                            // files with output on multiple lines require special handling
                            // combine all lines here
                            if (_isMultiline)
                            {
                                _lineStartMatch = _lineStartRegex.Match(line);
                                // start of a line (date time string)
                                if (_lineStartMatch.Success)
                                {
                                    if (_stringBuilder.Length > 0)
                                    {
                                        // there's a previous line so add that
                                        _parentForm.AddLineQueueLine(_fileName, _stringBuilder.ToString());
                                    }
                                    // start over
                                    _stringBuilder.Clear();
                                }
                                // append the line and replace newlines/tabs with space
                                _stringBuilder.Append(line);
                                _stringBuilder.Replace('\n', ' ');
                                _stringBuilder.Replace('\t', ' ');
                                _stringBuilder.Replace('\r', ' ');
                            }
                            else
                            {
                                // not multiline so just add it
                                _parentForm.AddLineQueueLine(_fileName, line);
                            }

                            //ThreadLogDebug(line);
                        }
                        else
                        {
                            // couldn't read a line, wait patiently
                            SpinAndWait(1000);
                        }
                    }
                    if(_fileRotated)
                    {
                        ThreadLogDebug(Path.GetFileName(_filePath) + " was rotated");
                        if (_sr != null) { _sr.Close(); }
                        if (_fs != null) { _fs.Close(); }
                        _fileRotated = false;
                    }
                }
                catch (Exception)
                {
                    // Unable to open file so wait and try again
                    SpinAndWait(5000);
                }
                finally
                {
                    if (_sr != null) { _sr.Close(); }
                    if (_fs != null) { _fs.Close(); }
                }
            }
            //ThreadLogDebug("Producer exiting");
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

    #endregion

    #region Consumer thread

    public class Consumer : GenericWorkerThread
    {
        private string _fileName;
        private Regex _regex1;
        private Regex _regex2;
        private Match _match1;
        private Match _match2;
        private List<frmMain.FileCheck> _fileChecks;
        private Int64 linesRead;
        private string guid;
        private string uid;
        private string player;
        private string ip;
        private string date;
        private int slot;
        private string rule;
        private Int32 unixtime = 0;
        private string evt;
        private Regex server_console1 = new Regex(@"\d+:\d+:\d+ (?:BattlEye Server: Player #(?<slot>\d+) (?<player>.*?) (?:- GUID: (?<guid>[a-f0-9]{32}) \(unverified\)|\((?<ip>[0-9.]+?):\d+\) connected)|Player (?<player>.*?) (?:kicked off by BattlEye: (?<evt>Admin Ban)|connected \(id=(?<uid>[0-9AX]+)\)\.|(?<evt>disconnected)\.|kicked off by BattlEye: (?<evt>Global Ban) #[a-f0-9]+)|Player #(?<slot>\d+) (?<player>.*?) \([a-f0-9]{32}\) has been kicked by BattlEye: (?<evt>Invalid GUID)|BattlEye Server: \((?<evt>.*?)\) (?<player>.*?) *: (?<msg>.*)) *$", RegexOptions.Compiled | RegexOptions.Singleline);
        private Regex publicvariable1 = new Regex(@"([0-9]+\.[0-9]+\.[0-9]+ [0-9]+:[0-9]+:[0-9]+): (.*) \(([0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}):[0-9]+\) ([0-9a-z]{32}) - ", RegexOptions.Compiled | RegexOptions.Singleline);
        private Regex otherfiles1 = new Regex(@"(?<date>(?<D>\d+)\.(?<M>\d+)\.(?<Y>\d+) (?<h>\d+):(?<m>\d+):(?<s>\d+)): (?<player>.*?) ?\((?<ip>[0-9.]{7,15}):[0-9]{1,5}\) (?<guid>(?:-|[a-f0-9]{32})) - ", RegexOptions.Compiled | RegexOptions.Singleline);

        public Consumer(frmMain parentForm, string fileName)
        {
            this._parentForm = parentForm;
            this._fileName = fileName;
            this._fileChecks = _parentForm.GetFileChecks(fileName);
        }

        public void DoWork()
        {
            //ThreadLogDebug("Consumer starting for file " + _fileName);
            string line;
            bool res;

            while (!_shouldStop)
            {
                res = _parentForm.GetLineQueueLine(_fileName, out line);
                if (res)
                {
                    linesRead++;
                    //ThreadLogDebug("line " + linesRead + " " + line);

                    // Special checks
                    if (_fileName == "server_console")
                    {
                        _match1 = server_console1.Match(line);

                        if (_match1.Success)
                        {
                            Group guidExists = _match1.Groups["guid"];
                            Group uidExists = _match1.Groups["uid"];
                            Group ipExists = _match1.Groups["ip"];
                            Group evtExists = _match1.Groups["evt"];

                            if (guidExists.Success)
                            {
                                guid = guidExists.Value;
                                player = _match1.Groups["player"].Value;
                                slot = Convert.ToInt16(_match1.Groups["slot"].Value);
                                _parentForm.PlayerToGuidAdd(_match1.Groups["player"].Value, guid);
                                _parentForm.PlayerToSlotAdd(_match1.Groups["player"].Value, slot);
                                ThreadLogOutput(_parentForm.GetDateString() + " Player/GUID,Slot '" + player + "'=>" + guid + "," + slot);
                            }
                            else if (uidExists.Success)
                            {
                                player = _match1.Groups["player"].Value;
                                uid = uidExists.Value;
                                _parentForm.UidToPlayerAdd(uid, _match1.Groups["player"].Value);
                                ThreadLogOutput(_parentForm.GetDateString() + " UID/Player " + uid + "=>" + player);
                            }
                            else if (ipExists.Success)
                            {
                                slot = Convert.ToInt16(_match1.Groups["slot"].Value);
                                ip = _match1.Groups["ip"].Value;
                                _parentForm.SlotToIPAdd(slot, ip);
                            }
                            else if (evtExists.Success)
                            {
                                evt = _match1.Groups["evt"].Value;
                                switch (evt)
                                {
                                    case "disconnected":
                                        player = _match1.Groups["player"].Value;
                                        slot = _parentForm.PlayerToSlotGet(player);
                                        _parentForm.playerToSlot.Remove(player);
                                        if (slot > 0) { _parentForm.slotToIP.Remove(slot); }
                                        ThreadLogOutput(_parentForm.GetDateString() + " Player disconnected '" + player + "', deleting slot reference");
                                        break;
                                    case "Global Ban":
                                        slot = _parentForm.PlayerToSlotGet(player);
                                        if (slot == -1) { slot = 999; }
                                        ip = _parentForm.SlotToIPGet(slot);
                                        if (ip == null) { ip = "127.0.0.1"; }
                                        _parentForm.Ban(null, ip, null, -1, null, null, null);
                                        break;
                                    case "Invalid GUID":
                                        if (_match1.Groups["slot"].Value != null)
                                        {
                                            slot = Convert.ToInt16(_match1.Groups["slot"].Value);
                                        }
                                        else
                                        {
                                            slot = 999;
                                        }
                                        ip = _parentForm.SlotToIPGet(slot);
                                        if (ip == null) { ip = "127.0.0.1"; }
                                        _parentForm.Ban(null, ip, null, -1, null, null, null);
                                        break;
                                    case "Admin Ban":
                                        player = _match1.Groups["player"].Value;
                                        guid = _parentForm.PlayerToGuidGet(player);
                                        ThreadLogOutput(_parentForm.GetDateString() + " Ban with no reason " + guid + ", please unban");
                                        break;
                                    default:
                                        break;
                                }
                            }
                        }
                        else
                        {
                            slot = -1;
                            player = null;
                            ip = null;
                        }
                    }
                    else if (_fileName == "publicvariable")
                    {
                        _match1 = publicvariable1.Match(line);
                        if (_match1.Success)
                        {
                            // date
                            try
                            {
                                unixtime = (Int32)(Convert.ToDateTime(_match1.Groups[1].Value).Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
                            }
                            catch (Exception)
                            {
                                unixtime = (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
                            }

                            // player ip guid
                            player = _match1.Groups[2].Value;
                            ip = _match1.Groups[3].Value;
                            guid = _match1.Groups[4].Value;

                            if (_parentForm.IsNewPlayer(player, guid))
                            {
                                ThreadLogOutput(_match1.Groups[1].Value + " GUID seen '" + player + "'=>" + guid);
                                _parentForm.PlayerToGuidAdd(player, guid);
                            }
                        }
                        else
                        {
                            player = null;
                            ip = null;
                            guid = null;
                        }
                    }
                    else // other logs
                    {
                        _match1 = otherfiles1.Match(line);
                        if (_match1.Success)
                        {
                            Group guidExists = _match1.Groups["guid"];
                            Group playerExists = _match1.Groups["player"];
                            Group dateExists = _match1.Groups["date"];
                            Group ipExists = _match1.Groups["ip"];

                            if (guidExists.Success)
                            {
                                guid = guidExists.Value;
                            }

                            if (playerExists.Success)
                            {
                                player = playerExists.Value;
                            }

                            if (dateExists.Success)
                            {
                                date = _match1.Groups["date"].Value;
                            }
                            else
                            {
                                date = _parentForm.GetDateString();
                            }

                            if (ipExists.Success)
                            {
                                ip = ipExists.Value;
                            }
                            else
                            {
                                ip = "127.0.0.1";
                            }

                            if (guidExists.Success && playerExists.Success)
                            {
                                if (_parentForm.IsNewPlayer(player, guid))
                                {
                                    _parentForm.PlayerToGuidAdd(player, guid);
                                    ThreadLogOutput(date + " GUID seen '" + player + "'=>" + guid);
                                }

                            }
                        }
                    }

                    // regex file checks

                    for (int i = 0; i < _fileChecks.Count; i++)
                    {
                        rule = _fileName + "-" + (i + 1);


                        if (_fileChecks[i].regex_match != null)
                        {
                            _regex1 = new Regex(_fileChecks[i].regex_match, RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
                            _match1 = _regex1.Match(line);
                        }
                        else
                        {
                            _regex1 = null;
                            _match1 = null;
                        }

                        if (_fileChecks[i].regex_nomatch != null)
                        {
                            _regex2 = new Regex(_fileChecks[i].regex_nomatch, RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
                            _match2 = _regex2.Match(line);
                        }
                        else
                        {
                            _regex2 = null;
                            _match2 = null;
                        }

                        if (
                            ((_fileChecks[i].regex_match != null) && _match1.Success) && ((_fileChecks[i].regex_nomatch != null) && !_match2.Success) ||
                            ((_fileChecks[i].regex_match != null) && _match1.Success) && (_fileChecks[i].regex_nomatch == null) ||
                            ((_fileChecks[i].regex_match == null) && ((_fileChecks[i].regex_nomatch != null) && !_match2.Success))
                            )
                        {

                            if ((guid != null) && (guid != ""))
                            {
                                // if timestamp not detected in file then set to now
                                if (unixtime == 0)
                                {
                                    unixtime = (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
                                }

                                if (_fileChecks[i].seconds == 0)
                                {
                                    _parentForm.Ban(guid, ip, player, -1, date, rule, _fileChecks[i].command);

                                    // Remote Exec hack follow up
                                    if (_fileChecks[i].count == -1)
                                    {
                                        if (_fileName == "scripts")
                                        {
                                            if (_parentForm.bufRE.Count > 0 && player == _parentForm.bufRE["player"])
                                            {
                                                List<string> others = _parentForm.bufRE["others"].Split(',').ToList<string>();
                                                others.Remove(player);
                                                string[] cheaters = others.ToArray();

                                                if (cheaters.Length == 0)
                                                {
                                                    _parentForm.bufRE.Clear();
                                                }
                                                else
                                                {
                                                    _parentForm.bufRE["others"] = string.Join(",", cheaters);
                                                }
                                            }
                                        }
                                        else if (_fileName == "arma2oaserver")
                                        {
                                            Group tagExists = _match1.Groups["tag"];
                                            Group othersExists = _match1.Groups["others"];
                                            Group playerExists = _match1.Groups["player"];
                                            if (tagExists.Success && othersExists.Success && playerExists.Success)
                                            {
                                                _parentForm.bufRE.Add("tag", tagExists.Value);
                                                _parentForm.bufRE.Add("others", othersExists.Value);
                                                _parentForm.bufRE.Add("player", playerExists.Value);


                                                _parentForm.bufRE.Add("t", unixtime.ToString());
                                            }
                                        }
                                        else
                                        {
                                            // PV
                                            ThreadLogDebug("PV HACK...");
                                            string key = rule + guid + Convert.ToString(Convert.ToDecimal((unixtime / _fileChecks[i].seconds)));
                                            if (!_parentForm.ruleCounts.ContainsKey(key))
                                            {
                                                _parentForm.Ban(guid, ip, player, slot, date, rule, _fileChecks[i].command);
                                                _parentForm.UpdateRuleCount(key, 1);
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    string key = rule + guid + Convert.ToString(Convert.ToDecimal((unixtime / _fileChecks[i].seconds)));

                                    _parentForm.UpdateRuleCount(key);
                                    int currentCount = _parentForm.GetRuleCount(key);

                                    ThreadLogOutput(currentCount + "/" + _fileChecks[i].count + ":" + _fileName + ":" + line);
                                    if (currentCount == _fileChecks[i].count)
                                    {
                                        _parentForm.Ban(guid, ip, player, slot, date, rule, _fileChecks[i].command);
                                        _parentForm.ruleCounts.Remove(key);
                                    }
                                }
                            }
                        }
                    }
                    if (_parentForm.bufRE.Count > 0)
                    {
                        // RE
                        int delay = 0;
                        try
                        {
                            delay = Convert.ToInt16(_parentForm.bufRE["t"]);
                        }
                        catch
                        {
                            delay = 0;
                        }

                        string[] cheaters = _parentForm.bufRE["others"].Split(',');
                        if (delay > 120 && cheaters.Length > 0 && cheaters.Length < 4)
                        {
                            ThreadLogDebug(_parentForm.GetDateString() + " RE-HACK: Cheaters List: '" + string.Join(",", cheaters) + "', delay after Unit spawn:" + delay);
                            Random random = new Random();
                            string player = cheaters[random.Next(0, cheaters.Length)];
                            string guid = _parentForm.PlayerToGuidGet(player);
                            int slot = _parentForm.PlayerToSlotGet(player);
                            if (guid != null)
                            {
                                ThreadLogDebug(_parentForm.GetDateString() + " RE-HACK: Banning " + player + " (" + guid + ")");
                                if (slot >= 0)
                                {
                                    ThreadLogRcon("kickbyguid " + guid + " Ping too high " + random.Next(500, 800));
                                }
                                ThreadLogRcon("kickbyguid " + guid + " Game restart required");
                            }
                            _parentForm.bufRE.Clear();
                        }
                    }

                }
                else
                {
                    //threadLogDebug("No lines to read now, read " + linesRead + ", sleeping");
                    SpinAndWait(1000);
                }
            }
            //ThreadLogDebug("Consumer exiting");
        }

        public void ThreadLogOutput(String s)
        {
            _parentForm.LogOutput("Thread " + System.Threading.Thread.CurrentThread.ManagedThreadId + " " + s);
        }

        public void ThreadLogRcon(String s)
        {
            _parentForm.LogRcon(s);
        }
    }

    #endregion
    #endregion
}
