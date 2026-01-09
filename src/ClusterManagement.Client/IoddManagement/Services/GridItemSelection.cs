using ViciOne.Ui.Blazor.Components.Grid.Services;

namespace ClusterManagement.Client.IoddManagement.Services;

internal sealed class GridItemSelection<TGridItemKey> : IGridItemSelection<TGridItemKey>
{
    private readonly HashSet<TGridItemKey> _items = [];
    private readonly HashSet<TGridItemKey> _itemsAdded = [];
    private readonly HashSet<TGridItemKey> _itemsRemoved = [];
    private readonly HashSet<TGridItemKey> _noItems = [];
    private readonly Lock _concurrentLock = new();
    private int _updateLock;

    public int Count => _items.Count;
    public int UpdateLock => _updateLock;

    public event Action<GridItemSelectionChangedEventArgs<TGridItemKey>>? Changed;

    public bool Add(TGridItemKey item)
    {
        lock (_concurrentLock)
        {
            if (_items.Add(item))
            {
                if (UpdateLock == 0)
                {
                    var args = new GridItemSelectionChangedEventArgs<TGridItemKey>
                    {
                        Sender = this,
                        ItemsAdded = new HashSet<TGridItemKey> { item },
                        ItemsRemoved = _noItems
                    };

                    Changed?.Invoke(args);
                }
                else
                {
                    if (_itemsRemoved.Remove(item))
                    {
                        // item was removed before but now it is added again, consider it a no change
                    }
                    else
                    {
                        _itemsAdded.Add(item);
                    }
                }

                return true;
            }

            return false;
        }
    }

    public void AddRange(IEnumerable<TGridItemKey> items)
    {
        var itemsAdded = new HashSet<TGridItemKey>();

        lock (_concurrentLock)
        {
            foreach (var item in items)
            {
                if (_items.Add(item))
                {
                    itemsAdded.Add(item);

                    if (UpdateLock > 0)
                    {
                        if (_itemsRemoved.Remove(item))
                        {
                            // item was removed before but now it is added again, consider it a no change
                        }
                        else
                        {
                            _itemsAdded.Add(item);
                        }
                    }
                }
            }

            if (UpdateLock == 0)
                Changed?.Invoke(new GridItemSelectionChangedEventArgs<TGridItemKey> { Sender = this, ItemsAdded = itemsAdded, ItemsRemoved = _noItems });
        }
    }

    public void Clear()
        => Remove(item => true);

    public int Remove(Predicate<TGridItemKey> match)
    {
        lock (_concurrentLock)
        {
            var itemsRemovedCountBefore = _itemsRemoved.Count;

            var itemsToRemove = _items.Where(i => match(i)).ToList();
            foreach (var itemToRemove in itemsToRemove)
            {
                if (_items.Remove(itemToRemove))
                {
                    if (_itemsAdded.Remove(itemToRemove))
                    {
                        // item was added before but now it is removed again, consider it a no change
                    }
                    else
                    {
                        _itemsRemoved.Add(itemToRemove);
                    }
                }
            }

            var numberOfItemsRemoved = _itemsRemoved.Count - itemsRemovedCountBefore;

            if (numberOfItemsRemoved > 0)
            {
                if (UpdateLock == 0)
                {
                    Changed?.Invoke(new GridItemSelectionChangedEventArgs<TGridItemKey> { Sender = this, ItemsAdded = _noItems, ItemsRemoved = _itemsRemoved });

                    _itemsRemoved.Clear();
                }
            }

            return numberOfItemsRemoved;
        }
    }

    public bool Remove(TGridItemKey item)
        => Remove(i => EqualityComparer<TGridItemKey>.Default.Equals(i, item)) > 0;

    public bool Contains(TGridItemKey item)
    {
        lock (_concurrentLock)
        {
            return _items.Contains(item);
        }
    }

    public void BeginUpdate()
    {
        lock (_concurrentLock)
        {
            _updateLock++;
        }
    }

    public void EndUpdate()
    {
        lock (_concurrentLock)
        {
            _updateLock--;

            if (_updateLock > 0)
                return;

            _updateLock = 0;

            if (_itemsAdded.Count > 0 || _itemsRemoved.Count > 0)
            {
                Changed?.Invoke(new GridItemSelectionChangedEventArgs<TGridItemKey> { Sender = this, ItemsAdded = _itemsAdded, ItemsRemoved = _itemsRemoved });

                _itemsAdded.Clear();
                _itemsRemoved.Clear();
            }
        }
    }

    public IEnumerator<TGridItemKey> GetEnumerator()
    {
        lock (_concurrentLock)
        {
            return _items.ToList().GetEnumerator();
        }
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}
