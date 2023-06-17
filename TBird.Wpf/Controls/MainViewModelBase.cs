using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TBird.Core;

namespace TBird.Wpf.Controls
{
    public class MainViewModelBase : WindowViewModel
    {
        protected MainViewModelBase()
        {
            MessageService.SetService(new WpfMessageService());
        }
    }
}