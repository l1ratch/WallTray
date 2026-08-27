using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using BingWallTray.App.ViewModels;

namespace BingWallTray.App
{
    public partial class MainWindow : Window
    {
        private bool _isForceClose = false;
        private readonly DispatcherTimer _statusPanelCloseTimer;
        private Storyboard? _spinnerStoryboard;

        public MainWindow()
        {
            InitializeComponent();
            _statusPanelCloseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
            _statusPanelCloseTimer.Tick += StatusPanelCloseTimer_Tick;
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

        private void OpenSettingsWindow_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.OpenSettingsWindowCommand.Execute(null);
            }
        }

        private void Vm_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "CurrentSource")
            {
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
            else if (e.PropertyName == "IsStatusBusy")
            {
                Dispatcher.BeginInvoke(new Action(UpdateBusySpinner));
            }
            else if (e.PropertyName == "IsStatusPopupOpen")
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (DataContext is MainViewModel vm)
                    {
                        AnimateStatusPanel(vm.IsStatusPopupOpen);
                    }
                }));
            }
        }

        private void UpdateBusySpinner()
        {
            if (DataContext is not MainViewModel vm) return;

            var ring = FindVisualChildByName<System.Windows.Shapes.Ellipse>(StatusBadge, "BusyRing");
            if (ring?.RenderTransform is not RotateTransform rotateTransform) return;

            if (vm.IsStatusBusy)
            {
                _spinnerStoryboard = new Storyboard { RepeatBehavior = RepeatBehavior.Forever };
                var spinAnim = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1.1));
                Storyboard.SetTarget(spinAnim, rotateTransform);
                Storyboard.SetTargetProperty(spinAnim, new PropertyPath(RotateTransform.AngleProperty));
                _spinnerStoryboard.Children.Add(spinAnim);
                _spinnerStoryboard.Begin();
            }
            else
            {
                _spinnerStoryboard?.Stop();
                rotateTransform.Angle = 0;
            }
        }

        private void AnimateStatusPanel(bool open)
        {
            if (StatusPanel == null) return;

            if (StatusPanel.RenderTransform is not TransformGroup group) return;
            var scale = group.Children[0] as ScaleTransform;
            var translate = group.Children[1] as TranslateTransform;
            if (scale == null || translate == null) return;

            if (open)
            {
                StatusPanel.Visibility = Visibility.Visible;

                var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
                var dur = TimeSpan.FromSeconds(0.18);

                StatusPanel.BeginAnimation(UIElement.OpacityProperty,
                    new DoubleAnimation(1, dur) { EasingFunction = ease });
                scale.BeginAnimation(ScaleTransform.ScaleXProperty,
                    new DoubleAnimation(1, dur) { EasingFunction = ease });
                scale.BeginAnimation(ScaleTransform.ScaleYProperty,
                    new DoubleAnimation(1, dur) { EasingFunction = ease });
                translate.BeginAnimation(TranslateTransform.YProperty,
                    new DoubleAnimation(0, dur) { EasingFunction = ease });
            }
            else
            {
                var ease = new CubicEase { EasingMode = EasingMode.EaseIn };
                var dur = TimeSpan.FromSeconds(0.14);

                var fade = new DoubleAnimation(0, dur) { EasingFunction = ease };
                fade.Completed += (_, _) =>
                {
                    if (DataContext is MainViewModel vm && !vm.IsStatusPopupOpen)
                    {
                        StatusPanel.Visibility = Visibility.Collapsed;
                    }
                };
                StatusPanel.BeginAnimation(UIElement.OpacityProperty, fade);
                scale.BeginAnimation(ScaleTransform.ScaleXProperty,
                    new DoubleAnimation(0.94, dur) { EasingFunction = ease });
                scale.BeginAnimation(ScaleTransform.ScaleYProperty,
                    new DoubleAnimation(0.94, dur) { EasingFunction = ease });
                translate.BeginAnimation(TranslateTransform.YProperty,
                    new DoubleAnimation(-10, dur) { EasingFunction = ease });
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
                BingWallTray.App.Utils.MemoryOptimizer.TrimWorkingSet();
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
                BingWallTray.App.Utils.MemoryOptimizer.TrimWorkingSet();
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

        private T? FindVisualChildByName<T>(DependencyObject obj, string name) where T : FrameworkElement
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                var child = VisualTreeHelper.GetChild(obj, i);
                if (child is T t && t.Name == name)
                {
                    return t;
                }

                var childOfChild = FindVisualChildByName<T>(child, name);
                if (childOfChild != null)
                {
                    return childOfChild;
                }
            }
            return null;
        }

        private void StatusBadge_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _statusPanelCloseTimer.Stop();
            if (DataContext is MainViewModel vm)
            {
                vm.IsStatusPopupOpen = true;
                _ = vm.UpdateCacheStatsAsync();
            }
        }

        private void StatusBadge_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _statusPanelCloseTimer.Start();
        }

        private void StatusPopup_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _statusPanelCloseTimer.Stop();
            if (DataContext is MainViewModel vm)
            {
                vm.IsStatusPopupOpen = true;
            }
        }

        private void StatusPopup_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _statusPanelCloseTimer.Start();
        }

        private void StatusPanelCloseTimer_Tick(object? sender, EventArgs e)
        {
            _statusPanelCloseTimer.Stop();
            if (DataContext is MainViewModel vm)
            {
                vm.IsStatusPopupOpen = false;
            }
        }
    }
}
