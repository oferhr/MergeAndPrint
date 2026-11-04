using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using SimpleLogger;

namespace MergeAndPrint
{
    /// <summary>
    /// At start-up, finds out which installed network printers can read PDF files directly (their firmware has a
    /// PDF interpreter), and writes one line per printer to the log. Nothing is printed: for each printer we look up
    /// its IP address from its Windows port (asking the print server for shared printers), then ask the printer
    /// which languages it reads with the PJL command "@PJL INFO CONFIG" on port 9100.
    /// The answer decides whether a printer could later be sent PDF files directly instead of drawing every page.
    /// Turn off with "CheckPrintersOnStartup": false in config.json.
    /// </summary>
    internal static class PrinterCheck
    {
        private const int DefaultRawPort = 9100;
        private const int ConnectTimeoutMs = 3000;
        private const int ReadTimeoutMs = 4000;
        private const int WmiTimeoutSeconds = 5;

        private enum Verdict { Yes, No, Unknown, Skipped }

        private sealed class Result
        {
            public Verdict Verdict;
            public string Text;
        }

        public static void StartInBackground(IEnumerable<string> installedPrinters, IEnumerable<string> configuredPrinters)
        {
            if (!AppConfig.Current.CheckPrintersOnStartup)
            {
                SimpleLog.Info("Printer check: skipped (CheckPrintersOnStartup is false in config.json)");
                return;
            }
            var printers = installedPrinters.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct().ToList();
            var configured = new HashSet<string>(
                configuredPrinters.Where(p => !string.IsNullOrWhiteSpace(p)), StringComparer.OrdinalIgnoreCase);
            Task.Run(() => Run(printers, configured));
        }

        private static void Run(List<string> printers, HashSet<string> configured)
        {
            try
            {
                var watch = Stopwatch.StartNew();
                SimpleLog.Info($"Printer check: checking {printers.Count} installed printer(s) for direct PDF support " +
                    "(no pages are printed; * = used by this app)");

                var results = printers.Select(p => Task.Run(() => CheckSafely(p))).ToArray();
                Task.WaitAll(results);

                for (int i = 0; i < printers.Count; i++)
                {
                    var mark = configured.Contains(printers[i]) ? "* " : "";
                    SimpleLog.Info($"Printer check: {mark}'{printers[i]}' -> {results[i].Result.Text}");
                }

                string Names(Verdict v) => string.Join(", ", printers.Where((p, i) => results[i].Result.Verdict == v)
                    .Select(p => configured.Contains(p) ? "*" + p : p).DefaultIfEmpty("-"));
                SimpleLog.Info($"Printer check finished in {watch.ElapsedMilliseconds} ms. " +
                    $"PDF YES: {Names(Verdict.Yes)} | PDF NO: {Names(Verdict.No)} | UNKNOWN: {Names(Verdict.Unknown)}");
            }
            catch (Exception ex)
            {
                SimpleLog.Warning("Printer check failed: " + ex.Message);
            }
        }

        private static Result CheckSafely(string printer)
        {
            try
            {
                return Check(printer);
            }
            catch (Exception ex)
            {
                return new Result { Verdict = Verdict.Unknown, Text = "check failed: " + ex.Message + " -> UNKNOWN" };
            }
        }

        private static Result Check(string printer)
        {
            var info = QueryWmi(null, "SELECT PortName, ServerName, ShareName, DriverName FROM Win32_Printer WHERE Name = '"
                + Escape(printer) + "'").FirstOrDefault();
            if (info == null)
                return new Result { Verdict = Verdict.Unknown, Text = "not found in Windows -> UNKNOWN" };

            var port = (info["PortName"] as string ?? "").Trim();
            var server = (info["ServerName"] as string ?? "").Trim().TrimStart('\\');
            var driver = info["DriverName"] as string ?? "";
            var where = $"port '{port}'" + (server.Length > 0 ? $" on server {server}" : "") + $", driver '{driver}'";

            if (IsLocalOrVirtualPort(port))
                return new Result { Verdict = Verdict.Skipped, Text = $"not a network printer ({where}) -> skipped" };

            var address = ResolveAddress(port, server, out var rawPort, out var how);
            if (address == null)
                return new Result { Verdict = Verdict.Unknown, Text = $"IP address not found ({where}; {how}) -> UNKNOWN" };

            var target = $"{address}:{rawPort} ({how}; {where})";
            if (!TryQueryPjl(address, rawPort, out var reply, out var error))
                return new Result { Verdict = Verdict.Unknown, Text = $"{target} -> no answer: {error} -> UNKNOWN" };

            var model = ParseModel(reply);
            var languages = ParseLanguages(reply);
            var modelText = model == null ? "" : $"model '{model}', ";
            if (languages.Count == 0)
            {
                return new Result
                {
                    Verdict = Verdict.Unknown,
                    Text = $"{target} -> {modelText}no language list in the reply: \"{Shorten(reply)}\" -> UNKNOWN"
                };
            }
            var pdf = languages.Any(l => l.IndexOf("PDF", StringComparison.OrdinalIgnoreCase) >= 0);
            return new Result
            {
                Verdict = pdf ? Verdict.Yes : Verdict.No,
                Text = $"{target} -> {modelText}languages: {string.Join(", ", languages)} -> PDF: {(pdf ? "YES" : "NO")}"
            };
        }

