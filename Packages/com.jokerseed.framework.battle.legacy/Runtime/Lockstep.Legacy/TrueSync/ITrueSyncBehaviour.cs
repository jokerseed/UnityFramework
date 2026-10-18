using System;

namespace Framework.Lockstep
{
	public interface ITrueSyncBehaviour
	{
		void SetGameInfo(TSPlayerInfo localOwner, int numberOfPlayers);
	}
}
