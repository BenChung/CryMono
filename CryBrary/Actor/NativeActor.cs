﻿
namespace CryEngine
{
	/// <summary>
	/// Used for non-CryMono actors.
	/// </summary>
	[ExcludeFromCompilation]
	class NativeActor : Actor
	{
		public NativeActor() { }

		public NativeActor(ActorInfo actorInfo)
		{
			Id = new EntityId(actorInfo.Id);
			EntityPointer = actorInfo.EntityPtr;
			ActorPointer = actorInfo.ActorPtr;
		}

		internal NativeActor(EntityId id)
		{
			Id = id;
		}
	}
}
