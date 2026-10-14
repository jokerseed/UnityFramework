using System;

namespace Framework.Lockstep
{
	public interface ITrueSyncBehaviourGamePlay : ITrueSyncBehaviour
	{
		void OnPreSyncedUpdate();

		void OnSyncedInput();

		void OnSyncedUpdate();
	}
}
