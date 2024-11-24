using UnityEngine;

namespace VRGIN.Core
{
    public abstract class DefaultActorBehaviour<T> : ProtectedBehaviour, IActor where T : MonoBehaviour
    {
        public T Actor { get; protected set; }

        public virtual bool IsValid => (Object)Actor;

        public abstract Transform Eyes { get; }

        public abstract bool HasHead { get; set; }

        public static A Create<A>(T nativeActor) where A : DefaultActorBehaviour<T>
        {
            var val = nativeActor.GetComponent<A>();
            if (!(Object)val)
            {
                val = nativeActor.gameObject.AddComponent<A>();
                val.Initialize(nativeActor);
            }

            return val;
        }

        protected virtual void Initialize(T actor)
        {
            Actor = actor;
            VRLog.Info("Creating character {0}", actor.name);
        }
    }
}
