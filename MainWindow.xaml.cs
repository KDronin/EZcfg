using System;
using System.Collections.Generic;
using System.IO;
using System.Management;
using System.Net.Http;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Input;
using System.Diagnostics;
using System.Security.Principal;
using Newtonsoft.Json;
using System.Net.Http.Headers;

namespace LOLConfigCloud
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            if (!IsAdministrator())
            {
                var processInfo = new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName)
                {
                    Verb = "runas",
                    UseShellExecute = true
                };

                try
                {
                    Process.Start(processInfo);
                    Current.Shutdown();
                    return;
                }
                catch
                {
                    MessageBox.Show("需要管理员权限才能正常运行本程序");
                    Current.Shutdown();
                    return;
                }
            }

            base.OnStartup(e);
        }

        private static bool IsAdministrator()
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    public partial class MainWindow : Window
    {
        private const string ServerUrl = "https://lolcfg.kdrnn.online/upload.php";
        private const string BackupExtension = ".lolcfgbak";
        private string _gameConfigDir;
        private List<CloudFileInfo> _cloudFiles = new List<CloudFileInfo>();
        //private readonly DispatcherTimer _refreshTimer;
        private readonly string[] _configFiles = { "game.cfg", "PersistedSettings.json" };
        private DateTime _lastErrorTime = DateTime.MinValue;
        private bool _isShowingError = false;

        private readonly string _settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Ezcfg",
            "settings.json");

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;

            if (File.Exists(_settingsPath))
            {
                TxtApiKey.Text = JsonConvert.DeserializeObject<dynamic>(File.ReadAllText(_settingsPath))?.ApiKey ?? "";
            }

            this.MouseLeftButtonDown += (s, e) =>
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    this.DragMove();
                }
            };

            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            timer.Tick += (s, e) => {
                _gameConfigDir = GetGameConfigDirectory();
                RefreshFileStatus();
                RefreshCloudFileList();
            };
            timer.Start();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath));
            File.WriteAllText(_settingsPath, JsonConvert.SerializeObject(new { ApiKey = TxtApiKey.Text }));
            this.Close();
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
            catch
            {
                ShowStatusMessage("无法获取游戏路径", Brushes.Red);
            }
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
                    UpdateApiStatusLight(null); // 无API密钥状态
                    return;
                }

                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "LOLConfigCloud");
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    var response = await client.GetAsync($"{ServerUrl}?key={apiKey}");
                    if (response.IsSuccessStatusCode)
                    {
                        string content = await response.Content.ReadAsStringAsync();
                        var files = JsonConvert.DeserializeObject<List<CloudFileInfo>>(content);
                        _cloudFiles = files;
                        DgCloudFiles.ItemsSource = _cloudFiles;
                        UpdateApiStatusLight(true); // 成功状态
                    }
                    else
                    {
                        string error = await response.Content.ReadAsStringAsync();
                        UpdateApiStatusLight(false); // 失败状态
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateApiStatusLight(false); // 失败状态
            }
        }

        private async void BtnUpload_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string apiKey = TxtApiKey.Text.Trim();
                if (string.IsNullOrEmpty(apiKey))
                {
                    ShowStatusMessage("请输入API密钥", Brushes.Red);
                    return;
                }

                if (string.IsNullOrEmpty(_gameConfigDir) || !Directory.Exists(_gameConfigDir))
                {
                    //ShowStatusMessage("未找到游戏配置目录", Brushes.Red);
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
                                ShowStatusMessage($"{fileName} 上传成功", Brushes.Green);
                            }
                            else
                            {
                                string error = await response.Content.ReadAsStringAsync();
                                ShowStatusMessage($"{fileName} 上传失败: {error}", Brushes.Red);
                            }
                        }
                    }

                    if (anyUploaded)
                    {
                        RefreshCloudFileList();
                    }
                }
            }
            catch (Exception ex)
            {
                ShowStatusMessage($"上传失败: {ex.Message}", Brushes.Red);
            }
        }

        private async void BtnDownload_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string apiKey = TxtApiKey.Text.Trim();
                if (string.IsNullOrEmpty(apiKey))
                {
                    ShowStatusMessage("请输入API密钥", Brushes.Red);
                    return;
                }

                if (string.IsNullOrEmpty(_gameConfigDir) || !Directory.Exists(_gameConfigDir))
                {
                    //ShowStatusMessage("未找到游戏配置目录", Brushes.Red);
                    return;
                }

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string backupDir = Path.Combine(_gameConfigDir, $"Backup_{timestamp}");
                Directory.CreateDirectory(backupDir);

                bool moveSuccess = true;
                foreach (var fileName in _configFiles)
                {
                    string sourcePath = Path.Combine(_gameConfigDir, fileName);
                    string destPath = Path.Combine(backupDir, fileName);

                    try
                    {
                        if (File.Exists(sourcePath))
                        {
                            File.Move(sourcePath, destPath);
                        }
                    }
                    catch (Exception ex)
                    {
                        moveSuccess = false;
                        ShowStatusMessage($"备份失败: {ex.Message}", Brushes.Red);
                        break;
                    }
                }

                if (!moveSuccess)
                {
                    try
                    {
                        foreach (var fileName in _configFiles)
                        {
                            string sourcePath = Path.Combine(_gameConfigDir, fileName);
                            string destPath = Path.Combine(backupDir, fileName);

                            if (File.Exists(destPath))
                            {
                                File.Move(destPath, sourcePath);
                            }
                        }
                        Directory.Delete(backupDir);
                    }
                    catch { }
                    return;
                }

                bool downloadSuccess = true;
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "LOLConfigCloud");
                    client.Timeout = TimeSpan.FromSeconds(30);

                    foreach (var fileName in _configFiles)
                    {
                        string filePath = Path.Combine(_gameConfigDir, fileName);
                        try
                        {
                            var response = await client.GetAsync($"{ServerUrl}?key={apiKey}&file={fileName}");

                            if (response.IsSuccessStatusCode)
                            {
                                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                                string tempPath = filePath + ".tmp";
                                using (var fileStream = File.Create(tempPath))
                                {
                                    await response.Content.CopyToAsync(fileStream);
                                }

                                if (File.Exists(filePath)) File.Delete(filePath);
                                File.Move(tempPath, filePath);
                                ShowStatusMessage($"{fileName} 下载成功", Brushes.Green);
                            }
                            else
                            {
                                downloadSuccess = false;
                                string error = await response.Content.ReadAsStringAsync();
                                ShowStatusMessage($"{fileName} 下载失败: {error}", Brushes.Red);
                                break;
                            }
                        }
                        catch (Exception ex)
                        {
                            downloadSuccess = false;
                            ShowStatusMessage($"{fileName} 下载失败: {ex.Message}", Brushes.Red);
                            break;
                        }
                    }
                }

                if (downloadSuccess)
                {
                    ShowStatusMessage("所有文件下载成功", Brushes.Green);
                }
                else
                {
                    try
                    {
                        foreach (var fileName in _configFiles)
                        {
                            string sourcePath = Path.Combine(_gameConfigDir, fileName);
                            string destPath = Path.Combine(backupDir, fileName);

                            if (File.Exists(destPath))
                            {
                                if (File.Exists(sourcePath)) File.Delete(sourcePath);
                                File.Move(destPath, sourcePath);
                            }
                        }
                        Directory.Delete(backupDir);
                        ShowStatusMessage("已恢复原始文件", Brushes.Orange);
                    }
                    catch (Exception ex)
                    {
                        ShowStatusMessage($"恢复备份失败，请手动恢复: {backupDir}", Brushes.Red);
                    }
                }
            }
            catch (Exception ex)
            {
                ShowStatusMessage($"下载失败: {ex.Message}", Brushes.Red);
            }
            finally
            {
                RefreshFileStatus();
            }
        }

        private void SetFilesReadOnly(bool readOnly)
        {
            if (string.IsNullOrEmpty(_gameConfigDir) || !Directory.Exists(_gameConfigDir))
            {
                //ShowStatusMessage("未找到游戏配置目录", Brushes.Red);
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
                ShowStatusMessage(readOnly ? "文件已锁定" : "文件已解锁", Brushes.Green);
            }
            catch (Exception ex)
            {
                ShowStatusMessage($"操作失败: {ex.Message}", Brushes.Red);
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

        private void BtnHelp_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://blog.kdrnn.online/archives/147/",
                UseShellExecute = true
            });
        }

        private void ShowStatusMessage(string message, Brush color)
        {

            Dispatcher.Invoke(() =>
            {
                StatusText.Text = message;
                StatusText.Foreground = color;
                _lastErrorTime = DateTime.Now;
                _isShowingError = color == Brushes.Red;
            });
        }

        private void UpdateApiStatusLight(bool? status)
        {
            Dispatcher.Invoke(() =>
            {
                switch (status)
                {
                    case true: // 成功
                        ApiStatusLight.Background = Brushes.LimeGreen;
                        ApiStatusLight.ToolTip = "云端连接正常";
                        break;
                    case false: // 失败
                        ApiStatusLight.Background = Brushes.Red;
                        ApiStatusLight.ToolTip = "云端连接失败";
                        break;
                    default: // 无API密钥
                        ApiStatusLight.Background = Brushes.LightGray;
                        ApiStatusLight.ToolTip = "请输入API密钥";
                        break;
                }
            });
        }


    }

    public class CloudFileInfo
    {
        public string Name { get; set; }
        public string Size { get; set; }
        public string ModifiedTime { get; set; }
    }
}
