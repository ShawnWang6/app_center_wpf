using CtrlCenter.DataModel;
using DocumentFormat.OpenXml.EMMA;
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
    public enum AppStatus
    {
        NotInstalled,
        StoppedWithLoc,
        StoppedWithNoLoc,
        RunningWithLoc,
        RunningWithNoLoc,
    }
    public class AppViewModel : INotifyPropertyChanged
    {
        private AppModel _model;

        // 构造函数：传入 Model 实例
        public AppViewModel(AppModel model)
        {
            _model = model ?? new AppModel();
        }

        // 可以访问原始 Model（如果需要）
        public AppModel Model => _model;

        // 包装属性

        public AppStatus Status
        {
            get
            {
                if (string.IsNullOrEmpty(_model.FullName))
                {
                    return AppStatus.NotInstalled;
                }
                if  (_model.Type != AppType.HVC)
                {
                    return _model.Process == null ? AppStatus.StoppedWithLoc : AppStatus.RunningWithLoc;
                }
                
                if (_model.Process == null)
                {
                    return string.IsNullOrEmpty(_model.RptFolder) ? AppStatus.StoppedWithNoLoc : AppStatus.StoppedWithLoc;                    
                }
                else
                {
                    return string.IsNullOrEmpty(_model.RptFolder) ? AppStatus.RunningWithNoLoc : AppStatus.RunningWithNoLoc;
                }
            }
        }
        public string Name
        {
            get
            {
                return _model.Name;
            }
            set
            {
                if (_model.Name != value)
                {
                    _model.Name = value;
                    OnPropertyChanged();
                }
            }
        }

        

        public bool CanEditName
        {
            get => _model.CanEditName;
            set
            {
                if (_model.CanEditName != value)
                {
                    _model.CanEditName = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool CanSelectRptLoc
        {
            get => _model.CanSelectRptLoc;
            set
            {
                if (_model.CanSelectRptLoc != value)
                {
                    _model.CanSelectRptLoc = value;
                    OnPropertyChanged();
                }
            }
        }

        public string FullName
        {
            get => _model.FullName;
            set
            {
                if (_model.FullName != value)
                {
                    _model.FullName = value;
                    OnPropertyChanged();
                }
            }
        }

        public string RptFolder
        {
            get => _model.RptFolder;
            set
            {
                if (_model.RptFolder != value)
                {
                    _model.RptFolder = value;
                    OnPropertyChanged();
                }
            }
        }

        public string RptPattern
        {
            get => _model.RptPattern;
        }

        //注册表存储RptFolder的key
        public string RptFolderRegKey
        {
            get => $"{_model.Exe}_rpt";
        }

        public Process  Process
        {
            get => _model.Process;
            set
            {
                if (_model.Process != value)
                {
                    _model.Process = value;
                    OnPropertyChanged(nameof(ActionText));
                    OnPropertyChanged(nameof(Status));
                }
            }
        }

        public string ActionText
        {
            get
            {
                if (string.IsNullOrEmpty(_model.FullName))
                    return _model.Type == AppType.HVC ? "搜索app" : "未安装";
                //return _model.Process == null ? "启动app" : "关闭app";
                return _model.Process == null ? "启动app" : "弹出app";
            }
        }

        // 从 Model 更新 ViewModel
        public void UpdateFromModel(AppModel model)
        {
            if (model == null) return;
            _model = model;
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(Process));
            OnPropertyChanged(nameof(ActionText));
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
