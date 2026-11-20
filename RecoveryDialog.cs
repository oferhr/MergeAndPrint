using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace MergeAndPrint
{
    /// <summary>What the user chose to do with a run that did not finish.</summary>
    public enum RecoveryChoice
    {
        /// <summary>Not now: keep everything as it is and ask again before the next run.</summary>
        Later,
        /// <summary>Continue the run. While printing, the file that was being sent is printed again.</summary>
        Continue,
        /// <summary>Continue printing, without the file that was being sent when the app stopped.</summary>
        ContinueSkipInterrupted,
        /// <summary>Give the run up and put the letters somewhere safe.</summary>
        Discard
    }

    /// <summary>
    /// Shown when an unfinished run is found: at start-up, and before a new run or a refresh while one is pending.
    /// </summary>
    internal sealed class RecoveryDialog : Form
    {
        public RecoveryChoice Choice { get; private set; } = RecoveryChoice.Later;

        private RecoveryDialog(string message, IEnumerable<(string Text, RecoveryChoice Choice)> buttons)
        {
            Text = "הפעלה קודמת לא הסתיימה";
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = true;
            TopMost = true;
            StartPosition = FormStartPosition.CenterScreen;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Font = new Font("Segoe UI", 10F);
            Padding = new Padding(12);

            var layout = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                Dock = DockStyle.Fill
            };
            layout.Controls.Add(new Label
            {
                Text = message,
                AutoSize = true,
                MaximumSize = new Size(560, 0),
                Margin = new Padding(0, 0, 0, 12)
            });
            foreach (var (text, choice) in buttons)
            {
                var button = new Button
                {
                    Text = text,
                    AutoSize = true,
                    MinimumSize = new Size(560, 36),
                    MaximumSize = new Size(560, 0),
                    TextAlign = ContentAlignment.MiddleRight,
                    Margin = new Padding(0, 0, 0, 6)
                };
                button.Click += (s, e) =>
                {
                    Choice = choice;
                    DialogResult = DialogResult.OK;
                    Close();
                };
                layout.Controls.Add(button);
            }
            Controls.Add(layout);
        }

        /// <summary>Describe the unfinished run and ask what to do with it.</summary>
        public static RecoveryChoice Ask(IWin32Window owner, RunJournal journal)
        {
            var nl = Environment.NewLine;
            var message = $"הדפסת תקיה {journal.Folder} שהתחילה ב-{journal.Started:dd/MM/yyyy HH:mm} לא הסתיימה." + nl +
                $"היא נעצרה בשלב: {journal.StageText()}." + nl;
            var buttons = new List<(string, RecoveryChoice)>();

            if (journal.Stage == RunJournal.Stages.Print)
            {
                var unprinted = journal.Unprinted.Count;
                message += $"הודפסו {journal.PrintedCount} מתוך {journal.Files.Count} קבצים, נשארו {unprinted}." + nl;
                var interrupted = journal.Interrupted;
                if (interrupted != null)
                {
                    var index = journal.Files.IndexOf(interrupted) + 1;
                    message += nl + $"התוכנה נסגרה בזמן שליחת קובץ {index}: {System.IO.Path.GetFileName(interrupted.Path)}" + nl +
                        "ייתכן שהוא כבר יצא מהמדפסת. יש לבדוק במדפסת לפני שבוחרים:" + nl;
                    buttons.Add(("להדפיס שוב את הקובץ שנקטע ולהמשיך משם", RecoveryChoice.Continue));
                    buttons.Add(("הקובץ שנקטע כבר הודפס - להמשיך מהקובץ שאחריו", RecoveryChoice.ContinueSkipInterrupted));
                }
                else
                {
                    var failed = journal.Files.FindAll(f => f.Status == RunJournal.FileStates.Failed).Count;
                    if (failed > 0)
                        message += $"{failed} קבצים נכשלו בהדפסה." + nl;
                    buttons.Add(("להדפיס את הקבצים שנשארו", RecoveryChoice.Continue));
                }
                buttons.Add(("לא להדפיס - להעביר את הקבצים שנשארו לארכיון (תקיית \"לא הודפס\")", RecoveryChoice.Discard));
            }
            else if (journal.Stage == RunJournal.Stages.Archive || journal.Stage == RunJournal.Stages.Copy)
            {
                // Never shown: these stages are cleaned up without asking (the main folder is untouched).
                message += "המכתבים לא הוזזו מהתקיה הראשית.";
                buttons.Add(("אישור", RecoveryChoice.Discard));
            }
            else
            {
                message += "המכתבים נמצאים בתקיה הזמנית." + nl;
                buttons.Add(("להמשיך את התהליך (המרה, איחוד והדפסה)", RecoveryChoice.Continue));
                buttons.Add(("לא להמשיך - להחזיר את המכתבים לתקיה הראשית", RecoveryChoice.Discard));
            }
            buttons.Add(("לא עכשיו (אי אפשר להתחיל הדפסה חדשה עד שמחליטים)", RecoveryChoice.Later));

            using (var dialog = new RecoveryDialog(message, buttons))
            {
                dialog.ShowDialog(owner);
                return dialog.Choice;
            }
        }
    }
}
