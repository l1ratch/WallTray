using System;
using System.Windows;
using System.Windows.Media;
using BingWallTray.App.ViewModels;

namespace BingWallTray.App
{
    public partial class MainWindow : Window
    {
        private bool _isForceClose = false;

        public MainWindow()
        {
            InitializeComponent();
            PositionWindow();
            this.DataContextChanged += MainWindow_DataContextChanged;
        }

        private void MainWindow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is MainViewModel oldVm)
            {
                oldVm.PropertyChanged -= Vm_PropertyChanged;
            }
            if (e.NewValue is MainViewModel newVm)
            {
                newVm.PropertyChanged += Vm_PropertyChanged;
            }
        }

        private void Vm_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "CurrentSource")
            {
                // Сбрасываем скролл в начало
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (GalleryListBox != null)
                    {
                        var scrollViewer = FindVisualChild<System.Windows.Controls.ScrollViewer>(GalleryListBox);
                        if (scrollViewer != null)
                        {
                            scrollViewer.ScrollToTop();
                        }
                    }
                }));
            }
        }

        private void PositionWindow()
        {
            try
            {
                var workArea = SystemParameters.WorkArea;
                double width = this.ActualWidth > 0 ? this.ActualWidth : 460;
                double height = this.ActualHeight > 0 ? this.ActualHeight : 680;
                
                // Размещаем окно в правом нижнем углу над панелью задач с небольшим отступом
                this.Left = workArea.Right - width - 10;
                this.Top = workArea.Bottom - height - 10;
            }
            catch { }
        }

        private async void Window_Activated(object sender, EventArgs e)
        {
            PositionWindow();
            if (DataContext is MainViewModel vm)
            {
                await vm.LoadImagesAsync();
            }
        }

        protected override void OnDeactivated(EventArgs e)
        {
            base.OnDeactivated(e);
            try
            {
                this.Hide();
            }
            catch { }
        }

        public void ForceClose()
        {
            _isForceClose = true;
            try
            {
                this.Close();
            }
            catch { }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (!_isForceClose)
            {
                e.Cancel = true;
                this.Hide();
            }
            else
            {
                base.OnClosing(e);
            }
        }

        private void ListBox_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            if (sender is DependencyObject depObj)
            {
                var scrollViewer = FindVisualChild<System.Windows.Controls.ScrollViewer>(depObj);
                if (scrollViewer != null)
                {
                    if (e.Delta < 0)
                    {
                        scrollViewer.LineRight();
                        scrollViewer.LineRight(); // Двойной шаг для ускорения
                    }
                    else
                    {
                        scrollViewer.LineLeft();
                        scrollViewer.LineLeft();
                    }
                    e.Handled = true;
                }
            }
        }

        private void GalleryScrollViewer_ScrollChanged(object sender, System.Windows.Controls.ScrollChangedEventArgs e)
        {
            if (e.OriginalSource is System.Windows.Controls.ScrollViewer scrollViewer)
            {
                if (scrollViewer.ScrollableHeight > 0 && 
                    scrollViewer.VerticalOffset >= scrollViewer.ScrollableHeight - 50)
                {
                    if (DataContext is MainViewModel vm)
                    {
                        if (vm.LoadMoreHistoricalImagesCommand.CanExecute(null))
                        {
                            vm.LoadMoreHistoricalImagesCommand.Execute(null);
                        }
                    }
                }
            }
        }

        private T? FindVisualChild<T>(DependencyObject obj) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                var child = VisualTreeHelper.GetChild(obj, i);
                if (child is T t)
                {
                    return t;
                }

                var childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                {
                    return childOfChild;
                }
            }
            return null;
        }
    }
}