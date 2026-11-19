namespace Roguelike.Rules
{
    /// <summary>
    /// Holds the newest press made while the character was locked. When the lock ends the press is
    /// taken: it fires if it is no older than the buffer window, and is dropped otherwise.
    /// </summary>
    public sealed class InputBuffer<T>
    {
        T _item;
        float _storedAt;

        public bool HasItem { get; private set; }

        /// <summary>The newest press replaces an older one.</summary>
        public void Store(T item, float now)
        {
            _item = item;
            _storedAt = now;
            HasItem = true;
        }

        /// <summary>Empties the buffer. Returns true, with the press, only if it is still fresh.</summary>
        public bool TryTake(float now, float window, out T item)
        {
            item = default;
            if (!HasItem) return false;
            HasItem = false;
            if (now - _storedAt > window) return false;
            item = _item;
            return true;
        }

        /// <summary>True while a press waits and is no older than the window: it would fire if it were taken now.</summary>
        public bool IsFresh(float now, float window) => HasItem && now - _storedAt <= window;

        /// <summary>The press that waits, without taking it, fresh or not. False when the buffer is empty.</summary>
        public bool TryPeek(out T item)
        {
            item = HasItem ? _item : default;
            return HasItem;
        }

        public void Clear() => HasItem = false;
    }
}
