#region Copyright notice and license
// Protocol Buffers - Google's data interchange format
// Copyright 2008 Google Inc.  All rights reserved.
// https://developers.google.com/protocol-buffers/
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
//
//     * Redistributions of source code must retain the above copyright
// notice, this list of conditions and the following disclaimer.
//     * Redistributions in binary form must reproduce the above
// copyright notice, this list of conditions and the following disclaimer
// in the documentation and/or other materials provided with the
// distribution.
//     * Neither the name of Google Inc. nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion

using System;

namespace Google.Protobuf
{
    /// <summary>
    /// Provides a utility routine to copy small arrays much more quickly than Buffer.BlockCopy
    /// </summary>
    internal static class ByteArray
    {
        /// <summary>
        /// The threshold above which you should use Buffer.BlockCopy rather than ByteArray.Copy
        /// </summary>
        private const int CopyThreshold = 12;

        /// <summary>
        /// Determines which copy routine to use based on the number of bytes to be copied.
        /// </summary>
        internal static void Copy(byte[] src, int srcOffset, byte[] dst, int dstOffset, int count)
        {
            if (count > CopyThreshold)
            {
                Buffer.BlockCopy(src, srcOffset, dst, dstOffset, count);
            }
            else
            {
               
                // 对小数组使用循环展开优化
                // 避免循环开销，让JIT更容易优化
                switch (count)
                {
                    case 12:
                        dst[dstOffset + 11] = src[srcOffset + 11];
                        goto case 11;
                    case 11:
                        dst[dstOffset + 10] = src[srcOffset + 10];
                        goto case 10;
                    case 10:
                        dst[dstOffset + 9] = src[srcOffset + 9];
                        goto case 9;
                    case 9:
                        dst[dstOffset + 8] = src[srcOffset + 8];
                        goto case 8;
                    case 8:
                        dst[dstOffset + 7] = src[srcOffset + 7];
                        goto case 7;
                    case 7:
                        dst[dstOffset + 6] = src[srcOffset + 6];
                        goto case 6;
                    case 6:
                        dst[dstOffset + 5] = src[srcOffset + 5];
                        goto case 5;
                    case 5:
                        dst[dstOffset + 4] = src[srcOffset + 4];
                        goto case 4;
                    case 4:
                        dst[dstOffset + 3] = src[srcOffset + 3];
                        goto case 3;
                    case 3:
                        dst[dstOffset + 2] = src[srcOffset + 2];
                        goto case 2;
                    case 2:
                        dst[dstOffset + 1] = src[srcOffset + 1];
                        goto case 1;
                    case 1:
                        dst[dstOffset] = src[srcOffset];
                        break;
                    case 0:
                        break;
                    default:
                        // 对于超出预期范围的小数组，使用原始循环
                        int stop = srcOffset + count;
                        for (int i = srcOffset; i < stop; i++)
                        {
                            dst[dstOffset++] = src[i];
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// Reverses the order of bytes in the array
        /// </summary>
        internal static void Reverse(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));

            int length = bytes.Length;
            if (length <= 1) return; // 无需处理空数组或单元素数组

            // 使用指针操作提高性能（不安全代码）
            // 或者使用更优化的循环方式
            int mid = length / 2;
            for (int i = 0; i < mid; i++)
            {
                int j = length - i - 1;

                // 使用元组交换，避免临时变量
                (bytes[i], bytes[j]) = (bytes[j], bytes[i]);

                // 或者使用传统的交换方式（在某些情况下可能更快）
                // byte temp = bytes[i];
                // bytes[i] = bytes[j];
                // bytes[j] = temp;
            }
        }
    }
}