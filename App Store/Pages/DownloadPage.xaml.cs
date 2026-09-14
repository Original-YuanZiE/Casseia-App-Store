using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace App_Store.Pages;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class DownloadPage : Page
{
    public DownloadPage()
    {
        InitializeComponent();
        ResList.ItemsSource = App.core.Downloads;
    }

    private async void Cancel_Click(object sender, RoutedEventArgs e)
    {
        var result = await App.ShowDialog(
                    this.XamlRoot,
                    "取消任务？",
                    null,
                    "取消任务",
                    null,
                    "继续任务",
                    ContentDialogButton.Primary);
        if (result != ContentDialogResult.Primary)
        {
            return;
        }
        var btn = sender as Button;
        var task = btn.Tag as Core.DownloadTask;
        App.core.CancelDownload(task);
    }
}
