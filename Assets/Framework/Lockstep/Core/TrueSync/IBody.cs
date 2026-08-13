using System;

namespace Framework.Lockstep
{
	public interface IBody
	{
		bool TSDisabled
		{
			get;
			set;
		}

		string Checkum();
	}
}
