using TBird.Core;

namespace TBird.Wpf.Collections
{
    public abstract class BindableChildCollection<T> : BindableCollection<T>, IBindableChild
    {
        protected IBindableCollection Parent { get; private set; }

        protected BindableChildCollection(IBindableCollection collection)
        {
            Parent = collection;
            LockObject = ((BindableCollection)Parent).LockObject;

            AddDisposed((sender, e) =>
            {
                this.ForEach(x => x.TryDispose());

                if (Parent is IBindableChild i)
                {
                    i.Dispose();
                }
                Parent = null;
            });
        }
    }
}