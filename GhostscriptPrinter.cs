using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using SimpleLogger;

namespace MergeAndPrint
{
    /// <summary>
    /// Fast PDF printing: Ghostscript (a separate program, gswin64c.exe) converts the whole PDF to PCL 6 in a fraction
    /// of a second, and the result is handed to the same Windows print queue as a RAW job, so the print server delivers
    /// it exactly as it delivers Spire's jobs. Nothing draws pages on this PC and the printer driver is bypassed.
    ///
    /// Because the driver is bypassed, it does not put the queue's tray/duplex settings into the job, so we do:
    /// <see cref="ResolveSettings"/> reads them from Windows for the queue (the same per-user defaults Spire prints
    /// with) and they go into the job's PJL header. A value in <see cref="AppConfig.GhostscriptPrinters"/> overrides
    /// what is read from the queue.
    ///
    /// Settings and output checks follow Printers-POC2, which uses the same conversion in production at this client.
    /// Its own sending (raw socket :9100, job tracking, retries) is deliberately NOT reused: that is where its
    /// triple-printing bug came from, and port 9100 is blocked from the MergeAndPrint PC anyway.
    /// </summary>
    internal static class GhostscriptPrinter
    {
        private const int ConvertTimeoutMs = 120_000;
        private const string Uel = "\u001B%-12345X";

        /// <summary>
        /// Ghostscript can exit 0 while failing (e.g. a truncated PDF gives "Couldn't get page info" and a PCL file
        /// missing pages), so its output is scanned too. List taken from Printers-POC2, where it was learned the hard way.
        /// </summary>
        private static readonly string[] ErrorMarkers =
        {
            "Couldn't get page info",
            "Output may be incorrect",
            "**** Error: page not found",
            "Couldn't initialise file",
            "No pages will be processed"
        };

        private static readonly Lazy<string> FoundExe = new Lazy<string>(FindExecutable);

        /// <summary>Full path of gswin64c.exe, or null when Ghostscript is not installed.</summary>
        public static string ExecutablePath => FoundExe.Value;

        /// <summary>One log line describing the Ghostscript setup, written at start-up.</summary>
        public static string DescribeSetup()
        {
            var cfg = AppConfig.Current;
            var exe = ExecutablePath;
            var where = exe == null
                ? "NOT FOUND (install Ghostscript or set GhostscriptPath in config.json)"
                : exe;
            var printers = cfg.GhostscriptPrinters == null || cfg.GhostscriptPrinters.Count == 0
                ? "none (tray and duplex taken from each queue's Windows settings)"
                : string.Join("; ", cfg.GhostscriptPrinters.Select(p => $"'{p.Key}': tray={Show(p.Value?.Tray)}, duplex={Show(p.Value?.Duplex)}"));
            return $"Ghostscript: {where}, device={Device()}, printer settings: {printers}";
        }

        private static string Show(string value) => string.IsNullOrWhiteSpace(value) ? "default" : value.Trim();

