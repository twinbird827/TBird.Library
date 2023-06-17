using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TBird.Wpf
{
    public class ComboboxModel : BindableBase
    {
        public ComboboxModel(string group, IEnumerable<ComboboxItemModel> items)
        {
            Group = group;
            Items = items.ToArray();
        }

        public string Group
        {
            get => _Group;
            set => SetProperty(ref _Group, value);
        }
        private string _Group;

        public ComboboxItemModel[] Items
        {
            get => _Items;
            set => SetProperty(ref _Items, value);
        }
        private ComboboxItemModel[] _Items;
    }
}
