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
        private System.Timers.Timer timer1;
        private int timerCounter = 30;
        private string timeVal;
        private static string srcMinVal;
        private string CurrentDirectory = null;
        private static bool PrintImageObj;
        int pages = 0;
        private string ShlomoName;
        System.Drawing.Image printImg;
        public Form1()
        {
            InitializeComponent();


            ShlomoName = ConfigurationManager.AppSettings["shlomo"].ToString();
            // int.TryParse(ConfigurationManager.AppSettings["WaitSeconds"].ToString(), out timerCounter);
            var printers = GetAllPrinterList();
            cboPrinter1.Items.AddRange(printers.ToArray());
            cboPrinter2.Items.AddRange(printers.ToArray());
            cboPrinter3.Items.AddRange(printers.ToArray());
            cboPrinter11.Items.AddRange(printers.ToArray());
            cboPrinter22.Items.AddRange(printers.ToArray());
            cboPrinter33.Items.AddRange(printers.ToArray());
            cboPrinter9.Items.AddRange(printers.ToArray());

            timeVal = Properties.Settings.Default.TimerPeriod;
            if (!string.IsNullOrEmpty(timeVal))
            {
                txtTimer.Text = timeVal;
            }
            else
            {
                timeVal = "30";
                txtTimer.Text = "30";
            }
            srcMinVal = Properties.Settings.Default.SrcMinPeriod;
            if (!string.IsNullOrEmpty(srcMinVal))
            {
                txtSrcMinutes.Text = srcMinVal;
            }
            else
            {
                srcMinVal = "5";
                txtSrcMinutes.Text = "5";
            }
            var dirsrcPath = Properties.Settings.Default.SourcePath;
            if (!string.IsNullOrEmpty(dirsrcPath))
            {
                txtSource.Text = dirsrcPath;
            }
            var dirmPath = Properties.Settings.Default.MainPath;
            if (!string.IsNullOrEmpty(dirmPath))
            {
                txtMain.Text = dirmPath;
            }
            var dirwPath = Properties.Settings.Default.WorkingPath;
            if (!string.IsNullOrEmpty(dirwPath))
            {
                txtWorkFol.Text = dirwPath;
            }
            var dirpPath = Properties.Settings.Default.PrintPath;
            if (!string.IsNullOrEmpty(dirpPath))
            {
                txtPrint.Text = dirpPath;
            }
            var diraPath = Properties.Settings.Default.ArchivePath;
            if (!string.IsNullOrEmpty(diraPath))
            {
                txtArchive.Text = diraPath;
            }

            var dirPrinter1 = Properties.Settings.Default.Printer1;
            var counter = 0;
            var counter1 = 0;
            if (!string.IsNullOrEmpty(dirPrinter1))
            {
                Printer1 = dirPrinter1;
                foreach (var printer in PrinterSettings.InstalledPrinters)
                {
                    if (printer.ToString() == dirPrinter1)
                        counter1 = counter;

                    counter++;
                }
                cboPrinter1.SelectedIndex = counter1;
            }
            var dirPrinter2 = Properties.Settings.Default.Printer2;
            if (!string.IsNullOrEmpty(dirPrinter2))
            {
                Printer2 = dirPrinter2;
                counter = 0;
                counter1 = 0;
                foreach (var printer in PrinterSettings.InstalledPrinters)
                {
                    if (printer.ToString() == dirPrinter2)
                        counter1 = counter;

                    counter++;
                }
                cboPrinter2.SelectedIndex = counter1;

            }
            var dirPrinter3 = Properties.Settings.Default.Printer3;
            if (!string.IsNullOrEmpty(dirPrinter3))
            {
                Printer3 = dirPrinter3;
                counter = 0;
                counter1 = 0;
                foreach (var printer in PrinterSettings.InstalledPrinters)
                {
                    if (printer.ToString() == dirPrinter3)
                        counter1 = counter;

                    counter++;
                }
                cboPrinter3.SelectedIndex = counter1;
            }
            var dirPrinter11 = Properties.Settings.Default.Printer11;
            if (!string.IsNullOrEmpty(dirPrinter11))
            {
                Printer11 = dirPrinter11;
                counter = 0;
                counter1 = 0;
                foreach (var printer in PrinterSettings.InstalledPrinters)
                {
                    if (printer.ToString() == dirPrinter11)
                        counter1 = counter;

                    counter++;
                }
                cboPrinter11.SelectedIndex = counter1;
            }
            var dirPrinter22 = Properties.Settings.Default.Printer22;
            if (!string.IsNullOrEmpty(dirPrinter22))
            {
                Printer22 = dirPrinter22;
                counter = 0;
                counter1 = 0;
                foreach (var printer in PrinterSettings.InstalledPrinters)
                {
                    if (printer.ToString() == dirPrinter22)
                        counter1 = counter;

                    counter++;
                }
                cboPrinter22.SelectedIndex = counter1;
            }
            var dirPrinter33 = Properties.Settings.Default.Printer33;
            if (!string.IsNullOrEmpty(dirPrinter33))
            {
                Printer33 = dirPrinter33;
                counter = 0;
                counter1 = 0;
                foreach (var printer in PrinterSettings.InstalledPrinters)
                {
                    if (printer.ToString() == dirPrinter33)
                        counter1 = counter;

                    counter++;
                }
                cboPrinter33.SelectedIndex = counter1;
            }
            var dirPrinter9 = Properties.Settings.Default.Printer9;
            if (!string.IsNullOrEmpty(dirPrinter9))
            {
                Printer9 = dirPrinter9;
                counter = 0;
                counter1 = 0;
                foreach (var printer in PrinterSettings.InstalledPrinters)
                {
                    if (printer.ToString() == dirPrinter9)
                        counter1 = counter;

                    counter++;
                }
                cboPrinter9.SelectedIndex = counter1;
            }

            updateScreen();
            updateDirectories();
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
            var nums = new string[] { "1", "2", "3" };
            foreach (var num in nums)
            {
                var mpath = Path.Combine(txtMain.Text, num);
                MoveDir(mpath, num);

                var spath = Path.Combine(txtSource.Text, num);
                SourceToMain(spath, mpath);
            }
            //txtDetails.Clear();
            updateScreen();
            updateDirectories();
            Clean();
        }
        private void SourceToMain(string spath, string mpath)
        {
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
                foreach (var file in files)
                {
                    var dest = getDestPathForFile(file, mpath);

                    if (!File.Exists(dest))
                    {
                        File.Move(file, dest);
                    }
                }
            }
            catch (Exception ex)
            {
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
                                File.Delete(targetFile);
                            }
                            File.Move(file, targetFile);
                        }
                        Directory.Delete(dir, true);
                        //Directory.Move(dir, Path.Combine(Path.Combine(txtMain.Text, num.ToString() + "_999"), Path.GetFileName(dir)));
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
            if (item.ToString().Contains("תקיה " + "1_999"))
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
                chbox1.Items.Add(dir);
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
                if (!string.IsNullOrEmpty(timeVal))
                {
                    timerCounter = int.Parse(timeVal);
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
                if (!string.IsNullOrEmpty(timeVal))
                {
                    timerCounter = int.Parse(timeVal);
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
                string[] recentFileNameParts = Path.GetFileNameWithoutExtension(recentFilePath).Split('_');
                string key = $"{recentFileNameParts[0]}_{recentFileNameParts[1]}";

                // Check if there are matching files in the "restFiles" list
                List<string> matchingRestFiles = restFiles.Where(filePath => filePath.Contains(key)).ToList();

                recentFiles.AddRange(matchingRestFiles);

            }
            return recentFiles.OrderBy(item => item).ToList();
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
                Console.WriteLine($"Error: {ex.Message}");
                // Handle any exceptions that may occur during directory access
            }
        }






        private void logToScreen(string message)
        {
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
            if (!CheckProgress())
            {
                return;
            }
            btn1.Enabled = false;
            btn2.Enabled = false;
            btn3.Enabled = false;
            btn11.Enabled = false;
            btn22.Enabled = false;
            btn33.Enabled = false;
            btn9.Enabled = false;
            btnReset.Enabled = false;
            Directory.Delete(txtPrint.Text, true);
            Directory.CreateDirectory(txtPrint.Text);
            // isTimerFinnished = false;


            logToScreen("מתחיל בתהליך");
            logToScreen("מעתיק קבצים לארכיון...");
            archive(num, sdirs);
            logToScreen("מעתיק קבצים לתקיה זמנית...");
            SendToWorkingFolder(num, sdirs);
            if (IsMerge)
            {
                logToScreen("מתחיל בהמרת הקבצים...");
                if (!Convert(num, sdirs))
                {
                    MessageBox.Show("הפעולה נכשלה, יש לבחון קובץ לוג");
                    return;
                }
                logToScreen("מתחיל באיחוד הקבצים");
                if (!Merge(num, sdirs))
                {
                    MessageBox.Show("הפעולה נכשלה, יש לבחון קובץ לוג");
                    return;
                }
                logToScreen("מתחיל הדפסת הקבצים");
                Print(num);
            }
            else
            {
                logToScreen("מתחיל הדפסת הקבצים");
                printNoMerge(num);
            }


            logToScreen("ממתין לספירה לאחור להסתיים");
            CountDown();
            updateDirectories();
            //while (!isTimerFinnished)
            //{

            //}
            // Thread.Sleep(10000);



        }

        private void printNoMerge(string num)
        {

            var workingDir = Path.Combine(txtWorkFol.Text, num);
            var lwFiles = Directory.GetFiles(workingDir, "*.*", SearchOption.AllDirectories);
            foreach (var file in lwFiles)
            {
                File.Move(file, Path.Combine(txtPrint.Text, Path.GetFileName(file)));
            }


            var printingDir = txtPrint.Text;
            var lFiles = Directory.GetFiles(printingDir, "*.*", SearchOption.TopDirectoryOnly);
            foreach (var file in lFiles)
            {
                var ext = Path.GetExtension(file).Split('.')[1].ToLower();
                if (ext == "pdf")
                {
                    logToScreen("מדפיס קובץ  - " + Path.GetFileName(file));
                    Application.DoEvents();
                    using (var pdf = PdfDocument.FromFile(file))
                    {
                        pdf.Print(Printer9);
                        Thread.Sleep(1000);
                    }
                        
                }
                else if (ext == "jpeg" || ext == "jpg" || ext == "tif" || ext == "tiff")
                {
                    try
                    {
                        PrintImage(file);

                        while (PrintImageObj)
                        {
                            Thread.Sleep(500);
                        }

                    }
                    catch (Exception ex)
                    {
                        SimpleLog.Error("Print image failed");
                        SimpleLog.Log(ex);
                    }
                    finally
                    {
                        Thread.Sleep(1000);
                    }

                }
                else
                {
                    try
                    {
                        PrintFile(file);
                        while (PrintImageObj)
                        {
                            Thread.Sleep(500);
                        }
                    }
                    catch (Exception ex)
                    {
                        SimpleLog.Error("Print other failed");
                        SimpleLog.Log(ex);
                    }
                    finally
                    {
                        Thread.Sleep(1000);
                    }


                }

            }
            
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
            chbox1.Enabled = true;
            timerCounter = int.Parse(timeVal);
            try
            {
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
            timer1 = new System.Timers.Timer();
            timer1.Enabled = true;
            timer1.Elapsed += (object sender, System.Timers.ElapsedEventArgs e) =>
            {
                timerCounter--;
                if (InvokeRequired)
                {
                    Invoke(new MethodInvoker(() =>
                    {
                        lblCountDown.Text = timerCounter.ToString();
                    }));
                }
                else
                {
                    lblCountDown.Text = timerCounter.ToString();
                }
                if (timerCounter == 0)
                {
                    timer1.Stop();
                    if (InvokeRequired)
                    {
                        Invoke(new MethodInvoker(() =>
                        {
                            lblTime.Visible = false;
                            lblCountDown.Text = string.Empty;
                            logToScreen("מנקה את סביבת העבודה");
                            btnReset.Enabled = true;
                            updateScreen();
                            MessageBox.Show("הפעולה הסתיימה בהצלחה");
                        }));
                    }
                    else
                    {
                        lblTime.Visible = false;
                        lblCountDown.Text = string.Empty;
                        logToScreen("מנקה את סביבת העבודה");
                        btnReset.Enabled = true;
                        updateScreen();
                        MessageBox.Show("הפעולה הסתיימה בהצלחה");
                    }


                }
            };
            timer1.Interval = 1000; // 1 second

            timer1.Start();
            lblCountDown.Text = timerCounter.ToString();

            Application.DoEvents();
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
                var files = new List<string>();
                for (int i = 0; i < dirs.Length; i++)
                {
                    var dir = dirs[i];
                    logToScreen("מתחיל באיחוד קבצים לתקיה - " + Path.GetFileName(dir));
                    var lFiles = Directory.GetFiles(dir, "*.pdf", SearchOption.TopDirectoryOnly);
                    files.Clear();
                    var names = new List<string>();
                    foreach (var lfile in lFiles)
                    {
                        files.Add(lfile);
                        var fn = Path.GetFileName(Path.GetDirectoryName(lfile)) + "-" + Path.GetFileNameWithoutExtension(lfile).Split('_')[0] + "_" + Path.GetFileNameWithoutExtension(lfile).Split('_')[1];
                        names.Add(fn);
                    }
                    var unique_items = new HashSet<string>(names);
                    var pdfDocuments = new List<PdfDocument>();
                    var dic = new Dictionary<string, List<string>>();
                    foreach (string s in unique_items)
                    {
                        var fname = Path.GetFileNameWithoutExtension(s).Split('-')[1];
                        var sorted = files.FindAll(f => f.Contains(fname)).OrderBy(o => o).ToList();
                        dic[s] = sorted;
                    }
                    foreach (string s in dic.Keys)
                    {

                        logToScreen("מאחד קובץ - " + s + ".pdf");
                        var mergeName = s;
                        var arr = dic[s];
                        foreach (var path in arr)
                        {
                            pdfDocuments.Add(PdfDocument.FromFile(path));
                        }
                        // var newdir = Path.GetDirectoryName(arr[0]);
                        var mergedPdfDocument = PdfDocument.Merge(pdfDocuments);
                        mergedPdfDocument.SaveAs(Path.Combine(txtPrint.Text, mergeName + ".pdf"));
                        pdfDocuments = new List<PdfDocument>();
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
        private void Print(string num)
        {
            var printingDir = txtPrint.Text;
            var lFiles = Directory.GetFiles(printingDir, "*.pdf", SearchOption.TopDirectoryOnly);

            foreach (var file in lFiles)
            {
                try
                {
                    var pprint = string.Empty;
                    switch (num)
                    {
                        case "1":
                            pprint = Printer1;
                            break;
                        case "2":
                            pprint = Printer2;
                            break;
                        case "3":
                            pprint = Printer3;
                            break;
                        case "1_999":
                            pprint = Printer11;
                            break;
                        case "2_999":
                            pprint = Printer22;
                            break;
                        case "3_999":
                            pprint = Printer33;
                            break;
                        default:
                            pprint = Printer1;
                            break;
                    }
                    logToScreen("מדפיס קובץ  - " + Path.GetFileName(file));
                    Application.DoEvents();
                    using (var pdf = PdfDocument.FromFile(file))
                    {
                        pdf.Print(pprint);
                    }
                        
                    Thread.Sleep(1000);
                    //var doc = pdf.GetPrintDocument();
                    //doc.EndPrint += Doc_EndPrint;
                    //doc.PrinterSettings.PrinterName = num == "1" ? Printer1 : num == "2" ? Printer2 : Printer3;
                    //doc.Print();
                }
                catch (Exception ex)
                {
                    SimpleLogger.SimpleLog.Log(ex);
                    logToScreen("הדפסת קבצים נכשלה לקובץ - " + file);
                    MessageBox.Show("הדפסת קבצים נכשלה לקובץ - " + file);
                }
            }


        }

        //private void Doc_EndPrint(object sender, PrintEventArgs e)
        //{
        //    throw new NotImplementedException();
        //}
        private void PrintImage(string fileName)
        {
            using (printImg = System.Drawing.Image.FromFile(fileName))
            {
                var h = printImg.Height;
                var w = printImg.Width;
                pages = 0;
                using (PrintDocument printDoc = new PrintDocument())
                {
                    PrintImageObj = true;
                    if (!printDoc.PrinterSettings.IsValid)
                    {
                        MessageBox.Show(@"Printer settings are invalid");
                        return;
                    }
                    printDoc.PrinterSettings.PrinterName = Printer9;
                    printDoc.PrintPage += TiffPrintPage;
                    printDoc.DefaultPageSettings.Landscape = w > h;
                    printDoc.EndPrint += PrintTiffFileEndded;
                    printDoc.PrintController = new StandardPrintController();
                    printDoc.Print();
                }
            }
                
        }
        private void PrintFile(string fileName)
        {
            using (Process shellProcess = new Process())
            {
                shellProcess.StartInfo.FileName = fileName;
                shellProcess.StartInfo.CreateNoWindow = true;
                shellProcess.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                shellProcess.StartInfo.Arguments = "\"" + Printer9 + "\"";
                shellProcess.StartInfo.Verb = "PrintTo";
                shellProcess.Start();
                if (shellProcess.HasExited == false)
                {
                    shellProcess.WaitForExit(10000);
                }

                shellProcess.EnableRaisingEvents = true;

                shellProcess.Close();
            }
        }
        private void PrintTiffFileEndded(object sender, PrintEventArgs e)
        {
            PrintImageObj = false;
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
                foreach (var sdir in sdirs)
                {
                    if (dir.Contains(sdir))
                    {
                        Directory.Delete(dir, true);
                    }
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
                    Properties.Settings.Default.SourcePath = fbd.SelectedPath;
                    Properties.Settings.Default.Save();
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
                    Properties.Settings.Default.MainPath = fbd.SelectedPath;
                    Properties.Settings.Default.Save();
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
                    Properties.Settings.Default.WorkingPath = fbd.SelectedPath;
                    Properties.Settings.Default.Save();
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
                    Properties.Settings.Default.PrintPath = fbd.SelectedPath;
                    Properties.Settings.Default.Save();
                }
            }
        }

        private void cboPrinter1_SelectedIndexChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.Printer1 = cboPrinter1.SelectedItem.ToString();
            Properties.Settings.Default.Save();
            Printer1 = cboPrinter1.SelectedItem.ToString();

        }

        private void cboPrinter2_SelectedIndexChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.Printer2 = cboPrinter2.SelectedItem.ToString();
            Properties.Settings.Default.Save();
            Printer2 = cboPrinter2.SelectedItem.ToString();
        }

        private void cboPrinter3_SelectedIndexChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.Printer3 = cboPrinter3.SelectedItem.ToString();
            Properties.Settings.Default.Save();
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
                    Properties.Settings.Default.ArchivePath = fbd.SelectedPath;
                    Properties.Settings.Default.Save();
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
            Properties.Settings.Default.TimerPeriod = txtTimer.Text;
            Properties.Settings.Default.SrcMinPeriod = txtSrcMinutes.Text;
            Properties.Settings.Default.Save();
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
            }

            for (int i = 0; i < col.Count; i++)
            {
                sdirs.Add(col[i].ToString());
            }

            Start(num, sdirs);

        }

        private void button1_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void cboPrinter11_SelectedIndexChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.Printer11 = cboPrinter11.SelectedItem.ToString();
            Properties.Settings.Default.Save();
            Printer11 = cboPrinter11.SelectedItem.ToString();
        }

        private void cboPrinter22_SelectedIndexChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.Printer22 = cboPrinter22.SelectedItem.ToString();
            Properties.Settings.Default.Save();
            Printer22 = cboPrinter22.SelectedItem.ToString();
        }

        private void cboPrinter33_SelectedIndexChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.Printer33 = cboPrinter33.SelectedItem.ToString();
            Properties.Settings.Default.Save();
            Printer33 = cboPrinter33.SelectedItem.ToString();
        }

        private void cboPrinter9_SelectedIndexChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.Printer9 = cboPrinter9.SelectedItem.ToString();
            Properties.Settings.Default.Save();
            Printer9 = cboPrinter9.SelectedItem.ToString();
        }
    }


}

