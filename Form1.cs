using IronPdf;
using SimpleLogger;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MergeAndPrint
{
    public partial class Form1 : Form
    {
        private string Printer1;
        private string Printer2;
        private string Printer3;
        private string Printer11;
        private string Printer22;
        private string Printer33;
        private string Printer9;
        private System.Windows.Forms.Timer timer1;
        private int printFailures;
        // The run found no files to print (e.g. an empty supplier folder). Not an error, the user is just told.
        private bool nothingToPrint;
        // True from the start of a run until its countdown ends or it fails. DoEvents lets clicks through
        // mid-run, so every entry point checks this before touching any folder.
        private bool _running;
        private Stopwatch runStopwatch = new Stopwatch();
        private int timerCounter = 30;
        private string timeVal;
        private static string srcMinVal;
        private string CurrentDirectory = null;
        private bool _loadingConfig;
        int pages = 0;
        private string ShlomoName;
        System.Drawing.Image printImg;
        public Form1()
        {
            InitializeComponent();


            ShlomoName = ConfigurationManager.AppSettings["shlomo"];
            if (string.IsNullOrWhiteSpace(ShlomoName))
            {
                ShlomoName = "Shlomo-Letters";
                SimpleLog.Warning("appSettings 'shlomo' is missing from the .dll.config; using " + ShlomoName);
            }
            // int.TryParse(ConfigurationManager.AppSettings["WaitSeconds"].ToString(), out timerCounter);
            var printers = GetAllPrinterList();
            cboPrinter1.Items.AddRange(printers.ToArray());
            cboPrinter2.Items.AddRange(printers.ToArray());
            cboPrinter3.Items.AddRange(printers.ToArray());
            cboPrinter11.Items.AddRange(printers.ToArray());
            cboPrinter22.Items.AddRange(printers.ToArray());
            cboPrinter33.Items.AddRange(printers.ToArray());
            cboPrinter9.Items.AddRange(printers.ToArray());

            LoadConfigToScreen();
            LogConfiguration();
            PrinterCheck.StartInBackground(printers,
                new[] { Printer1, Printer2, Printer3, Printer11, Printer22, Printer33, Printer9 });
            GhostscriptPrinter.LogQueueSettingsInBackground(
                new[] { Printer1, Printer2, Printer3, Printer11, Printer22, Printer33, Printer9 });

            updateScreen();
            updateDirectories();
        }
        /// <summary>
        /// טעינת ההגדרות מקובץ config.json אל מסך הקונפיגורציה.
        /// </summary>
        private void LoadConfigToScreen()
        {
            _loadingConfig = true;
            try
            {
                var cfg = AppConfig.Current;

                timeVal = string.IsNullOrEmpty(cfg.TimerPeriod) ? "30" : cfg.TimerPeriod;
                txtTimer.Text = timeVal;

                srcMinVal = string.IsNullOrEmpty(cfg.SrcMinPeriod) ? "5" : cfg.SrcMinPeriod;
                txtSrcMinutes.Text = srcMinVal;

                txtSource.Text = cfg.SourcePath;
                txtMain.Text = cfg.MainPath;
                txtWorkFol.Text = cfg.WorkingPath;
                txtPrint.Text = cfg.PrintPath;
                txtArchive.Text = cfg.ArchivePath;

                Printer1 = SelectPrinter(cboPrinter1, cfg.Printer1);
                Printer2 = SelectPrinter(cboPrinter2, cfg.Printer2);
                Printer3 = SelectPrinter(cboPrinter3, cfg.Printer3);
                Printer11 = SelectPrinter(cboPrinter11, cfg.Printer11);
                Printer22 = SelectPrinter(cboPrinter22, cfg.Printer22);
                Printer33 = SelectPrinter(cboPrinter33, cfg.Printer33);
                Printer9 = SelectPrinter(cboPrinter9, cfg.Printer9);
                SelectPrintEngine(cfg.PrintEngine);
            }
            finally
            {
                _loadingConfig = false;
            }
        }

        /// <summary>
        /// Write the active folders and printers to the log, so a log file shows what the app was set up to do.
        /// </summary>
        private void LogConfiguration()
        {
            SimpleLog.Info($"Config: source='{txtSource.Text}', main='{txtMain.Text}', working='{txtWorkFol.Text}', " +
                $"print='{txtPrint.Text}', archive='{txtArchive.Text}', timer={timeVal}s, srcMinutes={srcMinVal}, shlomo='{ShlomoName}'");
            SimpleLog.Info($"Printers: 1='{Printer1}', 2='{Printer2}', 3='{Printer3}', 1_999='{Printer11}', " +
                $"2_999='{Printer22}', 3_999='{Printer33}', 9='{Printer9}', engine='{AppConfig.Current.PrintEngine}'");
            SimpleLog.Info(GhostscriptPrinter.DescribeSetup());
        }

        /// <summary>
        /// בחירת המדפסת ברשימה לפי שמה בקובץ ההגדרות.
        /// אם המדפסת אינה מותקנת במחשב זה, הרשימה נשארת ריקה והשם שבקובץ נשמר כפי שהוא.
        /// </summary>
        private string SelectPrinter(ComboBox combo, string printerName)
        {
            if (string.IsNullOrEmpty(printerName))
            {
                combo.SelectedIndex = -1;
                return null;
            }

            var index = combo.FindStringExact(printerName);
            combo.SelectedIndex = index;
            if (index < 0)
                SimpleLogger.SimpleLog.Log("מדפסת מקובץ ההגדרות אינה מותקנת במחשב: " + printerName,
                    SimpleLogger.SimpleLog.Severity.Warning);

            return printerName;
        }

        /// <summary>
        /// Printer settings check (PrinterCapture): every configured queue gets a test document through its driver,
        /// written to a file instead of the printer, so nothing is printed. Runs off the UI thread; at the end the zip
        /// to send is shown in Explorer.
        /// </summary>
        private void btnPrinterCapture_Click(object sender, EventArgs e)
        {
            var printers = new[]
            {
                new KeyValuePair<string, string>("1", Printer1),
                new KeyValuePair<string, string>("2", Printer2),
                new KeyValuePair<string, string>("3", Printer3),
                new KeyValuePair<string, string>("1_999", Printer11),
                new KeyValuePair<string, string>("2_999", Printer22),
                new KeyValuePair<string, string>("3_999", Printer33),
                new KeyValuePair<string, string>("9", Printer9)
            };
            if (printers.All(p => string.IsNullOrWhiteSpace(p.Value)))
            {
                MessageBox.Show("לא הוגדרו מדפסות.", "בדיקת הגדרות מדפסות", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var originalText = btnPrinterCapture.Text;
            btnPrinterCapture.Enabled = false;
            SimpleLog.Info("Printer capture started (print to file, nothing is printed)");
            var worker = new Thread(() =>
            {
                string zip = null, error = null;
                try
                {
                    zip = PrinterCapture.Run(printers, step => BeginInvoke(new Action(() => btnPrinterCapture.Text = step)));
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    SimpleLog.Log(ex);
                }
                BeginInvoke(new Action(() =>
                {
                    btnPrinterCapture.Text = originalText;
                    btnPrinterCapture.Enabled = true;
                    if (zip == null)
                    {
                        MessageBox.Show("הבדיקה נכשלה: " + error, "בדיקת הגדרות מדפסות", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    MessageBox.Show("הבדיקה הסתיימה ולא הודפס דבר." + Environment.NewLine +
                        "יש לשלוח למפתח את הקובץ שיסומן עכשיו בחלון שייפתח:" + Environment.NewLine + zip,
                        "בדיקת הגדרות מדפסות", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    try { Process.Start("explorer.exe", "/select,\"" + zip + "\""); } catch { /* the path is in the message */ }
                }));
            })
            { IsBackground = true, Name = "PrinterCapture" };
            worker.SetApartmentState(ApartmentState.STA);
            worker.Start();
        }

        /// <summary>
        /// שמירת ההגדרות שבמסך לקובץ config.json.
        /// זו הפעולה היחידה שכותבת לקובץ - שינוי בשדות במסך אינו נשמר עד לחיצה על הכפתור.
        /// </summary>
        private void btnSaveConfig_Click(object sender, EventArgs e)
        {
            try
            {
                var cfg = AppConfig.Current;

                cfg.SourcePath = txtSource.Text;
                cfg.MainPath = txtMain.Text;
                cfg.WorkingPath = txtWorkFol.Text;
                cfg.PrintPath = txtPrint.Text;
                cfg.ArchivePath = txtArchive.Text;
                cfg.TimerPeriod = txtTimer.Text;
                cfg.SrcMinPeriod = txtSrcMinutes.Text;

                cfg.Printer1 = Printer1 ?? "";
                cfg.Printer2 = Printer2 ?? "";
                cfg.Printer3 = Printer3 ?? "";
                cfg.Printer11 = Printer11 ?? "";
                cfg.Printer22 = Printer22 ?? "";
                cfg.Printer33 = Printer33 ?? "";
                cfg.Printer9 = Printer9 ?? "";
                if (cboPrintEngine.SelectedItem is PrintEngineOption engine)
                    cfg.PrintEngine = engine.Value;

                cfg.Save();
                SimpleLog.Info($"Config saved: engine='{cfg.PrintEngine}'");
            }
            catch (Exception ex)
            {
                MessageBox.Show("שמירת ההגדרות נכשלה:" + Environment.NewLine + AppConfig.FilePath
                    + Environment.NewLine + ex.Message, "שגיאה בשמירת הגדרות",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            MessageBox.Show("ההגדרות נשמרו לקובץ:" + Environment.NewLine + AppConfig.FilePath);
            if (PrintEngines.Normalize(AppConfig.Current.PrintEngine) == PrintEngines.Custom && GhostscriptPrinter.ExecutablePath == null)
            {
                MessageBox.Show("מנוע ההדפסה המהיר (custom) דורש את Ghostscript, והוא לא נמצא בתיקיית התוכנה (gs\\bin)." + Environment.NewLine +
                    "עד שיותקן, קובצי PDF יודפסו עם Spire.", "Ghostscript לא נמצא", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>An entry in the print-engine dropdown: the value saved to config.json and the text shown.</summary>
        private sealed class PrintEngineOption
        {
            public PrintEngineOption(string value, string text) { Value = value; Text = text; }
            public string Value { get; }
            public string Text { get; }
            public override string ToString() => Text;
        }

        private static readonly PrintEngineOption[] PrintEngineOptions =
        {
            new PrintEngineOption(PrintEngines.Spire, "רגיל (Spire)"),
            new PrintEngineOption(PrintEngines.Custom, "מהיר (custom)"),
            new PrintEngineOption(PrintEngines.IronPdf, "ישן ואיטי (IronPdf)"),
        };

        /// <summary>Fill the print-engine dropdown (once) and select the configured engine; unknown values show as Spire.</summary>
        private void SelectPrintEngine(string configured)
        {
            if (cboPrintEngine.Items.Count == 0)
                cboPrintEngine.Items.AddRange(PrintEngineOptions);
            var normalized = PrintEngines.Normalize(configured);
            cboPrintEngine.SelectedItem = PrintEngineOptions.First(o => o.Value == normalized);
        }

        private void updateScreen()
        {
            lstFolder.Items.Clear();
            var col = updateDirectories();
            for (var i = 0; i < col.Count; i++)
            {
                col[i] = "תקיה " + col[i];
            }
            lstFolder.Items.AddRange(col.ToArray());
            chbox1.Items.Clear();
        }
        private void btnReset_Click(object sender, EventArgs e)
        {
            if (_running)
            {
                return;
            }
            // ההגדרות נטענות מחדש מהקובץ בכל רענון
            try
            {
                AppConfig.Reload();
            }
            catch (Exception ex)
            {
                MessageBox.Show("טעינת ההגדרות נכשלה. הרענון בוטל וההגדרות הנוכחיות נשמרו:" + Environment.NewLine
                    + AppConfig.FilePath + Environment.NewLine + ex.Message, "שגיאה בטעינת הגדרות",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            LoadConfigToScreen();

            // Refresh is what uses the minimum file age, so validate it here before moving anything.
            if (!TryParseSrcMinutes(srcMinVal, out _))
            {
                MessageBox.Show(SrcMinutesError);
                return;
            }

            SimpleLog.Info("Refresh started");
            LogConfiguration();
            var nums = new string[] { "1", "2", "3" };
            foreach (var num in nums)
            {
                var mpath = Path.Combine(txtMain.Text, num);
                MoveDir(mpath, num);

                var spath = Path.Combine(txtSource.Text, num);
                SourceToMain(spath, mpath);
            }
            SimpleLog.Info("Refresh finished");
            updateScreen();
            updateDirectories();
            Clean();
        }
        private void SourceToMain(string spath, string mpath)
        {
            if (!Directory.Exists(spath))
            {
                return;
            }
            try
            {
                var files = GetRecentFilesInFolder(spath);
                var dirs = Directory.GetDirectories(spath);
                if (!Directory.Exists(mpath))
                {
                    Directory.CreateDirectory(mpath);
                }
                foreach (var dir in dirs)
                {
                    var count = Directory.GetFiles(dir).Length;
                    if (count == 0)
                    {
                        continue;
                    }
                    var dird = Path.GetFileName(dir);
                    var dirDest = Path.Combine(mpath, dird);
                    if (!Directory.Exists(dirDest))
                    {
                        Directory.CreateDirectory(dirDest);
                    }
                }
                int moved = 0, skipped = 0;
                foreach (var file in files)
                {
                    var dest = getDestPathForFile(file, mpath);

                    if (!File.Exists(dest))
                    {
                        File.Move(file, dest);
                        moved++;
                        SimpleLog.Info($"Refresh: moved '{file}' -> '{dest}'");
                    }
                    else
                    {
                        skipped++;
                        SimpleLog.Warning($"Refresh: skipped '{file}', already exists at '{dest}'");
                    }
                }
                SimpleLog.Info($"Refresh '{spath}': {moved} moved, {skipped} skipped (already in main)");
            }
            catch (Exception ex)
            {
                SimpleLog.Error($"Refresh failed for '{spath}' -> '{mpath}'");
                SimpleLogger.SimpleLog.Log(ex);
            }

        }
        private string getDestPathForFile(string file, string mpath)
        {
            var dir = Path.GetDirectoryName(file);
            var dirDest = Path.Combine(mpath, Path.GetFileName(dir));
            var dest = Path.Combine(dirDest, Path.GetFileName(file));
            return dest;
        }
        private void MoveDir(string path, string num)
        {
            if (!Directory.Exists(path))
            {
                return;
            }
            try
            {
                var dirs = Directory.GetDirectories(path);
                foreach (var dir in dirs)
                {
                    if (Path.GetFileName(dir).StartsWith("999_"))
                    {
                        var p999 = Path.Combine(txtMain.Text, num.ToString() + "_999");
                        if (!Directory.Exists(p999))
                        {
                            Directory.CreateDirectory(p999);
                        }
                        var dest = Path.Combine(p999, Path.GetFileName(dir));
                        if (!Directory.Exists(dest))
                        {
                            Directory.CreateDirectory(dest);
                        }
                        foreach (var file in Directory.GetFiles(dir, "*.*"))
                        {
                            string targetFile = Path.Combine(dest, Path.GetFileName(file));
                            if (File.Exists(targetFile))
                            {
                                SimpleLog.Warning($"999 routing: replacing existing '{targetFile}'");
                                File.Delete(targetFile);
                            }
                            File.Move(file, targetFile);
                        }
                        Directory.Delete(dir, true);
                        SimpleLog.Info($"999 routing: moved supplier '{dir}' -> '{dest}'");
                    }
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.SimpleLog.Log(ex);
            }
        }
        private void lstFolder_SelectedIndexChanged(object sender, EventArgs e)
        {
            string num;
            chbox1.Items.Clear();
            var item = lstFolder.SelectedItem;
            // Clearing the list (updateScreen) raises this event with no selection.
            if (item == null)
            {
                CurrentDirectory = null;
                return;
            }
            if (item.ToString().Contains("תקיה " + "9"))
            {
                num = ShlomoName;
            }
            else if (item.ToString().Contains("תקיה " + "1_999"))
            {
                num = "1_999";
            }
            else if (item.ToString().Contains("תקיה " + "2_999"))
            {
                num = "2_999";
            }
            else if (item.ToString().Contains("תקיה " + "3_999"))
            {
                num = "3_999";
            }
            else if (item.ToString().Contains("תקיה " + "1"))
            {
                num = "1";
            }
            else if (item.ToString().Contains("תקיה " + "2"))
            {
                num = "2";
            }
            else
            {
                num = "3";
            }
            CurrentDirectory = num;
            var dirInfo = new DirectoryInfo(Path.Combine(txtMain.Text, num));
            var lDir = dirInfo.GetDirectories("*", SearchOption.TopDirectoryOnly).ToList();
            foreach (var dir in lDir)
            {
                // Store the folder name only: on .NET 8 DirectoryInfo.ToString() returns the full path,
                // which made Path.Combine(root, item) ignore root.
                chbox1.Items.Add(dir.Name);
            }
            groupBox1.Text = "תקיה " + num;
        }
        private List<string> updateDirectories()
        {
            var dirInfos = new List<string>();
            chbox1.Items.Clear();


            if (!string.IsNullOrEmpty(txtMain.Text))
            {
                var path1 = Path.Combine(txtMain.Text, "1");
                DirectoryInfo dirInfo;
                if (Directory.Exists(path1))
                {
                    int count = Directory.GetDirectories(path1, "*", SearchOption.TopDirectoryOnly)
                    .Where(subDir => Directory.GetFiles(subDir).Length > 0)
                    .Count();
                    lb1.Text = count + " ספקים ";
                    dirInfos.Add("1 - " + lb1.Text);

                }
                path1 = Path.Combine(txtMain.Text, "2");
                if (Directory.Exists(path1))
                {
                    int count = Directory.GetDirectories(path1, "*", SearchOption.TopDirectoryOnly)
                    .Where(subDir => Directory.GetFiles(subDir).Length > 0)
                    .Count();
                    lb2.Text = count + " ספקים ";
                    dirInfos.Add("2 - " + lb2.Text);

                }
                path1 = Path.Combine(txtMain.Text, "3");
                if (Directory.Exists(path1))
                {
                    dirInfo = new DirectoryInfo(path1);
                    //  var lDir = dirInfo.GetDirectories("*", SearchOption.TopDirectoryOnly).ToList();
                    int count = Directory.GetDirectories(path1, "*", SearchOption.TopDirectoryOnly)
                    .Where(subDir => Directory.GetFiles(subDir).Length > 0)
                    .Count();
                    lb3.Text = count + " ספקים ";
                    dirInfos.Add("3 - " + lb3.Text);

                }
                path1 = Path.Combine(txtMain.Text, "1_999");
                if (Directory.Exists(path1))
                {
                    int count = Directory.GetDirectories(path1, "*", SearchOption.TopDirectoryOnly)
                    .Where(subDir => Directory.GetFiles(subDir).Length > 0)
                    .Count();
                    lb11.Text = count + " ספקים ";
                    dirInfos.Add("1_999 - " + lb11.Text);

                }
                else
                {
                    lb11.Text = " 0 ספקים ";
                }
                path1 = Path.Combine(txtMain.Text, "2_999");
                if (Directory.Exists(path1))
                {
                    int count = Directory.GetDirectories(path1, "*", SearchOption.TopDirectoryOnly)
                     .Where(subDir => Directory.GetFiles(subDir).Length > 0)
                     .Count();
                    lb22.Text = count + " ספקים ";
                    dirInfos.Add("2_999 - " + lb22.Text);

                }
                else
                {
                    lb22.Text = " 0 ספקים ";
                }
                path1 = Path.Combine(txtMain.Text, "3_999");
                if (Directory.Exists(path1))
                {
                    int count = Directory.GetDirectories(path1, "*", SearchOption.TopDirectoryOnly)
                    .Where(subDir => Directory.GetFiles(subDir).Length > 0)
                    .Count();
                    lb33.Text = count + " ספקים ";
                    dirInfos.Add("3_999 - " + lb33.Text);

                }
                else
                {
                    lb33.Text = " 0 ספקים ";
                }
                path1 = Path.Combine(txtMain.Text, ShlomoName);
                if (Directory.Exists(path1))
                {
                    int count = Directory.GetDirectories(path1, "*", SearchOption.TopDirectoryOnly)
                    .Where(subDir => Directory.GetFiles(subDir).Length > 0)
                    .Count();
                    lbl9.Text = count + " ספקים ";
                    dirInfos.Add("9 - " + lbl9.Text);

                }
                else
                {
                    lbl9.Text = " 0 ספקים ";
                }
            }
            return dirInfos;
        }

        static List<string> GetRecentFilesInFolder(string folderPath)
        {
            List<string> recentFiles;
            List<string> restFiles;

            GetRecentAndRestFiles(folderPath, TimeSpan.FromMinutes(double.Parse(srcMinVal)), out recentFiles, out restFiles);
            var tmp = new List<string>(recentFiles);

            foreach (string recentFilePath in tmp)
            {
                string key = GetMergeKey(recentFilePath);
                string supplierDir = Path.GetDirectoryName(recentFilePath);

                // Bring along the newer files of the same document: same supplier folder and exact same key.
                // (Contains() on the full path also matched "12_345" and other suppliers' files.)
                List<string> matchingRestFiles = restFiles.Where(filePath =>
                    string.Equals(Path.GetDirectoryName(filePath), supplierDir, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(GetMergeKey(filePath), key, StringComparison.OrdinalIgnoreCase)).ToList();

                recentFiles.AddRange(matchingRestFiles);
            }
            return recentFiles.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(item => item).ToList();
        }
        static void GetRecentAndRestFiles(string folderPath, TimeSpan timeSpan, out List<string> recentFiles, out List<string> restFiles)
        {
            recentFiles = new List<string>();
            restFiles = new List<string>();

            try
            {
                DirectoryInfo directoryInfo = new DirectoryInfo(folderPath);
                DateTime currentTime = DateTime.Now;

                foreach (FileInfo fileInfo in directoryInfo.GetFiles("*", SearchOption.AllDirectories))
                {
                    // Calculate the time difference between the current time and the file's last modification time
                    TimeSpan fileAge = currentTime - fileInfo.LastWriteTime;

                    if (fileAge >= timeSpan)
                    {
                        recentFiles.Add(fileInfo.FullName);
                    }
                    else
                    {
                        restFiles.Add(fileInfo.FullName);
                    }
                }
            }
            catch (Exception ex)
            {
                // Was Console.WriteLine, which a windowed app never shows.
                SimpleLog.Error($"Refresh: could not read source folder '{folderPath}'");
                SimpleLog.Log(ex);
            }
        }






        private void logToScreen(string message)
        {
            // Every on-screen progress step also goes to the log file, so a run can be reconstructed later.
            SimpleLog.Info(message);
            txtDetails.Text += message + Environment.NewLine;
            txtDetails.SelectionStart = txtDetails.Text.Length;
            txtDetails.ScrollToCaret();
            Application.DoEvents();
        }
        private void btn1_Click(object sender, EventArgs e)
        {
            string num = "1";
            Start(num, null);
        }
        private void btn2_Click(object sender, EventArgs e)
        {
            string num = "2";
            Start(num, null);
        }
        private void btn3_Click(object sender, EventArgs e)
        {
            string num = "3";
            Start(num, null);
        }
        private void btn11_Click(object sender, EventArgs e)
        {
            string num = "1_999";
            Start(num, null);
        }

        private void btn22_Click(object sender, EventArgs e)
        {
            string num = "2_999";
            Start(num, null);
        }

        private void btn33_Click(object sender, EventArgs e)
        {
            string num = "3_999";
            Start(num, null);
        }
        private void btn9_Click(object sender, EventArgs e)
        {
            string num = ShlomoName;
            Start(num, null, false);
        }
        private void Start(string num, List<string> sdirs, bool IsMerge = true)
        {
            if (_running)
            {
                return;
            }
            if (!CheckProgress())
            {
                return;
            }
            _running = true;
            printFailures = 0;
            nothingToPrint = false;
            runStopwatch = Stopwatch.StartNew();
            SimpleLog.Info($"Run started: folder='{num}', merge={IsMerge}, printer='{PrinterFor(num)}', " +
                (sdirs == null ? "all suppliers" : "selected=[" + string.Join(", ", sdirs) + "]"));
            btnPrint.Enabled = false;
            btn1.Enabled = false;
            btn2.Enabled = false;
            btn3.Enabled = false;
            btn11.Enabled = false;
            btn22.Enabled = false;
            btn33.Enabled = false;
            btn9.Enabled = false;
            btnReset.Enabled = false;
            chbox1.Enabled = false;

            try
            {
                // The first run after start-up waits here until Spire/IronPdf have finished loading.
                Warmup.WaitIfRunning(logToScreen);

                if (Directory.Exists(txtPrint.Text))
                    Directory.Delete(txtPrint.Text, true);
                Directory.CreateDirectory(txtPrint.Text);

                var stage = Stopwatch.StartNew();
                logToScreen("מתחיל בתהליך");
                logToScreen("מעתיק קבצים לארכיון...");
                archive(num, sdirs);
                LogStage("archive copy", stage);
                logToScreen("מעתיק קבצים לתקיה זמנית...");
                SendToWorkingFolder(num, sdirs);
                LogStage("copy to working folder", stage);
                if (IsMerge)
                {
                    logToScreen("מתחיל בהמרת הקבצים...");
                    if (!Convert(num, sdirs))
                    {
                        FailRun();
                        return;
                    }
                    LogStage("convert", stage);
                    logToScreen("מתחיל באיחוד הקבצים");
                    if (!Merge(num, sdirs))
                    {
                        FailRun();
                        return;
                    }
                    LogStage("merge", stage);
                    logToScreen("מתחיל הדפסת הקבצים");
                    Print(num);
                    LogStage("print (incl. 1 s pause per file)", stage);
                }
                else
                {
                    logToScreen("מתחיל הדפסת הקבצים");
                    printNoMerge(num, sdirs);
                    LogStage("print (incl. 1 s pause per file)", stage);
                }
            }
            catch (Exception ex)
            {
                SimpleLog.Log(ex);
                logToScreen("הפעולה נכשלה - " + ex.Message);
                FailRun();
                return;
            }

            logToScreen("ממתין לספירה לאחור להסתיים");
            CountDown();
            updateDirectories();
        }

        /// <summary>Log how long a stage of the run took, then restart the stopwatch for the next stage.</summary>
        private static void LogStage(string name, Stopwatch stage)
        {
            SimpleLog.Info($"Stage '{name}' took {stage.ElapsedMilliseconds} ms");
            stage.Restart();
        }

        /// <summary>
        /// A run stopped part-way: tell the user and re-enable Refresh so the app isn't left locked.
        /// Working/print folders are left as they are for inspection; Refresh cleans up and re-enables the buttons.
        /// </summary>
        private void FailRun()
        {
            lblCountDown.Text = string.Empty;
            btnReset.Enabled = true;
            btnPrint.Enabled = true;
            chbox1.Enabled = true;
            _running = false;
            SimpleLog.Error($"Run FAILED after {runStopwatch.Elapsed.TotalSeconds:0}s; working/print folders left for inspection");
            MessageBox.Show("הפעולה נכשלה, יש לבחון קובץ לוג");
        }

        private void printNoMerge(string num, List<string> sdirs)
        {
            var workingDir = Path.Combine(txtWorkFol.Text, num);
            // With a selection, take only the selected suppliers' folders: leftovers from an earlier
            // failed run in the working folder must not be printed along with them.
            var lwFiles = sdirs == null
                ? Directory.GetFiles(workingDir, "*.*", SearchOption.AllDirectories)
                : sdirs.Select(d => Path.Combine(workingDir, d))
                       .Where(Directory.Exists)
                       .SelectMany(d => Directory.GetFiles(d, "*.*", SearchOption.AllDirectories))
                       .ToArray();
            foreach (var file in lwFiles)
            {
                File.Move(file, Path.Combine(txtPrint.Text, Path.GetFileName(file)));
            }


            var printingDir = txtPrint.Text;
            var lFiles = Directory.GetFiles(printingDir, "*.*", SearchOption.TopDirectoryOnly);
            if (lFiles.Length == 0)
                ReportEmptyBatch();

            foreach (var file in lFiles)
            {
                var ext = Path.GetExtension(file).TrimStart('.').ToLowerInvariant();
                logToScreen("מדפיס קובץ  - " + Path.GetFileName(file));
                Application.DoEvents();
                try
                {
                    if (ext == "pdf")
                    {
                        PrintPdf(file, Printer9);
                    }
                    else if (ext == "jpeg" || ext == "jpg" || ext == "tif" || ext == "tiff")
                    {
                        PrintImage(file);
                        SimpleLog.Info($"Printed image ({pages} page(s)) to '{Printer9}': {file}");
                    }
                    else if (BlockedExtensions.Contains(ext))
                    {
                        throw new InvalidOperationException("Refusing to print a program/script file: " + file);
                    }
                    else
                    {
                        PrintFile(file);
                        SimpleLog.Info($"Sent to the shell print handler for '{Printer9}': {file}");
                    }
                }
                catch (Exception ex)
                {
                    ReportPrintFailure(file, ex);
                }
                finally
                {
                    Thread.Sleep(1000);
                }
            }
        }

        /// <summary>
        /// Extensions never handed to the shell: with the PrintTo verb they would at best fail,
        /// and they must never be run.
        /// </summary>
        private static readonly HashSet<string> BlockedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "", "exe", "com", "bat", "cmd", "msi", "msp", "scr", "pif", "lnk", "url",
            "ps1", "psm1", "vbs", "vbe", "js", "jse", "wsf", "wsh", "hta", "cpl", "reg", "dll", "jar"
        };

        private void ReportPrintFailure(string file, Exception ex)
        {
            printFailures++;
            SimpleLog.Log(ex);
            logToScreen("הדפסת קבצים נכשלה לקובץ - " + file);
            MessageBox.Show("הדפסת קבצים נכשלה לקובץ - " + file + Environment.NewLine + ex.Message);
        }

        private void ReportEmptyBatch()
        {
            nothingToPrint = true;
            SimpleLog.Info("No files found to print in " + txtPrint.Text);
            logToScreen("לא נמצאו קבצים להדפסה");
        }

        private void Clean()
        {
            lblCountDown.Text = string.Empty;
            btn1.Enabled = true;
            btn2.Enabled = true;
            btn3.Enabled = true;
            btn11.Enabled = true;
            btn22.Enabled = true;
            btn33.Enabled = true;
            btn9.Enabled = true;
            btnPrint.Enabled = true;
            chbox1.Enabled = true;
            timerCounter = ParseTimer(timeVal);
            try
            {
                if (Directory.Exists(txtPrint.Text))
                    Directory.Delete(txtPrint.Text, true);
                Directory.CreateDirectory(txtPrint.Text);
            }
            catch 
            {
                MessageBox.Show("לא ניתן למחוק את תיקיית ההדפסה. נא למחוק ידנית");
            }
            
        }
        private void CountDown()
        {
            lblTime.Visible = true;
            timerCounter = ParseTimer(timeVal);

            // WinForms timer ticks on the UI thread: no Invoke, no overlapping ticks that skip past zero.
            timer1?.Stop();
            timer1?.Dispose();
            timer1 = new System.Windows.Forms.Timer { Interval = 1000 };
            timer1.Tick += (sender, e) =>
            {
                timerCounter--;
                lblCountDown.Text = timerCounter.ToString();
                if (timerCounter > 0)
                    return;

                timer1.Stop();
                lblTime.Visible = false;
                lblCountDown.Text = string.Empty;
                logToScreen("מנקה את סביבת העבודה");
                btnReset.Enabled = true;
                btnPrint.Enabled = true;
                chbox1.Enabled = true;
                _running = false;
                SimpleLog.Info($"Run finished after {runStopwatch.Elapsed.TotalSeconds:0}s: " +
                    (printFailures > 0 ? $"{printFailures} print error(s)" : nothingToPrint ? "nothing to print" : "success"));
                updateScreen();
                if (printFailures > 0)
                    MessageBox.Show("הפעולה הסתיימה עם " + printFailures + " שגיאות הדפסה, יש לבחון קובץ לוג");
                else if (nothingToPrint)
                    MessageBox.Show("לא נמצאו קבצים להדפסה בתיקייה שנבחרה.", "אין מה להדפיס",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                else
                    MessageBox.Show("הפעולה הסתיימה בהצלחה");
            };
            timer1.Start();
            lblCountDown.Text = timerCounter.ToString();
        }

        private const string SrcMinutesError = "מספר הדקות בלשונית קונפיגורציה חייב להיות בין 0 ל-10080";

        /// <summary>
        /// Minimum file age (minutes) for Refresh: a finite number from 0 to one week.
        /// Rejects text, negatives, NaN and values TimeSpan.FromMinutes would overflow on.
        /// </summary>
        internal static bool TryParseSrcMinutes(string value, out double minutes)
        {
            return double.TryParse(value, out minutes) && double.IsFinite(minutes)
                && minutes >= 0 && minutes <= 7 * 24 * 60;
        }

        /// <summary>
        /// Countdown length in seconds from the config; falls back to 30 for missing, invalid or non-positive values.
        /// </summary>
        private static int ParseTimer(string value)
        {
            return int.TryParse(value, out var seconds) && seconds > 0 ? seconds : 30;
        }


        private bool Convert(string num, List<string> sdirs)
        {
            try
            {
                bool isPicked = sdirs != null;
                var files = new List<string>();
                var counter = 0;
                var workingDir = Path.Combine(txtWorkFol.Text, num);
                string[] dirs;
                if (isPicked)
                {
                    dirs = new string[sdirs.Count];
                    foreach (var ddir in sdirs)
                    {
                        dirs[counter] = Path.Combine(workingDir, ddir);
                        counter++;
                    }

                }
                else
                {
                    dirs = Directory.GetDirectories(workingDir);
                }
                counter = 0;
                for (int i = 0; i < dirs.Length; i++)
                {
                    var dir = dirs[i];
                    if (!num.EndsWith("999") && Path.GetFileName(dir).StartsWith("999_"))
                    {
                        var p999 = Path.Combine(txtMain.Text, num.ToString() + "_999");
                        if (!Directory.Exists(p999))
                        {
                            Directory.CreateDirectory(Path.Combine(txtMain.Text, num.ToString() + "_999"));
                        }
                        var dest = Path.Combine(p999, Path.GetFileName(dir));
                        if (!Directory.Exists(dest))
                        {
                            Directory.CreateDirectory(dest);
                        }
                        foreach (var file in Directory.GetFiles(dir, "*.*"))
                        {
                            string targetFile = Path.Combine(dest, Path.GetFileName(file));
                            if (File.Exists(targetFile))
                            {
                                File.Delete(targetFile);
                            }
                            File.Move(file, targetFile);
                        }
                        Directory.Delete(dir, true);
                        continue;
                    }
                    logToScreen("מתחיל בהמרת קבצים בתיקיה  - " + Path.GetFileName(dir));
                    var lFiles = Directory.GetFiles(dir, "*.*", SearchOption.TopDirectoryOnly);
                    files = new List<string>();
                    foreach (var lfile in lFiles)
                    {
                        files.Add(lfile);
                    }

                    foreach (var file in files)
                    {
                        try
                        {
                            logToScreen("ממיר קובץ   - " + Path.GetFileName(Path.GetDirectoryName(file)) + @"\" + Path.GetFileName(file));
                            var fn = Path.GetFileNameWithoutExtension(file);
                            var ext = Path.GetExtension(file);
                            counter++;

                            //lblCount.Text = $"Converting file {counter} of {files.Count}";
                            Application.DoEvents();
                            var convertWatch = Stopwatch.StartNew();
                            var converts = IsConvertedType(ext);
                            if (ext.ToLower() == ".jpg" || ext.ToLower() == ".jpeg")
                            {
                                using (var converted = ImageToPdfConverter.ImageToPdf(file, IronPdf.Imaging.ImageBehavior.FitToPage))
                                {
                                    // converted.CompressImages(10);
                                    converted.SaveAs(Path.Combine(Path.GetDirectoryName(file), fn + ".pdf"));
                                }

                                //    var converted = IronPdf.ImageToPdfConverter.ImageToPdf(file);
                                //converted.SaveAs(Path.Combine(Path.GetDirectoryName(file), fn + ".pdf"));

                                //ImageToPdfConverter.ImageToPdf(file, ImageBehavior.CropPage).SaveAs(Path.Combine(Path.GetDirectoryName(file), fn + ".pdf"));
                            }
                            else if (ext.ToLower() == ".tiff" || ext.ToLower() == ".tif")
                            {

                                using (var converted = ImageToPdfConverter.ImageToPdf(file, IronPdf.Imaging.ImageBehavior.FitToPage))
                                {
                                    converted.SaveAs(Path.Combine(Path.GetDirectoryName(file), fn + ".pdf"));
                                }
                                //var converted = IronPdf.ImageToPdfConverter.ImageToPdf(file);
                                //converted.SaveAs(Path.Combine(Path.GetDirectoryName(file), fn + ".pdf"));
                                //Image tiffImage = Image.FromFile(file);
                                //Image[] images = SplitTIFFImage(tiffImage);
                                //ImageToPdfConverter.ImageToPdf(images, ImageBehavior.CropPage).SaveAs(Path.Combine(Path.GetDirectoryName(file), fn + ".pdf"));
                            }
                            else if (ext.ToLower() == ".html" || ext.ToLower() == ".htm")
                            {
                                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                                var Renderer = new ChromePdfRenderer();
                                Renderer.RenderingOptions.InputEncoding = Encoding.GetEncoding(1255);
                                Renderer.RenderingOptions.PrintHtmlBackgrounds = false;
                                //Renderer.PrintOptions.PaperSize = PdfPrintOptions.PdfPaperSize.A4;
                                //Renderer.PrintOptions.CssMediaType = PdfPrintOptions.PdfCssMediaType.Print;

                                Renderer.RenderingOptions.PaperSize = IronPdf.Rendering.PdfPaperSize.A4;
                                Renderer.RenderingOptions.CssMediaType = IronPdf.Rendering.PdfCssMediaType.Print;

                                //Renderer.PrintOptions.EnableJavaScript = true;
                                //Renderer.PrintOptions.ViewPortWidth = 1280;
                                //Renderer.PrintOptions.RenderDelay = 500; //milliseconds
                                Renderer.RenderingOptions.MarginLeft = 10;
                                Renderer.RenderingOptions.MarginRight = 10;
                                Renderer.RenderingOptions.MarginTop = 10;
                                Renderer.RenderingOptions.MarginBottom = 10;
                                Renderer.RenderingOptions.Zoom = 100;

                                using (var PDF = Renderer.RenderHtmlFileAsPdf(file))
                                {
                                    var OutputPath = Path.Combine(Path.GetDirectoryName(file), fn + ".pdf");
                                    PDF.SaveAs(OutputPath);
                                }
                            }
                            if (converts)
                            {
                                SimpleLog.Info($"Converted {ext.ToLower()} to PDF in {convertWatch.ElapsedMilliseconds} ms " +
                                    $"({new FileInfo(file).Length / 1024} KB file): {file}");
                            }
                        }
                        catch (Exception ex)
                        {
                            SimpleLogger.SimpleLog.Log(ex);
                            logToScreen("המרת הקובץ נכשלה -  " + file);
                            logToScreen("הפעולה תסתיים");
                            return false;
                        }

                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                SimpleLogger.SimpleLog.Log(ex);
                logToScreen("המרת הקבצים נכשלה");
                logToScreen("הפעולה תסתיים");
                return false;
            }

        }
        /// <summary>File types that <see cref="Convert"/> turns into a PDF with IronPdf.</summary>
        private static bool IsConvertedType(string ext)
        {
            switch (ext.ToLowerInvariant())
            {
                case ".jpg": case ".jpeg": case ".tif": case ".tiff": case ".htm": case ".html":
                    return true;
                default:
                    return false;
            }
        }

        private bool Merge(string num, List<string> sdirs)
        {
            try
            {
                int counter = 0;
                bool isPicked = sdirs != null;
                var workingDir = Path.Combine(txtWorkFol.Text, num);
                string[] dirs = null;
                if (isPicked)
                {
                    dirs = new string[sdirs.Count];
                    foreach (var ddir in sdirs)
                    {
                        dirs[counter] = Path.Combine(workingDir, ddir);
                        counter++;
                    }

                }
                else
                {
                    dirs = Directory.GetDirectories(workingDir);
                }
                for (int i = 0; i < dirs.Length; i++)
                {
                    var dir = dirs[i];
                    // Convert moved selected 999_ suppliers to the parallel N_999 folder; they wait there and
                    // aren't missing. Only that exact case is skipped, any other missing folder still fails.
                    if (isPicked && !Directory.Exists(dir) && !num.EndsWith("999")
                        && Path.GetFileName(dir).StartsWith("999_"))
                    {
                        logToScreen("הספק הועבר לתקיית 999 - " + Path.GetFileName(dir));
                        continue;
                    }
                    logToScreen("מתחיל באיחוד קבצים לתקיה - " + Path.GetFileName(dir));
                    var lFiles = Directory.GetFiles(dir, "*.pdf", SearchOption.TopDirectoryOnly);
                    var supplier = Path.GetFileName(dir);

                    // Group by the exact document key (first two "_" parts of the file name).
                    // The old code matched with Contains() on the full path, so "12_34" also took "12_345",
                    // and a supplier name containing '-' or '.' broke the key.
                    var groups = lFiles
                        .GroupBy(f => GetMergeKey(f), StringComparer.OrdinalIgnoreCase)
                        .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

                    foreach (var group in groups)
                    {
                        var mergeName = supplier + "-" + group.Key;
                        logToScreen("מאחד קובץ - " + mergeName + ".pdf");
                        var pdfDocuments = new List<PdfDocument>();
                        var mergeWatch = Stopwatch.StartNew();
                        try
                        {
                            foreach (var path in group.OrderBy(o => o, StringComparer.OrdinalIgnoreCase))
                            {
                                pdfDocuments.Add(PdfDocument.FromFile(path));
                            }
                            var mergedPath = Path.Combine(txtPrint.Text, mergeName + ".pdf");
                            using (var mergedPdfDocument = PdfDocument.Merge(pdfDocuments))
                            {
                                mergedPdfDocument.SaveAs(mergedPath);
                            }
                            SimpleLog.Info($"Merged {pdfDocuments.Count} file(s) in {mergeWatch.ElapsedMilliseconds} ms " +
                                $"({new FileInfo(mergedPath).Length / 1024} KB result): {mergedPath}");
                        }
                        finally
                        {
                            foreach (var doc in pdfDocuments)
                                doc.Dispose();
                        }
                    }
                }
                Directory.Delete(txtWorkFol.Text, true);
                Directory.CreateDirectory(txtWorkFol.Text);
                return true;
            }
            catch (Exception ex)
            {
                SimpleLogger.SimpleLog.Log(ex);
                logToScreen("איחוד הקבצים נכשל");
                logToScreen("הפעולה תסתיים");
                return false;
            }
        }
        /// <summary>
        /// Merge key for a supplier file: "12_34_2.pdf" -> "12_34". A name without a second "_" part
        /// is merged on its own instead of crashing the whole merge.
        /// </summary>
        internal static string GetMergeKey(string file)
        {
            var parts = Path.GetFileNameWithoutExtension(file).Split('_');
            return parts.Length >= 2 ? parts[0] + "_" + parts[1] : Path.GetFileNameWithoutExtension(file);
        }

        /// <summary>Printer configured for a folder (Shlomo's folder uses Printer9; unknown folders fall back to Printer1).</summary>
        private string PrinterFor(string num)
        {
            switch (num)
            {
                case "1": return Printer1;
                case "2": return Printer2;
                case "3": return Printer3;
                case "1_999": return Printer11;
                case "2_999": return Printer22;
                case "3_999": return Printer33;
                default: return num == ShlomoName ? Printer9 : Printer1;
            }
        }

        private void Print(string num)
        {
            var printingDir = txtPrint.Text;
            var lFiles = Directory.GetFiles(printingDir, "*.pdf", SearchOption.TopDirectoryOnly);
            if (lFiles.Length == 0)
                ReportEmptyBatch();

            foreach (var file in lFiles)
            {
                try
                {
                    var pprint = PrinterFor(num);
                    logToScreen("מדפיס קובץ  - " + Path.GetFileName(file));
                    Application.DoEvents();
                    PrintPdf(file, pprint);

                    Thread.Sleep(1000);
                    //var doc = pdf.GetPrintDocument();
                    //doc.EndPrint += Doc_EndPrint;
                    //doc.PrinterSettings.PrinterName = num == "1" ? Printer1 : num == "2" ? Printer2 : Printer3;
                    //doc.Print();
                }
                catch (Exception ex)
                {
                    ReportPrintFailure(file, ex);
                }
            }
        }

        /// <summary>
        /// Print a PDF with the engine chosen by <see cref="AppConfig.PrintEngine"/> (Spire by default).
        /// The page count means pages were sent to the driver, not that paper came out.
        /// </summary>
        private static void PrintPdf(string file, string printerName)
        {
            var target = string.IsNullOrEmpty(printerName) ? "(default printer)" : printerName;
            var engine = PrintEngines.Normalize(AppConfig.Current.PrintEngine);
            var watch = Stopwatch.StartNew();

            int printed;
            if (engine == PrintEngines.Custom && !string.IsNullOrEmpty(printerName))
            {
                printed = PrintPdfWithGhostscript(file, printerName, out var fallbackReason);
                if (fallbackReason != null)
                {
                    // Nothing was sent: the conversion failed before the queue was touched, so Spire can print it.
                    SimpleLog.Warning($"Ghostscript could not convert {file}, printing it with Spire instead: {fallbackReason}");
                    engine = PrintEngines.Spire;
                    printed = PrintPdfWithSpire(file, printerName);
                }
            }
            else
            {
                if (engine == PrintEngines.Custom)
                    engine = PrintEngines.Spire;   // RAW jobs need a named queue; no printer configured means default printer
                printed = engine == PrintEngines.IronPdf ? PrintPdfWithIronPdf(file, printerName) : PrintPdfWithSpire(file, printerName);
            }

            if (printed <= 0)
                throw new InvalidOperationException($"No pages were printed to '{target}': {file}");
            SimpleLog.Info($"Printed {printed} page(s) to '{target}' with {engine} in {watch.ElapsedMilliseconds} ms " +
                $"({new FileInfo(file).Length / 1024} KB file): {file}");
        }

        /// <summary>
        /// Ghostscript converts the PDF to PCL 6 and the job goes to the queue as RAW data (see GhostscriptPrinter).
        /// Returns the page count; when the conversion fails, returns 0 with a reason and nothing has been sent.
        /// </summary>
        private static int PrintPdfWithGhostscript(string file, string printerName, out string fallbackReason)
        {
            var check = new PrinterSettings { PrinterName = printerName };
            if (!check.IsValid)
                throw new InvalidOperationException("Printer is not installed or not valid: " + printerName);

            int pages;
            using (var doc = new Spire.Pdf.PdfDocument())
            {
                doc.LoadFromFile(file);
                pages = doc.Pages.Count;
            }

            // The driver is bypassed, so the queue's tray/duplex are read here and written into the job.
            var settings = GhostscriptPrinter.ResolveSettings(printerName, out var settingsText);

            var convertWatch = Stopwatch.StartNew();
            var job = GhostscriptPrinter.TryConvert(file, settings, out fallbackReason);
            if (job == null)
                return 0;
            var convertMs = convertWatch.ElapsedMilliseconds;

            var sendWatch = Stopwatch.StartNew();
            GhostscriptPrinter.SendRaw(printerName, Path.GetFileName(file), job);
            SimpleLog.Info($"Ghostscript: {settingsText}; converted in {convertMs} ms, {job.Length / 1024} KB job queued in {sendWatch.ElapsedMilliseconds} ms");
            return pages;
        }

        /// <summary>
        /// Spire draws each page into the printer as text and vector shapes, so the spool job stays
        /// close to the PDF's size. Print() is synchronous: it returns after the last page is spooled.
        /// StandardPrintController hides the "Printing..." progress window.
        /// </summary>
        private static int PrintPdfWithSpire(string file, string printerName)
        {
            using (var doc = new Spire.Pdf.PdfDocument())
            {
                doc.LoadFromFile(file);
                if (!string.IsNullOrEmpty(printerName))
                {
                    doc.PrintSettings.PrinterName = printerName;
                    // An unknown printer name must fail, not fall back to another printer.
                    var check = new PrinterSettings { PrinterName = printerName };
                    if (!check.IsValid)
                        throw new InvalidOperationException("Printer is not installed or not valid: " + printerName);
                }
                doc.PrintSettings.PrintController = new StandardPrintController();
                doc.Print();
                return doc.Pages.Count;
            }
        }

        /// <summary>
        /// IronPdf renders every page to a 300 DPI bitmap, which makes the spool job very large
        /// (a 130 KB PDF can become ~100 MB). Kept as a fallback via PrintEngine = "IronPdf".
        /// Print returns Task&lt;int&gt;; started via Task.Run so it doesn't capture the WinForms
        /// synchronization context (blocking on it from the UI thread could deadlock).
        /// </summary>
        private static int PrintPdfWithIronPdf(string file, string printerName)
        {
            using (var pdf = PdfDocument.FromFile(file))
            {
                return Task.Run(() => pdf.Print(printerName)).GetAwaiter().GetResult();
            }
        }

        //private void Doc_EndPrint(object sender, PrintEventArgs e)
        //{
        //    throw new NotImplementedException();
        //}
        private void PrintImage(string fileName)
        {
            using (printImg = System.Drawing.Image.FromFile(fileName))
            using (var printDoc = new PrintDocument())
            {
                pages = 0;
                // Choose the target printer BEFORE validating; validating first checked the default printer
                // instead, and an invalid default left the old busy-flag loop waiting forever.
                if (!string.IsNullOrEmpty(Printer9))
                    printDoc.PrinterSettings.PrinterName = Printer9;
                if (!printDoc.PrinterSettings.IsValid)
                    throw new InvalidOperationException("Printer is not installed or not valid: " + Printer9);

                printDoc.PrintPage += TiffPrintPage;
                printDoc.DefaultPageSettings.Landscape = printImg.Width > printImg.Height;
                printDoc.PrintController = new StandardPrintController();
                printDoc.Print(); // synchronous: returns after the last page is spooled
            }
        }

        private void PrintFile(string fileName)
        {
            // .NET 8 defaults UseShellExecute to false, which skips the shell "PrintTo" verb
            // and would start the file as a program instead. It must be true here.
            var hasPrinter = !string.IsNullOrEmpty(Printer9);
            var psi = new ProcessStartInfo(fileName)
            {
                UseShellExecute = true,
                Verb = hasPrinter ? "PrintTo" : "Print",
                Arguments = hasPrinter ? "\"" + Printer9 + "\"" : "",
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            using (var shellProcess = Process.Start(psi))
            {
                // Null when the shell handed the job to an already-running application: nothing to check.
                if (shellProcess == null)
                    return;

                if (!shellProcess.WaitForExit(10000))
                {
                    // Many print handlers (Word, Acrobat...) stay open after printing, so a timeout is
                    // logged as unconfirmed rather than counted as a failure.
                    SimpleLog.Warning("Print handler still running after 10s, print not confirmed: " + fileName);
                    logToScreen("לא התקבל אישור הדפסה לקובץ - " + Path.GetFileName(fileName));
                    return;
                }
                if (shellProcess.ExitCode != 0)
                    throw new InvalidOperationException(
                        $"Print handler exited with code {shellProcess.ExitCode}: {fileName}");
            }
        }
        private void TiffPrintPage(object sender, PrintPageEventArgs e)
        {
            printImg.SelectActiveFrame(FrameDimension.Page, pages);
            pages++;
            e.Graphics.DrawImage(printImg, 0, 0);
            if (pages < printImg.GetFrameCount(FrameDimension.Page))
            {
                e.HasMorePages = true;
            }
        }
        //private Image[] SplitTIFFImage(Image tiffImage)
        //{
        //    int frameCount = tiffImage.GetFrameCount(FrameDimension.Page);
        //    Image[] images = new Image[frameCount];
        //    Guid objGuid = tiffImage.FrameDimensionsList[0];
        //    FrameDimension objDimension = new FrameDimension(objGuid);
        //    for (int i = 0; i < frameCount; i++)
        //    {
        //        tiffImage.SelectActiveFrame(objDimension, i);
        //        using (MemoryStream ms = new MemoryStream())
        //        {
        //            tiffImage.Save(ms, ImageFormat.Tiff);
        //            images[i] = Image.FromStream(ms);
        //        }
        //    }
        //    return images;
        //}
        private void SendToWorkingFolder(string num, List<string> sdirs)
        {
            var diSource = new DirectoryInfo(Path.Combine(txtMain.Text, num));
            if (!Directory.Exists(Path.Combine(txtWorkFol.Text, num)))
            {
                Directory.CreateDirectory(Path.Combine(txtWorkFol.Text, num));
            }
            var diTarget = new DirectoryInfo(Path.Combine(txtWorkFol.Text, num));
            var baseTarget = diTarget.FullName;
            var baseSource = diSource.FullName;
            if (sdirs == null)
            {
                CopyFolder(diSource, diTarget, false);
                DelSource(num);
            }
            else
            {
                foreach (var dir in sdirs)
                {
                    diSource = new DirectoryInfo(Path.Combine(baseSource, dir));
                    diTarget = new DirectoryInfo(Path.Combine(baseTarget, dir));
                    CopyFolder(diSource, diTarget, false);
                }
                DelSource(num, sdirs);
            }

        }
        private void DelSource(string num, List<string> sdirs)
        {
            var diSource = new DirectoryInfo(Path.Combine(txtMain.Text, num));
            var dirs = Directory.GetDirectories(diSource.FullName);
            foreach (var dir in dirs)
            {
                // Exact name match: Contains() made "Acme" also delete "Acme2".
                var name = Path.GetFileName(dir);
                if (sdirs.Any(sdir => string.Equals(sdir, name, StringComparison.OrdinalIgnoreCase)))
                {
                    Directory.Delete(dir, true);
                }
            }
        }
        private void DelSource(string num)
        {
            var diSource = new DirectoryInfo(Path.Combine(txtMain.Text, num));
            diSource.Delete(true);
            Directory.CreateDirectory(Path.Combine(txtMain.Text, num));
        }
        private void archive(string num, List<string> sdirs)
        {
            var diSource = new DirectoryInfo(Path.Combine(txtMain.Text, num));
            var dt = DateTime.Now.ToString("dd.MM.yy.HH.mm");
            var isPicked = sdirs != null;
            var selected = isPicked ? "-selected" : string.Empty;
            var path = Path.Combine(txtArchive.Text, dt + "-" + num + selected);
            var diTarget = new DirectoryInfo(path);

            if (!isPicked)
            {
                CopyFolder(diSource, diTarget, true);
            }
            else
            {
                var baseTarget = diTarget.FullName;
                var baseSource = diSource.FullName;
                foreach (var dir in sdirs)
                {
                    diSource = new DirectoryInfo(Path.Combine(baseSource, dir));
                    diTarget = new DirectoryInfo(Path.Combine(baseTarget, dir));
                    CopyFolder(diSource, diTarget, true);
                }
            }
        }
        private void CopyFolder(DirectoryInfo source, DirectoryInfo target, bool includeSubDirs)
        {
            if (!Directory.Exists(target.FullName))
            {
                Directory.CreateDirectory(target.FullName);
            }


            // Copy each file into the new directory.
            foreach (FileInfo fi in source.GetFiles())
            {
                fi.CopyTo(Path.Combine(target.FullName, fi.Name), true);
            }

            // Copy each subdirectory using recursion.
            foreach (DirectoryInfo diSourceSubDir in source.GetDirectories())
            {
                if (includeSubDirs || !diSourceSubDir.Name.Contains("888"))
                {
                    DirectoryInfo nextTargetSubDir = target.CreateSubdirectory(diSourceSubDir.Name);
                    CopyFolder(diSourceSubDir, nextTargetSubDir, includeSubDirs);
                }

            }
        }

        private List<string> GetAllPrinterList()
        {
            var _prntrs = new List<string>();
            //ManagementScope objScope = new ManagementScope(ManagementPath.DefaultPath); //For the local Access
            //objScope.Connect();

            //SelectQuery selectQuery = new SelectQuery();
            //selectQuery.QueryString = "Select * from win32_Printer";
            //ManagementObjectSearcher MOS = new ManagementObjectSearcher(objScope, selectQuery);
            //ManagementObjectCollection MOC = MOS.Get();
            //foreach (ManagementObject mo in MOC)
            //{
            //    _prntrs.Add(mo["Name"].ToString());
            //}
            foreach (string printer in System.Drawing.Printing.PrinterSettings.InstalledPrinters)
            {
                _prntrs.Add(printer);
            }
            return _prntrs;
        }

        private void btnBrowseSrc_Click(object sender, EventArgs e)
        {
            using (var fbd = new FolderBrowserDialog())
            {
                DialogResult result = fbd.ShowDialog();

                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                {
                    txtSource.Text = fbd.SelectedPath;
                }
            }
        }
        private void btnBrowse_Click(object sender, EventArgs e)
        {
            using (var fbd = new FolderBrowserDialog())
            {
                DialogResult result = fbd.ShowDialog();

                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                {
                    txtMain.Text = fbd.SelectedPath;
                }
            }
        }

        private void butWorkFol_Click(object sender, EventArgs e)
        {
            using (var fbd = new FolderBrowserDialog())
            {
                DialogResult result = fbd.ShowDialog();

                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                {
                    txtWorkFol.Text = fbd.SelectedPath;
                }
            }
        }

        private void bprint_Click(object sender, EventArgs e)
        {
            using (var fbd = new FolderBrowserDialog())
            {
                DialogResult result = fbd.ShowDialog();

                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                {
                    txtPrint.Text = fbd.SelectedPath;
                }
            }
        }

        private void cboPrinter1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_loadingConfig || cboPrinter1.SelectedItem == null)
                return;

            Printer1 = cboPrinter1.SelectedItem.ToString();

        }

        private void cboPrinter2_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_loadingConfig || cboPrinter2.SelectedItem == null)
                return;

            Printer2 = cboPrinter2.SelectedItem.ToString();
        }

        private void cboPrinter3_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_loadingConfig || cboPrinter3.SelectedItem == null)
                return;

            Printer3 = cboPrinter3.SelectedItem.ToString();
        }

        private void bArchive_Click(object sender, EventArgs e)
        {
            using (var fbd = new FolderBrowserDialog())
            {
                DialogResult result = fbd.ShowDialog();

                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                {
                    txtArchive.Text = fbd.SelectedPath;
                }
            }
        }
        private bool CheckProgress()
        {
            if (string.IsNullOrEmpty(txtSource.Text) || string.IsNullOrEmpty(txtArchive.Text) || string.IsNullOrEmpty(txtMain.Text) || string.IsNullOrEmpty(txtPrint.Text) || string.IsNullOrEmpty(txtWorkFol.Text) || string.IsNullOrEmpty(txtTimer.Text))
            {
                MessageBox.Show("פרטים חסרים בלשונית קונפיגורציה");
                return false;
            }
            if (!int.TryParse(txtTimer.Text, out var seconds) || seconds <= 0)
            {
                MessageBox.Show("זמן הספירה לאחור חייב להיות מספר שלם חיובי");
                return false;
            }
            if (!TryParseSrcMinutes(txtSrcMinutes.Text, out _))
            {
                MessageBox.Show(SrcMinutesError);
                return false;
            }
            timeVal = txtTimer.Text;
            srcMinVal = txtSrcMinutes.Text;
            return true;
        }



        private void btnPrint_Click(object sender, EventArgs e)
        {
            if (chbox1.Items.Count == 0)// && chbox2.Items.Count == 0 && chbox3.Items.Count == 0)
            {
                MessageBox.Show("יש לבחור לפחות ספק אחד");
                return;
            }
            tabMain.SelectedIndex = 0;
            var sdirs = new List<string>();
            string num = "0";
            CheckedListBox.CheckedItemCollection col = null;
            if (chbox1.CheckedItems.Count > 0)
            {
                num = CurrentDirectory;
                col = chbox1.CheckedItems;
            }
            else
            {
                MessageBox.Show("יש לבחור תקיות");
                return;
            }

            if (string.IsNullOrEmpty(num))
            {
                MessageBox.Show("יש לבחור תקיה");
                return;
            }

            for (int i = 0; i < col.Count; i++)
            {
                sdirs.Add(col[i].ToString());
            }

            // Shlomo's folder is printed as-is, never merged (same as btn9).
            Start(num, sdirs, num != ShlomoName);

        }

        private void button1_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void cboPrinter11_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_loadingConfig || cboPrinter11.SelectedItem == null)
                return;

            Printer11 = cboPrinter11.SelectedItem.ToString();
        }

        private void cboPrinter22_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_loadingConfig || cboPrinter22.SelectedItem == null)
                return;

            Printer22 = cboPrinter22.SelectedItem.ToString();
        }

        private void cboPrinter33_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_loadingConfig || cboPrinter33.SelectedItem == null)
                return;

            Printer33 = cboPrinter33.SelectedItem.ToString();
        }

        private void cboPrinter9_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_loadingConfig || cboPrinter9.SelectedItem == null)
                return;

            Printer9 = cboPrinter9.SelectedItem.ToString();
        }
    }


}

