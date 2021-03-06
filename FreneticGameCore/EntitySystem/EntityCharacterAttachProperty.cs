﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BEPUutilities;
using BEPUphysics.Character;

namespace FreneticGameCore.EntitySystem
{   
     /// <summary>
     /// Attaches an entity to a character.
     /// </summary>
    public class EntityCharacterAttachProperty<T, T2, T3> : EntitySimpleAttachProperty<T, T2> where T : BasicEntity<T, T2> where T2 : BasicEngine<T, T2> where T3 : EntityPhysicsProperty<T, T2>
    {
        /// <summary>
        /// The entity this entity is attached to.
        /// </summary>
        public override T AttachedTo
        {
            get
            {
                return base.AttachedTo;
            }
            set
            {
                base.AttachedTo = value;
                Character = AttachedTo.GetProperty<T3>().OriginalObject as CharacterController;
            }
        }

        /// <summary>
        /// The character controller of <see cref="AttachedTo"/>.
        /// </summary>
        public CharacterController Character;

        /// <summary>
        /// The view height multiplier of the character controller.
        /// <para>That is, standing height * 0.5 * view height = used height.</para>
        /// </summary>
        public double ViewHeight = 0.95;

        /// <summary>
        /// Handles the spawn event.
        /// </summary>
        public override void OnSpawn()
        {
            base.OnSpawn();
            Entity.OnTick += Tick;
        }

        /// <summary>
        /// Gets the relative quaternion for this attachment.
        /// </summary>
        /// <returns>The relative quaternion.</returns>
        public Quaternion GetRelativeQuaternion()
        {
            // TODO: Less complicated option?!
            Matrix relative = Matrix.CreateLookAtRH(Vector3.Zero, Character.ViewDirection, -Character.Down);
            return BEPUutilities.Quaternion.CreateFromRotationMatrix(relative).ToCore().Inverse();
        }

        /// <summary>
        /// Gets the accurate location for this attachment.
        /// </summary>
        /// <param name="basePos">The base entity position of the character.</param>
        /// <returns>The accurate position.</returns>
        public Location GetAccuratePosition(Location basePos)
        {
            return basePos + new Location(Character.Down) * (Character.StanceManager.StandingHeight * ViewHeight * (-0.5));
        }

        /// <summary>
        /// Set relative offset, based on an entity's offsets from the default positioning.
        /// </summary>
        /// <param name="relPos">The relative position.</param>
        /// <param name="relQuat">The relative quaternion.</param>
        public void SetRelativeForEntity(Location relPos, Quaternion relQuat)
        {
            // TODO: Less cheating! This can be resolved mathematically!
            Vector3 viewDir = Character.ViewDirection;
            Vector3 downDir = Character.Down;
            Character.Down = -Vector3.UnitZ;
            Character.ViewDirection = Vector3.UnitY;
            Entity.SetPosition(GetAccuratePosition(AttachedTo.LastKnownPosition) + relPos);
            Entity.SetOrientation(relQuat);
            SetRelativeToCurrent();
            Character.Down = downDir;
            Character.ViewDirection = viewDir;
            Tick();
        }

        /// <summary>
        /// Set the relative offset to the current relative locations and orientation.
        /// </summary>
        public override void SetRelativeToCurrent()
        {
            SetRelativeBasedOn(GetRelativeQuaternion(), GetAccuratePosition(AttachedTo.LastKnownPosition));
        }

        /// <summary>
        /// Fixes this entity's position based on its attachment.
        /// </summary>
        public override void FixPosition(Location position)
        {
            SetPositionOrientation(GetAccuratePosition(position), GetRelativeQuaternion());
        }

        /// <summary>
        /// Fixes this entity's orientation based on its attachment.
        /// Does nothing for this property.
        /// </summary>
        public override void FixOrientation(Quaternion orientation)
        {
        }

        /// <summary>
        /// Handles the tick event.
        /// </summary>
        public void Tick()
        {
            FixPosition(AttachedTo.LastKnownPosition);
        }

        /// <summary>
        /// Handles the despawn event.
        /// </summary>
        public override void OnDespawn()
        {
            base.OnDespawn();
            Entity.OnTick -= Tick;
        }
    }
}
