using System;
using System.Collections.Generic;

namespace Framework.Lockstep
{
	[Serializable]
	public class SyncedData : ResourcePoolItem
	{
		internal static ResourcePoolSyncedData pool = new ResourcePoolSyncedData();

		internal static ResourcePoolListSyncedData poolList = new ResourcePoolListSyncedData();

		public InputDataBase inputData;

		public int tick;

		[NonSerialized]
		public bool fake;

		[NonSerialized]
		public bool dirty;

		[NonSerialized]
		public bool dropPlayer;

		[NonSerialized]
		public byte dropFromPlayerId;

		public SyncedData()
		{
		}

		/// <summary>
		/// 绑定输入工厂。传入 null 时才回退到 <see cref="AbstractLockstep.instance"/>，供旧的进程级池使用。
		/// </summary>
		/// <param name="provider">本场锁步的输入工厂；可为 null。</param>
		internal void AttachProvider(TrueSyncInputDataProvider provider)
		{
			if (this.inputData != null)
			{
				return;
			}

			if (provider != null)
			{
				this.inputData = provider();
				return;
			}

			var current = AbstractLockstep.instance;
			if (current != null && current.InputDataProvider != null)
			{
				this.inputData = current.InputDataProvider();
			}
		}

		public void Init(byte ownerID, int tick)
		{
			this.inputData.ownerID = ownerID;
			this.tick = tick;
			this.fake = false;
			this.dirty = false;
		}

		public void GetEncodedHeader(List<byte> bytes)
		{
			Utils.GetBytes(this.tick, bytes);
			bytes.Add(this.inputData.ownerID);
			bytes.Add(this.dropFromPlayerId);
			bytes.Add((byte)(this.dropPlayer ? 1 : 0));
		}

		public void GetEncodedActions(List<byte> bytes)
		{
			this.inputData.Serialize(bytes);
		}

		/// <summary>用进程级池解码。失败时抛出，不返回半截列表。</summary>
		/// <param name="data">载荷。</param>
		/// <returns>解码结果；没有动作时为空列表。</returns>
		/// <exception cref="ArgumentException">载荷过短、为空，或动作解码越界。</exception>
		public static List<SyncedData> Decode(byte[] data)
		{
			List<SyncedData> list;
			if (!TryDecode(data, out list))
			{
				throw new ArgumentException("SyncedData payload is invalid.", nameof(data));
			}

			return list;
		}

		/// <summary>用进程级池尝试解码。失败时不抛异常，池对象会归还。</summary>
		/// <param name="data">载荷；可为 null。</param>
		/// <param name="list">成功时的列表；失败时为 null。调用方负责归还。</param>
		/// <returns>头部至少 7 字节且动作没有越界时返回 true。</returns>
		public static bool TryDecode(byte[] data, out List<SyncedData> list)
		{
			return TryDecode(data, pool, poolList, out list);
		}

		internal static bool TryDecode(byte[] data, ResourcePoolSyncedData itemPool, ResourcePoolListSyncedData listPool, out List<SyncedData> list)
		{
			list = null;
			if (data == null || data.Length < 7 || itemPool == null || listPool == null)
			{
				return false;
			}

			list = listPool.GetNew();
			list.Clear();
			try
			{
				int i = 0;
				int tick = BitConverter.ToInt32(data, i);
				i += 4;
				byte ownerID = data[i++];
				byte dropFrom = data[i++];
				bool drop = data[i++] == 1;
				int currentTick = tick;
				while (i < data.Length)
				{
					SyncedData item = itemPool.GetNew();
					list.Add(item);
					if (item.inputData == null)
					{
						throw new InvalidOperationException("Input provider is missing.");
					}

					item.Init(ownerID, currentTick--);
					item.inputData.Deserialize(data, ref i);
					if (i < 0 || i > data.Length)
					{
						throw new ArgumentOutOfRangeException(nameof(data));
					}
				}

				if (list.Count > 0)
				{
					list[0].dropPlayer = drop;
					list[0].dropFromPlayerId = dropFrom;
				}

				return true;
			}
			catch (Exception)
			{
				for (int n = 0; n < list.Count; n++)
				{
					itemPool.GiveBack(list[n]);
				}

				listPool.GiveBack(list);
				list = null;
				return false;
			}
		}

        // 编码
		public static byte[] Encode(SyncedData[] syncedData)
		{
			var bytesToEncode = new List<byte>();
			bool flag = syncedData.Length != 0;
			if (flag)
			{
				syncedData[0].GetEncodedHeader(bytesToEncode);
				for (int i = 0; i < syncedData.Length; i++)
				{
					syncedData[i].GetEncodedActions(bytesToEncode);
				}
			}
			byte[] array = new byte[bytesToEncode.Count];
			int j = 0;
			int num = array.Length;
			while (j < num)
			{
				array[j] = bytesToEncode[j];
				j++;
			}
			return array;
		}

		public SyncedData clone()
		{
			return CloneTo(pool);
		}

		internal SyncedData CloneTo(ResourcePoolSyncedData itemPool)
		{
			SyncedData @new = itemPool.GetNew();
			@new.Init(this.inputData.ownerID, this.tick);
			@new.inputData.CopyFrom(this.inputData);
			return @new;
		}

		public bool EqualsData(SyncedData other)
		{
			return this.inputData.EqualsData(other.inputData);
		}

		public void CleanUp()
		{
			this.inputData.CleanUp();
		}
	}
}
