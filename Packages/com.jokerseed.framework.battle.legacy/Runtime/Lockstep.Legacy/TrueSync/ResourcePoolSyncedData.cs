using System;

namespace Framework.Lockstep
{
	internal class ResourcePoolSyncedData : ResourcePool<SyncedData>
	{
		/// <summary>本池创建输入时使用的工厂。为 null 时回退到进程级 <c>AbstractLockstep.instance</c>。</summary>
		internal TrueSyncInputDataProvider Provider;

		protected override SyncedData NewInstance()
		{
			var data = new SyncedData();
			data.AttachProvider(Provider);
			return data;
		}

		public void FillStack(int instances)
		{
			for (int i = 0; i < instances; i++)
			{
				this.stack.Push(NewInstance());
			}
		}
	}
}
