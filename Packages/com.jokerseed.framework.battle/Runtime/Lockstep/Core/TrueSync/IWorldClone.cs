using System;

namespace Framework.Lockstep
{
	public interface IWorldClone
	{
		string checksum
		{
			get;
		}

		void Clone(IWorld iWorld);

		void Clone(IWorld iWorld, bool doChecksum);

		void Restore(IWorld iWorld);
	}
}
