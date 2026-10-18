using System;

namespace Framework.FixedMath {

    /**
     *  @brief Generates random numbers based on a deterministic approach.
     **/
    public class TSRandom {
        // From http://www.codeproject.com/Articles/164087/Random-Number-Generation
        // Class TSRandom generates random numbers
        // from a uniform distribution using the Mersenne
        // Twister algorithm.
        /// <summary>内部状态表长度。拷贝状态时目标数组至少要这么长。</summary>
        public const int StateLength = 624;

        private const int N = StateLength;
        private const int M = 397;
        private const uint MATRIX_A = 0x9908b0dfU;
        private const uint UPPER_MASK = 0x80000000U;
        private const uint LOWER_MASK = 0x7fffffffU;
        private const int MAX_RAND_INT = 0x7fffffff;
        private uint[] mag01 = { 0x0U, MATRIX_A };
        private uint[] mt = new uint[N];
        private int mti = N + 1;
        bool _stateHashValid;
        int _cachedStateHash;

        // 进程级的静态实例（instance / value / Range / CallNext）已删除：它从未初始化（调用即空引用），
        // 而且进程共享的随机状态不进快照、会被多场战斗共用，必然导致不同步。战斗一律注入 New(seed) 创建的实例。

        /**
         *  @brief Generates a new instance based on a given seed.
         **/
        public static TSRandom New(int seed) {
            TSRandom r = new TSRandom(seed);

            //StateTracker.AddTracking(r, "mt");
            //StateTracker.AddTracking(r, "mti");

            return r;
        }

        private TSRandom() {
            init_genrand((uint)DateTime.Now.Millisecond); // determinism-allow: 无种子构造不进战斗；战斗用 New(seed)
        }

        private TSRandom(int seed) {
            init_genrand((uint)seed);
        }

        private TSRandom(int[] init) {
            uint[] initArray = new uint[init.Length];
            for (int i = 0; i < init.Length; ++i)
                initArray[i] = (uint)init[i];
            init_by_array(initArray, (uint)initArray.Length);
        }

        public static int MaxRandomInt { get { return 0x7fffffff; } }

        /**
         *  @brief Returns a random integer.
         **/
        public int Next() {
            return genrand_int31();
        }

        /**
         *  @brief Returns a integer between a min value [inclusive] and a max value [exclusive].
         *  min==max 时直接返回该值，不消耗随机数。其它范围仍是 Next() % range，已有序列不变。
         **/
        public int Next(int minValue, int maxValue) {
            if (minValue > maxValue) {
                int tmp = maxValue;
                maxValue = minValue;
                minValue = tmp;
            }

            if (minValue == maxValue) {
                return minValue;
            }

            int range = maxValue - minValue;

            return minValue + Next() % range;
        }

        /**
         *  @brief Returns a {@link FP} between a min value [inclusive] and a max value [inclusive].
         **/
        public FP Next(FP minValue, FP maxValue) {
            int minValueInt = (int)(minValue * 1000), maxValueInt = (int)(maxValue * 1000);

            if (minValueInt > maxValueInt) {
                int tmp = maxValueInt;
                maxValueInt = minValueInt;
                minValueInt = tmp;
            }

            return (FP.Floor((maxValueInt - minValueInt + 1) * NextFP() +
                minValueInt)) / 1000;
        }

        /**
         *  @brief Returns a {@link FP} between 0.0 [inclusive] and 1.0 [inclusive].
         **/
        public FP NextFP() {
            return ((FP) Next()) / (MaxRandomInt);
        }

        /// <summary>把内部 MT 状态拷出，不消耗随机序列。</summary>
        /// <param name="destination">长度至少为 <see cref="StateLength"/> 的数组。</param>
        /// <param name="index">当前 <c>mti</c>。</param>
        /// <exception cref="ArgumentException">数组过短。</exception>
        public void CopyState(uint[] destination, out int index)
        {
            if (destination == null || destination.Length < N)
            {
                throw new ArgumentException("destination must hold StateLength uints.", nameof(destination));
            }

            Array.Copy(mt, destination, N);
            index = mti;
        }

        /// <summary>从先前的 <see cref="CopyState"/> 结果恢复，使后续随机序列与拷贝时一致。</summary>
        /// <param name="source">状态表。</param>
        /// <param name="index"><see cref="CopyState"/> 返回的下标。</param>
        /// <exception cref="ArgumentException">数组过短。</exception>
        /// <exception cref="ArgumentOutOfRangeException">下标非法。</exception>
        public void LoadState(uint[] source, int index)
        {
            if (source == null || source.Length < N)
            {
                throw new ArgumentException("source must hold StateLength uints.", nameof(source));
            }

            if (index < 0 || index > N + 1)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            Array.Copy(source, mt, N);
            mti = index;
            InvalidateStateHash();
        }

