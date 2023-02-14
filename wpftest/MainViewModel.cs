using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TBird.Wpf;
using TBird.Wpf.Controls;

namespace wpftest
{
    public class MainViewModel : WindowViewModel
    {
        public MainViewModel()
        {
            Loaded.Add(() => Task.Delay(2000));

            Closing.Add(() => Task.Delay(2000));
            Closing.Add(() => Thread.Sleep(2000));
        }

        public string Text
        {
            get => _Text;
            set => SetProperty(ref _Text, value);
        }
        private string _Text;

        public IRelayCommand Command => RelayCommand.Create(_ =>
        {
            Text += DateTime.Now.ToString("yyyy.MM.dd-HH:mm:ss.fff ");
        });
    }
}
