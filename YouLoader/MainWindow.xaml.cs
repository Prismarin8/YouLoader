using Microsoft.Data.SqlClient;
using Microsoft.Win32;
using System;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfButton = System.Windows.Controls.Button;

namespace YouLoader
{
    public partial class MainWindow : Window
    {
        private readonly string connectionString;
        private System.Windows.Forms.NotifyIcon? notifyIcon;
        private CancellationTokenSource? _cts;
        private Process? _currentProcess;

        public MainWindow()
        {
            InitializeComponent();
            connectionString = @"Data Source=DESKTOP-G41BP9H\SQLEXPRESS;Initial Catalog=YouTubeDownloaderDB;Integrated Security=True;Trust Server Certificate=True";
            LoadThemePreference();
            InitializeNotifyIcon();
            LoadSettings();
            _ = LoadHistoryWithSkeleton();
            _ = LoadQueueWithSkeleton();
        }

        // ==================== ТЁМНАЯ ТЕМА ====================
        private void LoadThemePreference()
        {
            using var conn = new SqlConnection(connectionString);
            conn.Open();
            var cmd = new SqlCommand("SELECT Value FROM Settings WHERE [Key]='UseDarkTheme'", conn);
            var result = cmd.ExecuteScalar();
            bool isDark = result != null && result.ToString() == "True";
            ThemeToggle.IsChecked = isDark;
            ApplyTheme(isDark);
        }

