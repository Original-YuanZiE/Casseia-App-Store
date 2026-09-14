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
        WinGetCli cli = new WinGetCli();
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

                var results = await cli.SearchAsync(SearchTextBox.Text);

                string res = "";

                foreach (WinGetCli.WinGetPackage package in results)
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
                    ContentDialogButton.Primary);
            }
            catch (Exception ex)
            {
                await App.ShowDialog(
                    this.XamlRoot,
                    "WinGet 异常",
                    $"{ex.GetType().FullName}\n\n{ex.Message}\n\n{ex.StackTrace}",
                    "好",
                    null,
                    null,
                    ContentDialogButton.Primary);
            }
        }

        
    }
}
