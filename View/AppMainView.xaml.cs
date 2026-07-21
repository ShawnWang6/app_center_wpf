using System.Windows;

namespace CtrlCenter.View
{
    public partial class AppMainView : Window
    {
        public AppMainView()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;            
        }        

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            
        }
    }
}
