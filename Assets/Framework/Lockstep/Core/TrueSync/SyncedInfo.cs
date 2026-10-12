using System;
using System.Collections.Generic;
using System.Text;

namespace Framework.Lockstep
{
	public class SyncedInfo
	{
		private const int CHECKSUM_LENGTH = 32;

		public byte playerId;

		public int tick;

		public string checksum;

		public SyncedInfo()
		{
		}

		public SyncedInfo(byte playerId, int tick, string checksum)
		{
			this.tick = tick;
			this.checksum = checksum;
		}

		public static byte[] Encode(SyncedInfo info)
		{
			List<byte> list = new List<byte>();
			list.Add(info.playerId);
			bool flag = info.checksum != null;
			if (flag)
			{
				list.AddRange(BitConverter.GetBytes(info.tick));
				list.AddRange(Encoding.ASCII.GetBytes(info.checksum));
			}
			return list.ToArray();
		}

		/// <summary>解码校验信息。长度不合法时抛出。</summary>
		/// <param name="infoBytes">载荷。</param>
		/// <returns>校验信息。</returns>
		/// <exception cref="ArgumentException">为空、短于 1 字节，或带校验和但不足 37 字节。</exception>
		public static SyncedInfo Decode(byte[] infoBytes)
		{
			SyncedInfo info;
			if (!TryDecode(infoBytes, out info))
			{
				throw new ArgumentException("SyncedInfo payload is invalid.", nameof(infoBytes));
			}

			return info;
		}

		/// <summary>
		/// 尝试解码。只有玩家 id 时长度为 1；带校验和时至少 37 字节，多出来的尾部忽略。
		/// 不足 37 字节的半截包返回 false，不再抛异常。
		/// </summary>
		/// <param name="infoBytes">载荷；可为 null。</param>
		/// <param name="info">成功时的结果；失败时为 null。</param>
		/// <returns>长度合法时返回 true。</returns>
		public static bool TryDecode(byte[] infoBytes, out SyncedInfo info)
		{
			info = null;
			if (infoBytes == null || infoBytes.Length < 1)
			{
				return false;
			}

			if (infoBytes.Length > 1 && infoBytes.Length < 1 + 4 + CHECKSUM_LENGTH)
			{
				return false;
			}

			var decoded = new SyncedInfo();
			int num = 0;
			decoded.playerId = infoBytes[num++];
			if (num < infoBytes.Length)
			{
				decoded.tick = BitConverter.ToInt32(infoBytes, num);
				num += 4;
				decoded.checksum = Encoding.ASCII.GetString(infoBytes, num, CHECKSUM_LENGTH);
			}

			info = decoded;
			return true;
		}
	}
}