        /// <summary>
        /// Convert <paramref name="file"/> to PCL 6 (in memory). Returns null and a reason when Ghostscript is missing
        /// or the conversion is not trustworthy; nothing has been sent to the printer at that point, so the caller can
        /// safely print the file another way.
        /// </summary>
        public static byte[] TryConvert(string file, GhostscriptPrinterSettings settings, out string failure)
        {
            failure = null;
            var exe = ExecutablePath;
            if (exe == null)
            {
                failure = "Ghostscript is not installed (no gswin64c.exe found)";
                return null;
            }

            var tempDir = Path.Combine(Path.GetTempPath(), "MergeAndPrint-gs");
            Directory.CreateDirectory(tempDir);
            var output = Path.Combine(tempDir, Guid.NewGuid().ToString("N") + ".pcl");
            try
            {
                var psi = new ProcessStartInfo(exe)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                foreach (var arg in new[] { "-dSAFER", "-dBATCH", "-dNOPAUSE", "-dQUIET",
                                            "-sDEVICE=" + Device(), "-r600",
                                            "-sPAPERSIZE=a4", "-dFIXEDMEDIA", "-dPDFFitPage" })
                    psi.ArgumentList.Add(arg);
                var duplex = DuplexMode(settings);
                if (duplex != null && duplex != "OFF")
                {
                    psi.ArgumentList.Add("-dDuplex");
                    if (duplex == "SHORTEDGE")
                        psi.ArgumentList.Add("-dTumble");
                }
                psi.ArgumentList.Add("-o");
                psi.ArgumentList.Add(output);
                psi.ArgumentList.Add(file);

                string stdout, stderr;
                using (var process = Process.Start(psi))
                {
                    var outTask = process.StandardOutput.ReadToEndAsync();
                    var errTask = process.StandardError.ReadToEndAsync();
                    if (!process.WaitForExit(ConvertTimeoutMs))
                    {
                        try { process.Kill(true); } catch { /* already gone */ }
                        failure = $"Ghostscript did not finish within {ConvertTimeoutMs / 1000} s";
                        return null;
                    }
                    process.WaitForExit();
                    stdout = outTask.Result;
                    stderr = errTask.Result;
                    if (process.ExitCode != 0)
                    {
                        failure = $"Ghostscript exit code {process.ExitCode}: {Shorten(stderr + " " + stdout)}";
                        return null;
                    }
                }

                // Scan BOTH streams: on the known-bad sample every error line arrives on stdout and stderr is empty.
                var marker = ErrorMarkers.FirstOrDefault(m =>
                    (stdout + "\n" + stderr).IndexOf(m, StringComparison.OrdinalIgnoreCase) >= 0);
                if (marker != null)
                {
                    failure = $"Ghostscript reported \"{marker}\" (output would be incomplete): {Shorten(stdout + " " + stderr)}";
                    return null;
                }

                if (!File.Exists(output) || new FileInfo(output).Length == 0)
                {
                    failure = "Ghostscript produced no output";
                    return null;
                }

                var pcl = File.ReadAllBytes(output);
                var job = WrapInPjl(pcl, Path.GetFileName(file), settings, out failure);
                return job;
            }
            catch (Exception ex)
            {
                failure = "Ghostscript could not be run: " + ex.Message;
                return null;
            }
            finally
            {
                try { File.Delete(output); } catch { /* temp file */ }
            }
        }

        /// <summary>
        /// Replace Ghostscript's own short PJL header with ours (job name, tray, duplex) and close the job with EOJ.
        /// Ghostscript's output is: UEL + its PJL lines + ") HP-PCL XL;..." stream + UEL.
        /// </summary>
        internal static byte[] WrapInPjl(byte[] pcl, string jobName, GhostscriptPrinterSettings settings, out string failure)
        {
            failure = null;
            var start = IndexOf(pcl, Encoding.ASCII.GetBytes(") HP-PCL XL"), 4096);
            if (start < 0)
            {
                failure = "Ghostscript output does not contain a PCL XL stream header";
                return null;
            }

            var header = new StringBuilder();
            header.Append(Uel).Append("@PJL JOB NAME=\"").Append(SafeJobName(jobName)).Append("\"\r\n");
            header.Append("@PJL SET RESOLUTION=600\r\n");
            if (Device() == "pxlmono")
                header.Append("@PJL SET RENDERMODE=GRAYSCALE\r\n");
            var duplex = DuplexMode(settings);
            if (duplex == "OFF")
                header.Append("@PJL SET DUPLEX=OFF\r\n");
            else if (duplex != null)
                header.Append("@PJL SET DUPLEX=ON\r\n@PJL SET BINDING=").Append(duplex).Append("\r\n");
            // Numeric form plus INPUTTRAY: this is what selects the tray on this client's HP printers in
            // Printers-POC2's production runs ("@PJL SET MEDIASOURCE=3" + "INPUTTRAY=3" -> paper from Tray 3).
            var tray = TrayName(settings);
            if (tray != null)
            {
                header.Append("@PJL SET MEDIASOURCE=").Append(tray).Append("\r\n");
                if (Regex.IsMatch(tray, @"^\d+$"))
                    header.Append("@PJL SET INPUTTRAY=").Append(tray).Append("\r\n");
            }
            header.Append("@PJL ENTER LANGUAGE=PCLXL\r\n");

            var body = new ReadOnlySpan<byte>(pcl, start, pcl.Length - start);
            var endsWithUel = body.EndsWith(Encoding.ASCII.GetBytes(Uel));
            // HP form: UEL immediately followed by @PJL EOJ, then a final UEL.
            var footer = (endsWithUel ? "" : Uel) + "@PJL EOJ\r\n" + Uel;

            var head = Encoding.ASCII.GetBytes(header.ToString());
            var foot = Encoding.ASCII.GetBytes(footer);
            var job = new byte[head.Length + body.Length + foot.Length];
            head.CopyTo(job, 0);
            body.CopyTo(new Span<byte>(job, head.Length, body.Length));
            foot.CopyTo(job, head.Length + body.Length);
            return job;
        }

