using System.Collections.Generic;

namespace Framework.GamePlay
{
    /// <summary>
    /// 行为意图帧队列：采样/编码后入队，逻辑步出队执行。
    /// 单机 Host 入队后立即出队；联机时在此等待远端帧。
    /// </summary>
    public sealed class BattleIntentFrameQueue
    {
        readonly Queue<BattleIntentFrame> _frames = new Queue<BattleIntentFrame>(8);
        readonly Stack<BattleIntentFrame> _pool = new Stack<BattleIntentFrame>(8);

        /// <summary>尚未执行的帧数量。</summary>
        public int Count => _frames.Count;

        /// <summary>从池中取一帧并清空内容。</summary>
        /// <returns>可写入的意图帧。</returns>
        public BattleIntentFrame Rent()
        {
            var frame = _pool.Count > 0 ? _pool.Pop() : new BattleIntentFrame();
            frame.Reset();
            return frame;
        }

        /// <summary>将一帧意图入队，等待逻辑步消费。</summary>
        /// <param name="frame">已填充的意图帧。</param>
        public void Enqueue(BattleIntentFrame frame)
        {
            if (frame != null)
            {
                _frames.Enqueue(frame);
            }
        }

        /// <summary>取出下一帧意图。</summary>
        /// <param name="frame">队首帧。</param>
        /// <returns>队列非空时返回 true。</returns>
        public bool TryDequeue(out BattleIntentFrame frame)
        {
            if (_frames.Count == 0)
            {
                frame = null;
                return false;
            }

            frame = _frames.Dequeue();
            return true;
        }

        /// <summary>回收帧对象到池。</summary>
        /// <param name="frame">已执行完毕的帧。</param>
        public void Recycle(BattleIntentFrame frame)
        {
            if (frame == null)
            {
                return;
            }

            frame.Reset();
            _pool.Push(frame);
        }

        /// <summary>清空队列并将帧还池。</summary>
        public void Clear()
        {
            while (_frames.Count > 0)
            {
                Recycle(_frames.Dequeue());
            }
        }
    }
}
