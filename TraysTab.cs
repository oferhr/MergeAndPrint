using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace MergeAndPrint
{
    /// <summary>
    /// לשונית "מגשים": מגש, עמודים ראשונים ממגש אחר והידוק לכל מדפסת, למנוע ההדפסה המהיר (custom).
    /// הערכים נשמרים ב-config.json תחת GhostscriptPrinters, לפי מספר התיקיה ("1", "2", "3", "11", "22", "33", "9").
    /// </summary>
    public partial class Form1
    {
        private TabPage tTrays;
        private DataGridView gridTrays;

        private const string ColFolder = "folder", ColPrinter = "printer", ColFirstCount = "firstCount",
                             ColFirstTray = "firstTray", ColTray = "tray", ColStaple = "staple";

        private const string FromQueue = "לפי הגדרות המדפסת";
        private const string NoFirstPages = "ללא";

        /// <summary>The folders on the main screen, the key saved in config.json, and the printer chosen for each.</summary>
        private (string Folder, string Key, string Printer)[] TraySlots() => new[]
        {
            ("1", "1", Printer1), ("2", "2", Printer2), ("3", "3", Printer3),
            ("1_999", "11", Printer11), ("2_999", "22", Printer22), ("3_999", "33", Printer33),
            ("9", "9", Printer9)
        };

        private static readonly (string Value, string Text)[] TrayOptions =
        {
            ("", FromQueue), ("1", "מגש 1"), ("2", "מגש 2"), ("3", "מגש 3"), ("4", "מגש 4"), ("5", "מגש 5"), ("MANUAL", "הזנה ידנית")
        };

        private static readonly (string Value, string Text)[] StapleOptions =
        {
            ("", "ברירת מחדל"), ("RIGHT_1PT", "סיכה אחת מימין"), ("LEFT_1PT", "סיכה אחת משמאל"),
            ("LEFT_2PTS", "2 סיכות משמאל"), ("OFF", "ללא הידוק")
        };

        private void BuildTraysTab()
        {
            tTrays = new TabPage
            {
                Text = "מגשים",
                BackColor = Color.WhiteSmoke,
                Name = "tTrays",
                Padding = new Padding(12)
            };

            var hint = new Label
            {
                Dock = DockStyle.Top,
                Height = 70,
                Font = new Font("Arial", 10F),
                Text = "הגדרות אלה חלות רק על מנוע ההדפסה המהיר (custom). במנוע הרגיל (Spire) המגשים נקבעים לפי הגדרות המדפסת עצמה." + Environment.NewLine +
                       "\"עמודים ראשונים\": כמה עמודים בתחילת כל מכתב יודפסו מהמגש שנבחר (למשל נייר לוגו), ושאר העמודים מהמגש הרגיל." + Environment.NewLine +
                       "ההידוק הוא לכל מכתב, בסוף הקובץ."
            };

            gridTrays = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                EditMode = DataGridViewEditMode.EditOnEnter,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.WhiteSmoke,
                Font = new Font("Arial", 11F),
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
                RightToLeft = RightToLeft.Yes
            };
            gridTrays.RowTemplate.Height = 32;
            gridTrays.Columns.Add(new DataGridViewTextBoxColumn { Name = ColFolder, HeaderText = "תיקיה", ReadOnly = true, FillWeight = 45 });
            gridTrays.Columns.Add(new DataGridViewTextBoxColumn { Name = ColPrinter, HeaderText = "מדפסת", ReadOnly = true, FillWeight = 120 });
            gridTrays.Columns.Add(ComboColumn(ColFirstCount, "עמודים ראשונים", 70,
                new[] { NoFirstPages }.Concat(Enumerable.Range(1, 9).Select(n => n.ToString()))));
            gridTrays.Columns.Add(ComboColumn(ColFirstTray, "ממגש", 90, TrayOptions.Skip(1).Select(o => o.Text)));
            gridTrays.Columns.Add(ComboColumn(ColTray, "שאר העמודים ממגש", 120, TrayOptions.Select(o => o.Text)));
            gridTrays.Columns.Add(ComboColumn(ColStaple, "הידוק", 100, StapleOptions.Select(o => o.Text)));
            gridTrays.Columns[ColFolder].DefaultCellStyle.BackColor = Color.Gainsboro;
            gridTrays.Columns[ColPrinter].DefaultCellStyle.BackColor = Color.Gainsboro;
            gridTrays.DataError += (s, e) => e.ThrowException = false;
            gridTrays.CellValueChanged += (s, e) => UpdateFirstTrayCell(e.RowIndex);
            // Apply a dropdown choice at once, not only when the cell is left.
            gridTrays.CurrentCellDirtyStateChanged += (s, e) => { if (gridTrays.IsCurrentCellDirty) gridTrays.CommitEdit(DataGridViewDataErrorContexts.Commit); };

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 50, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 8, 0, 0) };
            var save = new Button
            {
                Text = "שמירת הגדרות",
                Size = new Size(200, 35),
                BackColor = SystemColors.ActiveCaption,
                Font = new Font("Arial", 10F, FontStyle.Bold),
                UseVisualStyleBackColor = false
            };
            save.Click += btnSaveConfig_Click;
            buttons.Controls.Add(save);

            tTrays.Controls.Add(gridTrays);
            tTrays.Controls.Add(hint);
            tTrays.Controls.Add(buttons);
            tabMain.TabPages.Add(tTrays);
            // The printer names come from the settings tab; show the current choice whenever this tab is opened.
            tabMain.Selected += (s, e) => { if (e.TabPage == tTrays) RefreshTrayPrinters(); };
        }

        private static DataGridViewComboBoxColumn ComboColumn(string name, string header, int weight, IEnumerable<string> items)
        {
            var column = new DataGridViewComboBoxColumn
            {
                Name = name,
                HeaderText = header,
                FillWeight = weight,
                FlatStyle = FlatStyle.Flat,
                DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton
            };
            column.Items.AddRange(items.Cast<object>().ToArray());
            return column;
        }

        /// <summary>The "from tray" cell only means something when there are first pages; grey it out otherwise.</summary>
        private void UpdateFirstTrayCell(int row)
        {
            if (row < 0 || gridTrays == null || row >= gridTrays.Rows.Count)
                return;
            var none = (gridTrays.Rows[row].Cells[ColFirstCount].Value as string ?? NoFirstPages) == NoFirstPages;
            var cell = gridTrays.Rows[row].Cells[ColFirstTray];
            cell.ReadOnly = none;
            cell.Style.BackColor = none ? Color.Gainsboro : Color.Empty;
            if (!none && cell.Value == null)
                cell.Value = "מגש 2";
        }

        private void RefreshTrayPrinters()
        {
            if (gridTrays == null)
                return;
            var slots = TraySlots();
            for (int i = 0; i < slots.Length && i < gridTrays.Rows.Count; i++)
                gridTrays.Rows[i].Cells[ColPrinter].Value = string.IsNullOrWhiteSpace(slots[i].Printer) ? "(לא נבחרה)" : slots[i].Printer;
        }

        /// <summary>The entry for a folder: by printer name first (older config files), then by folder key.</summary>
        private static string EntryKey(AppConfig cfg, string key, string printer)
        {
            var all = cfg.GhostscriptPrinters;
            if (all == null)
                return null;
            if (!string.IsNullOrWhiteSpace(printer))
            {
                var byName = all.Keys.FirstOrDefault(k => string.Equals(k?.Trim(), printer.Trim(), StringComparison.OrdinalIgnoreCase));
                if (byName != null)
                    return byName;
            }
            return all.Keys.FirstOrDefault(k => k?.Trim() == key);
        }

        private void LoadTraysToScreen(AppConfig cfg)
        {
            if (gridTrays == null)
                return;
            gridTrays.Rows.Clear();
            foreach (var slot in TraySlots())
            {
                var entryKey = EntryKey(cfg, slot.Key, slot.Printer);
                var entry = entryKey == null ? null : cfg.GhostscriptPrinters[entryKey];
                var tray = GhostscriptPrinter.TrayName(entry) ?? "";
                var firstTray = GhostscriptPrinter.FirstPagesTray(entry);
                var staple = (entry?.Staple ?? "").Trim().ToUpperInvariant();

                var row = gridTrays.Rows[gridTrays.Rows.Add()];
                row.Cells[ColFolder].Value = slot.Folder;
                row.Cells[ColFirstCount].Value = firstTray == null ? NoFirstPages : Math.Min(entry.FirstPages, 9).ToString();
                row.Cells[ColFirstTray].Value = firstTray == null ? null : OptionText(row.Cells[ColFirstTray], TrayOptions, firstTray);
                row.Cells[ColTray].Value = OptionText(row.Cells[ColTray], TrayOptions, tray);
                row.Cells[ColStaple].Value = OptionText(row.Cells[ColStaple], StapleOptions, staple == "NONE" ? "OFF" : staple);
                UpdateFirstTrayCell(row.Index);
            }
            RefreshTrayPrinters();
        }

        /// <summary>The text shown for a saved value; a value this screen does not offer is added to the cell's list as is.</summary>
        private static string OptionText(DataGridViewCell cell, (string Value, string Text)[] options, string value)
        {
            var match = options.FirstOrDefault(o => string.Equals(o.Value, value, StringComparison.OrdinalIgnoreCase));
            if (match.Text != null)
                return match.Text;
            var combo = (DataGridViewComboBoxCell)cell;
            if (!combo.Items.Contains(value))
                combo.Items.Add(value);
            return value;
        }

        private static string OptionValue((string Value, string Text)[] options, object text)
        {
            var shown = text as string ?? "";
            var match = options.FirstOrDefault(o => o.Text == shown);
            return match.Text != null ? match.Value : shown;
        }

        /// <summary>Copy the grid into config.json's GhostscriptPrinters, keyed by folder. Duplex, set only in the file, is kept.</summary>
        private void SaveTraysFromScreen(AppConfig cfg)
        {
            if (gridTrays == null)
                return;
            gridTrays.EndEdit();
            cfg.GhostscriptPrinters ??= new Dictionary<string, GhostscriptPrinterSettings>();
            var slots = TraySlots();
            for (int i = 0; i < slots.Length && i < gridTrays.Rows.Count; i++)
            {
                var row = gridTrays.Rows[i];
                var oldKey = EntryKey(cfg, slots[i].Key, slots[i].Printer);
                var old = oldKey == null ? null : cfg.GhostscriptPrinters[oldKey];
                if (oldKey != null)
                    cfg.GhostscriptPrinters.Remove(oldKey);
                foreach (var stale in cfg.GhostscriptPrinters.Keys.Where(k => k?.Trim() == slots[i].Key).ToList())
                    cfg.GhostscriptPrinters.Remove(stale);

                var countText = row.Cells[ColFirstCount].Value as string ?? NoFirstPages;
                var count = countText == NoFirstPages ? 0 : int.Parse(countText);
                var entry = new GhostscriptPrinterSettings
                {
                    Tray = OptionValue(TrayOptions, row.Cells[ColTray].Value),
                    Duplex = old?.Duplex ?? "",
                    Staple = OptionValue(StapleOptions, row.Cells[ColStaple].Value),
                    FirstPages = count,
                    FirstPagesTray = count == 0 ? "" : OptionValue(TrayOptions, row.Cells[ColFirstTray].Value)
                };
                var empty = entry.Tray.Length == 0 && entry.Duplex.Length == 0 && entry.Staple.Length == 0 && entry.FirstPages == 0;
                if (!empty)
                    cfg.GhostscriptPrinters[slots[i].Key] = entry;
            }
            SimpleLogger.SimpleLog.Info("Trays saved: " + GhostscriptPrinter.DescribeSetup());
        }
    }
}
