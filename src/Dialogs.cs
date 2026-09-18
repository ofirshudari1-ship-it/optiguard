using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace UninstallerPro
{
    public class CheckableResidual : INotifyPropertyChanged
    {
        private bool _isChecked;
        public bool IsChecked { get { return _isChecked; } set { _isChecked = value; OnChanged("IsChecked"); } }
        public ResidualItem Item { get; set; }
        public string Display { get; set; }
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnChanged(string p) { if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(p)); }
    }

    public class CheckableFinding : INotifyPropertyChanged
    {
        private bool _isChecked;
        public bool IsChecked { get { return _isChecked; } set { _isChecked = value; OnChanged("IsChecked"); } }
        public WizardFinding Finding { get; set; }
        public string Display { get; set; }
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnChanged(string p) { if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(p)); }
    }

    public static class Dialogs
    {
        public static void ShowError(string title, string message)
        {
            Logger.Log("Error: " + title + " - " + message);
            MessageBox.Show(message, string.IsNullOrEmpty(title) ? I18n.T("generic_error_title") : title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public static bool Confirm(string title, string message)
        {
            return MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
        }

        public static void Info(string title, string message)
        {
            MessageBox.Show(message, string.IsNullOrEmpty(title) ? I18n.T("generic_done_title") : title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private static Window StyledDialog(string title, double width, double height)
        {
            var w = new Window
            {
                Title = title,
                Width = width,
                Height = height,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Background = Theme.Get("BgBrush"),
                FlowDirection = I18n.IsRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
                FontFamily = new FontFamily("Segoe UI")
            };
            try
            {
                var exeIcon = System.Drawing.Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location);
                w.Icon = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(exeIcon.Handle, Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
            }
            catch { }
            return w;
        }

        public static void ShowResidualPicker(List<ResidualItem> items, string subject)
        {
            if (items.Count == 0) { Info(subject, I18n.T("no_leftovers_found")); return; }

            var w = StyledDialog(subject, 820, 560);
            var root = new DockPanel();
            w.Content = root;

            var infoLbl = new TextBlock
            {
                Text = string.Format(I18n.T("residual_found_note"), items.Count),
                Margin = new Thickness(14,12,14,6),
                TextWrapping = TextWrapping.Wrap,
                Foreground = Theme.Get("TextBrush")
            };
            DockPanel.SetDock(infoLbl, Dock.Top);
            root.Children.Add(infoLbl);

            var checkable = items.Select(i => new CheckableResidual { Item = i, Display = "[" + i.TypeText + "] " + i.DisplayPath + "   <=  " + i.Reason }).ToList();

            var progressPanel = new StackPanel { Margin = new Thickness(14,0,14,8), Visibility = Visibility.Collapsed };
            DockPanel.SetDock(progressPanel, Dock.Bottom);
            var progressBar = new ProgressBar { Height = 8, Minimum = 0, Maximum = 100 };
            var progressLbl = new TextBlock { Foreground = Theme.Get("TextMutedBrush"), Margin = new Thickness(0,4,0,0), FontSize = 12 };
            progressPanel.Children.Add(progressBar);
            progressPanel.Children.Add(progressLbl);
            root.Children.Add(progressPanel);

            var bottom = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = I18n.IsRtl ? HorizontalAlignment.Right : HorizontalAlignment.Left, Margin = new Thickness(14,0,14,14) };
            DockPanel.SetDock(bottom, Dock.Bottom);

            var btnAll = new Button { Content = I18n.T("btn_select_all"), Style = (Style)Theme.GetStyle("GhostButtonStyle"), Width = 110, Margin = new Thickness(0,0,6,0) };
            btnAll.Click += (s, e) => { foreach (var c in checkable) c.IsChecked = true; };
            var btnNone = new Button { Content = I18n.T("btn_deselect_all"), Style = (Style)Theme.GetStyle("GhostButtonStyle"), Width = 110, Margin = new Thickness(0,0,6,0) };
            btnNone.Click += (s, e) => { foreach (var c in checkable) c.IsChecked = false; };
            var btnDelete = new Button { Content = I18n.T("btn_delete_selected"), Style = (Style)Theme.GetStyle("DangerButtonStyle"), Width = 140, Margin = new Thickness(0,0,6,0) };
            var btnClose = new Button { Content = I18n.T("btn_close"), Style = (Style)Theme.GetStyle("GhostButtonStyle"), Width = 100 };
            btnDelete.Click += async (s, e) =>
            {
                var chosen = checkable.Where(c => c.IsChecked).ToList();
                if (chosen.Count == 0) { Info("", I18n.T("no_items_selected")); return; }
                if (!Confirm(I18n.T("confirm_delete_residual_title"), string.Format(I18n.T("confirm_delete_residual_msg"), chosen.Count))) return;

                btnAll.IsEnabled = false; btnNone.IsEnabled = false; btnDelete.IsEnabled = false; btnClose.IsEnabled = false;
                progressPanel.Visibility = Visibility.Visible;
                progressBar.IsIndeterminate = true;
                progressLbl.Text = I18n.T("progress_creating_restore_point");

                var dispatcher = w.Dispatcher;
                int deleted = await Task.Run(() =>
                {
                    RestorePoint.Create("OptiGuard - before removing leftovers");
                    dispatcher.BeginInvoke((Action)(() => progressBar.IsIndeterminate = false));
                    int count = 0;
                    for (int i = 0; i < chosen.Count; i++)
                    {
                        var idx = i;
                        dispatcher.BeginInvoke((Action)(() =>
                        {
                            progressBar.Value = (idx * 100.0) / chosen.Count;
                            progressLbl.Text = string.Format(I18n.T("progress_deleting_x_of_y"), idx + 1, chosen.Count);
                        }));
                        if (ProgramsData.RemoveResidualItem(chosen[idx].Item)) count++;
                    }
                    return count;
                });

                progressBar.Value = 100;
                progressLbl.Text = I18n.T("progress_done");
                if (deleted > 0) Stats.Add(d => d.ResidualItemsRemoved += deleted);
                Info(I18n.T("generic_done_title"), string.Format(I18n.T("deleted_count_msg"), deleted, AppPaths.LogFile));
                w.Close();
            };
            btnClose.Click += (s, e) => w.Close();

            bottom.Children.Add(btnAll);
            bottom.Children.Add(btnNone);
            bottom.Children.Add(btnDelete);
            bottom.Children.Add(btnClose);
            root.Children.Add(bottom);

            // ה-ListBox חייב להיות ה-child האחרון ב-DockPanel כדי שימלא את השטח הנותר
            var listBox = new ListBox { Margin = new Thickness(14,0,14,8), Background = Theme.Get("PanelBrush"), BorderBrush = Theme.Get("BorderColorBrush") };
            var template = new DataTemplate();
            var cbFactory = new FrameworkElementFactory(typeof(CheckBox));
            cbFactory.SetBinding(CheckBox.IsCheckedProperty, new System.Windows.Data.Binding("IsChecked") { Mode = System.Windows.Data.BindingMode.TwoWay });
            cbFactory.SetBinding(CheckBox.ContentProperty, new System.Windows.Data.Binding("Display"));
            cbFactory.SetValue(CheckBox.ForegroundProperty, Theme.Get("TextBrush"));
            cbFactory.SetValue(CheckBox.PaddingProperty, new Thickness(6,2,6,2));
            template.VisualTree = cbFactory;
            listBox.ItemTemplate = template;
            listBox.ItemsSource = checkable;
            root.Children.Add(listBox);

            w.ShowDialog();
        }

        public static void ShowLeftoverHunter()
        {
            var w = StyledDialog(I18n.T("hunter_title"), 480, 230);
            w.ResizeMode = ResizeMode.NoResize;
            var panel = new StackPanel { Margin = new Thickness(20) };
            w.Content = panel;

            panel.Children.Add(new TextBlock { Text = I18n.T("hunter_name_label"), Foreground = Theme.Get("TextBrush"), Margin = new Thickness(0,0,0,4) });
            var txtName = new TextBox { Height = 28, FontSize = 13 };
            panel.Children.Add(txtName);
            panel.Children.Add(new TextBlock { Text = I18n.T("hunter_publisher_label"), Foreground = Theme.Get("TextBrush"), Margin = new Thickness(0,14,0,4) });
            var txtPub = new TextBox { Height = 28, FontSize = 13 };
            panel.Children.Add(txtPub);

            var btnScan = new Button { Content = I18n.T("btn_scan"), Style = (Style)Theme.GetStyle("AccentButtonStyle"), Width = 140, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0,20,0,0) };
            btnScan.Click += async (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtName.Text)) { Info("", I18n.T("hunter_enter_name")); return; }
                Mouse.OverrideCursor = Cursors.Wait;
                List<ResidualItem> items;
                var name = txtName.Text; var pub = txtPub.Text;
                try { items = await Task.Run(() => ProgramsData.FindResidualItems(name, pub, null)); }
                catch (Exception ex) { Mouse.OverrideCursor = null; ShowError(I18n.T("scan_error_title"), string.Format(I18n.T("scan_error_msg"), ex.Message)); return; }
                Mouse.OverrideCursor = null;
                ShowResidualPicker(items, name);
            };
            panel.Children.Add(btnScan);

            w.ShowDialog();
        }

        public static void ShowDuplicatePurposeReport(List<InstalledProgram> programs)
        {
            var results = DuplicatePurposeFinder.Find(programs);
            if (results.Count == 0) { Info(I18n.T("dup_report_title"), I18n.T("dup_report_none")); return; }

            var w = StyledDialog(I18n.T("dup_report_title"), 760, 560);
            var outer = new DockPanel();
            w.Content = outer;

            var infoLbl = new TextBlock
            {
                Text = string.Format(I18n.T("dup_report_intro"), results.Count),
                Margin = new Thickness(14,12,14,10), TextWrapping = TextWrapping.Wrap, Foreground = Theme.Get("TextBrush")
            };
            DockPanel.SetDock(infoLbl, Dock.Top);
            outer.Children.Add(infoLbl);

            var scroll = new ScrollViewer { Margin = new Thickness(14,0,14,14), VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var stack = new StackPanel();
            scroll.Content = stack;
            outer.Children.Add(scroll);

            foreach (var group in results)
            {
                var card = new Border { Background = Theme.Get("PanelBrush"), BorderBrush = Theme.Get("BorderColorBrush"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Margin = new Thickness(0,0,0,12), Padding = new Thickness(14) };
                var inner = new StackPanel();
                inner.Children.Add(new TextBlock { Text = group.Label, FontWeight = FontWeights.Bold, FontSize = 14, Foreground = Theme.Get("TextBrush"), Margin = new Thickness(0,0,0,8) });

                var ordered = group.Matches.OrderByDescending(m => ParseDate(m.InstallDate)).ToList();
                for (int i = 0; i < ordered.Count; i++)
                {
                    var m = ordered[i];
                    var row = new TextBlock { Foreground = Theme.Get("TextBrush"), Margin = new Thickness(0,2,0,2), TextWrapping = TextWrapping.Wrap };
                    var note = i == 0 ? I18n.T("dup_report_newest_note") : "";
                    row.Text = "• " + m.DisplayName + "   |   " + I18n.T("dup_installed_label") + ": " + m.InstallDateText + "   |   " + I18n.T("dup_size_label") + ": " + m.SizeText + note;
                    inner.Children.Add(row);
                }
                card.Child = inner;
                stack.Children.Add(card);
            }

            w.ShowDialog();
        }

        private static DateTime ParseDate(string yyyymmdd)
        {
            DateTime dt;
            if (!string.IsNullOrWhiteSpace(yyyymmdd) && DateTime.TryParseExact(yyyymmdd, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out dt)) return dt;
            return DateTime.MinValue;
        }

        public static async void ShowEmptyFolderCleaner()
        {
            using (var fbd = new System.Windows.Forms.FolderBrowserDialog { Description = I18n.T("empty_folder_picker_title"), SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) })
            {
                if (fbd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                var root = fbd.SelectedPath;
                if (SafetyGuard.IsProtectedPath(root)) { Info("", I18n.T("empty_folder_protected")); return; }
                List<string> empties;
                Mouse.OverrideCursor = Cursors.Wait;
                try { empties = await Task.Run(() => SafetyGuard.GetEmptyDirectoriesRecursive(root)); }
                catch (Exception ex) { Mouse.OverrideCursor = null; ShowError(I18n.T("scan_error_title"), string.Format(I18n.T("scan_error_msg"), ex.Message)); return; }
                Mouse.OverrideCursor = null;
                if (empties.Count == 0) { Info("", string.Format(I18n.T("empty_folder_none_found"), root)); return; }
                var items = empties.Select(e => new ResidualItem { Type = ResidualType.Folder, Path = e, DisplayPath = e, Reason = "Empty (including empty subfolders)" }).ToList();
                ShowResidualPicker(items, string.Format(I18n.T("empty_folder_title_result"), root));
            }
        }

        private static string KindLabel(WizardKind k)
        {
            switch (k)
            {
                case WizardKind.Junk: return I18n.T("wizard_kind_junk");
                case WizardKind.BrokenStartup: return I18n.T("wizard_kind_broken_startup");
                default: return I18n.T("wizard_kind_empty_folder");
            }
        }

        // מציג את תוצאות האשף החכם (זבל + הפעלה אוטומטית שבורה + תיקיות ריקות
        // יחד) ומחזיר סיכום ניקוי לצורך אנימציית ההצלחה בדשבורד; null אם בוטל.
        public static WizardCleanSummary ShowWizardResults(List<WizardFinding> findings)
        {
            if (findings.Count == 0) { Info(I18n.T("wizard_results_title"), I18n.T("wizard_no_issues")); return null; }

            WizardCleanSummary summary = null;
            var w = StyledDialog(I18n.T("wizard_results_title"), 820, 560);
            var root = new DockPanel();
            w.Content = root;

            var infoLbl = new TextBlock
            {
                Text = string.Format(I18n.T("residual_found_note"), findings.Count),
                Margin = new Thickness(14,12,14,6), TextWrapping = TextWrapping.Wrap, Foreground = Theme.Get("TextBrush")
            };
            DockPanel.SetDock(infoLbl, Dock.Top);
            root.Children.Add(infoLbl);

            var checkable = findings.Select(f => new CheckableFinding { Finding = f, Display = "[" + KindLabel(f.Kind) + "] " + f.Name + "   <=  " + f.Detail }).ToList();

            var progressPanel = new StackPanel { Margin = new Thickness(14,0,14,8), Visibility = Visibility.Collapsed };
            DockPanel.SetDock(progressPanel, Dock.Bottom);
            var progressBar = new ProgressBar { Height = 8, Minimum = 0, Maximum = 100 };
            var progressLbl = new TextBlock { Foreground = Theme.Get("TextMutedBrush"), Margin = new Thickness(0,4,0,0), FontSize = 12 };
            progressPanel.Children.Add(progressBar);
            progressPanel.Children.Add(progressLbl);
            root.Children.Add(progressPanel);

            var bottom = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = I18n.IsRtl ? HorizontalAlignment.Right : HorizontalAlignment.Left, Margin = new Thickness(14,0,14,14) };
            DockPanel.SetDock(bottom, Dock.Bottom);

            var btnAll = new Button { Content = I18n.T("btn_select_all"), Style = (Style)Theme.GetStyle("GhostButtonStyle"), Width = 110, Margin = new Thickness(0,0,6,0) };
            btnAll.Click += (s, e) => { foreach (var c in checkable) c.IsChecked = true; };
            var btnNone = new Button { Content = I18n.T("btn_deselect_all"), Style = (Style)Theme.GetStyle("GhostButtonStyle"), Width = 110, Margin = new Thickness(0,0,6,0) };
            btnNone.Click += (s, e) => { foreach (var c in checkable) c.IsChecked = false; };
            var btnClean = new Button { Content = I18n.T("btn_clean_selected"), Style = (Style)Theme.GetStyle("DangerButtonStyle"), Width = 150, Margin = new Thickness(0,0,6,0) };
            var btnClose = new Button { Content = I18n.T("btn_close"), Style = (Style)Theme.GetStyle("GhostButtonStyle"), Width = 100 };
            btnClean.Click += async (s, e) =>
            {
                var chosen = checkable.Where(c => c.IsChecked).Select(c => c.Finding).ToList();
                if (chosen.Count == 0) { Info("", I18n.T("no_items_selected")); return; }
                if (!Confirm(I18n.T("confirm_delete_residual_title"), string.Format(I18n.T("confirm_delete_residual_msg"), chosen.Count))) return;
                btnAll.IsEnabled = false; btnNone.IsEnabled = false; btnClean.IsEnabled = false; btnClose.IsEnabled = false;
                progressPanel.Visibility = Visibility.Visible;
                progressBar.IsIndeterminate = true;
                progressLbl.Text = I18n.T("progress_creating_restore_point");
                var dispatcher = w.Dispatcher;
                var result = await Task.Run(() => SmartWizard.Clean(chosen, (idx, total) =>
                {
                    dispatcher.BeginInvoke((Action)(() =>
                    {
                        progressBar.IsIndeterminate = false;
                        progressBar.Value = (idx * 100.0) / total;
                        progressLbl.Text = string.Format(I18n.T("progress_deleting_x_of_y"), idx + 1, total);
                    }));
                }));
                progressBar.Value = 100;
                summary = result;
                Info(I18n.T("generic_done_title"), string.Format(I18n.T("wizard_clean_summary"), JunkCleanerData.FormatSize(summary.JunkBytesFreed), summary.StartupItemsCleaned, summary.EmptyFoldersRemoved));
                w.Close();
            };
            btnClose.Click += (s, e) => w.Close();

            bottom.Children.Add(btnAll);
            bottom.Children.Add(btnNone);
            bottom.Children.Add(btnClean);
            bottom.Children.Add(btnClose);
            root.Children.Add(bottom);

            var listBox = new ListBox { Margin = new Thickness(14,0,14,8), Background = Theme.Get("PanelBrush"), BorderBrush = Theme.Get("BorderColorBrush") };
            var template = new DataTemplate();
            var cbFactory = new FrameworkElementFactory(typeof(CheckBox));
            cbFactory.SetBinding(CheckBox.IsCheckedProperty, new System.Windows.Data.Binding("IsChecked") { Mode = System.Windows.Data.BindingMode.TwoWay });
            cbFactory.SetBinding(CheckBox.ContentProperty, new System.Windows.Data.Binding("Display"));
            cbFactory.SetValue(CheckBox.ForegroundProperty, Theme.Get("TextBrush"));
            cbFactory.SetValue(CheckBox.PaddingProperty, new Thickness(6,2,6,2));
            template.VisualTree = cbFactory;
            listBox.ItemTemplate = template;
            listBox.ItemsSource = checkable;
            root.Children.Add(listBox);

            w.ShowDialog();
            return summary;
        }

        // חלון קלט טקסט פשוט (שם תוכנית וכו'). מחזיר null אם בוטל.
        public static string PromptForText(string title, string message, string defaultValue)
        {
            var w = StyledDialog(title, 480, 230);
            w.ResizeMode = ResizeMode.NoResize;
            string result = null;

            var panel = new StackPanel { Margin = new Thickness(20) };
            w.Content = panel;
            panel.Children.Add(new TextBlock { Text = message, Foreground = Theme.Get("TextBrush"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,10) });
            var txt = new TextBox { Text = defaultValue ?? "", Height = 28, FontSize = 13 };
            panel.Children.Add(txt);

            var bottom = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = I18n.IsRtl ? HorizontalAlignment.Right : HorizontalAlignment.Left, Margin = new Thickness(0,16,0,0) };
            var btnOk = new Button { Content = I18n.T("btn_save_fingerprint"), Style = (Style)Theme.GetStyle("AccentButtonStyle"), Width = 110, Margin = new Thickness(0,0,6,0) };
            btnOk.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txt.Text)) { Info("", I18n.T("monitor_enter_name")); return; }
                result = txt.Text.Trim();
                w.Close();
            };
            var btnCancel = new Button { Content = I18n.T("btn_cancel"), Style = (Style)Theme.GetStyle("GhostButtonStyle"), Width = 100 };
            btnCancel.Click += (s, e) => w.Close();
            bottom.Children.Add(btnOk);
            bottom.Children.Add(btnCancel);
            panel.Children.Add(bottom);

            txt.KeyDown += (s, e) => { if (e.Key == Key.Enter) btnOk.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); };
            w.ShowDialog();
            return result;
        }

        public static void ShowTextReport(string title, string text)
        {
            var w = StyledDialog(title, 720, 520);
            var dock = new DockPanel();
            w.Content = dock;

            var bottom = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = I18n.IsRtl ? HorizontalAlignment.Right : HorizontalAlignment.Left, Margin = new Thickness(14,0,14,14) };
            DockPanel.SetDock(bottom, Dock.Bottom);
            var btnClose = new Button { Content = I18n.T("btn_close"), Style = (Style)Theme.GetStyle("GhostButtonStyle"), Width = 100 };
            btnClose.Click += (s, e) => w.Close();
            bottom.Children.Add(btnClose);
            dock.Children.Add(bottom);

            // ה-TextBox חייב להיות ה-child האחרון כדי שימלא את השטח הנותר
            var box = new TextBox
            {
                Text = text, IsReadOnly = true, TextWrapping = TextWrapping.NoWrap, AcceptsReturn = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = new FontFamily("Consolas"), FontSize = 12, Margin = new Thickness(14,14,14,8),
                Background = Theme.Get("PanelBrush"), Foreground = Theme.Get("TextBrush"), BorderBrush = Theme.Get("BorderColorBrush")
            };
            dock.Children.Add(box);

            w.ShowDialog();
        }

        // רשימת הקבצים הגדולים ביותר בכונן - תצוגה בלבד עם אפשרות לפתוח את
        // התיקייה המכילה; לא משולב עם מחיקה כדי לשמור על היקף מצומצם וברור.
        public static void ShowLargestFiles(List<SpaceEntry> files)
        {
            var w = StyledDialog(I18n.T("largest_files_title"), 780, 540);
            var dock = new DockPanel();
            w.Content = dock;

            var bottom = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = I18n.IsRtl ? HorizontalAlignment.Right : HorizontalAlignment.Left, Margin = new Thickness(14,0,14,14) };
            DockPanel.SetDock(bottom, Dock.Bottom);
            var btnOpenFolder = new Button { Content = I18n.T("btn_open_containing_folder"), Style = (Style)Theme.GetStyle("GhostButtonStyle"), Width = 150, Margin = new Thickness(0,0,6,0) };
            var btnClose = new Button { Content = I18n.T("btn_close"), Style = (Style)Theme.GetStyle("GhostButtonStyle"), Width = 100 };
            bottom.Children.Add(btnOpenFolder);
            bottom.Children.Add(btnClose);
            dock.Children.Add(bottom);

            var grid = new DataGrid
            {
                Style = (Style)Theme.GetStyle("ModernDataGridStyle"),
                ColumnHeaderStyle = (Style)Theme.GetStyle("ModernColumnHeaderStyle"),
                CellStyle = (Style)Theme.GetStyle("ModernCellStyle"),
                RowStyle = (Style)Theme.GetStyle("ModernRowStyle"),
                Margin = new Thickness(14,14,14,8),
                ItemsSource = files
            };
            grid.Columns.Add(new DataGridTextColumn { Header = I18n.T("col_space_name"), Binding = new System.Windows.Data.Binding("Name"), Width = new DataGridLength(2, DataGridLengthUnitType.Star) });
            grid.Columns.Add(new DataGridTextColumn { Header = I18n.T("col_space_size"), Binding = new System.Windows.Data.Binding("SizeText"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            grid.Columns.Add(new DataGridTextColumn { Header = I18n.T("col_reg_path"), Binding = new System.Windows.Data.Binding("Path"), Width = new DataGridLength(3, DataGridLengthUnitType.Star) });
            dock.Children.Add(grid);

            btnOpenFolder.Click += (s, e) =>
            {
                var sel = grid.SelectedItem as SpaceEntry;
                if (sel == null) return;
                try { System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + sel.Path + "\""); } catch { }
            };
            btnClose.Click += (s, e) => w.Close();

            w.ShowDialog();
        }

        // אשף אבטחה: מציג רק בעיות אמיתיות שנמצאו (לא "סטטוס" כללי) עם כפתור
        // תיקון ייעודי לכל אחת. issues מגיע כבר סרוק (הסריקה עצמה רצה מחוץ
        // לדיאלוג, ב-Task.Run, כדי לא לחסום את חוט ה-UI לפני שהחלון נפתח).
        public static void ShowSecurityWizard(List<SecurityIssue> issues)
        {
            var w = StyledDialog(I18n.T("secwiz_title"), 720, 540);
            var root = new DockPanel();
            w.Content = root;

            var bottom = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = I18n.IsRtl ? HorizontalAlignment.Right : HorizontalAlignment.Left, Margin = new Thickness(16,0,16,14) };
            DockPanel.SetDock(bottom, Dock.Bottom);
            var btnClose = new Button { Content = I18n.T("btn_close"), Style = (Style)Theme.GetStyle("GhostButtonStyle"), Width = 100 };
            btnClose.Click += (s, e) => w.Close();
            bottom.Children.Add(btnClose);
            root.Children.Add(bottom);

            var scroll = new ScrollViewer { Margin = new Thickness(16,14,16,8) };
            var listPanel = new StackPanel();
            scroll.Content = listPanel;
            root.Children.Add(scroll);

            if (issues.Count == 0)
            {
                listPanel.Children.Add(new TextBlock { Text = I18n.T("secwiz_all_good"), Foreground = Theme.Get("TextBrush"), FontSize = 14, TextWrapping = TextWrapping.Wrap });
            }
            else
            {
                foreach (var issue in issues)
                {
                    var card = new Border { Background = Theme.Get("PanelBrush"), BorderBrush = Theme.Get("DangerBrush"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(14), Margin = new Thickness(0,0,0,10) };
                    var row = new DockPanel();
                    var text = string.IsNullOrEmpty(issue.DetailText) ? I18n.T(issue.TitleKey) : string.Format(I18n.T(issue.TitleKey), issue.DetailText);
                    var textBlock = new TextBlock { Text = text, Foreground = Theme.Get("TextBrush"), TextWrapping = TextWrapping.Wrap, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
                    row.Children.Add(textBlock);

                    if (issue.FixButtonKey != null)
                    {
                        var btnFix = new Button { Content = I18n.T(issue.FixButtonKey), Style = (Style)Theme.GetStyle("AccentButtonStyle"), Width = 170, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10,0,0,0) };
                        DockPanel.SetDock(btnFix, I18n.IsRtl ? Dock.Left : Dock.Right);
                        var capturedIssue = issue;
                        btnFix.Click += async (s, e) =>
                        {
                            btnFix.IsEnabled = false;
                            var original = btnFix.Content;
                            bool handled = true;
                            switch (capturedIssue.Kind)
                            {
                                case SecurityIssueKind.DefenderRtpOff:
                                    SecurityData.OpenWindowsSecurityApp();
                                    break;
                                case SecurityIssueKind.UpdateStale:
                                    SecurityData.OpenWindowsUpdateSettings();
                                    break;
                                case SecurityIssueKind.FirewallProfileOff:
                                    btnFix.Content = "...";
                                    handled = await Task.Run(() => SecurityData.SetFirewallProfileEnabled(capturedIssue.ProfileName, true));
                                    break;
                                case SecurityIssueKind.NoRecentScan:
                                    if (!Confirm(I18n.T("btn_run_quick_scan"), I18n.T("sec_scan_running"))) { btnFix.IsEnabled = true; return; }
                                    btnFix.Content = I18n.T("sec_scan_running");
                                    var scanResult = await Task.Run(() =>
                                    {
                                        string error;
                                        bool ok = SecurityData.RunQuickScan(out error);
                                        return Tuple.Create(ok, error);
                                    });
                                    handled = scanResult.Item1;
                                    break;
                            }
                            if (handled) { btnFix.Content = I18n.T("secwiz_fixed"); }
                            else { btnFix.IsEnabled = true; btnFix.Content = original; Info("", I18n.T("secwiz_fix_failed")); }
                        };
                        row.Children.Add(btnFix);
                    }
                    card.Child = row;
                    listPanel.Children.Add(card);
                }
            }

            w.ShowDialog();
        }

        // אשף פרטיות: בוחרים פרופיל (מאוזן/מקסימלי) ורואים בדיוק אילו הגדרות
        // ישתנו לפני שמאשרים - שום דבר לא מוחל עד לחיצה על "החל".
        public static void ShowPrivacyWizard()
        {
            var w = StyledDialog(I18n.T("privwiz_title"), 680, 560);
            var root = new DockPanel();
            w.Content = root;

            var bottom = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = I18n.IsRtl ? HorizontalAlignment.Right : HorizontalAlignment.Left, Margin = new Thickness(16,0,16,14) };
            DockPanel.SetDock(bottom, Dock.Bottom);
            var btnApply = new Button { Content = I18n.T("btn_apply_profile"), Style = (Style)Theme.GetStyle("AccentButtonStyle"), Width = 180, Margin = new Thickness(0,0,6,0) };
            var btnClose = new Button { Content = I18n.T("btn_close"), Style = (Style)Theme.GetStyle("GhostButtonStyle"), Width = 100 };
            btnClose.Click += (s, e) => w.Close();
            bottom.Children.Add(btnApply);
            bottom.Children.Add(btnClose);
            root.Children.Add(bottom);

            var intro = new TextBlock { Text = I18n.T("privwiz_intro"), Margin = new Thickness(16,14,16,10), TextWrapping = TextWrapping.Wrap, Foreground = Theme.Get("TextMutedBrush") };
            DockPanel.SetDock(intro, Dock.Top);
            root.Children.Add(intro);

            var scroll = new ScrollViewer { Margin = new Thickness(16,0,16,8) };
            var content = new StackPanel();
            scroll.Content = content;
            root.Children.Add(scroll);

            var profileRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0,0,0,14) };
            var rbBalanced = new RadioButton { Content = I18n.T("privwiz_profile_balanced"), GroupName = "profile", IsChecked = true, Foreground = Theme.Get("TextBrush"), Margin = new Thickness(0,0,20,0), FontWeight = FontWeights.Bold };
            var rbMax = new RadioButton { Content = I18n.T("privwiz_profile_maximum"), GroupName = "profile", Foreground = Theme.Get("TextBrush"), FontWeight = FontWeights.Bold };
            profileRow.Children.Add(rbBalanced);
            profileRow.Children.Add(rbMax);
            content.Children.Add(profileRow);

            var descLbl = new TextBlock { Foreground = Theme.Get("TextMutedBrush"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,14) };
            content.Children.Add(descLbl);

            content.Children.Add(new TextBlock { Text = I18n.T("privwiz_preview_header"), FontWeight = FontWeights.Bold, Foreground = Theme.Get("TextBrush"), Margin = new Thickness(0,0,0,8) });
            var previewPanel = new StackPanel();
            content.Children.Add(previewPanel);

            var allToggles = PrivacyData.GetAll();

            Action refresh = () =>
            {
                var profile = rbMax.IsChecked == true ? PrivacyProfile.Maximum : PrivacyProfile.Balanced;
                descLbl.Text = profile == PrivacyProfile.Maximum ? I18n.T("privwiz_profile_maximum_desc") : I18n.T("privwiz_profile_balanced_desc");
                previewPanel.Children.Clear();
                var proposed = PrivacyWizard.GetProfileValues(profile);
                foreach (var toggle in allToggles)
                {
                    bool newVal = proposed.ContainsKey(toggle.Key) ? proposed[toggle.Key] : toggle.CurrentState;
                    if (newVal == toggle.CurrentState) continue;
                    var row = new DockPanel { Margin = new Thickness(0,0,0,6) };
                    var arrow = (toggle.CurrentState ? I18n.T("sec_on") : I18n.T("sec_off")) + "  →  " + (newVal ? I18n.T("sec_on") : I18n.T("sec_off"));
                    row.Children.Add(new TextBlock { Text = I18n.T(toggle.LabelKey), Foreground = Theme.Get("TextBrush"), Width = 260 });
                    row.Children.Add(new TextBlock { Text = arrow, Foreground = newVal ? Theme.Get("AccentBrush") : Theme.Get("DangerBrush") });
                    previewPanel.Children.Add(row);
                }
                if (previewPanel.Children.Count == 0) previewPanel.Children.Add(new TextBlock { Text = "-", Foreground = Theme.Get("TextMutedBrush") });
            };

            rbBalanced.Checked += (s, e) => refresh();
            rbMax.Checked += (s, e) => refresh();
            refresh();

            btnApply.Click += (s, e) =>
            {
                var profile = rbMax.IsChecked == true ? PrivacyProfile.Maximum : PrivacyProfile.Balanced;
                PrivacyWizard.Apply(profile);
                Info(I18n.T("generic_done_title"), I18n.T("privwiz_applied_msg"));
                w.Close();
            };

            w.ShowDialog();
        }
    }
}
