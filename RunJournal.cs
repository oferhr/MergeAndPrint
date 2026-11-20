using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MergeAndPrint
{
    /// <summary>
    /// A run's progress on disk, so a run cut short by a crash (or by closing the app) can be continued
    /// from where it stopped: the stage it reached and, while printing, the state of every file.
    /// It is saved after every step, written to a temp file and swapped in, so a crash never leaves half a journal.
    /// It lives next to the print folder (not inside it, because the print folder is deleted and printed from,
    /// and not next to the exe, because every version installs to a new folder).
    /// The file exists only while a run is unfinished; a run that printed everything deletes it.
    /// </summary>
    public sealed class RunJournal
    {
        public const string FileName = "MergeAndPrint.run.json";

        /// <summary>Stages in the order a run goes through them.</summary>
        public static class Stages
        {
            /// <summary>Copying to the archive. The main folder is untouched.</summary>
            public const string Archive = "archive";
            /// <summary>Copying to the working folder. The main folder is untouched.</summary>
            public const string Copy = "copy";
            /// <summary>Copied to the working folder; from here the letters may already be gone from the main folder.</summary>
            public const string Copied = "copied";
            public const string Convert = "convert";
            public const string Merge = "merge";
            /// <summary>The files to print are in the print folder and listed in <see cref="Files"/>.</summary>
            public const string Print = "print";
        }

        /// <summary>Values of <see cref="JournalFile.Status"/>.</summary>
        public static class FileStates
        {
            public const string Pending = "pending";
            /// <summary>Handed to the printer and not confirmed: if the app died here, the file may or may not have printed.</summary>
            public const string Sending = "sending";
            public const string Printed = "printed";
            public const string Failed = "failed";
            /// <summary>The user chose not to print it again after a crash.</summary>
            public const string Skipped = "skipped";
        }

        public string AppVersion { get; set; } = "";
        public int ProcessId { get; set; }
        public DateTime Started { get; set; }
        public DateTime Updated { get; set; }

        /// <summary>The folder button that started the run: "1", "2_999", Shlomo's folder name...</summary>
        public string Folder { get; set; } = "";
        /// <summary>The suppliers ticked on screen, or null for the whole folder.</summary>
        public List<string> Selected { get; set; }
        public bool Merge { get; set; }
        public string Printer { get; set; } = "";
        public string Engine { get; set; } = "";

        public string MainPath { get; set; } = "";
        public string WorkingPath { get; set; } = "";
        public string PrintPath { get; set; } = "";
        public string ArchivePath { get; set; } = "";

        public string Stage { get; set; } = Stages.Archive;
        public List<JournalFile> Files { get; set; } = new List<JournalFile>();

        [JsonIgnore]
        public string FilePath { get; private set; }

        /// <summary>A save failed even after retrying: later saves try once, so a broken folder does not slow every file by seconds.</summary>
        [JsonIgnore]
        public bool SaveFailed { get; private set; }

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        /// <summary>
        /// Where the journal for this print folder lives: in the folder that holds the print folder
        /// (L:\Letters\Print -> L:\Letters\MergeAndPrint.run.json), or %LOCALAPPDATA%\MergeAndPrint for a drive root.
        /// </summary>
        public static string PathFor(string printPath)
        {
            string parent = null;
            if (!string.IsNullOrWhiteSpace(printPath))
            {
                try
                {
                    parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(Path.GetFullPath(printPath.Trim())));
                }
                catch (Exception)
                {
                    parent = null;
                }
            }
            if (string.IsNullOrEmpty(parent))
                parent = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MergeAndPrint");
            return Path.Combine(parent, FileName);
        }

        /// <summary>The unfinished run for this print folder, or null when there is none.</summary>
        public static RunJournal Load(string printPath)
        {
            var path = PathFor(printPath);
            if (!File.Exists(path))
                return null;
            var journal = JsonSerializer.Deserialize<RunJournal>(File.ReadAllText(path), JsonOptions)
                ?? throw new InvalidDataException("The run journal is empty: " + path);
            journal.FilePath = path;
            journal.Files ??= new List<JournalFile>();
            return journal;
        }

        public static RunJournal Begin(string folder, List<string> selected, bool merge, string printer,
            string mainPath, string workingPath, string printPath, string archivePath)
        {
            var journal = new RunJournal
            {
                AppVersion = System.Diagnostics.FileVersionInfo.GetVersionInfo(Environment.ProcessPath ?? "").FileVersion ?? "",
                ProcessId = Environment.ProcessId,
                Started = DateTime.Now,
                Folder = folder,
                Selected = selected?.ToList(),
                Merge = merge,
                Printer = printer ?? "",
                Engine = PrintEngines.Normalize(AppConfig.Current.PrintEngine),
                MainPath = mainPath,
                WorkingPath = workingPath,
                PrintPath = printPath,
                ArchivePath = archivePath,
                FilePath = PathFor(printPath)
            };
            journal.Save();
            return journal;
        }

        public void SetStage(string stage)
        {
            Stage = stage;
            Save();
        }

        /// <summary>Start the print stage with these files, in the order they will be printed.</summary>
        public void SetFilesToPrint(IEnumerable<string> files)
        {
            Files = files.Select(f => new JournalFile { Path = f, Status = FileStates.Pending }).ToList();
            Stage = Stages.Print;
            Save();
        }

        public void SetStatus(JournalFile file, string status)
        {
            file.Status = status;
            Save();
        }

        [JsonIgnore]
        public JournalFile Interrupted => Files.FirstOrDefault(f => f.Status == FileStates.Sending);

        /// <summary>Files that still have to be printed (pending, failed, or cut off while sending).</summary>
        [JsonIgnore]
        public List<JournalFile> Unprinted => Files.Where(f => !f.IsDone).ToList();

        [JsonIgnore]
        public int PrintedCount => Files.Count(f => f.Status == FileStates.Printed);

        /// <summary>
        /// Write to a temp file and swap it in, so a crash mid-write leaves the previous journal intact.
        /// Another program reading the file at that moment (antivirus, backup, someone opening it) blocks the swap,
        /// so it is retried for about 3 seconds. If it still fails, the run goes on with a warning in the log:
        /// a journal one step behind only means the user is asked about one more file after a crash.
        /// </summary>
        public void Save()
        {
            Updated = DateTime.Now;
            Exception last = null;
            var attempts = SaveFailed ? 1 : 15;
            for (var attempt = 1; attempt <= attempts; attempt++)
            {
                try
                {
                    WriteFile();
                    if (attempt > 1)
                        SimpleLogger.SimpleLog.Info($"Run journal saved on attempt {attempt} (the file was busy): {FilePath}");
                    SaveFailed = false;
                    return;
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    last = ex;
                    if (attempt < attempts)
                        System.Threading.Thread.Sleep(200);
                }
            }
            SaveFailed = true;
            SimpleLogger.SimpleLog.Warning($"Run journal could not be saved (stage '{Stage}'), the run continues: {FilePath}: " +
                PrinterCheck.Describe(last));
        }

        private void WriteFile()
        {
            var dir = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            var temp = FilePath + ".tmp";
            var bytes = JsonSerializer.SerializeToUtf8Bytes(this, JsonOptions);
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            File.Move(temp, FilePath, true);
        }

        public void Delete()
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    if (File.Exists(FilePath))
                        File.Delete(FilePath);
                    return;
                }
                catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException) && attempt < 15)
                {
                    System.Threading.Thread.Sleep(200);
                }
            }
        }

        /// <summary>Stage name for the user.</summary>
        public string StageText()
        {
            switch (Stage)
            {
                case Stages.Archive: return "העתקה לארכיון";
                case Stages.Copy: return "העתקה לתקיה הזמנית";
                case Stages.Copied: return "אחרי ההעתקה לתקיה הזמנית";
                case Stages.Convert: return "המרת הקבצים";
                case Stages.Merge: return "איחוד הקבצים";
                case Stages.Print: return "הדפסה";
                default: return Stage;
            }
        }

        /// <summary>One line for the log: what the run was doing when it stopped.</summary>
        public string Describe()
        {
            var text = $"run of folder '{Folder}' started {Started:dd/MM/yyyy HH:mm:ss} by version {AppVersion} (process {ProcessId}), " +
                $"last update {Updated:HH:mm:ss}, stage '{Stage}'";
            if (Stage == Stages.Print)
            {
                text += $", printed {PrintedCount} of {Files.Count}";
                var interrupted = Interrupted;
                if (interrupted != null)
                    text += $", was sending file {Files.IndexOf(interrupted) + 1}: {interrupted.Path}";
                var failed = Files.Count(f => f.Status == FileStates.Failed);
                if (failed > 0)
                    text += $", {failed} failed";
            }
            return text;
        }
    }

    public sealed class JournalFile
    {
        public string Path { get; set; } = "";
        public string Status { get; set; } = RunJournal.FileStates.Pending;

        [JsonIgnore]
        public bool IsDone => Status == RunJournal.FileStates.Printed || Status == RunJournal.FileStates.Skipped;
    }
}
