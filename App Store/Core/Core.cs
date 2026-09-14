using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace App_Store.Core
{
    public class AppInfo : INotifyPropertyChanged
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string IconUrl { get; set; }
        public BitmapImage IconImage => new BitmapImage(new Uri(IconUrl));
        public string Publisher { get; set; }
        public bool isWinGetApp { get; set; } = true;
        public bool isInstalled { get; set; } = false;
        public string Version { get; set; }
        public string Description { get; set; }
        public string DownloadUrl { get; set; }

        event PropertyChangedEventHandler? INotifyPropertyChanged.PropertyChanged
        {
            add
            {
                throw new NotImplementedException();
            }

            remove
            {
                throw new NotImplementedException();
            }
        }
    }

    public class DownloadTask : INotifyPropertyChanged
    {
        public string Name { get; set; }
        public string SavePath { get; set; }

        private double _progress;
        public double Progress
        {
            get => _progress;
            set { _progress = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Progress))); }
        }

        private string _speed = "等待中";
        public string Speed
        {
            get => _speed;
            set { _speed = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Speed))); }
        }

        public CancellationTokenSource Cts { get; set; } = new();
        public event PropertyChangedEventHandler PropertyChanged;
    }
    public class Core
    {
        private const string CatalogUrl = "https://raw.githubusercontent.com/Original-YuanZiE/Casseia-App-Store/master/source/main.xml";
        private const string IconBaseUrl = "https://raw.githubusercontent.com/Original-YuanZiE/Casseia-App-Store/master/source/icons/";

        public ObservableCollection<DownloadTask> Downloads { get; } = new();
        private DispatcherQueue _dispatcher;


        public async Task<List<AppInfo>> GetAppListAsync()
        {
            try
            {
                using (HttpClient cli = new HttpClient())
                {
                    cli.Timeout = TimeSpan.FromSeconds(10);
                    var xml = await cli.GetStringAsync(CatalogUrl);
                    var doc = XDocument.Parse(xml);
                    var appList = new List<AppInfo>();

                    foreach (var element in doc.Root.Elements("package"))
                    {
                        string id = element.Attribute("id")?.Value;
                        string name = element.Attribute("name")?.Value;
                        string icon = IconBaseUrl + element.Attribute("icon")?.Value;
                        string description = element.Attribute("description")?.Value;
                        bool winget = element.Attribute("winget")?.Value == "true";
                        appList.Add(new AppInfo { Id = id, Name = name, IconUrl = icon, isWinGetApp = winget, Description = description});
                    }

                    return appList;
                }
            }
            catch
            {
                return null;
            }
        }

        public async Task<List<AppInfo>> SearchAppAsync(List<AppInfo> appList, string keyWord)
        {
            if (appList == null || string.IsNullOrEmpty(keyWord))
            {
                return null;
            }

            if (keyWord == "*")
            {
                return appList;
            }

            keyWord = keyWord.ToLower();

            List<AppInfo> res = new List<AppInfo>() { };

            foreach (var app in appList)
            {
                var name = app.Name.ToLower();

                if (name.Contains(keyWord))
                {
                    res.Add(app);
                    continue;
                }

                int j = 0;
                for (int i = 0; i < name.Length && j < keyWord.Length; i++)
                {
                    if (name[i] == keyWord[j])
                        j++;
                }

                if ((float)j >= (float)keyWord.Length * 0.4)
                    res.Add(app);
            }

            return res;
        }

        private SemaphoreSlim _lock = new SemaphoreSlim(1, 1);

        public async Task<string> RunWinGetAsync(string arguments, int timeoutMs = 60000)
        {
            await _lock.WaitAsync();
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "winget",
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = false,
                    UseShellExecute = false,
                    StandardOutputEncoding = Encoding.UTF8,
                    WindowStyle = ProcessWindowStyle.Hidden,
                };

                using var process = Process.Start(psi);
                using var cts = new CancellationTokenSource(timeoutMs);
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync(cts.Token);
                return output;
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<AppInfo> FinishAppInfoSingle(AppInfo originalInfo)
        {
            var args = $"show --id {originalInfo.Id} --exact --accept-source-agreements";

            string WinGetOutput = await RunWinGetAsync(args);

            string[] lines = WinGetOutput.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                if (line.Trim().StartsWith("Version:"))
                {
                    originalInfo.Version = line.Trim().Replace("Version:", "");
                }
                if (line.Trim().StartsWith("Publisher:"))
                {
                    originalInfo.Publisher = line.Trim().Replace("Publisher:", "");
                }
                if (line.Trim().StartsWith("Installer Url:"))
                {
                    originalInfo.DownloadUrl = line.Trim().Replace("Installer Url:", "");
                }
                if (line.Trim().StartsWith("Description:"))
                {
                    originalInfo.Description = line.TrimStart().TrimEnd().Replace("Description:", "");
                }
            }

            return originalInfo;
        }

        public async Task<List<AppInfo>> FinishAppInfoAsync(List<AppInfo> originalInfos)
        {
            List<AppInfo> appInfos = new List<AppInfo>();

            foreach (var info in originalInfos)
            {
                 appInfos.Add(await FinishAppInfoSingle(info));
            }

            return appInfos;
        }

        public void SetDispatcher(DispatcherQueue dispatcher)
        {
            _dispatcher = dispatcher;
        }

        public async Task DownloadAsync(AppInfo app)
        {
            if (string.IsNullOrEmpty(app.DownloadUrl))
            {
                await FinishAppInfoSingle(app);
            }

            try
            {
                Directory.CreateDirectory(Path.Combine(App.Root, "DownloadCache"));
            }
            catch
            { }

            var task = new DownloadTask
            {
                Name = app.Name,
                SavePath = Path.Combine(App.Root, "DownloadCache", Path.GetFileName(new Uri(app.DownloadUrl).AbsolutePath))
            };
            Downloads.Add(task);

            try
            {
                using var http = new HttpClient();
                using var response = await http.GetAsync(
                    app.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, task.Cts.Token);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1;
                using var stream = await response.Content.ReadAsStreamAsync(task.Cts.Token);
                using var file = File.Create(task.SavePath);

                var buffer = new byte[81920];
                long downloaded = 0;
                int read;
                var lastTime = DateTime.Now;
                long lastBytes = 0;

                while ((read = await stream.ReadAsync(buffer, task.Cts.Token)) > 0)
                {
                    await file.WriteAsync(buffer.AsMemory(0, read), task.Cts.Token);
                    downloaded += read;

                    var now = DateTime.Now;
                    if ((now - lastTime).TotalMilliseconds > 200)
                    {
                        var speed = (downloaded - lastBytes) / (now - lastTime).TotalSeconds;
                        lastBytes = downloaded;
                        lastTime = now;

                        _dispatcher?.TryEnqueue(() =>
                        {
                            task.Progress = totalBytes > 0
                                ? (double)downloaded / totalBytes * 100 : 0;
                            task.Speed = speed > 1048576
                                ? $"{speed / 1048576:F1} MB/s"
                                : $"{speed / 1024:F0} KB/s";
                        });
                    }
                }

                _dispatcher?.TryEnqueue(() =>
                {
                    task.Progress = 100;
                    task.Speed = "下载完成";

                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = task.SavePath,
                            UseShellExecute = true
                        });
                    }
                    catch { }
                });
            }
            catch (OperationCanceledException)
            {
                _dispatcher?.TryEnqueue(() => Downloads.Remove(task));
                try { File.Delete(task.SavePath); } catch { }
            }
            catch (Exception ex)
            {
                _dispatcher?.TryEnqueue(() =>
                {
                    task.Speed = "失败: " + ex.Message;
                });
                try { File.Delete(task.SavePath); } catch { }
            }
        }

        public void CancelDownload(DownloadTask task)
        {
            task.Cts.Cancel();
            Downloads.Remove(task);
        }
    }
}
