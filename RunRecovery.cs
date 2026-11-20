using SimpleLogger;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace MergeAndPrint
{
    /// <summary>
    /// Continuing a run that did not finish, from its <see cref="RunJournal"/>.
    /// Checked at start-up and before every run; until the user decides, no new run starts and Refresh
    /// leaves the print folder alone, so letters that were not printed are never deleted.
    /// </summary>
    public partial class Form1
    {
        /// <summary>The journal of the run in progress, null between runs.</summary>
        private RunJournal journal;

        private static readonly HashSet<string> ConvertedSourceExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".htm", ".html", ".jpg", ".jpeg", ".tif", ".tiff"
        };

        private void CheckUnfinishedRunAtStartup()
        {
            if (_running)
                return;
            ResolveUnfinishedRun();
        }

        private bool HasUnfinishedRun()
        {
            try
            {
                return File.Exists(RunJournal.PathFor(txtPrint.Text));
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// True when there is no unfinished run (any more) and a new run may start. Otherwise the user is
        /// asked what to do with it, and false is returned: the click that got here does not start a new run.
        /// </summary>
        private bool ResolveUnfinishedRun()
        {
            if (string.IsNullOrWhiteSpace(txtPrint.Text))
                return true;

            RunJournal pending;
            try
            {
                pending = RunJournal.Load(txtPrint.Text);
            }
            catch (Exception ex)
            {
                // Saved by swapping in a complete file, so this means someone edited or damaged it.
                var path = RunJournal.PathFor(txtPrint.Text);
                SimpleLog.Error("The run journal cannot be read and is set aside: " + path + ": " + PrinterCheck.Describe(ex));
                try { File.Move(path, path + "." + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".bad", true); } catch { /* reported below */ }
                MessageBox.Show("קובץ המצב של ההפעלה הקודמת פגום ולא ניתן להמשיך ממנו:" + Environment.NewLine + path + Environment.NewLine +
                    "הקבצים שבתיקיית ההדפסה לא נמחקו. יש לבדוק אותם לפני הדפסה חדשה.", "הפעלה קודמת",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            if (pending == null)
                return true;

            SimpleLog.Warning("Unfinished run found: " + pending.Describe(), false);
            if (!PrepareUnfinishedRun(pending))
                return false;
            if (!File.Exists(pending.FilePath))
                return true;   // nothing left to continue; it was cleaned up without asking

            var choice = RecoveryDialog.Ask(this, pending);
            SimpleLog.Info("Unfinished run: the user chose " + choice);
            switch (choice)
            {
                case RecoveryChoice.Continue:
                case RecoveryChoice.ContinueSkipInterrupted:
                    ResumeRun(pending, choice == RecoveryChoice.ContinueSkipInterrupted);
                    break;
                case RecoveryChoice.Discard:
                    DiscardRun(pending);
                    break;
                default:
                    logToScreen("הפעלה קודמת לא הסתיימה - יש להחליט מה לעשות בה לפני הדפסה חדשה");
                    break;
            }
            return false;
        }

        /// <summary>
        /// Look at the folders and bring the journal up to date with what is really on disk:
        /// a run stopped before the letters left the main folder is cleaned up without asking, and a run stopped
        /// while files were already in the print folder is moved on to its print stage.
        /// Returns false when something failed (already reported).
        /// </summary>
        private bool PrepareUnfinishedRun(RunJournal pending)
        {
            try
            {
                var workingDir = Path.Combine(pending.WorkingPath, pending.Folder);
                var mainDir = Path.Combine(pending.MainPath, pending.Folder);

                if (pending.Stage == RunJournal.Stages.Archive || pending.Stage == RunJournal.Stages.Copy)
                {
                    // The main folder still has every letter: drop the half-made copies and start over normally.
                    var removed = DeleteWorkingCopies(workingDir, mainDir);
                    pending.Delete();
                    SimpleLog.Info($"Unfinished run stopped before the letters left the main folder; removed {removed} copied file(s) " +
                        "from the working folder. Nothing to continue.");
                    logToScreen("הפעלה קודמת נעצרה לפני שהמכתבים הוזזו - לא אבד דבר, אפשר להדפיס שוב");
                    return true;
                }

                if (pending.Stage != RunJournal.Stages.Print)
                {
                    var workingFiles = Directory.Exists(workingDir)
                        ? Directory.GetFiles(workingDir, "*.*", SearchOption.AllDirectories) : new string[0];
                    var printFiles = Directory.Exists(pending.PrintPath)
                        ? Directory.GetFiles(pending.PrintPath, pending.Merge ? "*.pdf" : "*.*", SearchOption.TopDirectoryOnly) : new string[0];

                    if (!pending.Merge && printFiles.Length > 0)
                    {
                        // Shlomo's folder was being moved to the print folder: finish the move, then it is a print stage.
                        MoveWorkingFilesTo(workingDir, pending.Selected, pending.PrintPath);
                        pending.SetFilesToPrint(Directory.GetFiles(pending.PrintPath, "*.*", SearchOption.TopDirectoryOnly));
                    }
                    else if (workingFiles.Length == 0 && printFiles.Length > 0)
                    {
                        // The merge had finished (it empties the working folder) just before the print stage began.
                        pending.SetFilesToPrint(printFiles);
                    }
                    else if (workingFiles.Length == 0)
                    {
                        pending.Delete();
                        SimpleLog.Warning("Unfinished run has no files left in the working or print folder; nothing to continue. " +
                            "Its letters are in the archive: " + pending.ArchivePath);
                        MessageBox.Show("ההפעלה הקודמת לא הסתיימה, אבל לא נשארו קבצים להמשיך מהם." + Environment.NewLine +
                            "עותק של המכתבים נמצא בארכיון.", "הפעלה קודמת", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return true;
                    }
                }
                else
                {
                    // A merged run is fully in the print folder; what is left in the working folder are its sources
                    // (copies are in the archive). Left there, the next run would merge and print them again.
                    if (pending.Merge && Directory.Exists(workingDir) && Directory.EnumerateFiles(workingDir, "*.*", SearchOption.AllDirectories).Any())
                    {
                        Directory.Delete(workingDir, true);
                        SimpleLog.Info("Unfinished run: removed the already merged sources left in the working folder: " + workingDir);
                    }
                    var missing = pending.Unprinted.Where(f => !File.Exists(f.Path)).ToList();
                    if (missing.Count > 0)
                        SimpleLog.Warning($"Unfinished run: {missing.Count} unprinted file(s) are no longer in the print folder: " +
                            string.Join(", ", missing.Select(f => f.Path)));
                    if (pending.Unprinted.Count == 0)
                    {
                        pending.Delete();
                        SimpleLog.Info("Unfinished run had printed every file; nothing to continue.");
                        return true;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                SimpleLog.Log(ex);
                MessageBox.Show("בדיקת ההפעלה הקודמת נכשלה, יש לבחון קובץ לוג:" + Environment.NewLine + ex.Message,
                    "הפעלה קודמת", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        /// <summary>Continue the run where it stopped, with the same folder and selection.</summary>
        private void ResumeRun(RunJournal pending, bool skipInterrupted)
        {
            BeginRun($"Run resumed: {pending.Describe()}" + (skipInterrupted ? "; the interrupted file is skipped" : ""));
            try
            {
                Warmup.WaitIfRunning(logToScreen);
                journal = pending;
                logToScreen($"ממשיך את הדפסת תקיה {pending.Folder} מ-{pending.Started:HH:mm}");

                if (pending.Stage != RunJournal.Stages.Print)
                {
                    var stage = Stopwatch.StartNew();
                    var workingDir = Path.Combine(pending.WorkingPath, pending.Folder);
                    var mainDir = Path.Combine(pending.MainPath, pending.Folder);
                    Directory.CreateDirectory(workingDir);
                    Directory.CreateDirectory(pending.PrintPath);
                    // The run may have stopped before (or while) the copied letters were removed from the main folder:
                    // remove them now, or they would be printed again by the next run.
                    var removed = DeleteMainOriginals(workingDir, mainDir);
                    if (removed > 0)
                        SimpleLog.Info($"Resume: removed {removed} file(s) from the main folder that were already copied to the working folder");
                    DeleteConverterLeftovers(workingDir);
                    pending.SetStage(RunJournal.Stages.Copied);

                    // Selected 999_ suppliers may already have been moved on to the N_999 folder by Convert.
                    var selected = pending.Selected?.Where(d => Directory.Exists(Path.Combine(workingDir, d))).ToList();
                    if (!ProcessWorkingFolder(pending.Folder, selected, pending.Merge, stage))
                    {
                        FailRun();
                        return;
                    }
                }
                else
                {
                    var interrupted = pending.Interrupted;
                    if (interrupted != null && skipInterrupted)
                        pending.SetStatus(interrupted, RunJournal.FileStates.Skipped);
                    logToScreen("מתחיל הדפסת הקבצים");
                    PrintJournalFiles();
                }
            }
            catch (Exception ex)
            {
                SimpleLog.Log(ex);
                logToScreen("הפעולה נכשלה - " + ex.Message);
                FailRun();
                return;
            }
            FinishPrinting();
        }

        /// <summary>
        /// Give up the run without losing letters: before printing they go back to the main folder; while printing,
        /// the files not printed yet go to the archive, in a "not printed" folder.
        /// </summary>
        private void DiscardRun(RunJournal pending)
        {
            var nl = Environment.NewLine;
            try
            {
                if (pending.Stage == RunJournal.Stages.Print)
                {
                    var left = pending.Unprinted.Where(f => File.Exists(f.Path)).ToList();
                    if (MessageBox.Show($"{left.Count} קבצים שלא הודפסו יועברו לארכיון ולא יודפסו." + nl + "להמשיך?", "ביטול ההפעלה הקודמת",
                            MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                        return;
                    var target = Path.Combine(pending.ArchivePath,
                        DateTime.Now.ToString("dd.MM.yy.HH.mm") + "-" + pending.Folder + "-לא הודפס");
                    Directory.CreateDirectory(target);
                    foreach (var file in left)
                        File.Move(file.Path, UniquePath(Path.Combine(target, Path.GetFileName(file.Path))));
                    pending.Delete();
                    SimpleLog.Warning($"Unfinished run discarded: {left.Count} unprinted file(s) moved to {target}");
                    MessageBox.Show("הקבצים שלא הודפסו הועברו אל:" + nl + target, "ביטול ההפעלה הקודמת",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    if (MessageBox.Show("המכתבים יוחזרו מהתקיה הזמנית לתקיה הראשית, ואפשר יהיה להדפיס אותם שוב." + nl + "להמשיך?",
                            "ביטול ההפעלה הקודמת", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                        return;
                    var workingDir = Path.Combine(pending.WorkingPath, pending.Folder);
                    var mainDir = Path.Combine(pending.MainPath, pending.Folder);
                    var restored = RestoreWorkingToMain(workingDir, mainDir, pending.Merge);
                    // What the merge had made so far is rebuilt by the next run.
                    if (pending.Merge && Directory.Exists(pending.PrintPath))
                        foreach (var merged in Directory.GetFiles(pending.PrintPath, "*.pdf", SearchOption.TopDirectoryOnly))
                            File.Delete(merged);
                    pending.Delete();
                    SimpleLog.Warning($"Unfinished run discarded: {restored} file(s) moved back from {workingDir} to {mainDir}");
                    MessageBox.Show("המכתבים הוחזרו לתקיה הראשית. אפשר לרענן ולהדפיס שוב.", "ביטול ההפעלה הקודמת",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                updateScreen();
            }
            catch (Exception ex)
            {
                SimpleLog.Log(ex);
                MessageBox.Show("ביטול ההפעלה הקודמת נכשל, יש לבחון קובץ לוג:" + nl + ex.Message, "ביטול ההפעלה הקודמת",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>Files in the working folder that are exact copies of a file still in the main folder.</summary>
        private static IEnumerable<(string Working, string Main)> CopiedPairs(string workingDir, string mainDir)
        {
            if (!Directory.Exists(workingDir))
                yield break;
            foreach (var working in Directory.GetFiles(workingDir, "*.*", SearchOption.AllDirectories))
            {
                var main = Path.Combine(mainDir, Path.GetRelativePath(workingDir, working));
                if (!File.Exists(main))
                    continue;
                var a = new FileInfo(working);
                var b = new FileInfo(main);
                // File.Copy keeps the modified time, so a copy matches on size and time; a newer letter with the same name does not.
                if (a.Length == b.Length && a.LastWriteTimeUtc == b.LastWriteTimeUtc)
                    yield return (working, main);
            }
        }

        /// <summary>Before the letters were removed from the main folder: delete their copies from the working folder.</summary>
        private static int DeleteWorkingCopies(string workingDir, string mainDir)
        {
            var pairs = CopiedPairs(workingDir, mainDir).ToList();
            foreach (var pair in pairs)
                File.Delete(pair.Working);
            DeleteEmptyFolders(workingDir);
            return pairs.Count;
        }

        /// <summary>After the letters were copied to the working folder: delete the originals still in the main folder.</summary>
        private static int DeleteMainOriginals(string workingDir, string mainDir)
        {
            var pairs = CopiedPairs(workingDir, mainDir).ToList();
            foreach (var pair in pairs)
                File.Delete(pair.Main);
            DeleteEmptyFolders(mainDir, keepRoot: true);
            return pairs.Count;
        }

        /// <summary>
        /// Move the letters from the working folder back to the main folder. PDFs that Convert made from an
        /// HTML/image file are dropped (the next run makes them again); a name already in the main folder is kept
        /// in the working folder and logged.
        /// </summary>
        private static int RestoreWorkingToMain(string workingDir, string mainDir, bool merge)
        {
            if (!Directory.Exists(workingDir))
                return 0;
            DeleteConverterLeftovers(workingDir);
            var moved = 0;
            foreach (var file in Directory.GetFiles(workingDir, "*.*", SearchOption.AllDirectories))
            {
                if (merge && IsConvertedPdf(file))
                {
                    File.Delete(file);
                    continue;
                }
                var target = Path.Combine(mainDir, Path.GetRelativePath(workingDir, file));
                if (File.Exists(target))
                {
                    SimpleLog.Warning("Not moved back, the main folder already has a file with this name: " + file);
                    continue;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Move(file, target);
                moved++;
            }
            DeleteEmptyFolders(workingDir);
            return moved;
        }

        /// <summary>
        /// IronPdf writes a temporary copy of an HTML letter next to it while converting, named by a GUID
        /// ("fa04e4e8-06d9-486b-8700-0c76f04ccf76" + digits + ".html"), and deletes it when done. If the app died
        /// mid-conversion it is still there, and would be converted and printed as an extra letter.
        /// </summary>
        private static void DeleteConverterLeftovers(string workingDir)
        {
            if (!Directory.Exists(workingDir))
                return;
            foreach (var file in Directory.GetFiles(workingDir, "*.htm*", SearchOption.AllDirectories))
            {
                if (!ConverterTempName.IsMatch(Path.GetFileName(file)))
                    continue;
                File.Delete(file);
                SimpleLog.Info("Resume: deleted a temporary file left by the interrupted HTML conversion: " + file);
            }
        }

        private static readonly System.Text.RegularExpressions.Regex ConverterTempName = new System.Text.RegularExpressions.Regex(
            @"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\d*\.html?$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        private static bool IsConvertedPdf(string file)
        {
            if (!string.Equals(Path.GetExtension(file), ".pdf", StringComparison.OrdinalIgnoreCase))
                return false;
            var stem = Path.Combine(Path.GetDirectoryName(file), Path.GetFileNameWithoutExtension(file));
            return ConvertedSourceExtensions.Any(ext => File.Exists(stem + ext));
        }

        private static void MoveWorkingFilesTo(string workingDir, List<string> selected, string printDir)
        {
            if (!Directory.Exists(workingDir))
                return;
            var files = selected == null
                ? Directory.GetFiles(workingDir, "*.*", SearchOption.AllDirectories)
                : selected.Select(d => Path.Combine(workingDir, d)).Where(Directory.Exists)
                    .SelectMany(d => Directory.GetFiles(d, "*.*", SearchOption.AllDirectories)).ToArray();
            foreach (var file in files)
                File.Move(file, Path.Combine(printDir, Path.GetFileName(file)));
        }

        private static void DeleteEmptyFolders(string root, bool keepRoot = false)
        {
            if (!Directory.Exists(root))
                return;
            foreach (var dir in Directory.GetDirectories(root, "*", SearchOption.AllDirectories).OrderByDescending(d => d.Length))
            {
                if (!Directory.EnumerateFileSystemEntries(dir).Any())
                    Directory.Delete(dir);
            }
            if (!keepRoot && !Directory.EnumerateFileSystemEntries(root).Any())
                Directory.Delete(root);
        }

        private static string UniquePath(string path)
        {
            if (!File.Exists(path))
                return path;
            var dir = Path.GetDirectoryName(path);
            var stem = Path.GetFileNameWithoutExtension(path);
            var ext = Path.GetExtension(path);
            for (var i = 2; ; i++)
            {
                var candidate = Path.Combine(dir, $"{stem} ({i}){ext}");
                if (!File.Exists(candidate))
                    return candidate;
            }
        }
    }
}
