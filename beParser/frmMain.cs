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
using System.Reflection;

namespace beParser
{
    public partial class frmMain : Form
    {
        //private
        private List<Producer> producerObjects = new List<Producer>();
        private List<Consumer> consumerObjects = new List<Consumer>();
        private List<Thread> workerThreads = new List<Thread>();

        private ConcurrentQueue<string> debugLogQueue = new ConcurrentQueue<string>();
        private ConcurrentQueue<string> outputLogQueue = new ConcurrentQueue<string>();
        private ConcurrentQueue<string> rconLogQueue = new ConcurrentQueue<string>();

        private string basePath = "C:\\arma2oa\\dayz_2\\BattlEye";
        //string basePath = "testlogs\\BattlEye";

        // public
        public Dictionary<string, string> playerToGuid = new Dictionary<string, string>();
        public Dictionary<string, string> uidToPlayer = new Dictionary<string, string>();
        public Dictionary<int, string> slotToIP = new Dictionary<int, string>();
        public Dictionary<string, int> playerToSlot = new Dictionary<string, int>();
        public List<string> alreadyBannedGuids = new List<string>();
        public Dictionary<string, ConcurrentQueue<string>> lineQueues = new Dictionary<string, ConcurrentQueue<string>>();
        public Dictionary<string, List<fileCheck>> fileChecks = new Dictionary<string, List<fileCheck>>();
        public Dictionary<string, int> ruleCounts = new Dictionary<string, int>();

        public struct fileCheck
        {
            public int count;
            public int seconds;
            public string regex_match;
            public string regex_nomatch;
            public string command;

            public fileCheck(int count, int seconds, string regex_match = null, string regex_nomatch = null, string command = null)
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

        public frmMain()
        {
            InitializeComponent();
        }

