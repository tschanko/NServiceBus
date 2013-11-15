﻿namespace NServiceBus.Pipeline
{
    using System.Collections.Generic;
    using ObjectBuilder;

    abstract class BehaviorContext
    {
        protected BehaviorContext(BehaviorContext parentContext)
        {
            this.parentContext = parentContext;
        }

        public bool ChainAborted { get; private set; }

        public IBuilder Builder
        {
            get
            {
                return Get<IBuilder>();
            }
        }

        public void AbortChain()
        {
            ChainAborted = true;
        }

        public T Get<T>()
        {
            return Get<T>(typeof(T).FullName);
        }

        public bool TryGet<T>(out T result)
        {
            return TryGet(typeof(T).FullName, out result);
        }

        public bool TryGet<T>(string key,out T result)
        {
            if (stash.ContainsKey(key))
            {
                result = (T)stash[key];
                return true;
            }

            if (parentContext != null)
            {
                return parentContext.TryGet(key, out result);
            }

            if (typeof(T).IsValueType)
            {
                result=default(T);

                return true;
            }
            result = default(T);
            return false;
        }

        public T Get<T>(string key)
        {
            T result;

            if (!TryGet(key, out result))
            {
                throw new KeyNotFoundException("No item found in behavior context with key: " + key);
            }

            return result;
        }

        public void Set<T>(T t)
        {
            Set(typeof(T).FullName, t);
        }

        public void Set<T>(string key, T t)
        {
            stash[key] = t;
        }


        readonly BehaviorContext parentContext;

        internal bool handleCurrentMessageLaterWasCalled;

        Dictionary<string, object> stash = new Dictionary<string, object>();
    }
}