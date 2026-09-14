using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Principal;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using App_Store.Core;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Documents;
using System.Media;
using Windows.Media.Playback;
using Windows.Media.Core;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace App_Store.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SearchPage : Page
    {
        Core.Core core = new Core.Core();
        public class AppResData
        {

        }
        public SearchPage()
        {
            InitializeComponent();

        }

        private async void SearchBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ResRow.Height = new GridLength(1, GridUnitType.Star);
                BoxRow.Height = new GridLength(150);

                var list = await core.GetAppListAsync();

                var results = await core.SearchAppAsync(list, SearchTextBox.Text);

                //var final = await core.FinishAppInfoAsync(results);

                string res = "";

                /*foreach (Core.AppInfo package in results)
                {
                    res += package.Name + Environment.NewLine;
                }

                await App.ShowDialog(
                    this.XamlRoot,
                    "测试结果",
                    res,
                    "好",
                    null,
                    null,
                    ContentDialogButton.Primary);*/

                ResList.ItemsSource = results;

            }
            catch (Exception ex)
            {
                await App.ShowDialog(
                    this.XamlRoot,
                    "出现异常",
                    $"{ex.GetType().FullName}\n\n{ex.Message}\n\n{ex.StackTrace}",
                    "好",
                    null,
                    null,
                    ContentDialogButton.Primary);
            }
        }

        private void SearchTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                e.Handled = true;
                SearchBtn_Click(sender, e);
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SearchTextBox.Text.Replace("\r", "").Replace("\n", "");
        }

        private async void ResButton_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var appInfo = btn.DataContext as Core.AppInfo;


            ScrollViewer sv = new ScrollViewer();
            sv.VerticalScrollMode = ScrollMode.Auto;

            StackPanel sp = new StackPanel();
            sp.Orientation = Orientation.Vertical;
            sp.Spacing = 20;

            Image image = new Image();
            image.Stretch = Stretch.Uniform;
            image.Height = 50;
            image.Width = 50;
            sp.Children.Add(image);

            TextBlock title = new TextBlock();
            title.Text = appInfo.Name;
            title.TextAlignment = TextAlignment.Center;
            title.FontSize = 18;
            title.TextWrapping = TextWrapping.NoWrap;
            sp.Children.Add(title);

            TextBlock description = new TextBlock();
            description.Text = "正在加载";
            description.TextAlignment = TextAlignment.Center;
            description.FontSize = 14;
            description.TextWrapping = TextWrapping.Wrap;
            sp.Children.Add(description);

            sv.Content = sp;

            sp.Loaded += async (s, e) =>
            {
                var fullInfo = await core.FinishAppInfoSingle(appInfo);
                image.Source = fullInfo.IconImage;
                description.Text = fullInfo.Version + "\n" + fullInfo.Publisher + "\n\n" + fullInfo.Description;
            };

            var result = await App.ShowDialog(
                    this.XamlRoot,
                    "应用详情",
                    sv,
                    "获取",
                    null,
                    "取消",
                    ContentDialogButton.Primary);

            if (result == ContentDialogResult.Primary)
            {
                MediaPlayer player = new MediaPlayer();
                player.Source = MediaSource.CreateFromUri(new Uri(Path.Combine(App.Root, "Assets", "Install_Sound.mp3")));
                player.MediaEnded += (s, e) =>
                {
                    player.Dispose();
                };
                player.Play();
                await App.ShowDialog(
                    this.XamlRoot,
                    "应用已添加至下载列表",
                    null,
                    "好",
                    null,
                    null,
                    ContentDialogButton.Primary);
            }
        }
    }
}