        /// <summary>
        /// Hand the job to the Windows print queue as RAW data: the spooler and print server deliver it unchanged.
        /// Returns once the whole job is in the queue (like Spire's Print). Nothing is retried, so a failure can
        /// never print the same document twice; a job that fails part-way is aborted rather than left half-sent.
        /// </summary>
        public static void SendRaw(string printerName, string documentName, byte[] job)
        {
            if (!OpenPrinter(printerName, out var printer, IntPtr.Zero))
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"Cannot open printer '{printerName}'");
            try
            {
                var doc = new DOC_INFO_1 { pDocName = documentName, pOutputFile = null, pDataType = "RAW" };
                if (StartDocPrinter(printer, 1, doc) == 0)
                    throw new Win32Exception(Marshal.GetLastWin32Error(), $"Cannot start a print job on '{printerName}'");

                var sent = false;
                try
                {
                    if (!StartPagePrinter(printer))
                        throw new Win32Exception(Marshal.GetLastWin32Error(), "StartPagePrinter failed");
                    var handle = GCHandle.Alloc(job, GCHandleType.Pinned);
                    try
                    {
                        var offset = 0;
                        while (offset < job.Length)
                        {
                            if (!WritePrinter(printer, handle.AddrOfPinnedObject() + offset, job.Length - offset, out var written) || written <= 0)
                                throw new Win32Exception(Marshal.GetLastWin32Error(),
                                    $"Writing to '{printerName}' failed after {offset} of {job.Length} bytes");
                            offset += written;
                        }
                    }
                    finally
                    {
                        handle.Free();
                    }
                    EndPagePrinter(printer);
                    sent = true;
                }
                finally
                {
                    if (sent)
                    {
                        if (!EndDocPrinter(printer))
                            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Cannot finish the print job on '{printerName}'");
                    }
                    else
                    {
                        AbortPrinter(printer);
                    }
                }
            }
            finally
            {
                ClosePrinter(printer);
            }
        }

        private static string Device()
        {
            var device = (AppConfig.Current.GhostscriptDevice ?? "").Trim().ToLowerInvariant();
            return device == "pxlcolor" ? "pxlcolor" : "pxlmono";
        }

        private static GhostscriptPrinterSettings SettingsFor(string printerName)
        {
            var all = AppConfig.Current.GhostscriptPrinters;
            if (all == null || string.IsNullOrEmpty(printerName))
                return null;
            return all.FirstOrDefault(p => string.Equals(p.Key?.Trim(), printerName.Trim(), StringComparison.OrdinalIgnoreCase)).Value;
        }

        /// <summary>null = printer default, otherwise OFF, LONGEDGE or SHORTEDGE.</summary>
        private static string DuplexMode(GhostscriptPrinterSettings settings)
        {
            var value = (settings?.Duplex ?? "").Trim().Replace(" ", "").ToUpperInvariant();
            switch (value)
            {
                case "": case "DEFAULT": return null;
                case "OFF": case "NO": case "FALSE": case "SIMPLEX": return "OFF";
                case "SHORTEDGE": case "SHORT": case "TUMBLE": return "SHORTEDGE";
                default: return "LONGEDGE";   // ON, YES, TRUE, LONGEDGE
            }
        }

        /// <summary>null = printer default; "2" or "Tray2" -> 2; MANUAL / AUTO passed through.</summary>
        internal static string TrayName(GhostscriptPrinterSettings settings)
        {
            var value = (settings?.Tray ?? "").Trim().Replace(" ", "").ToUpperInvariant();
            if (value.Length == 0 || value == "DEFAULT")
                return null;
            var numbered = Regex.Match(value, @"^(?:TRAY)?(\d+)$");
            if (numbered.Success)
                return numbered.Groups[1].Value;
            return Regex.IsMatch(value, @"^[A-Z0-9]+$") ? value : null;
        }

