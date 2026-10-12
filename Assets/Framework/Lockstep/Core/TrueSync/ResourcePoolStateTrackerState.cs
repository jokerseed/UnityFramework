using System;

namespace Framework.Lockstep
{
#pragma warning disable 618
	internal class ResourcePoolStateTrackerState : ResourcePool<StateTracker.State>
	{
		protected override StateTracker.State NewInstance()
		{
			return new StateTracker.State();
		}
	}
#pragma warning restore 618
}
