using System;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MergeAndPrint
{
    /// <summary>
    /// Finding out why the app closed by itself.
    /// A session file is written at start-up and deleted at a normal exit. If it is still there at the next
    /// start-up, the previous session ended without "Shutdown": a crash, or the process was ended. Windows writes
    /// such crashes to its Application event log (the faulting module and exception code), and they are copied
    /// from there into our log, because a crash inside a printer driver or a native library never reaches our code.
    /// </summary>
    internal static class CrashReport
    {
        private static readonly string Dir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MergeAndPrint");
        private static readonly string SessionFile = Path.Combine(Dir, "session.txt");

        private static Mutex _instance;

        /// <summary>
        /// Only one copy of the app may run: two copies share the same folders, and both would act on the same
        /// unfinished run. Returns false when another copy is already running (its window is brought forward).
        /// </summary>
        public static bool TryBecomeSingleInstance()
        {
            _instance = new Mutex(true, @"Local\MergeAndPrint.SingleInstance", out var createdNew);
            if (createdNew)
                return true;
            try
            {
                if (_instance.WaitOne(0))
                    return true;   // the other copy died holding it (abandoned mutex throws below instead)
            }
            catch (AbandonedMutexException)
            {
                return true;       // the previous copy crashed; this one owns the mutex now
            }
            BringOtherInstanceForward();
            return false;
        }

        private static void BringOtherInstanceForward()
        {
            try
            {
                var me = Process.GetCurrentProcess();
                foreach (var other in Process.GetProcessesByName(me.ProcessName).Where(p => p.Id != me.Id))
                {
                    var handle = other.MainWindowHandle;
                    if (handle == IntPtr.Zero)
                        continue;
                    ShowWindow(handle, 9 /* SW_RESTORE */);
                    SetForegroundWindow(handle);
                    break;
                }
            }
            catch (Exception)
            {
                // Only a convenience.
            }
        }

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        /// <summary>
        /// Record this session; if the previous one never reached Shutdown, log that, and look for its crash in the
        /// Windows event log in the background.
        /// </summary>
        public static void StartSession(string version)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                if (File.Exists(SessionFile))
                {
                    var previous = File.ReadAllText(SessionFile).Trim();
                    SimpleLogger.SimpleLog.Warning("The previous session did not close normally (no Shutdown): " + previous +
                        ". It crashed, or it was ended (Task Manager, sign-out, power).", false);
                    var since = ParseStarted(previous) ?? DateTime.Now.AddDays(-2);
                    Task.Run(() => LogWindowsCrashEvents(since));
                }
                File.WriteAllText(SessionFile,
                    $"process {Environment.ProcessId}, version {version}, started {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            }
            catch (Exception ex)
            {
                SimpleLogger.SimpleLog.Warning("Crash check at start-up failed: " + PrinterCheck.Describe(ex));
            }
        }

        public static void EndSession()
        {
            try
            {
                File.Delete(SessionFile);
            }
            catch (Exception)
            {
                // The next start-up only reports a false "did not close normally".
            }
        }

        private static DateTime? ParseStarted(string session)
        {
            var at = session.LastIndexOf("started ", StringComparison.Ordinal);
            return at >= 0 && DateTime.TryParse(session.Substring(at + 8), out var started) ? started : (DateTime?)null;
        }

        /// <summary>
        /// Copy the Windows "Application Error", ".NET Runtime", "Application Hang" and "Windows Error Reporting"
        /// entries about MergeAndPrint.exe since <paramref name="since"/> into our log.
        /// </summary>
        internal static void LogWindowsCrashEvents(DateTime since)
        {
            try
            {
                var from = since.AddMinutes(-1).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.000Z");
                var xpath = "*[System[(Provider[@Name='Application Error'] or Provider[@Name='.NET Runtime'] " +
                    "or Provider[@Name='Application Hang'] or Provider[@Name='Windows Error Reporting']) " +
                    $"and TimeCreated[@SystemTime>='{from}']]]";
                var query = new EventLogQuery("Application", PathType.LogName, xpath);
                var found = 0;
                using (var reader = new EventLogReader(query))
                {
                    for (var record = reader.ReadEvent(); record != null; record = reader.ReadEvent())
                    {
                        using (record)
                        {
                            string text;
                            try
                            {
                                text = record.FormatDescription();
                            }
                            catch (EventLogException)
                            {
                                text = null;
                            }
                            if (string.IsNullOrEmpty(text))
                                text = string.Join(" | ", record.Properties.Select(p => p.Value?.ToString()));
                            if (text.IndexOf("MergeAndPrint", StringComparison.OrdinalIgnoreCase) < 0)
                                continue;
                            found++;
                            if (text.Length > 4000)
                                text = text.Substring(0, 4000) + " ...";
                            SimpleLogger.SimpleLog.Error(
                                $"Windows event log: {record.ProviderName} event {record.Id} at {record.TimeCreated:yyyy-MM-dd HH:mm:ss}: {text}");
                        }
                    }
                }
                SimpleLogger.SimpleLog.Info(found == 0
                    ? $"Windows event log: no crash entries for MergeAndPrint since {since:yyyy-MM-dd HH:mm:ss} " +
                      "(none are written when the app is ended from Task Manager or by sign-out/shut-down)"
                    : $"Windows event log: {found} crash entr{(found == 1 ? "y" : "ies")} for MergeAndPrint since {since:yyyy-MM-dd HH:mm:ss}");
            }
            catch (Exception ex)
            {
                SimpleLogger.SimpleLog.Warning("Could not read the Windows event log: " + PrinterCheck.Describe(ex));
            }
        }
    }
}
