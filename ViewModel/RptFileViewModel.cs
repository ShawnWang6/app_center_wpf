using CtrlCenter.DataModel;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace CtrlCenter.ViewModel
{
    public class RptFileViewModel : INotifyPropertyChanged
    {
        private RptFile _model;
        private bool  _merged;

        // 构造函数：传入 Model 实例
        public RptFileViewModel(RptFile model, bool merged)
        {
            _model = model ?? new RptFile();
            _merged = merged;
        }
        // 可以访问原始 Model（如果需要）
        public RptFile Model => _model;

        // 包装属性
        public string FileType
        {
            get => _model.FileType.ToString();
        }

        public string SwitchNo
        {
            get => _model.SwitchNo;
        }

        public string FileName
        {
            get => _merged ? $"{_model.FileNameLowerCase}(已合并)" : _model.FileNameLowerCase;
        }

        public DateTime TimeStamp
        {
            get => Util.ParseYyMmDdHhMmSs(_model.TimeStamp);
        }


        // 从 Model 更新 ViewModel
        public void UpdateFromModel(RptFile model)
        {
            if (model == null) return;
            _model = model;
            OnPropertyChanged(nameof(FileName));
            OnPropertyChanged(nameof(SwitchNo));
            OnPropertyChanged(nameof(FileType));
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
