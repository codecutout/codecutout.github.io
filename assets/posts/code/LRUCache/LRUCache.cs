using System;
using System.Collections.Generic;
using System.Linq;

namespace LiteCache
{

    /// <summary>
    /// Represents a LRU cache. Recently accessed elements remain in the cache while older items
    /// are removed
    /// </summary>
    /// <typeparam name="Key"></typeparam>
    /// <typeparam name="Value"></typeparam>
    public class LRUCache<Key, Value>
    {
        protected class LRUCacheElement
        {
            public Key Key;
            public Value Value;
            public LRUCacheElement(Key key, Value value)
            {
                this.Key = key;
                this.Value = value;
            }
        }


        protected object _lock = new object();
        protected int _maxCacheSize;
        protected Converter<Key, Value> _retrivalMethod;
        protected Dictionary<Key, LinkedListNode<LRUCacheElement>> _cache = new Dictionary<Key, LinkedListNode<LRUCacheElement>>();
        protected LinkedList<LRUCacheElement> _lru = new LinkedList<LRUCacheElement>();

        /// <summary>
        /// Create a cache with unlimited size
        /// </summary>
        /// <param name="retrivalMethod">method used to retrieve items given a key</param>
        public LRUCache(Converter<Key, Value> retrivalMethod) : this(int.MaxValue, retrivalMethod) { }

        /// <summary>
        /// Create a LRU cache with the given size
        /// </summary>
        /// <param name="cacheSize">maximum size of the cache</param>
        /// <param name="retrivalMethod">method to use when retrieving items from keys</param>
        public LRUCache(int cacheSize, Converter<Key, Value> retrivalMethod)
        {
            this._maxCacheSize = cacheSize;
            this._retrivalMethod = retrivalMethod;
        }

        /// <summary>
        /// Maximum number of elemetns this cache can hold
        /// </summary>
        public int MaxCacheSize
        {
            get
            {
                return _maxCacheSize;
            }
        }

        /// <summary>
        /// Number of items currently held in the cache
        /// </summary>
        public int CurrentCacheSize
        {
            get
            {
                return _lru.Count;
            }
        }

        /// <summary>
        /// All the items currently held in this cache, in order of least recently used
        /// </summary>
        public List<Value> CachedItems
        {
            get
            {
                return _lru.ToList().ConvertAll(e=>e.Value);
            }
        }

        /// <summary>
        /// Get an item given a key. If this item is in the cache the cached value is used
        /// otherwise it will run the retrival method and cache the result
        /// </summary>
        /// <param name="key">key to get the item</param>
        /// <returns>the item</returns>
        public virtual Value this[Key key]
        {
            get
            {
                lock (_lock)
                {
                    LinkedListNode<LRUCacheElement> linkedListNode;

                    if (!_cache.TryGetValue(key, out linkedListNode))
                    {
                        Value value = _retrivalMethod(key);
                        _cache[key] = _lru.AddFirst(new LRUCacheElement(key, value));

                        if (_lru.Count > _maxCacheSize)
                        {
                            _cache.Remove(_lru.Last.Value.Key);
                            _lru.RemoveLast();
                        }
                        return value;
                    }
                    else
                    {
                        //only bother moving rencetly accessed items if
                        //our cache has a size limit, otherwise we dont need to bother
                        if (_maxCacheSize < int.MaxValue)
                        {
                            _lru.Remove(linkedListNode);
                            _lru.AddFirst(linkedListNode);
                        }
                        return linkedListNode.Value.Value;
                    }

                }

            }
        }


        /// <summary>
        /// Invalidates an item with the given key. This item is cleared from the cache but will be
        /// reretrieved using the retrival method on its next access
        /// </summary>
        /// <param name="key"></param>
        public virtual void Invalidate(Key key)
        {
            lock (_lock)
            {
                LinkedListNode<LRUCacheElement> linkedListNode;
                if (!_cache.TryGetValue(key, out linkedListNode))
                    return;
                _lru.Remove(linkedListNode);
                _cache.Remove(key);
            }
        }

        /// <summary>
        /// Invalidates all items in the cache
        /// </summary>
        public virtual void InvalidateAll()
        {
            lock (_lock)
            {
                _lru.Clear();
                _cache.Clear();
            }
        }
    }
}