        /// <summary>当前生成器内部状态的稳定哈希，不消耗随机序列。</summary>
        /// <returns>由 <c>mti</c> 与 <c>mt</c> 表混合得到的 32 位哈希。</returns>
        public int GetStateHash()
        {
            if (_stateHashValid)
            {
                return _cachedStateHash;
            }

            unchecked
            {
                var hash = (int)2166136261;
                hash = (hash * 16777619) ^ mti;
                for (var i = 0; i < N; i++)
                {
                    hash = (hash * 16777619) ^ (int)mt[i];
                }

                _cachedStateHash = hash;
                _stateHashValid = true;
                return hash;
            }
        }

        void InvalidateStateHash()
        {
            _stateHashValid = false;
        }

        /// <summary>
        /// 单位球内（含球面）均匀分布的点：在 [-1, 1]³ 里拒绝采样，落在球外就重抽。
        /// 旧的静态 <c>insideUnitSphere</c> 返回三个 [0, 1] 分量，实际是正立方体里的点，已删除。
        /// </summary>
        /// <returns>模长不超过 1 的向量。</returns>
        public TSVector InsideUnitSphere() {
            while (true) {
                var p = new TSVector(NextFP() * 2 - FP.One, NextFP() * 2 - FP.One, NextFP() * 2 - FP.One);
                if (p.sqrMagnitude <= FP.One) {
                    return p;
                }
            }
        }

        public void Initialize() {
            init_genrand((uint)DateTime.Now.Millisecond); // determinism-allow: 无种子重置不进战斗；战斗用 Initialize(int)
        }

        public void Initialize(int seed) {
            init_genrand((uint)seed);
        }

        public void Initialize(int[] init) {
            uint[] initArray = new uint[init.Length];
            for (int i = 0; i < init.Length; ++i)
                initArray[i] = (uint)init[i];
            init_by_array(initArray, (uint)initArray.Length);
        }

        private void init_genrand(uint s) {
            InvalidateStateHash();
            mt[0] = s & 0xffffffffU;
            for (mti = 1; mti < N; mti++) {
                mt[mti] = (uint)(1812433253U * (mt[mti - 1] ^ (mt[mti - 1] >> 30)) + mti);
                mt[mti] &= 0xffffffffU;
            }
        }

        private void init_by_array(uint[] init_key, uint key_length) {
            int i, j, k;
            init_genrand(19650218U);
            i = 1;
            j = 0;
            k = (int)(N > key_length ? N : key_length);
            for (; k > 0; k--) {
                mt[i] = (uint)((uint)(mt[i] ^ ((mt[i - 1] ^ (mt[i - 1] >> 30)) * 1664525U)) + init_key[j] + j);
                mt[i] &= 0xffffffffU;
                i++;
                j++;
                if (i >= N) {
                    mt[0] = mt[N - 1];
                    i = 1;
                }
                if (j >= key_length)
                    j = 0;
            }
            for (k = N - 1; k > 0; k--) {
                mt[i] = (uint)((uint)(mt[i] ^ ((mt[i - 1] ^ (mt[i - 1] >> 30)) *
                    1566083941U)) - i);
                mt[i] &= 0xffffffffU;
                i++;
                if (i >= N) {
                    mt[0] = mt[N - 1];
                    i = 1;
                }
            }
            mt[0] = 0x80000000U;
        }

        uint genrand_int32() {
            InvalidateStateHash();
            uint y;
            if (mti >= N) {
                int kk;
                if (mti == N + 1)
                    init_genrand(5489U);
                for (kk = 0; kk < N - M; kk++) {
                    y = (mt[kk] & UPPER_MASK) | (mt[kk + 1] & LOWER_MASK);
                    mt[kk] = mt[kk + M] ^ (y >> 1) ^ mag01[y & 0x1U];
                }
                for (; kk < N - 1; kk++) {
                    y = (mt[kk] & UPPER_MASK) | (mt[kk + 1] & LOWER_MASK);
                    mt[kk] = mt[kk + (M - N)] ^ (y >> 1) ^ mag01[y & 0x1U];
                }
                y = (mt[N - 1] & UPPER_MASK) | (mt[0] & LOWER_MASK);
                mt[N - 1] = mt[M - 1] ^ (y >> 1) ^ mag01[y & 0x1U];
                mti = 0;
            }
            y = mt[mti++];
            y ^= (y >> 11);
            y ^= (y << 7) & 0x9d2c5680U;
            y ^= (y << 15) & 0xefc60000U;
            y ^= (y >> 18);
            return y;
        }

        private int genrand_int31() {
            return (int)(genrand_int32() >> 1);
        }

        FP genrand_FP() {
            return (FP)genrand_int32() * (FP.One / (FP)4294967295);
        }
    }

}