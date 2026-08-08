using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using BingWallTray.App.Models;

namespace BingWallTray.App.Models
{
    public class AppState : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private bool _isChecking = false;
        public bool IsChecking
        {
            get => _isChecking;
            set => SetProperty(ref _isChecking, value);
        }

        private bool _isDownloading = false;
        public bool IsDownloading
        {
            get => _isDownloading;
            set => SetProperty(ref _isDownloading, value);
        }

        private string _statusMessage = string.Empty;
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private IReadOnlyList<BingImage> _todayImages = Array.Empty<BingImage>();
        public IReadOnlyList<BingImage> TodayImages
        {
            get => _todayImages;
            set => SetProperty(ref _todayImages, value);
        }

        private string _lastAppliedPath = string.Empty;
        public string LastAppliedPath
        {
            get => _lastAppliedPath;
            set => SetProperty(ref _lastAppliedPath, value);
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
