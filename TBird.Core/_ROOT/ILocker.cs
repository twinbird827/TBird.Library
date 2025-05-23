using System;

namespace TBird.Core
{
	public interface ILocker : IDisposable
	{
		string Lock { get; }
	}
}