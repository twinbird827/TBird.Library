using System.Collections.Specialized;
using NUnit.Framework;
using TBird.Maui.Collections;

namespace TBird.Maui.Tests;

public class BulkObservableCollectionTests
{
    [Test]
    public void ReplaceAll_RaisesSingleReset()
    {
        var c = new BulkObservableCollection<int> { 1, 2 };
        var actions = new List<NotifyCollectionChangedAction>();
        c.CollectionChanged += (_, e) => actions.Add(e.Action);

        c.ReplaceAll(new[] { 3, 4, 5 });

        Assert.That(actions, Is.EqualTo(new[] { NotifyCollectionChangedAction.Reset }));
    }

    [Test]
    public void ReplaceAll_KeepsInputOrder()
    {
        var c = new BulkObservableCollection<int> { 1, 2 };
        c.ReplaceAll(new[] { 5, 3, 4 });
        Assert.That(c, Is.EqualTo(new[] { 5, 3, 4 }));
    }

    [Test]
    public void ReplaceAll_Empty_ClearsItems()
    {
        var c = new BulkObservableCollection<int> { 1, 2 };
        c.ReplaceAll(Array.Empty<int>());
        Assert.That(c, Is.Empty);
    }

    [Test]
    public void ReplaceAll_FromSelf_KeepsItems()
    {
        var c = new BulkObservableCollection<int> { 1, 2, 3, 4 };
        c.ReplaceAll(c.Where(i => i % 2 == 0));
        Assert.That(c, Is.EqualTo(new[] { 2, 4 }));
    }
}
