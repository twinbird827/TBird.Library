using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace TBird.Maui.Collections;

public class BulkObservableCollection<T> : ObservableCollection<T>
{
    public void ReplaceAll(IEnumerable<T> items)
    {
        CheckReentrancy();
        // Clear より先に実体化する。items が自分自身を元にした遅延列だと Clear 後に空になるため。
        var list = items.ToList();
        Items.Clear();
        foreach (var item in list) Items.Add(item);
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
