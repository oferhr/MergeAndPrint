using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.IO.Compression;
using System.Linq;
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
        private const int TestPages = 4;

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
            report.AppendLine($"Each queue got a {TestPages}-page test document through its driver, written to a file (nothing printed).");
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
                    var file = Path.Combine(dir, $"{index:00}_{SafeFileName(name)}.prn");
                    report.Append(Describe(name));
                    var watch = Stopwatch.StartNew();
                    CaptureOne(name, file);
                    var size = WaitForFile(file);
                    var line = size > 0
                        ? $"captured {size / 1024} KB in {watch.ElapsedMilliseconds} ms -> {Path.GetFileName(file)}"
                        : $"NO OUTPUT FILE after {watch.ElapsedMilliseconds} ms (the driver may not support print-to-file, " +
                          "or the job is waiting in the queue, e.g. for manual duplex; such a job can be deleted)";
                    report.AppendLine("  " + line);
                    SimpleLog.Info($"Printer capture: '{name}' {line}");
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
        /// Print a small test document with the queue's default settings, redirected to <paramref name="file"/>.
        /// PrintToFile makes Windows write the driver's output to the file instead of sending it to the printer.
        /// </summary>
        private static void CaptureOne(string printerName, string file)
        {
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

                var page = 0;
                using (var font = new Font("Arial", 28, FontStyle.Bold))
                using (var small = new Font("Arial", 12))
                {
                    doc.PrintPage += (s, e) =>
                    {
                        page++;
                        e.Graphics.DrawString($"Page {page} of {TestPages}", font, Brushes.Black, 80, 120);
                        e.Graphics.DrawString($"MergeAndPrint printer check - {printerName}", small, Brushes.Black, 80, 200);
                        e.Graphics.DrawRectangle(Pens.Black, 60, 100, 500, 140);
                        e.HasMorePages = page < TestPages;
                    };
                    doc.Print();
                }
            }
        }

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
