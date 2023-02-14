using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using TBird.Core;

namespace TBird.Wpf.Controls
{
    public class WindowViewModel : BindableBase
    {
        public ICommand OnLoaded => _OnLoaded = _OnLoaded ?? RelayCommand.Create(async _ =>
        {
            ShowProgress = true;

            await Loaded.ExecuteAsync();

            ShowProgress = false;
        });
        private ICommand _OnLoaded;

        public TaskManager Loaded { get; } = new TaskManager();

        public ICommand OnClosing => _OnClosing = _OnClosing ?? RelayCommand.Create<CancelEventArgs>(e =>
        {
            ShowProgress = true;

            Closing.Execute(e);

            ShowProgress = false;
        });
        private ICommand _OnClosing;

        public TaskManager<CancelEventArgs> Closing { get; } = new TaskManager<CancelEventArgs>();

        /// <summary>
        /// ﾀﾞｲｱﾛｸﾞ結果
        /// </summary>
        public bool ShowProgress
        {
            get => _ShowProgress;
            set => SetProperty(ref _ShowProgress, value);
        }
        private bool _ShowProgress;
    }
}
