using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using App_Store.Core;

namespace App_Store.Pages
{
    public sealed partial class LocalPage : Page
    {
        private List<InstalledApp> _allApps;
        private bool _showUpdatesOnly = false;

        public LocalPage()
        {
            InitializeComponent();
            LoadInstalledApps();
        }

        private async void LoadInstalledApps()
        {
            var sourceApps = await App.core.GetAppListAsync();
            _allApps = App.core.GetInstalledAppsFromRegistry();
            App.core.MatchWithSource(_allApps, sourceApps);
            AppList.ItemsSource = _allApps;
        }

        private void SearchTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                Search();
            }
        }

        private void SearchBtn_Click(object sender, RoutedEventArgs e)
        {
            Search();
        }

        private void Search()
        {
            var keyword = SearchTextBox.Text.Trim().ToLower();
            if (string.IsNullOrEmpty(keyword))
            {
                AppList.ItemsSource = _allApps;
            }
            else
            {
                var filtered = _allApps.Where(a => a.Name.ToLower().Contains(keyword)).ToList();
                AppList.ItemsSource = filtered;
            }
            _showUpdatesOnly = false;
            ShowAllBtn.Visibility = Visibility.Collapsed;
            StatusText.Text = "";
        }

        private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
        {
            CheckUpdatesBtn.IsEnabled = false;
            LoadingRing.IsActive = true;
            StatusText.Text = "正在检查更新...";

            await App.core.CheckAllUpdatesAsync(_allApps);

            var updates = _allApps.Where(a => a.HasUpdate).ToList();
            AppList.ItemsSource = updates;

            LoadingRing.IsActive = false;
            CheckUpdatesBtn.IsEnabled = true;
            StatusText.Text = $"发现 {updates.Count} 个更新";
            ShowAllBtn.Visibility = Visibility.Visible;
            _showUpdatesOnly = true;
        }

        private void ShowAll_Click(object sender, RoutedEventArgs e)
        {
            AppList.ItemsSource = _allApps;
            ShowAllBtn.Visibility = Visibility.Collapsed;
            StatusText.Text = "";
            _showUpdatesOnly = false;
        }

        private async void AppItem_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var app = btn.Tag as InstalledApp;

            var content = $"名称：{app.Name}\n版本：{app.Version}\n发布者：{app.Publisher}";

            if (app.IsInSource && app.HasUpdate)
            {
                content += $"\n\n发现新版本：{app.LatestVersion}";
            }

            string primaryButton = app.HasUpdate ? "更新" : null;
            string secondaryButton = "卸载";
            string closeButton = "关闭";

            var result = await App.ShowDialog(
                this.XamlRoot,
                "应用信息",
                content,
                primaryButton,
                secondaryButton,
                closeButton,
                ContentDialogButton.Close);

            if (result == ContentDialogResult.Primary && app.HasUpdate)
            {
                await App.core.DownloadUpdateAsync(app);
            }
            else if (result == ContentDialogResult.Secondary)
            {
                var confirmResult = await App.ShowDialog(
                    this.XamlRoot,
                    "确认卸载",
                    $"确定要卸载 {app.Name} 吗？",
                    "卸载",
                    null,
                    "取消",
                    ContentDialogButton.Close);

                if (confirmResult == ContentDialogResult.Primary)
                {
                    var success = await App.core.UninstallAsync(app);
                    if (success)
                    {
                        _allApps.Remove(app);
                        if (_showUpdatesOnly)
                        {
                            AppList.ItemsSource = _allApps.Where(a => a.HasUpdate).ToList();
                        }
                        else
                        {
                            AppList.ItemsSource = _allApps;
                        }
                    }
                    else
                    {
                        await App.ShowDialog(
                            this.XamlRoot,
                            "卸载失败",
                            "无法启动卸载程序",
                            "确定",
                            null,
                            null,
                            ContentDialogButton.Primary);
                    }
                }
            }
        }
    }
}
