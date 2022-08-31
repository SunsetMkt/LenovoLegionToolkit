﻿using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.PackageDownloader;
using LenovoLegionToolkit.Lib.Utils;
using LenovoLegionToolkit.WPF.Utils;
using LenovoLegionToolkit.WPF.Windows.Packages;

namespace LenovoLegionToolkit.WPF.Controls.Packages
{
    public partial class PackageControl : UserControl, IProgress<float>
    {
        private readonly IPackageDownloader _packageDownloader;
        private readonly Package _package;

        private CancellationTokenSource? _downloadPackageTokenSource;

        public Func<string> _getDownloadPath;

        public bool IsDownloading { get; private set; }

        public PackageControl(IPackageDownloader packageDownloader, Package package, Func<string> getDownloadPath)
        {
            _packageDownloader = packageDownloader;
            _package = package;
            _getDownloadPath = getDownloadPath;

            InitializeComponent();

            Unloaded += PackageControl_Unloaded;

            _dateTextBlock.Text = package.ReleaseDate.ToString("d");
            _titleTextBlock.Text = package.Title;
            _descriptionTextBlock.Text = package.Description;
            _descriptionTextBlock.Visibility = string.IsNullOrWhiteSpace(package.Description) ? Visibility.Collapsed : Visibility.Visible;
            _categoryTextBlock.Text = package.Category;
            _detailTextBlock.Text = $"Version {package.Version}  |  {package.FileSize}  |  {package.FileName}";

            _readmeButton.Visibility = string.IsNullOrWhiteSpace(package.Readme) ? Visibility.Collapsed : Visibility.Visible;

            var showWarning = package.ReleaseDate < DateTime.UtcNow.AddYears(-1);
            _warningTextBlock.Visibility = showWarning ? Visibility.Visible : Visibility.Collapsed;
        }

        private void PackageControl_Unloaded(object sender, RoutedEventArgs e) => _downloadPackageTokenSource?.Cancel();

        public void Report(float value) => Dispatcher.Invoke(() =>
        {
            _downloadProgressRing.IsIndeterminate = !(value > 0);
            _downloadProgressRing.Progress = value * 100;
            _downloadProgressLabel.Content = $"{value * 100:0}%";
        });

        private void ReadmeButton_Click(object sender, RoutedEventArgs e)
        {
            if (_package.Readme is null)
                return;

            var updateWindow = new ReadmeWindow(_package.Readme)
            {
                Owner = Window.GetWindow(this),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ShowInTaskbar = false,
            };
            updateWindow.ShowDialog();
        }

        private async void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            IsDownloading = true;

            var result = false;

            try
            {
                _idleStackPanel.Visibility = Visibility.Collapsed;
                _downloadingStackPanel.Visibility = Visibility.Visible;

                _downloadPackageTokenSource?.Cancel();
                _downloadPackageTokenSource = new CancellationTokenSource();

                var token = _downloadPackageTokenSource.Token;

                await _packageDownloader.DownloadPackageFileAsync(_package, _getDownloadPath(), this, token);

                result = true;
            }
            catch (TaskCanceledException) { }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Not found 404.", ex);

                SnackbarHelper.Show("The file seems to be gone", "Server returned code 404.", true);
            }
            catch (HttpRequestException ex)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Error occured when downloading package file.", ex);

                SnackbarHelper.Show("Something went wrong", "Check if your internet connection is up and running.", true);
            }
            catch (Exception ex)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Error occured when downloading package file.", ex);

                SnackbarHelper.Show("Something went wrong", ex.Message, true);
            }
            finally
            {
                _idleStackPanel.Visibility = Visibility.Visible;
                _downloadingStackPanel.Visibility = Visibility.Collapsed;
                _downloadProgressRing.Progress = 0;
                _downloadProgressLabel.Content = null;

                IsDownloading = false;
            }

            if (result)
                await SnackbarHelper.ShowAsync("Download complete", $"{_package.FileName} downloaded!");
        }

        private void CancelDownloadButton_Click(object sender, RoutedEventArgs e) => _downloadPackageTokenSource?.Cancel();
    }
}