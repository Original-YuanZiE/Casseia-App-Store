using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.ComponentModel;
using System.IO;

namespace App_Store.Core
{
    public class InstalledApp : INotifyPropertyChanged
    {
        public string Name { get; set; }
        public string Version { get; set; }
        public string Publisher { get; set; }
        public string UninstallString { get; set; }
        public string LocalIconPath { get; set; }
        public AppInfo SourceInfo { get; set; }  // null = 非源内App

        public bool IsInSource => SourceInfo != null;

        public BitmapImage IconImage
        {
            get
            {
                if (!string.IsNullOrEmpty(LocalIconPath) && File.Exists(LocalIconPath))
                {
                    try
                    {
                        return new BitmapImage(new Uri(LocalIconPath));
                    }
                    catch { }
                }
                return SourceInfo?.IconImage;
            }
        }

        private bool _hasUpdate;
        public bool HasUpdate
        {
            get => _hasUpdate;
            set
            {
                _hasUpdate = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasUpdate)));
            }
        }

        private string _latestVersion;
        public string LatestVersion
        {
            get => _latestVersion;
            set
            {
                _latestVersion = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LatestVersion)));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
