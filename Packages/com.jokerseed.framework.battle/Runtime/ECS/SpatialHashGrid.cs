using System.Collections.Generic;
using Framework.FixedMath;

namespace Framework.ECS
{
    /// <summary>简易空间哈希，降低弹道碰撞与目标查询复杂度。格子索引使用定点坐标。</summary>
    public sealed class SpatialHashGrid
    {
        readonly FP _cellSize;
        readonly Dictionary<long, HashSet<uint>> _cells = new Dictionary<long, HashSet<uint>>();
        readonly List<uint> _queryScratch = new List<uint>(32);
        readonly HashSet<uint> _queryDedup = new HashSet<uint>();

        readonly Stack<HashSet<uint>> _setPool = new Stack<HashSet<uint>>(64);

        /// <summary>
        /// 自上次 <see cref="SpatialIndexService.RebuildActors"/> 以来，Actor 集合或传送坐标发生过变化。
        /// 仅速度变化不置位。逻辑步开头只在此为 true 时补一次重建；物理步进后由系统无条件重建并清零。
        /// </summary>
        public bool StructuralDirty { get; private set; }

        /// <summary>标记需要在下一次逻辑步开头补建索引（创建、移除、传送、复活）。</summary>
        public void MarkStructuralDirty() => StructuralDirty = true;

        /// <summary>重建完成后清掉结构脏标记。</summary>
        public void ClearStructuralDirty() => StructuralDirty = false;

        /// <summary>创建空间哈希网格。</summary>
        /// <param name="cellSize">单格边长（世界单位）；值越大每格容纳实体越多，查询精度越低。</param>
        public SpatialHashGrid(FP cellSize)
        {
            _cellSize = cellSize > FP.Zero ? cellSize : (FP)2;
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
        public void Insert(uint entityId, TSVector position)
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
        /// <param name="radius">查询半径（世界单位）。覆盖该圆轴对齐包围盒所跨的全部格子，因此可能包含实际超出半径的实体。</param>
        /// <returns>当前帧内复用的候选实体 ID 只读列表；下次调用前有效。</returns>
        public IReadOnlyList<uint> QueryNearby(TSVector position, FP radius)
        {
            _queryScratch.Clear();
            _queryDedup.Clear();
            if (radius < FP.Zero)
            {
                radius = FP.Zero;
            }

            var minX = TSMath.Floor((position.x - radius) / _cellSize).AsInt();
            var maxX = TSMath.Floor((position.x + radius) / _cellSize).AsInt();
            var minZ = TSMath.Floor((position.z - radius) / _cellSize).AsInt();
            var maxZ = TSMath.Floor((position.z + radius) / _cellSize).AsInt();

            for (var x = minX; x <= maxX; x++)
            {
                for (var z = minZ; z <= maxZ; z++)
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

            _queryScratch.Sort();
            return _queryScratch;
        }

        long Hash(TSVector position)
        {
            var x = TSMath.Floor(position.x / _cellSize).AsInt();
            var z = TSMath.Floor(position.z / _cellSize).AsInt();
            return Pack(x, z);
        }

        static long Pack(int x, int z) => ((long)x << 32) ^ (uint)z;
    }
}
