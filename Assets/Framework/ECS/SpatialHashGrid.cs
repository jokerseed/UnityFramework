using System.Collections.Generic;
using UnityEngine;

namespace Framework.ECS
{
    /// <summary>简易空间哈希，降低弹道碰撞与目标查询复杂度。</summary>
    public sealed class SpatialHashGrid
    {
        readonly float _cellSize;
        readonly Dictionary<long, HashSet<uint>> _cells = new Dictionary<long, HashSet<uint>>();
        readonly List<uint> _queryScratch = new List<uint>(32);
        readonly HashSet<uint> _queryDedup = new HashSet<uint>();

        readonly Stack<HashSet<uint>> _setPool = new Stack<HashSet<uint>>(64);

        /// <summary>创建空间哈希网格。</summary>
        /// <param name="cellSize">单格边长（世界单位）；值越大每格容纳实体越多，查询精度越低。</param>
        public SpatialHashGrid(float cellSize)
        {
            _cellSize = cellSize;
        }

        /// <summary>清空格子内容并回收 HashSet，避免每帧 new。</summary>
        public void Clear()
        {
            foreach (var pair in _cells)
            {
                pair.Value.Clear();
                _setPool.Push(pair.Value);
            }

            _cells.Clear();
        }

        /// <summary>将实体插入对应空间格子；同一格内同一实体只存一份。</summary>
        /// <param name="entityId">要插入的实体 ID。</param>
        /// <param name="position">实体的世界坐标；仅使用 X/Z 轴计算格子索引。</param>
        public void Insert(uint entityId, Vector3 position)
        {
            var key = Hash(position);
            if (!_cells.TryGetValue(key, out var set))
            {
                set = _setPool.Count > 0 ? _setPool.Pop() : new HashSet<uint>();
                _cells[key] = set;
            }

            set.Add(entityId);
        }

        /// <summary>查询指定位置半径范围内的候选实体 ID 列表。</summary>
        /// <param name="position">查询中心世界坐标；仅使用 X/Z 轴。</param>
        /// <param name="radius">查询半径（世界单位）；结果为格子级粗筛，可能包含实际超出半径的实体。</param>
        /// <returns>当前帧内复用的候选实体 ID 只读列表；下次调用前有效。</returns>
        public IReadOnlyList<uint> QueryNearby(Vector3 position, float radius)
        {
            _queryScratch.Clear();
            _queryDedup.Clear();
            var cellRadius = Mathf.CeilToInt(radius / _cellSize);
            var center = Cell(position);

            for (var x = center.x - cellRadius; x <= center.x + cellRadius; x++)
            {
                for (var z = center.y - cellRadius; z <= center.y + cellRadius; z++)
                {
                    var key = Pack(x, z);
                    if (!_cells.TryGetValue(key, out var set))
                    {
                        continue;
                    }

                    foreach (var id in set)
                    {
                        if (_queryDedup.Add(id))
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
