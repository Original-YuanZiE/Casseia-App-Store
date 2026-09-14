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
    }
}
