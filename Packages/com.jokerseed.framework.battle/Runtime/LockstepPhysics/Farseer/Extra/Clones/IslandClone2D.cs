using System.Collections.Generic;

namespace Framework.Lockstep.Physics2D {

    internal class IslandClone2D {

        private List<ContactCloneKey> _contactsKeys = new List<ContactCloneKey>(10);

        public List<Body> Bodies = new List<Body>(10);
        public int BodyCount;
        public int ContactCount;
        public int JointCount;

        public List<Velocity> _velocities = new List<Velocity>(10);
        public List<Position> _positions = new List<Position>(10);

        public int BodyCapacity;
        public int ContactCapacity;
        public int JointCapacity;

        public void Clone(Island island) {
            this.Bodies.Clear();
            var bodies = island != null ? island.Bodies : null;
            if (bodies != null) {
                for (int index = 0, length = bodies.Length; index < length; index++) {
                    this.Bodies.Add(bodies[index]);
                }
            }

            this._velocities.Clear();
            var velocities = island != null ? island._velocities : null;
            if (velocities != null) {
                for (int index = 0, length = velocities.Length; index < length; index++) {
                    this._velocities.Add(velocities[index]);
                }
            }

            this._positions.Clear();
            var positions = island != null ? island._positions : null;
            if (positions != null) {
                for (int index = 0, length = positions.Length; index < length; index++) {
                    this._positions.Add(positions[index]);
                }
            }

            if (island == null) {
                this.BodyCount = 0;
                this.ContactCount = 0;
                this.JointCount = 0;
                this.BodyCapacity = 0;
                this.ContactCapacity = 0;
                this.JointCapacity = 0;
                _contactsKeys.Clear();
                return;
            }

            _contactsKeys.Clear();
            if (island._contacts != null) {
                for (int index = 0; index < island.ContactCount; index++) {
                    _contactsKeys.Add(island._contacts[index].Key);
                }
            }

            this.BodyCount = island.BodyCount;
            this.ContactCount = island.ContactCount;
            this.JointCount = island.JointCount;
            this.BodyCapacity = island.BodyCapacity;
            this.ContactCapacity = island.ContactCapacity;
            this.JointCapacity = island.JointCapacity;
        }

        public void Restore(Island island, Dictionary<ContactCloneKey, Physics2D.Contact> contactDic) {
            island.Reset(this.BodyCapacity, this.ContactCapacity, this.JointCapacity, island._contactManager);

            for (int index = 0, length = this.Bodies.Count; index < length; index++) {
                island.Bodies[index] = this.Bodies[index];
            }

            for (int index = 0, length = this._velocities.Count; index < length; index++) {
                island._velocities[index] = this._velocities[index];
            }

            for (int index = 0, length = this._positions.Count; index < length; index++) {
                island._positions[index] = this._positions[index];
            }

            for (int index = 0; index < ContactCount; index++) {
                island._contacts[index] = contactDic[_contactsKeys[index]];
            }

            island.BodyCount = this.BodyCount;
            island.ContactCount = this.ContactCount;
            island.JointCount = this.JointCount;
        }

    }

}