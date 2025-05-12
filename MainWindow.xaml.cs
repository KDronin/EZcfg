using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace LOLConfigCloud
{
    public partial class MainWindow : Window
    {
        private const string ServerUrl = "https://xxx.xxx.xxx/upload.php";
        private const string BackupExtension = ".lolcfgbak";
        private string _gameConfigDir;
        private List<CloudFileInfo> _cloudFiles = new List<CloudFileInfo>();
        private readonly DispatcherTimer _refreshTimer;
        private readonly string[] _configFiles = { "game.cfg", "PersistedSettings.json" };

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;

            // Setup timer to periodically check for game directory
            _refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            _refreshTimer.Tick += RefreshTimer_Tick;
            _refreshTimer.Start();
        }

        private void RefreshTimer_Tick(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_gameConfigDir) || !Directory.Exists(_gameConfigDir))
            {
                _gameConfigDir = GetGameConfigDirectory();
                if (!string.IsNullOrEmpty(_gameConfigDir))
                {
                    RefreshFileStatus();
                }
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _gameConfigDir = GetGameConfigDirectory();
            RefreshFileStatus();
            RefreshCloudFileList();
        }

        private string GetGameConfigDirectory()
        {
            try
            {
                string clientPath = GetProcessPath("LeagueClientUxRender");
                if (!string.IsNullOrEmpty(clientPath))
                {
                    return Path.GetDirectoryName(clientPath)
                        .Replace("LeagueClient", "") + @"Game\Config";
                }
            }
            catch { }
            return null;
        }

        private string GetProcessPath(string processName)
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    $"SELECT ExecutablePath FROM Win32_Process WHERE Name = '{processName}.exe'"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        return obj["ExecutablePath"]?.ToString();
                    }
                }
            }
            catch { }
            return null;
        }

        private void RefreshFileStatus()
        {
            if (string.IsNullOrEmpty(_gameConfigDir) || !Directory.Exists(_gameConfigDir))
            {
                LocalFilesStatus.Text = "未找到游戏配置目录";
                LocalFilesStatus.Foreground = Brushes.Red;
                return;
            }

            bool allExist = true;
            bool allLocked = true;
            bool anyExist = false;

            foreach (var file in _configFiles)
            {
                string filePath = Path.Combine(_gameConfigDir, file);
                if (File.Exists(filePath))
                {
                    anyExist = true;
                    bool isReadOnly = (File.GetAttributes(filePath) & FileAttributes.ReadOnly) == FileAttributes.ReadOnly;
                    if (!isReadOnly) allLocked = false;
                }
                else
                {
                    allExist = false;
                }
            }

            if (!anyExist)
            {
                LocalFilesStatus.Text = "配置文件不存在";
                LocalFilesStatus.Foreground = Brushes.Red;
            }
            else if (allExist)
            {
                LocalFilesStatus.Text = allLocked ? "配置文件已锁定" : "配置文件未锁定";
                LocalFilesStatus.Foreground = allLocked ? Brushes.Green : Brushes.LightSkyBlue;
            }
            else
            {
                LocalFilesStatus.Text = "部分配置文件存在";
                LocalFilesStatus.Foreground = Brushes.Orange;
            }
        }

        private async void RefreshCloudFileList()
        {
            try
            {
                string apiKey = TxtApiKey.Text.Trim();
                if (string.IsNullOrEmpty(apiKey))
                {
                    return;
                }

                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "LOLConfigCloud");
                    client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                    var response = await client.GetAsync($"{ServerUrl}?key={apiKey}");
                    if (response.IsSuccessStatusCode)
                    {
                        string content = await response.Content.ReadAsStringAsync();
                        var files = System.Text.Json.JsonSerializer.Deserialize<List<CloudFileInfo>>(content);
                        _cloudFiles = files;
                        DgCloudFiles.ItemsSource = _cloudFiles;
                    }
                    else
                    {
                        string error = await response.Content.ReadAsStringAsync();
                        MessageBox.Show($"获取云端文件列表失败: {error}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"刷新失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnUpload_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string apiKey = TxtApiKey.Text.Trim();
                if (string.IsNullOrEmpty(apiKey))
                {
                    MessageBox.Show("请输入API密钥", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                if (string.IsNullOrEmpty(_gameConfigDir) || !Directory.Exists(_gameConfigDir))
                {
                    MessageBox.Show("未找到游戏配置目录", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "LOLConfigCloud");
                    bool anyUploaded = false;

                    foreach (var fileName in _configFiles)
                    {
                        string filePath = Path.Combine(_gameConfigDir, fileName);
                        if (!File.Exists(filePath))
                        {
                            continue;
                        }

                        using (var fileStream = File.OpenRead(filePath))
                        using (var content = new StreamContent(fileStream))
                        using (var formData = new MultipartFormDataContent())
                        {
                            formData.Add(content, "file", fileName);
                            var response = await client.PostAsync($"{ServerUrl}?key={apiKey}", formData);

                            if (response.IsSuccessStatusCode)
                            {
                                anyUploaded = true;
                            }
                            else
                            {
                                string error = await response.Content.ReadAsStringAsync();
                                MessageBox.Show($"上传 {fileName} 失败: {error}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        }
                    }

                    if (anyUploaded)
                    {
                        MessageBox.Show("上传操作完成", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                        RefreshCloudFileList();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"上传失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnDownload_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string apiKey = TxtApiKey.Text.Trim();
                if (string.IsNullOrEmpty(apiKey))
                {
                    MessageBox.Show("请输入API密钥", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                if (string.IsNullOrEmpty(_gameConfigDir) || !Directory.Exists(_gameConfigDir))
                {
                    MessageBox.Show("未找到游戏配置目录", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 1. Backup existing files
                Dictionary<string, string> backupFiles = new Dictionary<string, string>();
                bool backupSuccess = true;

                foreach (var fileName in _configFiles)
                {
                    string originalPath = Path.Combine(_gameConfigDir, fileName);
                    string backupPath = originalPath + BackupExtension;

                    if (File.Exists(originalPath))
                    {
                        try
                        {
                            if (File.Exists(backupPath))
                            {
                                File.Delete(backupPath);
                            }
                            File.Move(originalPath, backupPath);
                            backupFiles.Add(originalPath, backupPath);
                        }
                        catch (Exception ex)
                        {
                            backupSuccess = false;
                            MessageBox.Show($"备份文件 {fileName} 失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                            break;
                        }
                    }
                }

                if (!backupSuccess)
                {
                    RestoreBackups(backupFiles);
                    return;
                }

                // 2. Download files
                bool downloadSuccess = true;
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "LOLConfigCloud");

                    foreach (var fileName in _configFiles)
                    {
                        string originalPath = Path.Combine(_gameConfigDir, fileName);
                        try
                        {
                            var response = await client.GetAsync($"{ServerUrl}?key={apiKey}&file={fileName}");

                            if (response.IsSuccessStatusCode)
                            {
                                using (var fileStream = File.Create(originalPath))
                                {
                                    await response.Content.CopyToAsync(fileStream);
                                }
                            }
                            else
                            {
                                downloadSuccess = false;
                                string error = await response.Content.ReadAsStringAsync();
                                MessageBox.Show($"下载 {fileName} 失败: {error}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                                break;
                            }
                        }
                        catch (Exception ex)
                        {
                            downloadSuccess = false;
                            MessageBox.Show($"下载 {fileName} 失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                            break;
                        }
                    }
                }

                // 3. Handle result
                if (downloadSuccess)
                {
                    // Delete backups
                    foreach (var backup in backupFiles.Values)
                    {
                        try
                        {
                            if (File.Exists(backup))
                            {
                                File.Delete(backup);
                            }
                        }
                        catch { }
                    }

                    MessageBox.Show("所有配置文件下载成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    RefreshFileStatus();
                }
                else
                {
                    // Restore backups
                    RestoreBackups(backupFiles);
                    MessageBox.Show("下载失败，已恢复原始文件", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"下载失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RestoreBackups(Dictionary<string, string> backupFiles)
        {
            foreach (var kvp in backupFiles)
            {
                try
                {
                    if (File.Exists(kvp.Key))
                    {
                        File.Delete(kvp.Key);
                    }
                    if (File.Exists(kvp.Value))
                    {
                        File.Move(kvp.Value, kvp.Key);
                    }
                }
                catch { }
            }
        }

        private void SetFilesReadOnly(bool readOnly)
        {
            if (string.IsNullOrEmpty(_gameConfigDir) || !Directory.Exists(_gameConfigDir))
            {
                MessageBox.Show("未找到游戏配置目录", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                foreach (var file in _configFiles)
                {
                    string filePath = Path.Combine(_gameConfigDir, file);
                    if (File.Exists(filePath))
                    {
                        FileAttributes attributes = File.GetAttributes(filePath);
                        if (readOnly)
                            attributes |= FileAttributes.ReadOnly;
                        else
                            attributes &= ~FileAttributes.ReadOnly;

                        File.SetAttributes(filePath, attributes);
                    }
                }
                RefreshFileStatus();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"操作失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnSetReadOnly_Click(object sender, RoutedEventArgs e)
        {
            SetFilesReadOnly(true);
        }

        private void BtnCancelReadOnly_Click(object sender, RoutedEventArgs e)
        {
            SetFilesReadOnly(false);
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            _gameConfigDir = GetGameConfigDirectory();
            RefreshFileStatus();
        }

        private void BtnRefreshCloud_Click(object sender, RoutedEventArgs e)
        {
            RefreshCloudFileList();
        }
    }

    public class CloudFileInfo
    {
        public string Name { get; set; }
        public string Size { get; set; }
        public string ModifiedTime { get; set; }
    }
}
