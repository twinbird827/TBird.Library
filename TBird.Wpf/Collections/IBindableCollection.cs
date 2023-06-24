using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TBird.Wpf.Collections
{
    public interface IBindableCollection : INotifyCollectionChanged, IBindable
    {

    }

    public interface IBindableCollection<T> : IBindableCollection, IList<T>
    {

    }
}
