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

        }

        public string Text
        {
            get => _Text;
            set => SetProperty(ref _Text, value);
        }
        private string _Text;

        public IRelayCommand Command => RelayCommand.Create(async _ =>
        {
            Text += "B:" + DateTime.Now.ToString("yyyy.MM.dd-HH:mm:ss.fff ");
            await Task.Delay(new Random().Next(1000, 5000));
            Text += "E:" + DateTime.Now.ToString("yyyy.MM.dd-HH:mm:ss.fff ");
            Text += "\n";
        });
    }
}
