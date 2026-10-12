using System;
using System.Collections.Generic;
using UnityEngine;

namespace Framework.Lockstep
{
    // 帧同步玩家
	[Serializable]
	public class TSPlayer
	{
		[SerializeField]
		public TSPlayerInfo playerInfo; // 帧同步玩家信息

		[NonSerialized]
		public int dropCount;

		[NonSerialized]
		public bool dropped;

		[NonSerialized]
		public bool sentSyncedStart;

		[NonSerialized]
		internal ResourcePoolSyncedData dataPool;

		[NonSerialized]
		HashSet<byte> _dropVotes;

		[SerializeField]
		internal SerializableDictionaryIntSyncedData controls;

		private int lastTick;

		public void Clear()
		{
			playerInfo = null;
            controls.Clear();
            if (this._dropVotes != null)
            {
                this._dropVotes.Clear();
            }

        }

		public byte ID
		{
			get
			{
				return this.playerInfo.id;
			}
		}

		internal TSPlayer(byte id, string name) : this(id, name, null)
		{
		}

		internal TSPlayer(byte id, string name, ResourcePoolSyncedData dataPool)
		{
			this.playerInfo = new TSPlayerInfo(id, name);
			this.dropCount = 0;
			this.dropped = false;
			this.dataPool = dataPool;
			this.controls = new SerializableDictionaryIntSyncedData();
		}

		ResourcePoolSyncedData Pool
		{
			get { return this.dataPool ?? SyncedData.pool; }
		}

		/// <summary>同一来源只计一次掉线票。</summary>
		/// <param name="fromPlayerId">发起掉线的玩家。</param>
		/// <returns>第一次投票返回 true。</returns>
		internal bool RegisterDropVote(byte fromPlayerId)
		{
			if (this._dropVotes == null)
			{
				this._dropVotes = new HashSet<byte>();
			}

			return this._dropVotes.Add(fromPlayerId);
		}

        #region 公共方法
        // 数据是否已准备好
        public bool IsDataReady(int tick)
		{
			return this.controls.ContainsKey(tick) && !this.controls[tick].fake;
		}

        // 是不是脏数据
		public bool IsDataDirty(int tick)
		{
			bool flag = this.controls.ContainsKey(tick);
			return flag && this.controls[tick].dirty;
		}
        
        // 获取数据
        public SyncedData GetData(int tick)
		{
			bool flag = !this.controls.ContainsKey(tick);
			SyncedData result;
			if (flag) // 没有数据
			{
				bool flag2 = this.controls.ContainsKey(tick - 1); // 查询前一个数据
				SyncedData syncedData;
				if (flag2) // 有数据,把前一个数据复制为当前数据
				{
					syncedData = this.controls[tick - 1].CloneTo(Pool);
					syncedData.tick = tick;
				}
				else // 没有数据,获取一个新的数据
				{
					syncedData = Pool.GetNew();
					syncedData.Init(this.ID, tick);
				}
				syncedData.fake = true; // 设置为假数据
				this.controls[tick] = syncedData;
				result = syncedData;
			}
			else // 有数据
			{
				result = this.controls[tick];
			}
			return result;
		}

        // 添加数据
		public void AddData(SyncedData data)
		{
			int tick = data.tick;
			bool flag = this.controls.ContainsKey(tick);
			if (flag) // 数据已存在，把数据对象回收到池中
			{
				Pool.GiveBack(data);
			}
			else // 未有数据
			{
				this.controls[tick] = data;
				this.lastTick = tick;
			}
		}

        // 添加数据
		public void AddData(List<SyncedData> data)
		{
			for (int i = 0; i < data.Count; i++)
			{
				this.AddData(data[i]);
			}
		}

        // 移除数据
		public void RemoveData(int refTick)
		{
			bool flag = this.controls.ContainsKey(refTick);
			if (flag)
			{
				Pool.GiveBack(this.controls[refTick]);
				this.controls.Remove(refTick);
			}
		}

        // 更新同步数据
		public void AddDataProjected(int refTick, int window)
		{
			SyncedData syncedData = this.GetData(refTick);
			for (int i = 1; i <= window; i++)
			{
				SyncedData data = this.GetData(refTick + i);
				bool fake = data.fake;
				if (fake)
				{
					SyncedData syncedData2 = syncedData.CloneTo(Pool);
					syncedData2.fake = true;
					syncedData2.tick = refTick + i;
					bool flag = this.controls.ContainsKey(syncedData2.tick);
					if (flag)
					{
						Pool.GiveBack(this.controls[syncedData2.tick]);
					}
					this.controls[syncedData2.tick] = syncedData2;
				}
				else
				{
					bool dirty = data.dirty;
					if (dirty)
					{
						data.dirty = false;
						syncedData = data;
					}
				}
			}
		}

        // 添加数据
		public void AddDataRollback(List<SyncedData> data)
		{
			for (int i = 0; i < data.Count; i++)
			{
				SyncedData data2 = this.GetData(data[i].tick);
				bool fake = data2.fake;
				if (fake) // 取出的是假数据
				{
					bool flag = data2.EqualsData(data[i]);
					if (!flag) // 两个数据不想等
					{
						data[i].dirty = true; // 设置为脏数据
						Pool.GiveBack(this.controls[data[i].tick]); // 回收该位置的数据
						this.controls[data[i].tick] = data[i]; // 放入新数据
						break; // 中断，没有下一个数据要处理？
					}
					data2.fake = false;
					data2.dirty = false;
				}
				Pool.GiveBack(data[i]);
			}
		}

		public bool GetSendDataForDrop(byte fromPlayerId, SyncedData[] sendWindowArray)
		{
			bool flag = this.controls.Count == 0;
			bool result;
			if (flag)
			{
				result = false;
			}
			else
			{
				this.GetDataFromTick(this.lastTick, sendWindowArray);
				sendWindowArray[0] = sendWindowArray[0].CloneTo(Pool);
				sendWindowArray[0].dropFromPlayerId = fromPlayerId;
				sendWindowArray[0].dropPlayer = true;
				result = true;
			}
			return result;
		}

		public void GetSendData(int tick, SyncedData[] sendWindowArray)
		{
			this.GetDataFromTick(tick, sendWindowArray);
		}
        #endregion 公共方法

        #region 私有方法
        private void GetDataFromTick(int tick, SyncedData[] sendWindowArray)
        {
            for (int i = 0; i < sendWindowArray.Length; i++)
            {
                // 以倒序的方式把数据放入数组中
                sendWindowArray[i] = this.GetData(tick - i);
            }
        }
        #endregion 私有方法
    }
}