        /// <summary>
        /// The tray and duplex to put in the job for <paramref name="printerName"/>: the queue's own settings in Windows
        /// (what Spire would print with), each overridden by a non-empty value in config.json GhostscriptPrinters.
        /// <paramref name="description"/> says where each value came from, for the log.
        /// </summary>
        public static GhostscriptPrinterSettings ResolveSettings(string printerName, out string description)
        {
            var configured = SettingsFor(printerName);
            var queue = CachedQueueSettings(printerName, out var queueTray, out var queueDuplex);

            var resolved = new GhostscriptPrinterSettings();
            string trayFrom, duplexFrom;
            if (!string.IsNullOrWhiteSpace(configured?.Tray))
            {
                resolved.Tray = configured.Tray.Trim();
                trayFrom = "config.json";
            }
            else
            {
                resolved.Tray = queue?.Tray ?? "";
                trayFrom = "queue: " + queueTray;
            }
            if (!string.IsNullOrWhiteSpace(configured?.Duplex))
            {
                resolved.Duplex = configured.Duplex.Trim();
                duplexFrom = "config.json";
            }
            else
            {
                resolved.Duplex = queue?.Duplex ?? "";
                duplexFrom = "queue: " + queueDuplex;
            }

            var tray = TrayName(resolved);
            var duplex = DuplexMode(resolved);
            description = $"tray={tray ?? "printer default"} ({trayFrom}), " +
                          $"duplex={(duplex == null ? "printer default" : duplex.ToLowerInvariant())} ({duplexFrom})";
            return resolved;
        }

        /// <summary>How long a queue's settings are reused. Reading them takes 0.4-1 s, too slow to repeat for every file.</summary>
        private static readonly TimeSpan QueueCacheTime = TimeSpan.FromMinutes(1);

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (DateTime Read, GhostscriptPrinterSettings Settings, string Tray, string Duplex)>
            QueueCache = new System.Collections.Concurrent.ConcurrentDictionary<string, (DateTime, GhostscriptPrinterSettings, string, string)>(StringComparer.OrdinalIgnoreCase);

        private static GhostscriptPrinterSettings CachedQueueSettings(string printerName, out string trayText, out string duplexText)
        {
            var key = printerName ?? "";
            if (QueueCache.TryGetValue(key, out var hit) && DateTime.UtcNow - hit.Read < QueueCacheTime)
            {
                trayText = hit.Tray;
                duplexText = hit.Duplex;
                return hit.Settings;
            }
            var settings = ReadQueueSettings(printerName, out trayText, out duplexText);
            if (settings != null)
                QueueCache[key] = (DateTime.UtcNow, settings, trayText, duplexText);
            return settings;
        }

        /// <summary>
        /// Read the queue's paper source and duplex from Windows: the same defaults Spire's print job starts from.
        /// The paper source is matched by name ("Tray 2", "מגש 2" -> 2); a source that is not a numbered tray
        /// (automatic select, manual feed, a name we do not recognise) leaves the tray to the printer's default.
        /// Returns null when the queue cannot be read; <paramref name="trayText"/> and <paramref name="duplexText"/>
        /// describe what was found, for the log.
        /// </summary>
        internal static GhostscriptPrinterSettings ReadQueueSettings(string printerName, out string trayText, out string duplexText)
        {
            trayText = duplexText = "not read";
            if (string.IsNullOrWhiteSpace(printerName))
                return null;
            try
            {
                var ps = new System.Drawing.Printing.PrinterSettings { PrinterName = printerName };
                if (!ps.IsValid)
                {
                    trayText = duplexText = "printer not installed";
                    return null;
                }

                var result = new GhostscriptPrinterSettings();
                var source = ps.DefaultPageSettings.PaperSource;
                var sourceName = source?.SourceName ?? "";
                result.Tray = TrayFromSourceName(sourceName) ?? "";
                trayText = $"'{sourceName}' (bin {source?.RawKind})" + (result.Tray.Length == 0 ? " = not a numbered tray" : "");

                switch (ps.Duplex)
                {
                    case System.Drawing.Printing.Duplex.Simplex: result.Duplex = "Off"; break;
                    case System.Drawing.Printing.Duplex.Vertical: result.Duplex = "LongEdge"; break;
                    case System.Drawing.Printing.Duplex.Horizontal: result.Duplex = "ShortEdge"; break;
                    default: result.Duplex = ""; break;
                }
                duplexText = ps.Duplex.ToString();
                return result;
            }
            catch (Exception ex)
            {
                trayText = duplexText = "could not read (" + ex.Message + ")";
                return null;
            }
        }

