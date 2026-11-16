using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using SimpleLogger;

namespace MergeAndPrint
{
    /// <summary>
    /// Diagnostic, run from the configuration tab: for every configured printer, send a short test document through
    /// the printer driver with that queue's own settings (trays per page, staple, duplex...), but written to a file
    /// instead of the printer. NOTHING IS PRINTED. The files show exactly what the driver sends, so the "custom"
    /// engine can be taught to send the same. Everything ends up in one zip next to the log, to send to the developer.
    /// </summary>
    internal static class PrinterCapture
    {
        private const int TestPages = 5;

        /// <summary>Runs on the calling thread; returns the zip path. <paramref name="progress"/> gets one line per step.</summary>
        public static string Run(IEnumerable<KeyValuePair<string, string>> printers, Action<string> progress)
        {
            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var baseDir = Directory.Exists(SimpleLog.LogDir) ? SimpleLog.LogDir : AppContext.BaseDirectory;
            var dir = Path.Combine(baseDir, "PrinterCapture_" + stamp);
            Directory.CreateDirectory(dir);

            var report = new StringBuilder();
            report.AppendLine($"MergeAndPrint {FileVersionInfo.GetVersionInfo(Environment.ProcessPath ?? "").FileVersion}, " +
                              $"machine {Environment.MachineName}, user {Environment.UserName}, {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine($"Each queue got a {TestPages}-page test document three ways, through its driver, written to files (nothing printed).");
            report.AppendLine();

            // One capture per queue, even when several buttons use the same queue.
            var byQueue = printers.Where(p => !string.IsNullOrWhiteSpace(p.Value))
                .GroupBy(p => p.Value.Trim(), StringComparer.OrdinalIgnoreCase).ToList();
            var index = 0;
            foreach (var queue in byQueue)
            {
                index++;
                var name = queue.Key;
                var usedBy = string.Join(", ", queue.Select(p => p.Key));
                progress?.Invoke($"בודק מדפסת {index} מתוך {byQueue.Count}: {name}");
                report.AppendLine($"=== {name}  (used for: {usedBy})");
                try
                {
                    var describeWatch = Stopwatch.StartNew();
                    report.Append(Describe(name));
                    report.AppendLine($"  reading the settings above took {describeWatch.ElapsedMilliseconds} ms");
                    // Two ways, same test pages: plain Windows printing with the queue's settings untouched (like
                    // Ctrl+P in Word), and Spire exactly as the app prints. Per-page trays ("special pages") that
                    // show up in one and not the other tell where they get lost.
                    var ways = new (string Key, string Title, Func<string, string> Capture)[]
                    {
                        ("windows", "plain Windows printing (like Ctrl+P)", f => CaptureNative(name, f)),
                        ("dotnet", ".NET PrintDocument", f => CaptureDotNet(name, f)),
                        ("spire", "Spire, as the app prints", f => CaptureSpire(name, f, dir)),
                    };
                    foreach (var way in ways)
                    {
                        progress?.Invoke($"בודק מדפסת {index} מתוך {byQueue.Count} ({way.Key}): {name}");
                        var file = Path.Combine(dir, $"{index:00}_{SafeFileName(name)}_{way.Key}.prn");
                        var watch = Stopwatch.StartNew();
                        string steps;
                        try
                        {
                            steps = way.Capture(file);
                        }
                        catch (Exception ex)
                        {
                            report.AppendLine($"  {way.Title}: FAILED after {watch.ElapsedMilliseconds} ms: {ex.Message}");
                            SimpleLog.Warning($"Printer capture: '{name}' {way.Key} failed: {ex.Message}");
                            continue;
                        }
                        var spooled = watch.ElapsedMilliseconds;
                        var size = WaitForFile(file);
                        var line = size > 0
                            ? $"captured {size / 1024} KB in {watch.ElapsedMilliseconds} ms -> {Path.GetFileName(file)}"
                            : $"NO OUTPUT FILE after {watch.ElapsedMilliseconds} ms (the driver may not support print-to-file, " +
                              "or the job is waiting in the queue, e.g. for manual duplex; such a job can be deleted)";
                        steps += $", Windows writing the file {watch.ElapsedMilliseconds - spooled} ms (includes a 2 s check that it is complete)";
                        report.AppendLine($"  {way.Title}: {line}");
                        report.AppendLine("    steps: " + steps);
                        var trays = size > 0 ? PageTrays(file) : "";
                        if (size > 0)
                            report.AppendLine("    " + trays);
                        SimpleLog.Info($"Printer capture: '{name}' {way.Key}: {line}; steps: {steps}; {trays}");
                    }
                }
                catch (Exception ex)
                {
                    report.AppendLine("  FAILED: " + ex.Message);
                    SimpleLog.Warning($"Printer capture: '{name}' failed: {ex.Message}");
                }
                report.AppendLine();
            }

            File.WriteAllText(Path.Combine(dir, "report.txt"), report.ToString(), Encoding.UTF8);
            CopyTodaysLog(dir);

            var zip = dir + ".zip";
            ZipFile.CreateFromDirectory(dir, zip);
            try { Directory.Delete(dir, true); } catch { /* the zip is what matters */ }
            SimpleLog.Info("Printer capture finished: " + zip);
            return zip;
        }

        /// <summary>The settings Windows reports for the queue (what .NET and Spire can see of them).</summary>
        private static string Describe(string printerName)
        {
            var sb = new StringBuilder();
            try
            {
                var ps = new PrinterSettings { PrinterName = printerName };
                if (!ps.IsValid)
                    return "  printer is not installed on this PC\n";
                var page = ps.DefaultPageSettings;
                sb.AppendLine($"  paper: {page.PaperSize?.PaperName} ({page.PaperSize?.RawKind}), source: '{page.PaperSource?.SourceName}' (bin {page.PaperSource?.RawKind}), " +
                              $"duplex: {ps.Duplex}, color: {page.Color}, landscape: {page.Landscape}");
                sb.AppendLine("  sources: " + string.Join(", ",
                    ps.PaperSources.Cast<PaperSource>().Select(s => $"'{s.SourceName}'={s.RawKind}")));
                sb.AppendLine("  custom engine would use: " + DescribeCustom(printerName));
            }
            catch (Exception ex)
            {
                sb.AppendLine("  could not read settings: " + ex.Message);
            }
            return sb.ToString();
        }

        private static string DescribeCustom(string printerName)
        {
            GhostscriptPrinter.ResolveSettings(printerName, out var text);
            return text;
        }

        /// <summary>
        /// Print the test pages with plain Windows calls (CreateDC / StartDoc / StartPage / EndPage / EndDoc), the
        /// way Word or Acrobat print: the queue's own settings, untouched. StartDoc's output file makes Windows write
        /// the driver's output there instead of sending it to the printer. Returns how long each step took.
        /// </summary>
        private static string CaptureNative(string printerName, string file)
        {
            var steps = new StepTimer();
            var hdc = CreateDC("WINSPOOL", printerName, null, IntPtr.Zero);
            if (hdc == IntPtr.Zero)
                throw new InvalidOperationException($"could not open the printer (error {Marshal.GetLastWin32Error()})");
            steps.Mark("open printer");
            try
            {
                var info = new DOCINFO
                {
                    cbSize = Marshal.SizeOf<DOCINFO>(),
                    lpszDocName = "MergeAndPrint printer check (not printed)",
                    lpszOutput = file,   // to the file, never to the printer
                };
                if (StartDoc(hdc, ref info) <= 0)
                    throw new InvalidOperationException($"could not start the job (error {Marshal.GetLastWin32Error()}); nothing was sent");
                steps.Mark("start job");
                var ok = false;
                try
                {
                    using (var font = new Font("Arial", 28, FontStyle.Bold))
                    using (var small = new Font("Arial", 12))
                    {
                        for (var page = 1; page <= TestPages; page++)
                        {
                            StartPage(hdc);
                            using (var g = Graphics.FromHdc(hdc))
                            {
                                g.PageUnit = GraphicsUnit.Display;   // 1/100 inch, like PrintDocument
                                g.DrawString($"Page {page} of {TestPages}", font, Brushes.Black, 80, 120);
                                g.DrawString($"MergeAndPrint printer check (Windows) - {printerName}", small, Brushes.Black, 80, 200);
                                g.DrawRectangle(Pens.Black, 60, 100, 500, 140);
                            }
                            EndPage(hdc);
                            steps.Mark($"page {page}");
                        }
                    }
                    ok = true;
                }
                finally
                {
                    if (ok)
                        EndDoc(hdc);
                    else
                        AbortDoc(hdc);
                }
                steps.Mark("finish job");
            }
            finally
            {
                DeleteDC(hdc);
            }
            return steps.ToString();
        }

        /// <summary>
        /// Print the test pages with .NET's PrintDocument and the queue's default settings (what Spire builds on),
        /// redirected to <paramref name="file"/> with PrintToFile. Returns how long each step took.
        /// </summary>
        private static string CaptureDotNet(string printerName, string file)
        {
            var steps = new StepTimer();
            using (var doc = new PrintDocument())
            {
                doc.PrinterSettings.PrinterName = printerName;
                if (!doc.PrinterSettings.IsValid)
                    throw new InvalidOperationException("printer is not installed on this PC");
                // Safety first: without a file name nothing may be printed at all.
                doc.PrinterSettings.PrintToFile = true;
                doc.PrinterSettings.PrintFileName = file;
                if (!doc.PrinterSettings.PrintToFile || string.IsNullOrEmpty(doc.PrinterSettings.PrintFileName))
                    throw new InvalidOperationException("could not redirect the job to a file; nothing was sent");
                doc.DocumentName = "MergeAndPrint printer check (not printed)";
                doc.PrintController = new StandardPrintController();
                steps.Mark("open printer");

                var page = 0;
                using (var font = new Font("Arial", 28, FontStyle.Bold))
                using (var small = new Font("Arial", 12))
                {
                    doc.QueryPageSettings += (s, e) => { if (page == 0) steps.Mark("start job"); };
                    doc.PrintPage += (s, e) =>
                    {
                        steps.Mark(page == 0 ? "start first page" : $"page {page}");
                        page++;
                        e.Graphics.DrawString($"Page {page} of {TestPages}", font, Brushes.Black, 80, 120);
                        e.Graphics.DrawString($"MergeAndPrint printer check (.NET) - {printerName}", small, Brushes.Black, 80, 200);
                        e.Graphics.DrawRectangle(Pens.Black, 60, 100, 500, 140);
                        e.HasMorePages = page < TestPages;
                    };
                    doc.Print();
                    steps.Mark($"page {page} and finish job");
                }
            }
            return steps.ToString();
        }

        /// <summary>
        /// Print the same test pages exactly the way the app prints with Spire (see Form1.PrintPdfWithSpire),
        /// redirected to <paramref name="file"/> with Spire's PrintToFile. Returns how long each step took.
        /// </summary>
        private static string CaptureSpire(string printerName, string file, string dir)
        {
            var steps = new StepTimer();
            var pdf = TestPdf(dir);
            using (var doc = new Spire.Pdf.PdfDocument())
            {
                doc.LoadFromFile(pdf);
                doc.PrintSettings.PrinterName = printerName;
                if (!new PrinterSettings { PrinterName = printerName }.IsValid)
                    throw new InvalidOperationException("printer is not installed on this PC");
                doc.PrintSettings.PrintToFile(file);   // to the file, never to the printer
                doc.PrintSettings.DocumentName = "MergeAndPrint printer check (not printed)";
                doc.PrintSettings.PrintController = new StandardPrintController();
                steps.Mark("load");
                var page = 0;
                doc.PrintSettings.BeginPrint += (s, e) => steps.Mark("prepare");
                doc.PrintSettings.QueryPageSettings += (s, e) => { if (page == 0) steps.Mark("start job"); };
                doc.PrintSettings.PrintPage += (s, e) => steps.Mark(page++ == 0 ? "start first page" : $"page {page - 1}");
                doc.Print();
                steps.Mark($"page {page} and finish job");
            }
            return steps.ToString();
        }

        /// <summary>A PDF with <see cref="TestPages"/> numbered A4 pages, made once per check.</summary>
        private static string TestPdf(string dir)
        {
            var path = Path.Combine(dir, "test-pages.pdf");
            if (File.Exists(path))
                return path;
            using (var pdf = new Spire.Pdf.PdfDocument())
            {
                var font = new Spire.Pdf.Graphics.PdfFont(Spire.Pdf.Graphics.PdfFontFamily.Helvetica, 28f);
                var small = new Spire.Pdf.Graphics.PdfFont(Spire.Pdf.Graphics.PdfFontFamily.Helvetica, 12f);
                for (var i = 1; i <= TestPages; i++)
                {
                    var page = pdf.Pages.Add(Spire.Pdf.PdfPageSize.A4);
                    page.Canvas.DrawString($"Page {i} of {TestPages}", font, Spire.Pdf.Graphics.PdfBrushes.Black, 60, 80);
                    page.Canvas.DrawString("MergeAndPrint printer check (Spire)", small, Spire.Pdf.Graphics.PdfBrushes.Black, 60, 130);
                }
                pdf.SaveToFile(path);
            }
            return path;
        }

        /// <summary>
        /// Which tray each page asks for in a PCL 6 (PCL XL) job, plus the job's PJL tray / staple / duplex lines.
        /// Page values follow PCL XL: 1 = auto select, 2 = manual feed, 3 = Tray 1, 4 = Tray 2, 5 = Tray 3, 7 = Tray 4;
        /// HP writes higher trays as text, e.g. "TRAY5".
        /// </summary>
        internal static string PageTrays(string file)
        {
            byte[] b;
            try { b = File.ReadAllBytes(file); }
            catch (Exception ex) { return "could not read the file: " + ex.Message; }

            var trays = new List<string>();
            for (var i = 2; i + 1 < b.Length; i++)
            {
                if (b[i] != 0xF8 || b[i + 1] != 0x26)
                    continue;
                if (b[i - 2] == 0xC0 && b[i - 1] <= 20)
                {
                    trays.Add(TrayLabel(b[i - 1]));
                    continue;
                }
                for (var len = 1; len <= 16; len++)
                {
                    var k = i - len - 4;
                    if (k >= 0 && b[k] == 0xC8 && b[k + 1] == 0xC1 && b[k + 2] == len && b[k + 3] == 0)
                    {
                        var text = Encoding.ASCII.GetString(b, k + 4, len);
                        if (text.All(c => c >= 0x20 && c < 0x7F))
                            trays.Add(text);
                        break;
                    }
                }
            }

            var head = Encoding.ASCII.GetString(b, 0, Math.Min(b.Length, 8192));
            var pjl = System.Text.RegularExpressions.Regex.Matches(head, @"@PJL SET (MEDIASOURCE|INPUTTRAY|PROCESSINGOPTION|DUPLEX|BINDING|OUTBIN)\s*=\s*([^\r\n]*)")
                .Cast<System.Text.RegularExpressions.Match>()
                .Select(m => $"{m.Groups[1].Value}={m.Groups[2].Value.Trim()}")
                .Distinct();
            var pages = trays.Count == 0
                ? "no PCL 6 page trays found (not a PCL 6 driver?)"
                : "tray per page: " + string.Join(", ", trays.Select((t, n) => $"{n + 1}={t}"));
            return $"{pages}; PJL: {string.Join(" ", pjl)}";
        }

        private static string TrayLabel(byte value)
        {
            switch (value)
            {
                case 0: return "default(0)";
                case 1: return "auto(1)";
                case 2: return "manual(2)";
                case 3: return "Tray1(3)";
                case 4: return "Tray2(4)";
                case 5: return "Tray3(5)";
                case 6: return "envelope(6)";
                case 7: return "Tray4(7)";
                default: return $"source({value})";
            }
        }

        private sealed class StepTimer
        {
            private readonly Stopwatch watch = Stopwatch.StartNew();
            private readonly List<string> marks = new List<string>();
            private long last;

            public void Mark(string step)
            {
                var now = watch.ElapsedMilliseconds;
                marks.Add($"{step} {now - last} ms");
                last = now;
            }

            public override string ToString() => string.Join(", ", marks);
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DOCINFO
        {
            public int cbSize;
            public string lpszDocName;
            public string lpszOutput;
            public string lpszDatatype;
            public int fwType;
        }

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateDC(string driver, string device, string output, IntPtr devMode);

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int StartDoc(IntPtr hdc, ref DOCINFO info);

        [DllImport("gdi32.dll")] private static extern int StartPage(IntPtr hdc);
        [DllImport("gdi32.dll")] private static extern int EndPage(IntPtr hdc);
        [DllImport("gdi32.dll")] private static extern int EndDoc(IntPtr hdc);
        [DllImport("gdi32.dll")] private static extern int AbortDoc(IntPtr hdc);
        [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hdc);

        /// <summary>
        /// Print() returns once the job is spooled; the spooler writes the file afterwards. Wait until the file has
        /// content and stops growing (up to a minute). Returns its size, 0 when it stayed empty, -1 when missing.
        /// </summary>
        private static long WaitForFile(string file)
        {
            var deadline = DateTime.UtcNow.AddSeconds(60);
            long last = -1;
            var stableSince = DateTime.MaxValue;
            while (DateTime.UtcNow < deadline)
            {
                long size;
                try { size = File.Exists(file) ? new FileInfo(file).Length : -1; }
                catch { size = -1; }
                if (size > 0 && size == last)
                {
                    if (stableSince == DateTime.MaxValue)
                        stableSince = DateTime.UtcNow;
                    else if (DateTime.UtcNow - stableSince > TimeSpan.FromSeconds(2))
                        return size;
                }
                else
                {
                    stableSince = DateTime.MaxValue;
                }
                last = size;
                System.Threading.Thread.Sleep(250);
            }
            return last;
        }

        private static void CopyTodaysLog(string dir)
        {
            try
            {
                var log = SimpleLog.FileName;
                if (File.Exists(log))
                {
                    // The log is open for writing; copy through a shared read.
                    using (var src = new FileStream(log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    using (var dst = File.Create(Path.Combine(dir, Path.GetFileName(log))))
                        src.CopyTo(dst);
                }
            }
            catch (Exception ex)
            {
                SimpleLog.Warning("Printer capture: could not copy the log: " + ex.Message);
            }
        }

        private static string SafeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var safe = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim('_', ' ');
            return safe.Length == 0 ? "printer" : safe;
        }
    }
}
