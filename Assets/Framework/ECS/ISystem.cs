using System.Collections.Generic;

namespace Framework.ECS
{
    public interface IComponent { }

    public interface IComponentStorage
    {
        void Remove(uint entityId);
        void Clear();
    }

    public interface ISystem
    {
        void OnCreate(World world);
        void OnDestroy(World world);
        void Update(World world, float deltaTime);
    }
}
