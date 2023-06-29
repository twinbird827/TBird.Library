using System.Collections.Generic;
using System.Collections.Specialized;

namespace TBird.Wpf.Collections
{
    public interface IBindableCollection : INotifyCollectionChanged, IBindable
    {

    }

    public interface IBindableCollection<T> : IBindableCollection, IList<T>
    {

    }
}