using System;
using System.Windows;

namespace BingWallTray.App.Views
{
    public partial class ContextMenuWindow : Window
    {
        public ContextMenuWindow()
        {
            InitializeComponent();
        }

        protected override void OnDeactivated(EventArgs e)
        {
            base.OnDeactivated(e);
            try
            {
                this.Close();
            }
            catch { }
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                this.Close();
            }
            catch { }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            BingWallTray.App.Utils.MemoryOptimizer.TrimWorkingSet();
        }
    }
}
