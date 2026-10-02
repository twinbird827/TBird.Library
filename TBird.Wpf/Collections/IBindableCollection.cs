using System.Collections.Generic;

namespace TBird.Wpf.Collections
{
	public interface IBindableCollection : IBindable
	{

	}

	public interface IBindableCollection<T> : IBindableCollection, IList<T>
	{

	}
}