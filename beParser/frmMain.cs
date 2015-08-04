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
        List<Producer> producerObjects = new List<Producer>();
        List<Consumer> consumerObjects = new List<Consumer>();
        List<Thread> workerThreads = new List<Thread>();
        List<playerObject> playerObjects = new List<playerObject>();

        string basePath = "C:\\arma2oa\\dayz_2\\BattlEye";
        //string basePath = "testlogs\\BattlEye";

        ConcurrentQueue<string> debugLogQueue = new ConcurrentQueue<string>();
        ConcurrentQueue<string> outputLogQueue = new ConcurrentQueue<string>();

        Dictionary<string, ConcurrentQueue<string>> lineQueues = new Dictionary<string, ConcurrentQueue<string>>();
        Dictionary<string, Regex[]> fileRegexes = new Dictionary<string, Regex[]>();
        Dictionary<string, List<fileCheck>> fileChecks = new Dictionary<string, List<fileCheck>>();

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

        public struct playerObject
        {
            public string guid;
            public string name;
            public string ip;
            public int slot;
            public string uid;

            public playerObject(string guid, string name = null, string ip = null, int slot = -1, string uid = null)
            {
                this.guid = guid;
                this.name = name;
                this.ip = ip;
                this.slot = slot;
                this.uid = uid;
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
            logDebug("DEBUG");

            Thread tOutput = new Thread(doOutputLog);
            tOutput.IsBackground = true;
            tOutput.Start();
            logOutput("OUTPUT");

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

        public String getDateString()
        {
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        public String addDateString(String s)
        {
            return getDateString() + " " + s;
        }

        private void btnRCON_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Not yet implemented");
        }

        public bool getLineQueueLine(string fileName, out string line)
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

        public void addLineQueueLine(string fileName, string s)
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

        public List<fileCheck> getFileChecks(string fileName)
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
        private string _fileName;
        private Regex _regex1;
        private Regex _regex2;
        private Match _match1;
        private Match _match2;
        private Match _match3;
        private List<frmMain.fileCheck> _fileChecks;
        private Int64 linesRead;
        private string guid;
        private string uid;
        private string player;
        private string ip;
        private int slot;

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
                        /*
                        16:26:41 BattlEye Server: Player #0 ZBuffet (174.26.147.224:23204) connected
                        16:26:41 Player ZBuffet connecting.
                        16:26:41 Mission DayZMod read from bank.
                        16:26:42 BattlEye Server: Player #0 ZBuffet - GUID: 0f09332d84ea4d1cd6bcd7332ae81d24 (unverified)
                        16:26:42 Player ZBuffet connected (id=76561198054374215).
                        16:26:43 BattlEye Server: Verified GUID (0f09332d84ea4d1cd6bcd7332ae81d24) of player #0 ZBuffet
                        16:26:43 BattlEye Server: Player #0 ZBuffet - Legacy GUID: e9d3eee74198565932422a8c8a666aef
                        */
                        Regex server_console1 = new Regex(@"BattlEye Server: Player #([0-9]+) (.*) \(([0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}):[0-9]+\) connected");
                        Regex server_console2 = new Regex(@"BattlEye Server: Verified GUID \(([0-9a-z]+)\) of player #([0-9]+) (.*)");
                        Regex server_console3 = new Regex(@"Player (.*) connected \(id=([0-9]+)\)");
                        _match1 = server_console1.Match(line);
                        _match2 = server_console2.Match(line);
                        _match3 = server_console3.Match(line);

                        StringBuilder sb = new StringBuilder("sc ID: ");

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
                            sb.Append("Player #" + slot + " " + player + " " + ip + "");
                            threadLogOutput(sb.ToString());
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
                            sb.Append("Player #" + slot + " " + player + " (" + guid + ")");
                            threadLogOutput(sb.ToString());
                        }

                        if (_match3.Success)
                        {
                            // player uid
                            player = _match3.Groups[1].Value;
                            uid = _match3.Groups[2].Value;
                            sb.Append(player + " (" + uid + ")");
                            threadLogOutput(sb.ToString());
                        }
                    }

                    if(_fileName == "publicvariable")
                    {
                        // 03.08.2015 19:35:22: Nightmare (187.233.88.182:23204) b4e065d95d7ceb35128b2c9f5b71194a - #9 "PVDZ_plr_LoginRecord" = ["76561198075755817","3221",0]
                        Regex publicvariable1 = new Regex(@": (.*) \(([0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}):[0-9]+\) ([0-9a-z]+) - #([0-9]+) ""PVDZ_plr_LoginRecord"" = \[""([0-9]+)"",");
                        _match1 = publicvariable1.Match(line);
                        if(_match1.Success)
                        {
                            StringBuilder sb = new StringBuilder("pv ID: ");
                            // player ip guid slot uid
                            player = _match1.Groups[1].Value;
                            ip = _match1.Groups[2].Value;
                            guid = _match1.Groups[3].Value;
                            try
                            {
                                slot = Convert.ToInt16(_match1.Groups[4].Value);
                            }
                            catch (Exception)
                            {
                                slot = -1;
                            }
                            sb.Append("Player #" + slot + " " + player + " " + guid + " " + ip);
                            threadLogOutput(sb.ToString());
                        }
                    }

                    for (int i = 0; i < _fileChecks.Count; i++)
                    {
                        frmMain.fileCheck fileCheck = _fileChecks[i];

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

                        StringBuilder sb = new StringBuilder("BP-" + _fileName + "-" + i + " ");
                        // def, def
                        if (((fileCheck.regex_match != null) && _match1.Success) && ((fileCheck.regex_nomatch != null) && !_match2.Success))
                        {
                            sb.Append(_match1.Value);
                            threadLogOutput(sb.ToString());
                        }

                        // def, undef
                        if (((fileCheck.regex_match != null) && _match1.Success) && (fileCheck.regex_nomatch == null))
                        {
                            sb.Append(_match1.Value);
                            threadLogOutput(sb.ToString());
                        }
                        // undef, def
                        if ((fileCheck.regex_match == null) && ((fileCheck.regex_nomatch != null) && !_match2.Success))
                        {
                            sb.Append(_match2.Value);
                            threadLogOutput(sb.ToString());
                        }
                        // undef, undef
                        if ((fileCheck.regex_match == null) && (fileCheck.regex_nomatch == null))
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
    }
}
