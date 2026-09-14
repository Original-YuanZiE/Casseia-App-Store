using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
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
    public class Core
    {
        private const string CatalogUrl = "https://raw.githubusercontent.com/Original-YuanZiE/Casseia-App-Store/master/source/main.xml";
        private const string IconBaseUrl = "https://raw.githubusercontent.com/Original-YuanZiE/Casseia-App-Store/master/source/icons/";
        

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
            string WinGetOutput = await RunWinGetAsync($"show --id {originalInfo.Id} --exact --accept-source-agreements");

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
    }
}