        private void fmrMain_Load(object sender, EventArgs e)
        {
            // files to monitor
            // TODO: move this to config file
            fileChecks.Add("server_console", new List<fileCheck>());

            fileChecks.Add("publicvariable", new List<fileCheck>());
            fileChecks["publicvariable"].Add(new fileCheck(1, 0, "rmovein\"", null, "kickbyguid {0} BP-{1} {2} looting too fast {3}"));
            fileChecks["publicvariable"].Add(new fileCheck(1, 0, "\"remExField\" = .*?(?:usecEpi|setDamage|markerType|setVehicleInit|%|usecMorphine|r_player_blood|BIS_Effects_Burn|fnc_usec_damage|bowen|preproces)"));
            fileChecks["publicvariable"].Add(new fileCheck(1, 0, "(?:dayzJizz|dwarden)"));
            fileChecks["publicvariable"].Add(new fileCheck(1, 0, "83,99,114,105,112,116"));
            fileChecks["publicvariable"].Add(new fileCheck(-1, 10000, "wrong side", @"""PVDZ_sec_atp"" = \[""wrong side"",<NULL-object>\]"));
            fileChecks["publicvariable"].Add(new fileCheck(1, 0, "Plants texture hack", null, "kickbyguid $guid;!sleep 1;addban $guid 2880 Plant texture hack for $player $date, 2 day ban"));
            fileChecks["publicvariable"].Add(new fileCheck(75, 300, "time shift", null, "kickbyguid $guid You are time shifting/lagging, fix your internet."));

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

            Thread tRcon = new Thread(doRconLog);
            tRcon.IsBackground = true;
            tRcon.Start();

            // create and start producer threads
            foreach (String file in fileChecks.Keys)
            {
                ConcurrentQueue<string> lineQueue = new ConcurrentQueue<string>();
                lineQueues.Add(file, lineQueue);
                string fullPath = basePath;
                if (file == "server_console")
                {
                    fullPath += "\\..\\" + file + ".log";
                }
                else if (file == "arma2oaserver")
                {
                    fullPath += "\\..\\" + file + ".RPT";
                }
                else
                {
                    fullPath += "\\" + file + ".log";
                }
                Producer w = new Producer(this, fullPath, file);
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
                Consumer c = new Consumer(this, lineQueue.Key);
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
                if (debugLogQueue.TryDequeue(out s))
                {
                    updateDebugText(s);
                }
                else
                {
                    Thread.Sleep(100);
                }
            }
        }

        private void doOutputLog()
        {
            string s;
            for (;;)
            {
                if (outputLogQueue.TryDequeue(out s))
                {
                    updateOutputText(s);
                }
                else
                {
                    Thread.Sleep(100);
                }
            }
        }

        private void doRconLog()
        {
            string s;
            for (;;)
            {
                if (rconLogQueue.TryDequeue(out s))
                {
                    updateRconText(s);
                }
                else
                {
                    Thread.Sleep(100);
                }
            }
        }

        delegate void updateDebugTextCallback(string s);
        private void updateDebugText(string s)
        {
            if (this.rtbDebug.InvokeRequired)
            {
                updateDebugTextCallback d = new updateDebugTextCallback(updateDebugText);
                try
                {
                    Invoke(d, new object[] { s });
                }
                catch (Exception ex)
                {
                    logDebug(MethodBase.GetCurrentMethod().Name + " " + ex.Message);
                }
            }
            else
            {
                rtbDebug.Text += s + Environment.NewLine;
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
                try
                {
                    Invoke(d, new object[] { s });
                }
                catch (Exception ex)
                {
                    logDebug(MethodBase.GetCurrentMethod().Name + " " + ex.Message);
                }
            }
            else
            {
                rtbOutput.Text += s + Environment.NewLine;
                rtbOutput.SelectionStart = rtbOutput.Text.Length;
                rtbOutput.ScrollToCaret();
            }
        }

        delegate void updateRconTextCallback(string s);
        private void updateRconText(string s)
        {
            if (this.rtbRcon.InvokeRequired)
            {
                updateRconTextCallback d = new updateRconTextCallback(updateRconText);
                try
                {
                    Invoke(d, new object[] { s });
                }
                catch (Exception ex)
                {
                    logRcon(MethodBase.GetCurrentMethod().Name + " " + ex.Message);
                }
            }
            else
            {
                rtbRcon.Text += s + Environment.NewLine;
                rtbRcon.SelectionStart = rtbOutput.Text.Length;
                rtbRcon.ScrollToCaret();
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

        internal void logDebug(String s)
        {
            debugLogQueue.Enqueue(addDateString(s));
        }

        internal void logOutput(String s)
        {
            outputLogQueue.Enqueue(s);
        }

        internal void logRcon(String s)
        {
            rconLogQueue.Enqueue(s);
        }

        internal String getDateString()
        {
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        internal String addDateString(String s)
        {
            return getDateString() + " " + s;
        }

        private void btnRCON_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Not yet implemented");
        }

        internal bool getLineQueueLine(string fileName, out string line)
        {
            bool res = false;
            try
            {
                res = lineQueues[fileName].TryDequeue(out line);
                return res;
            }
            catch (Exception ex)
            {
                logDebug(MethodBase.GetCurrentMethod().Name + " " + ex.Message);
                line = "";
                return false;
            }

        }

        internal void addLineQueueLine(string fileName, string s)
        {
            try
            {
                lineQueues[fileName].Enqueue(s);
            }
            catch (Exception ex)
            {
                logDebug(MethodBase.GetCurrentMethod().Name + "() " + ex.Message);
            }
        }

        internal List<fileCheck> getFileChecks(string fileName)
        {
            try
            {
                return fileChecks[fileName];
            }
            catch (Exception ex)
            {
                logDebug(MethodBase.GetCurrentMethod().Name + "() " + ex.Message);
                return null;
            }
        }

        internal void playerToGuidAdd(string player, string guid)
        {
            if (!playerToGuid.ContainsKey(player))
            {
                playerToGuid.Add(player, guid);
            }
        }

        internal string playerToGuidGet(string player)
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

        internal void uidToPlayerAdd(string uid, string player)
        {
            if (!uidToPlayer.ContainsKey(uid))
            {
                uidToPlayer.Add(uid, player);
            }
        }

        internal string uidToPlayerGet(string uid)
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

        internal void slotToIPAdd(int slot, string ip)
        {
            if (!slotToIP.ContainsKey(slot))
            {
                slotToIP.Add(slot, ip);
            }
        }

        internal string slotToIPGet(int slot)
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

        internal void playerToSlotAdd(string player, int slot)
        {
            if (!playerToSlot.ContainsKey(player))
            {
                playerToSlot.Add(player, slot);
            }
        }

        internal int playerToSlotGet(string player)
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

        internal bool newPlayer(string player, string guid)
        {
            return !playerToGuid.ContainsKey(player);
        }

        internal void updateRuleCount(string key)
        {
            if (!ruleCounts.ContainsKey(key))
            {
                ruleCounts.Add(key, 1);
            }
            else
            {
                ruleCounts[key]++;
            }
        }

        internal int getRuleCount(string key)
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

    public class Producer : GenericWorkerThread
    {
        private String _filePath;
        private String _fileName;
        private FileStream _fs;
        private StreamReader _sr;
        private Int64 _linesRead = 0;
        private bool _fileRotated = false;

        public Producer(frmMain parentForm, String filePath, string fileName)
        {
            //threadLogDebug("Producer filePath " + filePath);
            this._parentForm = parentForm;
            this._filePath = filePath;
            this._fileName = fileName;
        }

        public void DoWork()
        {
            threadLogDebug("Producer starting for file " + _fileName);

            while (!_shouldStop)
            {
                try
                {
                    _fs = File.Open(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    _sr = new StreamReader(_fs);
                    Int64 lastSize = GetFileSize();
                    //sr.BaseStream.Seek(lastSize, SeekOrigin.Begin);
                    string line;
                    Int64 currentSize;
                    while (!_shouldStop && !_fileRotated)
                    {
                        currentSize = GetFileSize();
                        if (currentSize < lastSize)
                        {
                            threadLogDebug("File size reduced, assuming log was rotated");
                            _fileRotated = true;
                            _sr.Close();
                            _fs.Close();
                        }
                        else
                        {
                            if ((line = _sr.ReadLine()) != null)
                            {
                                _linesRead++;
                                if ((_linesRead % 100) == 0)
                                {
                                    threadLogDebug(_fileName + " " + _linesRead + " lines read");
                                }
                                _parentForm.addLineQueueLine(_fileName, line);
                                //threadLogOutput(line);
                            }
                            else
                            {
                                SpinAndWait(1000);
                            }
                            lastSize = currentSize;
                        }
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
        private string _fileName;
        private Regex _regex1;
        private Regex _regex2;
        private Match _match1;
        private Match _match2;
        private List<frmMain.fileCheck> _fileChecks;
        private Int64 linesRead;
        private string guid;
        private string player;
        private string ip;
        private int slot;
        private string rule;
        private Int32 unixtime = 0;

        public Consumer(frmMain parentForm, string fileName)
        {
            this._parentForm = parentForm;
            this._fileName = fileName;
            this._fileChecks = _parentForm.getFileChecks(fileName);
        }

        public void DoWork()
        {
            threadLogDebug("Consumer starting for file " + _fileName);
            string line;
            bool res;

            while (!_shouldStop)
            {
                res = _parentForm.getLineQueueLine(_fileName, out line);
                if (res)
                {
                    linesRead++;

                    // Special checks
                    if (_fileName == "server_console")
                    {
                        Regex server_console1 = new Regex(@"BattlEye Server: Player #([0-9]+) (.*) \(([0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}):[0-9]+\) connected");
                        Regex server_console2 = new Regex(@"BattlEye Server: Verified GUID \(([0-9a-z]+)\) of player #([0-9]+) (.*)");
                        _match1 = server_console1.Match(line);
                        _match2 = server_console2.Match(line);

                        if (_match1.Success)
                        {
                            // slot player ip
                            try
                            {
                                slot = Convert.ToInt16(_match1.Groups[1].Value);
                            }
                            catch (Exception)
                            {
                                slot = -1;
                            }
                            player = _match1.Groups[2].Value;
                            ip = _match1.Groups[3].Value;

                            _parentForm.slotToIPAdd(slot, ip);
                            _parentForm.playerToSlotAdd(player, slot);
                        }
                        else
                        {
                            slot = -1;
                            player = null;
                            ip = null;
                        }

                        if (_match2.Success)
                        {
                            // guid slot player
                            guid = _match2.Groups[1].Value;
                            try
                            {
                                slot = Convert.ToInt16(_match2.Groups[2].Value);
                            }
                            catch (Exception)
                            {
                                slot = -1;
                            }
                            player = _match2.Groups[3].Value;

                            if (_parentForm.newPlayer(player, guid))
                            {
                                threadLogOutput("GUID seen '" + player + "'=>" + guid);
                            }

                            _parentForm.playerToGuidAdd(player, guid);
                            _parentForm.playerToSlotAdd(player, slot);
                        }
                    }

                    if (_fileName == "publicvariable")
                    {
                        // 03.08.2015 19:35:22: Nightmare (187.233.88.182:23204) b4e065d95d7ceb35128b2c9f5b71194a - #9 "PVDZ_plr_LoginRecord" = ["76561198075755817","3221",0]
                        Regex publicvariable1 = new Regex(@"([0-9]+\.[0-9]+\.[0-9]+ [0-9]+:[0-9]+:[0-9]+): (.*) \(([0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}):[0-9]+\) ([0-9a-z]{32}) - ");
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

                            _parentForm.playerToGuidAdd(player, guid);
                        }
                        else
                        {
                            player = null;
                            ip = null;
                            guid = null;
                        }
                    }

                    for (int i = 0; i < _fileChecks.Count; i++)
                    {
                        frmMain.fileCheck fileCheck = _fileChecks[i];
                        rule = "BP-" + _fileName + "-" + i;

                        if (fileCheck.regex_match != null)
                        {
                            _regex1 = new Regex(fileCheck.regex_match);
                            _match1 = _regex1.Match(line);
                        }

                        if (fileCheck.regex_nomatch != null)
                        {
                            _regex2 = new Regex(fileCheck.regex_nomatch);
                            _match2 = _regex2.Match(line);
                        }

                        // def, def
                        if (
                            ((fileCheck.regex_match != null) && _match1.Success) && ((fileCheck.regex_nomatch != null) && !_match2.Success) ||
                            ((fileCheck.regex_match != null) && _match1.Success) && (fileCheck.regex_nomatch == null) ||
                            ((fileCheck.regex_match == null) && ((fileCheck.regex_nomatch != null) && !_match2.Success))
                            )
                        {
                            if ((guid != null) && (guid != ""))
                            {
                                if(unixtime == 0)
                                {
                                    unixtime = (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
                                }
                                string key = rule + guid + Convert.ToString(Convert.ToDecimal((unixtime / fileCheck.seconds)));

                                _parentForm.updateRuleCount(key);
                                int currentCount = _parentForm.getRuleCount(key);

                                threadLogOutput("key:" + key + " " + currentCount + "/" + fileCheck.count + ":" + _fileName + ":" + line);
                                if (currentCount == fileCheck.count)
                                {
                                    threadLogRcon("addBan " + guid + " -1 " + rule + " " + _parentForm.getDateString());
                                    _parentForm.ruleCounts.Remove(key);
                                }
                                else
                                {
                                }
                            }

                        }
                        // undef, undef
                        else
                        {
                        }
                    }

                }
                else
                {
                    //threadLogDebug("No lines to read now, read " + linesRead + ", sleeping");
                    SpinAndWait(1000);
                }
            }

            threadLogDebug("Consumer exiting");
        }

        public void threadLogOutput(String s)
        {
            _parentForm.logOutput(s);
        }

        public void threadLogRcon(String s)
        {
            _parentForm.logRcon(s);
        }
    }
}
