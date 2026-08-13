using UnityEngine;

namespace Framework.FixedMath
{
    /// <summary>
    /// 定点数与 Unity 浮点类型互转（仅表现层 / 调试；逻辑层请全程使用 <see cref="FP"/> / <see cref="TSVector"/>）。
    /// </summary>
    public static class FPConversions
    {
        /// <summary>float → FP。</summary>
        /// <param name="value">浮点值。</param>
        /// <returns>定点数。</returns>
        public static FP ToFP(float value) => value;

        /// <summary>FP → float。</summary>
        /// <param name="value">定点数。</param>
        /// <returns>浮点近似。</returns>
        public static float ToFloat(FP value) => value.AsFloat();

        /// <summary><see cref="Vector2"/> → <see cref="TSVector2"/>。</summary>
        /// <param name="v">Unity 向量。</param>
        /// <returns>定点数向量。</returns>
        public static TSVector2 ToFP(Vector2 v) => new TSVector2(v.x, v.y);

        /// <summary><see cref="TSVector2"/> → <see cref="Vector2"/>。</summary>
        /// <param name="v">定点数向量。</param>
        /// <returns>Unity 向量。</returns>
        public static Vector2 ToVector2(TSVector2 v) => new Vector2(v.x.AsFloat(), v.y.AsFloat());

        /// <summary><see cref="Vector3"/> → <see cref="TSVector"/>。</summary>
        /// <param name="v">Unity 向量。</param>
        /// <returns>定点数向量。</returns>
        public static TSVector ToFP(Vector3 v) => new TSVector(v.x, v.y, v.z);

        /// <summary><see cref="TSVector"/> → <see cref="Vector3"/>。</summary>
        /// <param name="v">定点数向量。</param>
        /// <returns>Unity 向量。</returns>
        public static Vector3 ToVector3(TSVector v) => new Vector3(v.x.AsFloat(), v.y.AsFloat(), v.z.AsFloat());

        /// <summary><see cref="Quaternion"/> → <see cref="TSQuaternion"/>（按分量转换）。</summary>
        /// <param name="q">Unity 四元数。</param>
        /// <returns>定点数四元数。</returns>
        public static TSQuaternion ToFP(Quaternion q) => new TSQuaternion(q.x, q.y, q.z, q.w);

        /// <summary><see cref="TSQuaternion"/> → <see cref="Quaternion"/>。</summary>
        /// <param name="q">定点数四元数。</param>
        /// <returns>Unity 四元数。</returns>
        public static Quaternion ToQuaternion(TSQuaternion q) =>
            new Quaternion(q.x.AsFloat(), q.y.AsFloat(), q.z.AsFloat(), q.w.AsFloat());
    }
}
