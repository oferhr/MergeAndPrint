using IronPdf;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Drawing;
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
        private System.Timers.Timer timer1;
        private int timerCounter = 30;
        private string timeVal;
        private string CurrentDirectory = null;
        public Form1()
        {
            InitializeComponent();

            
             
           // int.TryParse(ConfigurationManager.AppSettings["WaitSeconds"].ToString(), out timerCounter);
            var printers = GetAllPrinterList();
            cboPrinter1.Items.AddRange(printers.ToArray());
            cboPrinter2.Items.AddRange(printers.ToArray());
            cboPrinter3.Items.AddRange(printers.ToArray());
            cboPrinter11.Items.AddRange(printers.ToArray());
            cboPrinter22.Items.AddRange(printers.ToArray());
            cboPrinter33.Items.AddRange(printers.ToArray());

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
            var dirPrinter11 = Properties.Settings.Default.Printer22;
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
                var path = Path.Combine(txtMain.Text, num);
                var dirs = Directory.GetDirectories(path);
                foreach (var dir in dirs)
                {
                    if (Path.GetFileName(dir).StartsWith("999_"))
                    {
                        if (!Directory.Exists(Path.Combine(txtMain.Text, num.ToString() + "_999")))
                        {
                            Directory.CreateDirectory(Path.Combine(txtMain.Text, num.ToString() + "_999"));
                        }
                        Directory.Move(dir, Path.Combine(Path.Combine(txtMain.Text, num.ToString() + "_999"), Path.GetFileName(dir)));
                    }
                }
                
            }
            //txtDetails.Clear();
            updateScreen();
            updateDirectories();
            Clean();
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
                    dirInfo = new DirectoryInfo(path1);
                    var lDir = dirInfo.GetDirectories("*", SearchOption.TopDirectoryOnly).ToList();
                    lb1.Text = lDir.Count() + " ספקים ";
                    dirInfos.Add("1 - " + lb1.Text);
                   
                }
                path1 = Path.Combine(txtMain.Text, "2");
                if (Directory.Exists(path1))
                {
                    dirInfo = new DirectoryInfo(path1);
                    var lDir = dirInfo.GetDirectories("*", SearchOption.TopDirectoryOnly).ToList();
                    lb2.Text = lDir.Count() + " ספקים ";
                    dirInfos.Add("2 - " + lb2.Text);
                    
                }
                path1 = Path.Combine(txtMain.Text, "3");
                if (Directory.Exists(path1))
                {
                    dirInfo = new DirectoryInfo(path1);
                    var lDir = dirInfo.GetDirectories("*", SearchOption.TopDirectoryOnly).ToList();
                    lb3.Text = lDir.Count() + " ספקים ";
                    dirInfos.Add("3 - " + lb3.Text);
                    
                }
                path1 = Path.Combine(txtMain.Text, "1_999");
                if (Directory.Exists(path1))
                {
                    dirInfo = new DirectoryInfo(path1);
                    var lDir = dirInfo.GetDirectories("*", SearchOption.TopDirectoryOnly).ToList();
                    lb11.Text = lDir.Count() + " ספקים ";
                    dirInfos.Add("1_999 - " + lb11.Text);
                  
                }
                else
                {
                    lb11.Text = " 0 ספקים ";
                }
                path1 = Path.Combine(txtMain.Text, "2_999");
                if (Directory.Exists(path1))
                {
                    dirInfo = new DirectoryInfo(path1);
                    var lDir = dirInfo.GetDirectories("*", SearchOption.TopDirectoryOnly).ToList();
                    lb22.Text = lDir.Count() + " ספקים ";
                    dirInfos.Add("2_999 - " + lb22.Text);
                   
                }
                else
                {
                    lb22.Text = " 0 ספקים ";
                }
                path1 = Path.Combine(txtMain.Text, "3_999");
                if (Directory.Exists(path1))
                {
                    dirInfo = new DirectoryInfo(path1);
                    var lDir = dirInfo.GetDirectories("*", SearchOption.TopDirectoryOnly).ToList();
                    lb33.Text = lDir.Count() + " ספקים ";
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
                

            }
            return dirInfos;
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
        private void Start(string num, List<string> sdirs)
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
            btnReset.Enabled = false;
            Directory.Delete(txtPrint.Text, true);
            Directory.CreateDirectory(txtPrint.Text);
            // isTimerFinnished = false;


            logToScreen("מתחיל בתהליך");
            logToScreen("מעתיק קבצים לארכיון...");
            archive(num, sdirs);
            logToScreen("מעתיק קבצים לתקיה זמנית...");
            SendToWorkingFolder(num, sdirs);
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
            logToScreen("ממתין לספירה לאחור להסתיים");
            CountDown();
            updateDirectories();
            //while (!isTimerFinnished)
            //{

            //}
            // Thread.Sleep(10000);



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
            chbox1.Enabled = true;
            timerCounter = int.Parse(timeVal);
            Directory.Delete(txtPrint.Text, true);
            Directory.CreateDirectory(txtPrint.Text);
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
                        if (!Directory.Exists(Path.Combine(txtMain.Text, num.ToString() + "_999")))
                        {
                            Directory.CreateDirectory(Path.Combine(txtMain.Text, num.ToString() + "_999"));
                        }
                        Directory.Move(dir, Path.Combine(Path.Combine(txtMain.Text, num.ToString() + "_999"), Path.GetFileName(dir)));
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
                                var converted = IronPdf.ImageToPdfConverter.ImageToPdf(file);
                                converted.SaveAs(Path.Combine(Path.GetDirectoryName(file), fn + ".pdf"));
                                //ImageToPdfConverter.ImageToPdf(file, ImageBehavior.CropPage).SaveAs(Path.Combine(Path.GetDirectoryName(file), fn + ".pdf"));
                            }
                            else if (ext.ToLower() == ".tiff" || ext.ToLower() == ".tif")
                            {
                                var converted = IronPdf.ImageToPdfConverter.ImageToPdf(file);
                                converted.SaveAs(Path.Combine(Path.GetDirectoryName(file), fn + ".pdf"));
                                //Image tiffImage = Image.FromFile(file);
                                //Image[] images = SplitTIFFImage(tiffImage);
                                //ImageToPdfConverter.ImageToPdf(images, ImageBehavior.CropPage).SaveAs(Path.Combine(Path.GetDirectoryName(file), fn + ".pdf"));
                            }
                            else if (ext.ToLower() == ".html" || ext.ToLower() == ".htm")
                            {
                                using (var Renderer = new HtmlToPdf())
                                {
                                    Renderer.PrintOptions.InputEncoding = Encoding.GetEncoding(1255);
                                    Renderer.PrintOptions.PrintHtmlBackgrounds = false;
                                    Renderer.PrintOptions.PaperSize = PdfPrintOptions.PdfPaperSize.A4;
                                    Renderer.PrintOptions.CssMediaType = PdfPrintOptions.PdfCssMediaType.Print;
                                    //Renderer.PrintOptions.EnableJavaScript = true;
                                    //Renderer.PrintOptions.ViewPortWidth = 1280;
                                    //Renderer.PrintOptions.RenderDelay = 500; //milliseconds
                                    Renderer.PrintOptions.MarginLeft = 10;
                                    Renderer.PrintOptions.MarginRight = 10;
                                    Renderer.PrintOptions.MarginTop = 10;
                                    Renderer.PrintOptions.MarginBottom = 10;
                                    Renderer.PrintOptions.Zoom = 120;

                                    using (var PDF = Renderer.RenderHTMLFileAsPdf(file))
                                    {
                                        var OutputPath = Path.Combine(Path.GetDirectoryName(file), fn + ".pdf");
                                        PDF.SaveAs(OutputPath);
                                    }

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
                    var pdf = PdfDocument.FromFile(file);
                    pdf.Print(pprint);
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

        private Image[] SplitTIFFImage(Image tiffImage)
        {
            int frameCount = tiffImage.GetFrameCount(FrameDimension.Page);
            Image[] images = new Image[frameCount];
            Guid objGuid = tiffImage.FrameDimensionsList[0];
            FrameDimension objDimension = new FrameDimension(objGuid);
            for (int i = 0; i < frameCount; i++)
            {
                tiffImage.SelectActiveFrame(objDimension, i);
                using (MemoryStream ms = new MemoryStream())
                {
                    tiffImage.Save(ms, ImageFormat.Tiff);
                    images[i] = Image.FromStream(ms);
                }
            }
            return images;
        }
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
                CopyFolder(diSource, diTarget);
                DelSource(num);
            }
            else
            {
                foreach (var dir in sdirs)
                {
                    diSource = new DirectoryInfo(Path.Combine(baseSource, dir));
                    diTarget = new DirectoryInfo(Path.Combine(baseTarget, dir));
                    CopyFolder(diSource, diTarget);
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
            var baseTarget = diTarget.FullName;
            var baseSource = diSource.FullName;
            if (!isPicked)
            {
                CopyFolder(diSource, diTarget);
            }
            else
            {
                foreach (var dir in sdirs)
                {
                    diSource = new DirectoryInfo(Path.Combine(baseSource, dir));
                    diTarget = new DirectoryInfo(Path.Combine(baseTarget, dir));
                    CopyFolder(diSource, diTarget);
                }
            }
        }
        private void CopyFolder(DirectoryInfo source, DirectoryInfo target)
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
                DirectoryInfo nextTargetSubDir = target.CreateSubdirectory(diSourceSubDir.Name);
                CopyFolder(diSourceSubDir, nextTargetSubDir);
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
            if (string.IsNullOrEmpty(txtArchive.Text) || string.IsNullOrEmpty(txtMain.Text) || string.IsNullOrEmpty(txtPrint.Text) || string.IsNullOrEmpty(txtWorkFol.Text) || string.IsNullOrEmpty(txtTimer.Text))
            {
                MessageBox.Show("פרטים חסרים בלשונית קונפיגורציה");
                return false;
            }
            Properties.Settings.Default.TimerPeriod = txtTimer.Text;
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

        
    }


}

