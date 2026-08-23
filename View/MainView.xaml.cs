using CtrlCenter.ViewModel;
using System.Windows;

namespace CtrlCenter.View
{
    public partial class AppMainView : Window
    {
        public AppMainView(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            Loaded += MainWindow_Loaded;

            if (viewModel.ShowRptName)
            {
                FileColumn.Visibility = Visibility.Visible;
            }
            else
            {
                FileColumn.Visibility = Visibility.Collapsed;
            }
        }       

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            
        }
    }
}
