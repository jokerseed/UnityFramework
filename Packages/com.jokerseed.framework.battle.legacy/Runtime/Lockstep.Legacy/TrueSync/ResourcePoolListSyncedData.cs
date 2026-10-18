using System;
using System.Collections.Generic;

namespace Framework.Lockstep
{
	internal class ResourcePoolListSyncedData : ResourcePool<List<SyncedData>>
	{
		protected override List<SyncedData> NewInstance()
		{
			return new List<SyncedData>();
		}
	}
}
