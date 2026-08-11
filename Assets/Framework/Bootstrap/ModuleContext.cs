using System;
using System.Collections.Generic;

namespace Framework.Bootstrap
{
    public sealed class ModuleContext
    {
        readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();
        readonly Dictionary<Type, IGameModule> _modules = new Dictionary<Type, IGameModule>();

        public void RegisterService<T>(T service)
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }

            _services[typeof(T)] = service;
        }

        public T GetService<T>()
        {
            if (_services.TryGetValue(typeof(T), out var service))
            {
                return (T)service;
            }

            throw new InvalidOperationException($"Service not registered: {typeof(T).Name}");
        }

        public bool TryGetService<T>(out T service)
        {
            if (_services.TryGetValue(typeof(T), out var value))
            {
                service = (T)value;
                return true;
            }

            service = default;
            return false;
        }

        internal void RegisterModule(IGameModule module)
        {
            _modules[module.GetType()] = module;
        }
    }
}
