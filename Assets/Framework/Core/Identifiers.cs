namespace Framework.Core
{
    public readonly struct ActorId : System.IEquatable<ActorId>
    {
        public static readonly ActorId Invalid = new ActorId(0);

        public uint Value { get; }

        public bool IsValid => Value != 0;

        public ActorId(uint value) => Value = value;

        public bool Equals(ActorId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is ActorId other && Equals(other);

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => $"Actor({Value})";

        public static bool operator ==(ActorId left, ActorId right) => left.Equals(right);

        public static bool operator !=(ActorId left, ActorId right) => !left.Equals(right);
    }
}
