using CtrlCenter.ViewModel;
using DocumentFormat.OpenXml.EMMA;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CtrlCenter.View
{
    public partial class AppMainView : Window
    {
        private readonly MainViewModel _model;
        public AppMainView(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            _model = viewModel;
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
        
        private void RptHisDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

            var dataGrid = sender as DataGrid;

            // 方法1：通过 SelectedItems 获取选中数量
            //int selectedCount = dataGrid.SelectedItems.Count;

            // 方法2：通过 SelectedCells 获取选中单元格数量（如果 SelectionUnit 不是 FullRow）
            // int selectedCellsCount = dataGrid.SelectedCells.Count;

            // 获取选中的具体项
            _model.SelectedRptHis = dataGrid.SelectedItems.Cast<RptHisViewModel>().ToList();
           
        }


        private DateTime LastClickTime = DateTime.Now;
        private int RickClieckTimes = 0;
        private void TopmostButton_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var span = DateTime.Now - LastClickTime;
            if (span.TotalMilliseconds < 500)
            {
                RickClieckTimes += 1;
                if  (RickClieckTimes >= 5)
                {
                    _model.ShowSimTool = !_model.ShowSimTool;
                }
            }
            else
            {
                RickClieckTimes = 0;
            }
            LastClickTime = DateTime.Now;
                
            // 阻止事件继续传播，避免触发默认菜单
            e.Handled = true;
        }
    }
}
