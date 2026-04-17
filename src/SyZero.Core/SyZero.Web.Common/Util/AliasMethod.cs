using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace SyZero.Web.Common
{

    public class AliasMethod : IAliasMethod
    {
        private int[] _alias;

        private double[] _probability;

        /// <summary>
        /// 初始化采样
        /// </summary>
        /// <param name="probabilities"></param>
        public void Initialization(List<Double> probabilities)
        {
            if (probabilities == null)
            {
                throw new ArgumentNullException(nameof(probabilities));
            }

            if (probabilities.Count == 0)
            {
                throw new ArgumentException("Probabilities cannot be empty.", nameof(probabilities));
            }

            if (probabilities.Any(value => value < 0 || double.IsNaN(value) || double.IsInfinity(value)))
            {
                throw new ArgumentException("Probabilities must be finite and non-negative.", nameof(probabilities));
            }

            var total = probabilities.Sum();
            if (total <= 0)
            {
                throw new ArgumentException("At least one probability must be greater than zero.", nameof(probabilities));
            }

            var normalizedProbabilities = probabilities.Select(value => value / total).ToArray();
            _probability = new double[normalizedProbabilities.Length];
            _alias = new int[normalizedProbabilities.Length];
            double average = 1.0 / normalizedProbabilities.Length;
            var small = new Stack<int>();
            var large = new Stack<int>();
            for (int i = 0; i < normalizedProbabilities.Length; ++i)
            {
                if (normalizedProbabilities[i] >= average)
                    large.Push(i);
                else
                    small.Push(i);
            }
            while (small.Count > 0 && large.Count > 0)
            {
                int less = small.Pop();
                int more = large.Pop();
                _probability[less] = normalizedProbabilities[less] * normalizedProbabilities.Length;
                _alias[less] = more;
                normalizedProbabilities[more] = normalizedProbabilities[more] + normalizedProbabilities[less] - average;
                if (normalizedProbabilities[more] >= average)
                    large.Push(more);
                else
                    small.Push(more);
            }
            while (small.Count > 0)
                _probability[small.Pop()] = 1.0;
            while (large.Count > 0)
                _probability[large.Pop()] = 1.0;
        }


        /// <summary>
        /// 获取随机采样
        /// </summary>
        /// <returns></returns>
        public int Next()
        {
            if (_probability == null || _alias == null || _probability.Length == 0)
            {
                throw new InvalidOperationException("AliasMethod has not been initialized.");
            }

            int column = RandomNumberGenerator.GetInt32(_probability.Length);

            bool coinToss = NextDouble() < _probability[column];

            return coinToss ? column : _alias[column];
        }

        private static double NextDouble()
        {
            var bytes = new byte[8];
            RandomNumberGenerator.Fill(bytes);
            return BitConverter.ToUInt64(bytes, 0) / ((double)ulong.MaxValue + 1);
        }
    }
}
