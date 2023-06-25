using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TBird.Core;

namespace TBird.Wpf.Collections
{
    public abstract class BindableChildCollection<T> : BindableCollection<T>, IBindableChild
    {
        protected BindableChildCollection(IBindableCollection collection)
        {
            AddDisposed((sender, e) =>
            {
                this.ForEach(x => x.TryDispose());

                if (collection is IBindableChild i)
                {
                    i.Dispose();
                }
            });
        }
    }
}
