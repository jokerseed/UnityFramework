using Framework.FixedMath;
using System;
using System.Collections.Generic;
using FP = Framework.FixedMath.FP;
using TSRandom = Framework.FixedMath.TSRandom;

namespace Framework.Lockstep.Physics2D
{
    internal class PointGenerator
    {
        // 不用进程级静态随机源：每次调用各建一个固定种子的实例，结果可复现，也不跨调用共享状态。
        public static List<TriangulationPoint> UniformDistribution(int n, FP scale)
        {
            var RNG = TSRandom.New(0);
            List<TriangulationPoint> points = new List<TriangulationPoint>();
            for (int i = 0; i < n; i++)
            {
                points.Add(new TriangulationPoint(scale*((FP)(0.5) - RNG.NextFP()), scale*((FP)(0.5) - RNG.NextFP())));
            }
            return points;
        }

        public static List<TriangulationPoint> UniformGrid(int n, FP scale)
        {
            FP x = 0;
            FP size = scale/n;
            FP halfScale = (FP)(0.5)*scale;

            List<TriangulationPoint> points = new List<TriangulationPoint>();
            for (int i = 0; i < n + 1; i++)
            {
                x = halfScale - i*size;
                for (int j = 0; j < n + 1; j++)
                {
                    points.Add(new TriangulationPoint(x, halfScale - j*size));
                }
            }
            return points;
        }
    }
}