        private void ApplyTheme(bool isDark)
        {
            var resources = this.Resources;
            if (isDark)
            {
                resources["BackgroundBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(18, 18, 18));
                resources["CardBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 30, 30));
                resources["TextPrimary"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(233, 236, 239));
                resources["TextSecondary"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(173, 181, 189));
                resources["BorderBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(44, 44, 44));
                resources["HoverBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(42, 42, 42));
                resources["LogBackground"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(37, 37, 37));
            }
            else
            {
                resources["BackgroundBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 249, 250));
                resources["CardBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 255, 255));
                resources["TextPrimary"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(26, 26, 46));
                resources["TextSecondary"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(108, 117, 125));
                resources["BorderBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(233, 236, 239));
                resources["HoverBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(254, 243, 232));
                resources["LogBackground"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 249, 250));
            }
        }

        private void ThemeToggle_Checked(object sender, RoutedEventArgs e) => ApplyTheme(true);
        private void ThemeToggle_Unchecked(object sender, RoutedEventArgs e) => ApplyTheme(false);

        // ==================== УВЕДОМЛЕНИЯ ====================
        private void InitializeNotifyIcon()
        {
            notifyIcon = new System.Windows.Forms.NotifyIcon();
            notifyIcon.Icon = System.Drawing.SystemIcons.Information;
            notifyIcon.Visible = true;
            notifyIcon.Text = "YouLoader";
        }

        private void ShowNotification(string title, string message)
        {
            if (notifyIcon != null)
            {
                notifyIcon.BalloonTipTitle = title;
                notifyIcon.BalloonTipText = message;
                notifyIcon.ShowBalloonTip(3000);
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            _cts?.Cancel();
            if (_currentProcess != null && !_currentProcess.HasExited)
                _currentProcess.Kill();
            notifyIcon?.Dispose();
            base.OnClosing(e);
        }

        // ==================== SKELETON LOADING ====================
        private async Task LoadHistoryWithSkeleton() { await Task.Delay(100); await Dispatcher.InvokeAsync(() => LoadHistory()); }
        private async Task LoadQueueWithSkeleton() { await Task.Delay(100); await Dispatcher.InvokeAsync(() => LoadQueue()); }

        // ==================== НАСТРОЙКИ ====================
        private void LoadSettings()
        {
            using var conn = new SqlConnection(connectionString);
            conn.Open();
            var cmd = new SqlCommand("SELECT [Key], Value FROM Settings", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                string key = reader["Key"]?.ToString() ?? "";
                string val = reader["Value"]?.ToString() ?? "";
                switch (key)
                {
                    case "DownloadPath": txtDownloadPath.Text = val; break;
                    case "CookiesPath": txtCookiesPath.Text = val; break;
                    case "BypassBatPath": txtBypassBat.Text = val; break;
                    case "UseCookies": chkUseCookies.IsChecked = bool.TryParse(val, out var b) && b; break;
                    case "DefaultQuality":
                        for (int i = 0; i < cmbQuality.Items.Count; i++)
                            if (cmbQuality.Items[i].ToString() == val) { cmbQuality.SelectedIndex = i; break; }
                        break;
                    case "DefaultFormat":
                        for (int i = 0; i < cmbFormat.Items.Count; i++)
                            if (cmbFormat.Items[i].ToString() == val) { cmbFormat.SelectedIndex = i; break; }
                        break;
                }
            }
        }

        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            using var conn = new SqlConnection(connectionString);
            conn.Open();
            var cmd = new SqlCommand("UPDATE Settings SET Value=@val WHERE [Key]=@key", conn);
            cmd.Parameters.AddWithValue("@val", txtDownloadPath.Text);
            cmd.Parameters.AddWithValue("@key", "DownloadPath");
            cmd.ExecuteNonQuery();
            cmd.Parameters["@key"].Value = "CookiesPath";
            cmd.Parameters["@val"].Value = txtCookiesPath.Text;
            cmd.ExecuteNonQuery();
            cmd.Parameters["@key"].Value = "BypassBatPath";
            cmd.Parameters["@val"].Value = txtBypassBat.Text;
            cmd.ExecuteNonQuery();
            cmd.Parameters["@key"].Value = "UseCookies";
            cmd.Parameters["@val"].Value = chkUseCookies.IsChecked.ToString();
            cmd.ExecuteNonQuery();
            cmd.Parameters["@key"].Value = "DefaultQuality";
            cmd.Parameters["@val"].Value = cmbQuality.SelectedItem?.ToString() ?? "Лучшее (видео+аудио)";
            cmd.ExecuteNonQuery();
            cmd.Parameters["@key"].Value = "DefaultFormat";
            cmd.Parameters["@val"].Value = cmbFormat.SelectedItem?.ToString() ?? "MP4";
            cmd.ExecuteNonQuery();
            System.Windows.MessageBox.Show("Настройки сохранены", "YouLoader", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void CancelSettings_Click(object sender, RoutedEventArgs e) => LoadSettings();

        private void SelectDownloadFolder_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new System.Windows.Forms.FolderBrowserDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                txtDownloadPath.Text = dialog.SelectedPath;
        }

        private void SelectCookiesFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "txt files|*.txt" };
            if (dialog.ShowDialog() == true)
                txtCookiesPath.Text = dialog.FileName;
        }

        private void SelectBypassBat_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "bat files|*.bat" };
            if (dialog.ShowDialog() == true)
                txtBypassBat.Text = dialog.FileName;
        }

        // ==================== ИСТОРИЯ ====================
        private void LoadHistory()
        {
            using var conn = new SqlConnection(connectionString);
            conn.Open();
            var da = new SqlDataAdapter("SELECT Id, Url, Title, Quality, Format, DateAdded, IsSuccess FROM Downloads ORDER BY DateAdded DESC", conn);
            var dt = new DataTable();
            da.Fill(dt);
            dgHistory.ItemsSource = dt.DefaultView;
        }

        private void History_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (dgHistory.SelectedItem is DataRowView row)
            {
                txtUrl.Text = row["Url"]?.ToString() ?? "";
                string quality = row["Quality"]?.ToString() ?? "";
                for (int i = 0; i < cmbQuality.Items.Count; i++)
                    if (cmbQuality.Items[i].ToString() == quality) { cmbQuality.SelectedIndex = i; break; }
                string format = row["Format"]?.ToString() ?? "";
                for (int i = 0; i < cmbFormat.Items.Count; i++)
                    if (cmbFormat.Items[i].ToString() == format) { cmbFormat.SelectedIndex = i; break; }
                mainTabControl.SelectedIndex = 0;
            }
        }

        private void HistoryReDownload_Click(object sender, RoutedEventArgs e)
        {
            if (dgHistory.SelectedItem is DataRowView row)
            {
                txtUrl.Text = row["Url"]?.ToString() ?? "";
                string quality = row["Quality"]?.ToString() ?? "";
                string format = row["Format"]?.ToString() ?? "";
                for (int i = 0; i < cmbQuality.Items.Count; i++)
                    if (cmbQuality.Items[i].ToString() == quality) { cmbQuality.SelectedIndex = i; break; }
                for (int i = 0; i < cmbFormat.Items.Count; i++)
                    if (cmbFormat.Items[i].ToString() == format) { cmbFormat.SelectedIndex = i; break; }
                mainTabControl.SelectedIndex = 0;
            }
        }

        private void HistoryOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            if (dgHistory.SelectedItem is DataRowView row)
            {
                string folderPath = row["FilePath"]?.ToString() ?? "";
                if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath))
                    Process.Start("explorer.exe", folderPath);
                else
                    System.Windows.MessageBox.Show("Папка не найдена", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void HistoryCopyUrl_Click(object sender, RoutedEventArgs e)
        {
            if (dgHistory.SelectedItem is DataRowView row)
            {
                string url = row["Url"]?.ToString() ?? "";
                if (!string.IsNullOrEmpty(url))
                {
                    System.Windows.Clipboard.SetText(url);
                    System.Windows.MessageBox.Show("Ссылка скопирована", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        // ==================== ОЧЕРЕДЬ ====================
        private void LoadQueue()
        {
            using var conn = new SqlConnection(connectionString);
            conn.Open();
            var da = new SqlDataAdapter("SELECT Id, Url, Quality, Format, Position FROM Queue ORDER BY Position", conn);
            var dt = new DataTable();
            da.Fill(dt);
            dgQueue.ItemsSource = dt.DefaultView;
        }

        private void BtnAddToQueue_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtUrl.Text))
            {
                System.Windows.MessageBox.Show("Введите ссылку", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            using var conn = new SqlConnection(connectionString);
            conn.Open();
            var cmd = new SqlCommand("SELECT ISNULL(MAX(Position),0)+1 FROM Queue", conn);
            int nextPos = Convert.ToInt32(cmd.ExecuteScalar());
            cmd.CommandText = "INSERT INTO Queue (Url, Quality, Format, Position) VALUES (@url, @qual, @fmt, @pos)";
            cmd.Parameters.AddWithValue("@url", txtUrl.Text);
            cmd.Parameters.AddWithValue("@qual", cmbQuality.SelectedItem?.ToString());
            cmd.Parameters.AddWithValue("@fmt", cmbFormat.SelectedItem?.ToString());
            cmd.Parameters.AddWithValue("@pos", nextPos);
            cmd.ExecuteNonQuery();
            LoadQueue();
        }

        private void RemoveFromQueue_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as WpfButton;
            if (btn?.Tag == null) return;
            int id = Convert.ToInt32(btn.Tag);
            using var conn = new SqlConnection(connectionString);
            conn.Open();
            var cmd = new SqlCommand("DELETE FROM Queue WHERE Id=@id", conn);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
            LoadQueue();
        }

        private void MoveUpQueue_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as WpfButton;
            if (btn?.Tag == null) return;
            int id = Convert.ToInt32(btn.Tag);
            using var conn = new SqlConnection(connectionString);
            conn.Open();
            var cmd = new SqlCommand("SELECT Position FROM Queue WHERE Id=@id", conn);
            cmd.Parameters.AddWithValue("@id", id);
            int currentPos = Convert.ToInt32(cmd.ExecuteScalar());
            if (currentPos <= 1) return;
            cmd.CommandText = "SELECT Id FROM Queue WHERE Position=@pos";
            cmd.Parameters.AddWithValue("@pos", currentPos - 1);
            int prevId = Convert.ToInt32(cmd.ExecuteScalar());
            cmd.CommandText = "UPDATE Queue SET Position=@pos WHERE Id=@id";
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@pos", currentPos - 1);
            cmd.ExecuteNonQuery();
            cmd.Parameters["@id"].Value = prevId;
            cmd.Parameters["@pos"].Value = currentPos;
            cmd.ExecuteNonQuery();
            LoadQueue();
        }

        private void MoveDownQueue_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as WpfButton;
            if (btn?.Tag == null) return;
            int id = Convert.ToInt32(btn.Tag);
            using var conn = new SqlConnection(connectionString);
            conn.Open();
            var cmd = new SqlCommand("SELECT Position FROM Queue WHERE Id=@id", conn);
            cmd.Parameters.AddWithValue("@id", id);
            int currentPos = Convert.ToInt32(cmd.ExecuteScalar());
            cmd.CommandText = "SELECT COUNT(*) FROM Queue";
            int maxPos = Convert.ToInt32(cmd.ExecuteScalar());
            if (currentPos >= maxPos) return;
            cmd.CommandText = "SELECT Id FROM Queue WHERE Position=@pos";
            cmd.Parameters.AddWithValue("@pos", currentPos + 1);
            int nextId = Convert.ToInt32(cmd.ExecuteScalar());
            cmd.CommandText = "UPDATE Queue SET Position=@pos WHERE Id=@id";
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@pos", currentPos + 1);
            cmd.ExecuteNonQuery();
            cmd.Parameters["@id"].Value = nextId;
            cmd.Parameters["@pos"].Value = currentPos;
            cmd.ExecuteNonQuery();
            LoadQueue();
        }

        private async void StartQueue_Click(object sender, RoutedEventArgs e)
        {
            btnDownload.IsEnabled = false;
            var dt = dgQueue.ItemsSource as DataTable;
            if (dt == null) return;
            foreach (DataRow row in dt.Rows)
            {
                string url = row["Url"]?.ToString() ?? "";
                string qualityDisplay = row["Quality"]?.ToString() ?? "";
                string formatDisplay = row["Format"]?.ToString() ?? "";
                string qualityArg = MapQualityToArgument(qualityDisplay);
                string formatArg = string.IsNullOrEmpty(formatDisplay) ? "mp4" : formatDisplay.ToLower();
                if (qualityDisplay?.Contains("Только аудио") == true)
                    formatArg = "mp3";
                await DownloadVideoAsync(url, qualityArg, formatArg);
                int id = Convert.ToInt32(row["Id"]);
                using var conn = new SqlConnection(connectionString);
                conn.Open();
                var cmd = new SqlCommand("DELETE FROM Queue WHERE Id=@id", conn);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
                await Dispatcher.InvokeAsync(() => LoadQueue());
            }
            await Dispatcher.InvokeAsync(() => LoadQueue());
            btnDownload.IsEnabled = true;
        }

        private void ClearQueue_Click(object sender, RoutedEventArgs e)
        {
            using var conn = new SqlConnection(connectionString);
            conn.Open();
            new SqlCommand("DELETE FROM Queue", conn).ExecuteNonQuery();
            LoadQueue();
        }

        // ==================== СКАЧИВАНИЕ (ВЫЗОВ PYTHON) – ИСПРАВЛЕНО ====================
        private async void BtnDownload_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtUrl.Text))
            {
                System.Windows.MessageBox.Show("Введите ссылку", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            string qualityDisplay = cmbQuality.SelectedItem?.ToString();
            string qualityArg = MapQualityToArgument(qualityDisplay);

            // ИСПРАВЛЕНО: получаем чистый формат из Content ComboBoxItem
            string formatArg = "mp4";
            if (cmbFormat.SelectedItem is ComboBoxItem formatItem)
                formatArg = formatItem.Content.ToString().ToLower();

            if (qualityDisplay?.Contains("Только аудио") == true)
                formatArg = "mp3";

            await DownloadVideoAsync(txtUrl.Text, qualityArg, formatArg);
        }

        private void CancelDownload_Click(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
            if (_currentProcess != null && !_currentProcess.HasExited)
                _currentProcess.Kill();
            txtLog.AppendText("Загрузка отменена пользователем.\n");
        }

        private string MapQualityToArgument(string display)
        {
            if (string.IsNullOrEmpty(display)) return "bestvideo+bestaudio/best";
            return display switch
            {
                "🎬 Лучшее (видео+аудио)" => "bestvideo+bestaudio/best",
                "📺 1080p" => "bestvideo[height<=1080]+bestaudio",
                "📺 720p" => "bestvideo[height<=720]+bestaudio",
                "📱 360p" => "18",
                "🎵 Только аудио (MP3)" => "bestaudio",
                _ => "bestvideo+bestaudio/best"
            };
        }

        private async Task DownloadVideoAsync(string url, string quality, string format)
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            string outputDir = txtDownloadPath.Text.Trim();
            if (string.IsNullOrEmpty(outputDir)) outputDir = Path.Combine(Environment.CurrentDirectory, "Downloads");
            Directory.CreateDirectory(outputDir);

            string cookiesPath = chkUseCookies.IsChecked == true ? txtCookiesPath.Text : null;
            string bypassBat = txtBypassBat.Text;

            string pythonExe = "python"; // или полный путь "C:\\Python312\\python.exe"
            string scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "my_downloader.py");
            if (!File.Exists(scriptPath))
            {
                System.Windows.MessageBox.Show("Скрипт my_downloader.py не найден в папке приложения.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var args = new StringBuilder();
            args.Append($"\"{scriptPath}\" ");
            args.Append($"--url \"{url}\" ");
            args.Append($"--quality \"{quality}\" ");
            args.Append($"--output-format \"{format}\" ");   // ← теперь format правильный
            args.Append($"--output-dir \"{outputDir}\" ");
            if (!string.IsNullOrEmpty(cookiesPath) && File.Exists(cookiesPath))
                args.Append($"--cookies \"{cookiesPath}\" ");
            if (!string.IsNullOrEmpty(bypassBat) && File.Exists(bypassBat))
                args.Append($"--bypass-bat \"{bypassBat}\" ");

            txtLog.Clear();
            txtLog.AppendText($"Запуск Python: {pythonExe}\n");
            txtLog.AppendText($"Аргументы: {args}\n\n");

            var psi = new ProcessStartInfo
            {
                FileName = pythonExe,
                Arguments = args.ToString(),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            bool success = true;
            string errorMsg = null;
            using (_currentProcess = new Process { StartInfo = psi })
            {
                _currentProcess.OutputDataReceived += (s, e) => Dispatcher.Invoke(() => txtLog.AppendText(e.Data + "\n"));
                _currentProcess.ErrorDataReceived += (s, e) => Dispatcher.Invoke(() => txtLog.AppendText("[ERR] " + e.Data + "\n"));
                _currentProcess.Start();
                _currentProcess.BeginOutputReadLine();
                _currentProcess.BeginErrorReadLine();
                try
                {
                    await _currentProcess.WaitForExitAsync(token);
                    success = _currentProcess.ExitCode == 0;
                    if (!success) errorMsg = "Скрипт завершился с ошибкой";
                }
                catch (OperationCanceledException)
                {
                    _currentProcess.Kill();
                    txtLog.AppendText("Загрузка отменена.\n");
                    success = false;
                    errorMsg = "Отменено пользователем";
                }
                finally
                {
                    _currentProcess = null;
                }
            }

            string title = "Видео с YouTube";
            using var conn = new SqlConnection(connectionString);
            conn.Open();
            var cmd = new SqlCommand(@"INSERT INTO Downloads (Url, Title, FilePath, Quality, Format, IsSuccess, ErrorMessage) 
                                       VALUES (@url, @title, @path, @qual, @fmt, @success, @err)", conn);
            cmd.Parameters.AddWithValue("@url", url);
            cmd.Parameters.AddWithValue("@title", title);
            cmd.Parameters.AddWithValue("@path", outputDir);
            cmd.Parameters.AddWithValue("@qual", quality);
            cmd.Parameters.AddWithValue("@fmt", format);
            cmd.Parameters.AddWithValue("@success", success);
            cmd.Parameters.AddWithValue("@err", errorMsg ?? (object)DBNull.Value);
            cmd.ExecuteNonQuery();

            await Dispatcher.InvokeAsync(() => LoadHistory());
            if (success)
            {
                ShowNotification("YouLoader", "Загрузка завершена!");
                System.Windows.MessageBox.Show("Загрузка завершена!", "YouLoader", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else if (errorMsg != "Отменено пользователем")
            {
                System.Windows.MessageBox.Show("Ошибка загрузки. Проверьте лог.", "YouLoader", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PasteUrl_Click(object sender, RoutedEventArgs e)
        {
            if (System.Windows.Clipboard.ContainsText())
                txtUrl.Text = System.Windows.Clipboard.GetText();
        }

        private void ClearLog_Click(object sender, RoutedEventArgs e) => txtLog.Clear();
    }
}