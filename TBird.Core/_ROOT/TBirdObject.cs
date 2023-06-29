namespace TBird.Core
{
    public abstract class TBirdObject
    {
        protected T[] Arr<T>(params T[] arr)
        {
            return arr;
        }
    }
}