        /// <summary>Ports that are not a network printer: virtual printers, files, USB/parallel/serial, fax.</summary>
        private static bool IsLocalOrVirtualPort(string port)
        {
            if (port.Length == 0)
                return true;
            if (port.EndsWith(":", StringComparison.Ordinal))   // FILE:, PORTPROMPT:, nul:, LPT1:, COM1:, SHRFAX:, XPSPort:
                return true;
            return Regex.IsMatch(port, @"^(USB|DOT4|TS|LPT|COM)\d", RegexOptions.IgnoreCase)
                || port.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase)
                || port.StartsWith("PORTPROMPT", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Find the host behind a printer port: the local TCP/IP port settings, then the print server's
        /// (for shared printers such as \\dc-fs\Homer), then an IP or host name in the port name itself.
        /// </summary>
        private static string ResolveAddress(string port, string server, out int rawPort, out string how)
        {
            rawPort = DefaultRawPort;
            var notes = new List<string>();

            foreach (var machine in server.Length > 0 ? new[] { null, server } : new string[] { null })
            {
                try
                {
                    var tcp = QueryWmi(machine, "SELECT HostAddress, PortNumber, Protocol FROM Win32_TCPIPPrinterPort WHERE Name = '"
                        + Escape(port) + "'").FirstOrDefault();
                    if (tcp != null && !string.IsNullOrWhiteSpace(tcp["HostAddress"] as string))
                    {
                        // Protocol 1 = RAW (use its port number), 2 = LPR (the printer still listens on 9100 for PJL).
                        if (Convert.ToInt32(tcp["Protocol"] ?? 1) == 1 && tcp["PortNumber"] != null)
                            rawPort = Convert.ToInt32(tcp["PortNumber"]);
                        how = machine == null ? "from this PC's port settings" : $"from print server {machine}";
                        return ((string)tcp["HostAddress"]).Trim();
                    }
                }
                catch (Exception ex)
                {
                    notes.Add($"{(machine ?? "this PC")}: {ex.Message.Trim()}");
                }
            }

            if (port.StartsWith("WSD", StringComparison.OrdinalIgnoreCase))
            {
                how = "WSD port has no fixed IP" + (notes.Count > 0 ? "; " + string.Join("; ", notes) : "");
                return null;
            }

            var ip = Regex.Match(port, @"(?<![\d.])(\d{1,3}(?:\.\d{1,3}){3})(?![\d.])");
            if (ip.Success)
            {
                how = "from the port name";
                return ip.Groups[1].Value;
            }

            // A port named after the printer's host name, e.g. "hp-homer" or "hp-homer.corp.local".
            var host = Regex.Replace(port, @"^IP_", "", RegexOptions.IgnoreCase).Split(':')[0];
            if (Regex.IsMatch(host, @"^[A-Za-z0-9][A-Za-z0-9.\-]*$"))
            {
                try
                {
                    var found = Dns.GetHostAddresses(host).FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
                    if (found != null)
                    {
                        how = $"host name '{host}' from the port name";
                        return found.ToString();
                    }
                }
                catch (Exception ex)
                {
                    notes.Add($"DNS '{host}': {ex.Message.Trim()}");
                }
            }

            how = notes.Count > 0 ? string.Join("; ", notes) : "port name is not an IP or host name";
            return null;
        }

        /// <summary>Ask the printer for its model and installed languages. PJL INFO commands print nothing.</summary>
        private static bool TryQueryPjl(string host, int port, out string reply, out string error)
        {
            reply = null;
            error = null;
            try
            {
                using (var client = new TcpClient())
                {
                    if (!client.ConnectAsync(host, port).Wait(ConnectTimeoutMs))
                    {
                        error = $"could not connect within {ConnectTimeoutMs / 1000} s (port blocked or printer off?)";
                        return false;
                    }
                    var stream = client.GetStream();
                    stream.ReadTimeout = ReadTimeoutMs;

                    const string uel = "\u001B%-12345X";
                    var request = Encoding.ASCII.GetBytes(uel + "@PJL\r\n@PJL INFO ID\r\n@PJL INFO CONFIG\r\n" + uel);
                    stream.Write(request, 0, request.Length);

                    // Each INFO answer ends with a form feed; stop after both, on timeout, or when the printer closes.
                    var text = new StringBuilder();
                    var buffer = new byte[4096];
                    var watch = Stopwatch.StartNew();
                    while (watch.ElapsedMilliseconds < ReadTimeoutMs * 2 && text.ToString().Count(c => c == '\f') < 2)
                    {
                        int n;
                        try
                        {
                            n = stream.Read(buffer, 0, buffer.Length);
                        }
                        catch (System.IO.IOException)
                        {
                            break;   // read timeout: keep whatever arrived
                        }
                        if (n == 0)
                            break;
                        text.Append(Encoding.ASCII.GetString(buffer, 0, n));
                    }
                    reply = text.ToString();
                    if (reply.Trim().Length == 0)
                    {
                        error = "connected, but the printer sent no PJL reply";
                        return false;
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                error = (ex is AggregateException agg ? agg.InnerException ?? ex : ex).Message;
                return false;
            }
        }

        /// <summary>The line after "@PJL INFO ID", e.g. "HP LaserJet M609".</summary>
        private static string ParseModel(string reply)
        {
            var lines = SplitLines(reply);
            var at = lines.FindIndex(l => l.Trim().StartsWith("@PJL INFO ID", StringComparison.OrdinalIgnoreCase));
            if (at < 0 || at + 1 >= lines.Count)
                return null;
            var model = lines[at + 1].Trim().Trim('"').Trim();
            return model.Length == 0 || model.StartsWith("@PJL", StringComparison.OrdinalIgnoreCase) ? null : model;
        }

        /// <summary>The indented lines under "LANGUAGES [n ENUMERATED]", e.g. PCL, PCLXL, POSTSCRIPT, PDF.</summary>
        private static List<string> ParseLanguages(string reply)
        {
            var languages = new List<string>();
            var lines = SplitLines(reply);
            var at = lines.FindIndex(l => l.TrimStart().StartsWith("LANGUAGES", StringComparison.OrdinalIgnoreCase));
            if (at < 0)
                return languages;
            for (int i = at + 1; i < lines.Count; i++)
            {
                var line = lines[i];
                if (line.Length == 0 || !char.IsWhiteSpace(line[0]))
                    break;
                var value = line.Trim();
                if (value.Length > 0)
                    languages.Add(value);
            }
            return languages;
        }

        private static List<string> SplitLines(string text) =>
            text.Replace("\f", "\n").Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).ToList();

        private static string Shorten(string text)
        {
            var clean = Regex.Replace(text, @"[\x00-\x1F]+", " ").Trim();
            return clean.Length > 300 ? clean.Substring(0, 300) + "..." : clean;
        }

        private static List<ManagementBaseObject> QueryWmi(string machine, string query)
        {
            var path = machine == null ? @"root\cimv2" : $@"\\{machine}\root\cimv2";
            var options = new ConnectionOptions { Timeout = TimeSpan.FromSeconds(WmiTimeoutSeconds) };
            var scope = new ManagementScope(path, machine == null ? null : options);
            scope.Connect();
            var enumOptions = new EnumerationOptions { Timeout = TimeSpan.FromSeconds(WmiTimeoutSeconds), ReturnImmediately = false };
            using (var searcher = new ManagementObjectSearcher(scope, new ObjectQuery(query), enumOptions))
            {
                return searcher.Get().Cast<ManagementBaseObject>().ToList();
            }
        }

        /// <summary>Escape a value for a WQL string literal (backslashes and quotes).</summary>
        private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("'", "\\'");
    }
}
