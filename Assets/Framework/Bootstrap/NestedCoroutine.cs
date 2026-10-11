using System;
using System.Collections;
using System.Collections.Generic;

namespace Framework.Bootstrap
{
    /// <summary>
    /// 在同一个枚举器里驱动嵌套的 <see cref="IEnumerator"/>。
    /// Unity 对 <c>yield return</c> 另一条协程时，子协程里的异常不会回到外层的 try/catch。
    /// </summary>
    public static class NestedCoroutine
    {
        /// <summary>
        /// 栈式驱动 <paramref name="routine"/>。嵌套的 <see cref="IEnumerator"/> 在本次 <c>MoveNext</c> 内继续执行；
        /// <c>null</c> 与其它让出对象原样交给外层。异常通过 <paramref name="onError"/> 交出，不再向外抛。
        /// </summary>
        /// <param name="routine">要驱动的协程；为 null 时立即结束。</param>
        /// <param name="onError">捕获到异常时调用；可为 null。回调内不要再抛。</param>
        /// <returns>可被 <c>StartCoroutine</c> 或测试循环驱动的枚举器。</returns>
        public static IEnumerator Drive(IEnumerator routine, Action<Exception> onError)
        {
            if (routine == null)
            {
                yield break;
            }

            var stack = new Stack<IEnumerator>(8);
            stack.Push(routine);
            while (stack.Count > 0)
            {
                var currentRoutine = stack.Peek();
                object current;
                try
                {
                    if (!currentRoutine.MoveNext())
                    {
                        stack.Pop();
                        continue;
                    }

                    current = currentRoutine.Current;
                }
                catch (Exception ex)
                {
                    onError?.Invoke(ex);
                    yield break;
                }

                if (current is IEnumerator nested)
                {
                    stack.Push(nested);
                    continue;
                }

                yield return current;
            }
        }
    }
}
