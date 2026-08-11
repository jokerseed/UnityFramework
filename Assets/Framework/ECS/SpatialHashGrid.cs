using System.Collections.Generic;
using UnityEngine;

namespace Framework.ECS
{
    /// <summary>简易空间哈希，降低弹道碰撞查询复杂度。</summary>
    public sealed class SpatialHashGrid
    {
        readonly float _cellSize;
        readonly Dictionary<long, List<uint>> _cells = new Dictionary<long, List<uint>>();
        readonly List<uint> _queryScratch = new List<uint>(32);

        public SpatialHashGrid(float cellSize)
        {
            _cellSize = cellSize;
        }

        public void Clear() => _cells.Clear();

        public void Insert(uint entityId, Vector3 position)
        {
            var key = Hash(position);
            if (!_cells.TryGetValue(key, out var list))
            {
                list = new List<uint>(4);
                _cells[key] = list;
            }

            if (!list.Contains(entityId))
            {
                list.Add(entityId);
            }
        }

        public IReadOnlyList<uint> QueryNearby(Vector3 position, float radius)
        {
            _queryScratch.Clear();
            var cellRadius = Mathf.CeilToInt(radius / _cellSize);
            var center = Cell(position);

            for (var x = center.x - cellRadius; x <= center.x + cellRadius; x++)
            {
                for (var z = center.y - cellRadius; z <= center.y + cellRadius; z++)
                {
                    var key = Pack(x, z);
                    if (!_cells.TryGetValue(key, out var list))
                    {
                        continue;
                    }

                    for (var i = 0; i < list.Count; i++)
                    {
                        var id = list[i];
                        if (!_queryScratch.Contains(id))
                        {
                            _queryScratch.Add(id);
                        }
                    }
                }
            }

            return _queryScratch;
        }

        Vector2Int Cell(Vector3 position)
        {
            return new Vector2Int(
                Mathf.FloorToInt(position.x / _cellSize),
                Mathf.FloorToInt(position.z / _cellSize));
        }

        long Hash(Vector3 position)
        {
            var x = Mathf.FloorToInt(position.x / _cellSize);
            var z = Mathf.FloorToInt(position.z / _cellSize);
            return Pack(x, z);
        }

        static long Pack(int x, int z) => ((long)x << 32) ^ (uint)z;
    }
}