        /// <summary>"Tray 2", "HP Tray 2", "מגש 2" -> "2". Manual feed and anything else -> null (printer default).</summary>
        internal static string TrayFromSourceName(string sourceName)
        {
            var name = sourceName ?? "";
            if (Regex.IsMatch(name, @"manual|ידני", RegexOptions.IgnoreCase))
                return null;   // "Manual Feed in Tray 1" is not Tray 1
            var match = Regex.Match(name, @"(?:tray|מגש)\s*(\d+)", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : null;
        }

        /// <summary>
        /// Log, once at start-up and off the UI thread, the tray and duplex the custom engine will use for each
        /// configured printer, so a log file shows it before anything is printed.
        /// </summary>
        public static void LogQueueSettingsInBackground(IEnumerable<string> printerNames)
        {
            var names = printerNames.Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (names.Count == 0)
                return;
            System.Threading.Tasks.Task.Run(() =>
            {
                foreach (var name in names)
                {
                    try
                    {
                        ResolveSettings(name, out var description);
                        SimpleLog.Info($"Custom engine settings for '{name}': {description}");
                    }
                    catch (Exception ex)
                    {
                        SimpleLog.Warning($"Custom engine settings for '{name}' could not be read: {ex.Message}");
                    }
                }
            });
        }

        private static string SafeJobName(string name)
        {
            var safe = Regex.Replace(name ?? "MergeAndPrint", @"[^A-Za-z0-9_.\-]", "_");
            return safe.Length > 80 ? safe.Substring(0, 80) : safe;
        }

        private static int IndexOf(byte[] data, byte[] pattern, int limit)
        {
            var end = Math.Min(data.Length, limit) - pattern.Length;
            for (int i = 0; i <= end; i++)
            {
                var match = true;
                for (int j = 0; j < pattern.Length && match; j++)
                    match = data[i + j] == pattern[j];
                if (match)
                    return i;
            }
            return -1;
        }

        private static string Shorten(string text)
        {
            var clean = Regex.Replace(text ?? "", @"\s+", " ").Trim();
            return clean.Length > 300 ? clean.Substring(0, 300) + "..." : clean;
        }

        /// <summary>GhostscriptPath from config, then a copy next to the exe (gs\bin), then the newest installed version.</summary>
        private static string FindExecutable()
        {
            var configured = AppConfig.Current.GhostscriptPath;
            if (!string.IsNullOrWhiteSpace(configured))
                return File.Exists(configured.Trim()) ? configured.Trim() : null;

            var candidates = new List<string>
            {
                Path.Combine(AppContext.BaseDirectory, "gs", "bin", "gswin64c.exe")
            };
            foreach (var root in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) })
            {
                var gsRoot = Path.Combine(root, "gs");
                if (!Directory.Exists(gsRoot))
                    continue;
                candidates.AddRange(Directory.GetDirectories(gsRoot, "gs*")
                    .OrderByDescending(d => ParseVersion(Path.GetFileName(d)))
                    .Select(d => Path.Combine(d, "bin", "gswin64c.exe")));
            }
            return candidates.FirstOrDefault(File.Exists);
        }

        private static Version ParseVersion(string folder) =>
            Version.TryParse(folder.Substring(2), out var v) ? v : new Version(0, 0);

        // --- winspool ---

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private class DOC_INFO_1
        {
            public string pDocName;
            public string pOutputFile;
            public string pDataType;
        }

        [DllImport("winspool.drv", EntryPoint = "OpenPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool OpenPrinter(string pPrinterName, out IntPtr phPrinter, IntPtr pDefault);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool ClosePrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", EntryPoint = "StartDocPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern int StartDocPrinter(IntPtr hPrinter, int level, [In] DOC_INFO_1 pDocInfo);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool EndDocPrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool StartPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool EndPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, int dwCount, out int dwWritten);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool AbortPrinter(IntPtr hPrinter);
    }

    /// <summary>Per-printer job settings for the Ghostscript engine (the queue's own settings do not apply to RAW jobs).</summary>
    public class GhostscriptPrinterSettings
    {
        /// <summary>Paper tray: empty = the queue's setting in Windows, or 1, 2, 3 (Tray 1...), MANUAL, AUTO.</summary>
        public string Tray { get; set; } = "";

        /// <summary>Two-sided: empty = the queue's setting in Windows, Off, LongEdge (book style) or ShortEdge.</summary>
        public string Duplex { get; set; } = "";
    }
}
