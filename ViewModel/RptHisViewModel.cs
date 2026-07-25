using CtrlCenter.DataModel;
using CtrlCenter.Storage;
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
    public class RptHisViewModel : INotifyPropertyChanged
    {
        private SwitchHisEntity _model;

        // 构造函数：传入 Model 实例
        public RptHisViewModel(SwitchHisEntity model)
        {
            _model = model ?? new SwitchHisEntity();
        }

        // 可以访问原始 Model（如果需要）
        public SwitchHisEntity Model => _model;

        // 包装属性
        public long Id
        {
            get => _model.Id;
        }

        public string SwitchNo
        {
            get => _model.SwitchNo;
            set
            {
                if (_model.SwitchNo != value)
                {
                    _model.SwitchNo = value;
                    OnPropertyChanged();
                }
            }
        }

        public DateTime MinTime
        {
            get => _model.MinTime;
            set
            {
                if (_model.MinTime != value)
                {
                    _model.MinTime = value;
                    OnPropertyChanged();
                }
            }
        }

        public DateTime MaxTime
        {
            get => _model.MaxTime;
            set
            {
                if (_model.MaxTime != value)
                {
                    _model.MaxTime = value;
                    OnPropertyChanged();
                }
            }
        }

        public DateTime CreateTime
        {
            get => _model.CreateTime;
            set
            {
                if (_model.CreateTime != value)
                {
                    _model.CreateTime = value;
                    OnPropertyChanged();
                }
            }
        }

        public string RptJson
        {
            get => _model.RptJson;
            set
            {
                if (_model.RptJson != value)
                {
                    _model.RptJson = value;
                    OnPropertyChanged();
                }
            }
        }


        // 从 Model 更新 ViewModel
        public void UpdateFromModel(SwitchHisEntity model)
        {
            if (model == null) return;
            _model = model;
            //OnPropertyChanged(nameof(Name));
            //OnPropertyChanged(nameof(Process));
            //OnPropertyChanged(nameof(ActionText));
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
