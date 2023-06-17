using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TBird.Wpf
{
    public class ComboboxViewModel : BindableBase
    {
        public ComboboxViewModel(IEnumerable<ComboboxItemModel> items)
        {
            Items = new ObservableCollection<ComboboxItemModel>(items);
            SelectedItem = Items.FirstOrDefault();
        }

        public ObservableCollection<ComboboxItemModel> Items
        {
            get => _Items;
            set => SetProperty(ref _Items, value);
        }
        private ObservableCollection<ComboboxItemModel> _Items;

        public ComboboxItemModel SelectedItem
        {
            get => _SelectedItem;
            set => SetProperty(ref _SelectedItem, SelectedItem);
        }
        private ComboboxItemModel _SelectedItem;

        public ComboboxItemModel GetItemNotNull(string value)
        {
            return GetItem(value) ?? Items.FirstOrDefault();
        }

        public ComboboxItemModel GetItem(string value)
        {
            return Items.FirstOrDefault(x => x.Value == value);
        }
    }
}
