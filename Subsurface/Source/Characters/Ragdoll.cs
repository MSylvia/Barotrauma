﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using FarseerPhysics;
using FarseerPhysics.Dynamics;
using FarseerPhysics.Dynamics.Contacts;
using FarseerPhysics.Dynamics.Joints;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Barotrauma.Networking;

namespace Barotrauma
{
    class Ragdoll
    {
        public static List<Ragdoll> list = new List<Ragdoll>();

        protected Hull currentHull;

        public Limb[] Limbs;
        private Dictionary<LimbType, Limb> limbDictionary;
        public RevoluteJoint[] limbJoints;

        private bool simplePhysicsEnabled;

        private Character character;

        private Limb lowestLimb;

        protected float strongestImpact;

        float headPosition, headAngle;
        float torsoPosition, torsoAngle;

        protected double onFloorTimer;

        //the movement speed of the ragdoll
        public Vector2 movement;
        //the target speed towards which movement is interpolated
        private Vector2 targetMovement;

        //a movement vector that overrides targetmovement if trying to steer
        //a character to the position sent by server in multiplayer mode
        private Vector2 correctionMovement;
        
        protected float floorY;
        protected float surfaceY;
        
        protected bool inWater, headInWater;
        public bool onGround;
        private bool ignorePlatforms;

        private Limb refLimb;

        protected Structure stairs;
                
        protected Direction dir;

        //private byte ID;
        
        public Limb LowestLimb
        {
            get { return lowestLimb; }
        }

        public Limb RefLimb
        {
            get
            {
                return refLimb;
            }
        }

        public float Mass
        {
            get;
            private set;
        }

        public bool SimplePhysicsEnabled
        {
            get { return simplePhysicsEnabled; }
            set
            {
                if (value == simplePhysicsEnabled) return;

                simplePhysicsEnabled = value;

                foreach (Limb limb in Limbs)
                {
                    limb.body.Enabled = !simplePhysicsEnabled;
                }

                foreach (RevoluteJoint joint in limbJoints)
                {
                    joint.Enabled = !simplePhysicsEnabled;
                }

                refLimb.body.Enabled = true;
            }
        }

        public Vector2 TargetMovement
        {
            get 
            { 
                return (correctionMovement == Vector2.Zero) ? targetMovement : correctionMovement; 
            }
            set 
            {
                if (!MathUtils.IsValid(value)) return;
                targetMovement.X = MathHelper.Clamp(value.X, -3.0f, 3.0f);
                targetMovement.Y = MathHelper.Clamp(value.Y, -3.0f, 3.0f);
            }
        }

        public float HeadPosition
        { 
            get { return headPosition; } 
        }

        public float HeadAngle
        { 
            get { return headAngle; } 
        }
        
        public float TorsoPosition
        { 
            get { return torsoPosition; } 
        }

        public float TorsoAngle
        { 
            get { return torsoAngle; } 
        }

        public float Dir
        {
            get { return ((dir == Direction.Left) ? -1.0f : 1.0f); }
        }

        public bool InWater
        {
            get { return inWater; }
        }

        public bool HeadInWater
        {
            get { return headInWater; }
        }

        public Hull CurrentHull
        {
            get { return currentHull;}
        }

        public bool IgnorePlatforms
        {
            get { return ignorePlatforms; }
            set 
            {
                if (ignorePlatforms == value) return;
                ignorePlatforms = value;

                UpdateCollisionCategories();

            }
        }

        public float StrongestImpact
        {
            get { return strongestImpact; }
            set { strongestImpact = Math.Max(value, strongestImpact); }
        }

        public Structure Stairs
        {
            get { return stairs; }
        }
        
        public Ragdoll(Character character, XElement element)
        {
            list.Add(this);

            this.character = character;

            dir = Direction.Right;
            
            //int limbAmount = ;
            Limbs = new Limb[element.Elements("limb").Count()];
            limbJoints = new RevoluteJoint[element.Elements("joint").Count()];
            limbDictionary = new Dictionary<LimbType, Limb>();

            headPosition = ToolBox.GetAttributeFloat(element, "headposition", 50.0f);
            headPosition = ConvertUnits.ToSimUnits(headPosition);
            headAngle = MathHelper.ToRadians(ToolBox.GetAttributeFloat(element, "headangle", 0.0f));

            torsoPosition = ToolBox.GetAttributeFloat(element, "torsoposition", 50.0f);
            torsoPosition = ConvertUnits.ToSimUnits(torsoPosition);
            torsoAngle = MathHelper.ToRadians(ToolBox.GetAttributeFloat(element, "torsoangle", 0.0f));
                       
            foreach (XElement subElement in element.Elements())
            {
                switch (subElement.Name.ToString())
                {
                    case "limb":
                        byte ID = Convert.ToByte(subElement.Attribute("id").Value);

                        Limb limb = new Limb(character, subElement);

                        limb.body.FarseerBody.OnCollision += OnLimbCollision;
                        
                        Limbs[ID] = limb;
                        Mass += limb.Mass;
                        if (!limbDictionary.ContainsKey(limb.type)) limbDictionary.Add(limb.type, limb);
                        break;
                    case "joint":
                        Byte limb1ID = Convert.ToByte(subElement.Attribute("limb1").Value);
                        Byte limb2ID = Convert.ToByte(subElement.Attribute("limb2").Value);

                        Vector2 limb1Pos = ToolBox.GetAttributeVector2(subElement, "limb1anchor", Vector2.Zero);
                        limb1Pos = ConvertUnits.ToSimUnits(limb1Pos);

                        Vector2 limb2Pos = ToolBox.GetAttributeVector2(subElement, "limb2anchor", Vector2.Zero);
                        limb2Pos = ConvertUnits.ToSimUnits(limb2Pos);

                        RevoluteJoint joint = new RevoluteJoint(Limbs[limb1ID].body.FarseerBody, Limbs[limb2ID].body.FarseerBody, limb1Pos, limb2Pos);

                        joint.CollideConnected = false;

                        if (subElement.Attribute("lowerlimit")!=null)
                        {
                            joint.LimitEnabled = true;
                            joint.LowerLimit = float.Parse(subElement.Attribute("lowerlimit").Value) * ((float)Math.PI / 180.0f);
                            joint.UpperLimit = float.Parse(subElement.Attribute("upperlimit").Value) * ((float)Math.PI / 180.0f);
                        }

                        joint.MotorEnabled = true;
                        joint.MaxMotorTorque = 0.25f;

                        GameMain.World.AddJoint(joint);

                        for (int i = 0; i < limbJoints.Length; i++ )
                        {
                            if (limbJoints[i] != null) continue;
                            
                            limbJoints[i] = joint;
                            break;                            
                        }

                        break;
                }

            }

            refLimb = GetLimb(LimbType.Torso);
            if (refLimb == null) refLimb = GetLimb(LimbType.Head);
            if (refLimb == null) DebugConsole.ThrowError("Character ''" + character + "'' doesn't have a head or torso!");

            foreach (var joint in limbJoints)
            {

                joint.BodyB.SetTransform(
                    joint.BodyA.Position+joint.LocalAnchorA-joint.LocalAnchorB,
                    (joint.LowerLimit+joint.UpperLimit)/2.0f);
            }

            float startDepth = 0.1f;
            float increment = 0.001f;

            foreach (Character otherCharacter in Character.CharacterList)
            {
                if (otherCharacter==character) continue;
                startDepth+=increment;
            }

            foreach (Limb limb in Limbs)
            {
                limb.sprite.Depth = startDepth + limb.sprite.Depth * 0.0001f;
            }
        }
          
        public bool OnLimbCollision(Fixture f1, Fixture f2, Contact contact)
        {
            Structure structure = f2.Body.UserData as Structure;
            
            //always collides with bodies other than structures
            if (structure == null)
            {
                CalculateImpact(f1, f2, contact);
                return true;
            }
            
            if (structure.IsPlatform)
            {
                if (ignorePlatforms) return false;

                //the collision is ignored if the lowest limb is under the platform
                if (lowestLimb==null || lowestLimb.Position.Y < structure.Rect.Y) return false; 
            }
            else if (structure.StairDirection!=Direction.None && lowestLimb != null)
            {
                if (targetMovement.Y < 0.5f)
                {
                    if (inWater || lowestLimb.Position.Y < structure.Rect.Y - structure.Rect.Height + 50.0f)
                    {
                        stairs = null;
                        return false;
                    }
                }
                
                if (targetMovement.Y >= 0.0f && lowestLimb.SimPosition.Y > ConvertUnits.ToSimUnits(structure.Rect.Y - Submarine.GridSize.Y * 8.0f))
                {
                    stairs = null;
                    return false;
                }
                
                Limb limb = f1.Body.UserData as Limb;
                if (limb != null && (limb.type == LimbType.LeftFoot || limb.type == LimbType.RightFoot))
                {
                    if (contact.Manifold.LocalNormal.Y >= 0.0f)
                    {
                        stairs = structure;
                        return true;
                    }
                    else
                    {
                        stairs = null;
                        return false;
                    }                    
                }
                else
                {
                    return false;
                }
            }
                

            CalculateImpact(f1, f2, contact);
            return true;
        }

        private void CalculateImpact(Fixture f1, Fixture f2, Contact contact)
        {
            Vector2 normal = contact.Manifold.LocalNormal;

            Vector2 avgVelocity = Vector2.Zero;
            foreach (Limb limb in Limbs)
            {
                avgVelocity += limb.LinearVelocity;
            }

            avgVelocity = avgVelocity / Limbs.Count();
            
            float impact = Vector2.Dot((f1.Body.LinearVelocity + avgVelocity) / 2.0f, -normal);

            if (GameMain.Server != null) impact = impact / 2.0f;

            Limb l = (Limb)f1.Body.UserData;

            if (impact > 1.0f && l.HitSound != null && l.soundTimer <= 0.0f) l.HitSound.Play(Math.Min(impact / 5.0f, 1.0f), impact * 100.0f, l.body.FarseerBody);

            if (impact > l.impactTolerance)
            {   
                character.Health -= (impact - l.impactTolerance * 0.1f);
                strongestImpact = Math.Max(strongestImpact, impact - l.impactTolerance);

                AmbientSoundManager.PlayDamageSound(DamageSoundType.LimbBlunt, strongestImpact, l.body.FarseerBody);                

                if (Character.Controlled == character) GameMain.GameScreen.Cam.Shake = strongestImpact;
            }
        }

        public virtual void Draw(SpriteBatch spriteBatch)
        {            
            foreach (Limb limb in Limbs)
            {
                limb.Draw(spriteBatch);
            }  
        }

        public void DebugDraw(SpriteBatch spriteBatch)
        {
            if (!GameMain.DebugDraw) return;

            foreach (Limb limb in Limbs)
            {

                if (limb.pullJoint != null)
                {
                    Vector2 pos = ConvertUnits.ToDisplayUnits(limb.pullJoint.WorldAnchorA);
                    pos.Y = -pos.Y;
                    GUI.DrawRectangle(spriteBatch, new Rectangle((int)pos.X, (int)pos.Y, 5, 5), Color.Red, true, 0.01f);

                    if (limb.AnimTargetPos == Vector2.Zero) continue;

                    Vector2 pos2 = ConvertUnits.ToDisplayUnits(limb.AnimTargetPos);
                    pos2.Y = -pos2.Y;
                    GUI.DrawRectangle(spriteBatch, new Rectangle((int)pos2.X, (int)pos2.Y, 5, 5), Color.Blue, true, 0.01f);

                    GUI.DrawLine(spriteBatch, pos, pos2, Color.Green);
                }
            }

            foreach (RevoluteJoint joint in limbJoints)
            {
                Vector2 pos = ConvertUnits.ToDisplayUnits(joint.WorldAnchorA);
                GUI.DrawRectangle(spriteBatch, new Rectangle((int)pos.X, (int)-pos.Y, 5, 5), Color.White, true);

                pos = ConvertUnits.ToDisplayUnits(joint.WorldAnchorB);
                GUI.DrawRectangle(spriteBatch, new Rectangle((int)pos.X, (int)-pos.Y, 5, 5), Color.White, true);
            }

            foreach (Limb limb in Limbs)
            {
                if (limb.body.TargetPosition != Vector2.Zero)
                {
                    Vector2 pos = ConvertUnits.ToDisplayUnits(limb.body.TargetPosition);
                    pos.Y = -pos.Y;

                    GUI.DrawRectangle(spriteBatch, new Rectangle((int)pos.X - 10, (int)pos.Y - 10, 20, 20), Color.Cyan, false, 0.01f);
                    GUI.DrawLine(spriteBatch, pos, new Vector2(limb.Position.X, -limb.Position.Y), limb==RefLimb ? Color.Orange : Color.Cyan);
                }
            }


        }

        public virtual void Flip()
        {
            dir = (dir == Direction.Left) ? Direction.Right : Direction.Left;

            for (int i = 0; i < limbJoints.Count(); i++)
            {
                float lowerLimit = -limbJoints[i].UpperLimit;
                float upperLimit = -limbJoints[i].LowerLimit;

                limbJoints[i].LowerLimit = lowerLimit;
                limbJoints[i].UpperLimit = upperLimit;

                limbJoints[i].LocalAnchorA = new Vector2(-limbJoints[i].LocalAnchorA.X, limbJoints[i].LocalAnchorA.Y);
                limbJoints[i].LocalAnchorB = new Vector2(-limbJoints[i].LocalAnchorB.X, limbJoints[i].LocalAnchorB.Y);
            }


            for (int i = 0; i < Limbs.Count(); i++)
            {
                if (Limbs[i] == null) continue;

                Vector2 spriteOrigin = Limbs[i].sprite.Origin;
                spriteOrigin.X = Limbs[i].sprite.SourceRect.Width - spriteOrigin.X;
                Limbs[i].sprite.Origin = spriteOrigin;

                Limbs[i].Dir = Dir;

                if (Limbs[i].pullJoint == null) continue;

                Limbs[i].pullJoint.LocalAnchorA = 
                    new Vector2(
                        -Limbs[i].pullJoint.LocalAnchorA.X, 
                        Limbs[i].pullJoint.LocalAnchorA.Y);
            }            
        }

        public Vector2 GetCenterOfMass()
        {
            Vector2 centerOfMass = Vector2.Zero;
            foreach (Limb limb in Limbs)
            {
                centerOfMass += limb.Mass * limb.SimPosition;
            }

            centerOfMass /= Mass;

            return centerOfMass;
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="pullFromCenter">if false, force is applied to the position of pullJoint</param>
        protected void MoveLimb(Limb limb, Vector2 pos, float amount, bool pullFromCenter = false)
        {
            limb.Move(pos, amount, pullFromCenter);
        }
                
        public void ResetPullJoints()
        {
            for (int i = 0; i < Limbs.Count(); i++)
            {
                if (Limbs[i] == null || Limbs[i].pullJoint == null) continue;
                Limbs[i].pullJoint.Enabled = false;
            }
        }

        public static void UpdateAll(Camera cam, float deltaTime)
        {
            foreach (Ragdoll r in list)
            {
                r.Update(cam, deltaTime);
            }
        }

        public void FindHull()
        {
            Hull newHull = Hull.FindHull(
                ConvertUnits.ToDisplayUnits(refLimb.SimPosition), 
                currentHull);

            if (newHull == currentHull) return;

            currentHull = newHull;

            UpdateCollisionCategories();
        }

        private void UpdateCollisionCategories()
        {
            Category wall = currentHull == null ? 
                Physics.CollisionLevel | Physics.CollisionWall 
                : Physics.CollisionWall;

            Category collisionCategory = (ignorePlatforms) ?
                wall | Physics.CollisionProjectile | Physics.CollisionStairs
                : wall | Physics.CollisionProjectile | Physics.CollisionPlatform | Physics.CollisionStairs;

            foreach (Limb limb in Limbs)
            {
                if (limb.ignoreCollisions) continue;

                try
                {
                    limb.body.CollidesWith = collisionCategory;
                }
                catch (Exception e)
                {
                    DebugConsole.ThrowError("Failed to update ragdoll limb collisioncategories", e);
                }

            }
        }

        public void Update(Camera cam, float deltaTime)
        {
            if (!character.Enabled) return;

            UpdateNetplayerPosition();
            
            Vector2 flowForce = Vector2.Zero;

            FindLowestLimb();

            FindHull();
            
            //ragdoll isn't in any room -> it's in the water
            if (currentHull == null)
            {
                inWater = true;
                headInWater = true;
            }
            else
            {
                flowForce = GetFlowForce();

                inWater = false;
                headInWater = false;

                if (currentHull.Volume > currentHull.FullVolume * 0.95f || 
                    ConvertUnits.ToSimUnits(currentHull.Surface) - floorY > HeadPosition * 0.95f)
                    inWater = true;                
            }
                       
            foreach (Limb limb in Limbs)
            {
                Vector2 limbPosition = ConvertUnits.ToDisplayUnits(limb.SimPosition);

                //find the room which the limb is in
                //the room where the ragdoll is in is used as the "guess", meaning that it's checked first
                Hull limbHull = Hull.FindHull(limbPosition, currentHull);
                
                bool prevInWater = limb.inWater;
                limb.inWater = false;

                if (limbHull == null)
                {                  
                    //limb isn't in any room -> it's in the water
                    limb.inWater = true;
                }
                else if (limbHull.Volume > 0.0f && Submarine.RectContains(limbHull.Rect, limbPosition))
                {                    
                    if (limbPosition.Y < limbHull.Surface)                        
                    {
                        limb.inWater = true;

                        if (flowForce.Length() > 0.01f)
                        {
                            limb.body.ApplyForce(flowForce);
                            if (flowForce.Length() > 15.0f) surfaceY = limbHull.Surface;
                        }

                        surfaceY = limbHull.Surface;

                        if (limb.type == LimbType.Head)
                        {
                            headInWater = true;
                            surfaceY = limbHull.Surface;
                        }
                    }
                        //the limb has gone through the surface of the water
                    if (Math.Abs(limb.LinearVelocity.Y) > 3.0 && inWater != prevInWater)
                    {

                        //create a splash particle
                        Barotrauma.Particles.Particle splash = GameMain.ParticleManager.CreateParticle("watersplash",
                            new Vector2(limb.Position.X, limbHull.Surface),
                            new Vector2(0.0f, Math.Abs(-limb.LinearVelocity.Y * 10.0f)),
                            0.0f);

                        //if (splash != null) splash.yLimits = ConvertUnits.ToSimUnits(
                        //    new Vector2(
                        //        limbHull.Rect.Y,
                        //        limbHull.Rect.Y - limbHull.Rect.Height));

                        GameMain.ParticleManager.CreateParticle("bubbles",
                            new Vector2(limb.Position.X, limbHull.Surface),                            
                            limb.LinearVelocity*0.001f,
                            0.0f);



                        //if the character dropped into water, create a wave
                        if (limb.LinearVelocity.Y<0.0f)
                        {
                            //1.0 when the limb is parallel to the surface of the water
                            // = big splash and a large impact
                            float parallel = (float)Math.Abs(Math.Sin(limb.Rotation));
                            Vector2 impulse = Vector2.Multiply(limb.LinearVelocity, -parallel * limb.Mass);
                            //limb.body.ApplyLinearImpulse(impulse);
                            int n = (int)((limbPosition.X - limbHull.Rect.X) / Hull.WaveWidth);
                            limbHull.WaveVel[n] = Math.Min(impulse.Y * 1.0f, 5.0f);
                            StrongestImpact = ((impulse.Length() * 0.5f) - limb.impactTolerance);
                        }
                    }                    
                }

                limb.Update(deltaTime);

            }
            
        }

        private void UpdateNetplayerPosition()
        {
            if (refLimb.body.TargetPosition == Vector2.Zero)
            {
                correctionMovement = Vector2.Zero;
                return;
            }

            //if the limb is further away than resetdistance, all limbs are immediately snapped to their targetpositions
            float resetDistance = NetConfig.ResetRagdollDistance;

            //if the limb is closer than alloweddistance, just ignore the difference
            float allowedDistance = NetConfig.AllowedRagdollDistance * ((inWater) ? 2.0f : 1.0f);

            float dist = Vector2.Distance(refLimb.body.SimPosition, refLimb.body.TargetPosition);
            bool resetAll = dist > resetDistance;
            if (resetAll)
            {
                if (Limbs.FirstOrDefault(limb => !limb.ignoreCollisions && limb.body.TargetPosition == Vector2.Zero) != null) resetAll = false;
            }

            Vector2 diff = (refLimb.body.TargetPosition - refLimb.body.SimPosition);

            if (diff == Vector2.Zero || diff.Length() < allowedDistance)
            {
                refLimb.body.TargetPosition = Vector2.Zero;
                foreach (Limb limb in Limbs)
                {
                    limb.body.TargetPosition = Vector2.Zero;
                }

                correctionMovement = Vector2.Zero;
                return;
            }
            else
            {
                if (inWater)
                {
                    foreach (Limb limb in Limbs)
                    {
                        //if (limb.body.TargetPosition == Vector2.Zero) continue;

                        //limb.body.SetTransform(limb.SimPosition + newMovement * 0.1f, limb.Rotation);
                    }

                    correctionMovement =
                        Vector2.Lerp(targetMovement, Vector2.Normalize(diff) * MathHelper.Clamp(dist * 5.0f, 0.1f, 5.0f), 0.2f);
                }
                else
                {
                    correctionMovement =
                        Vector2.Lerp(targetMovement, Vector2.Normalize(diff) * MathHelper.Clamp(dist * 5.0f, 0.1f, 5.0f), 0.2f);
                        
                    if (Math.Abs(correctionMovement.Y) < 0.1f) correctionMovement.Y = 0.0f;
                }
            }

            if (resetAll)
            {
                System.Diagnostics.Debug.WriteLine("reset ragdoll limb positions");

                foreach (Limb limb in Limbs)
                {
                    if (limb.body.TargetPosition == Vector2.Zero)
                    {
                        limb.body.SetTransform(limb.body.SimPosition + diff, limb.body.Rotation);
                        continue;
                    }

                    limb.body.LinearVelocity = limb.body.TargetVelocity;
                    limb.body.AngularVelocity = limb.body.TargetAngularVelocity;

                    limb.body.SetTransform(limb.body.TargetPosition, limb.body.TargetRotation);
                    limb.body.TargetPosition = Vector2.Zero;
                }
            } 
        }


        private Vector2 GetFlowForce()
        {
            Vector2 limbPos = ConvertUnits.ToDisplayUnits(Limbs[0].SimPosition);

            Vector2 force = Vector2.Zero;
            foreach (MapEntity e in MapEntity.mapEntityList)
            {
                Gap gap = e as Gap;
                if (gap == null || gap.FlowTargetHull != currentHull || gap.LerpedFlowForce == Vector2.Zero) continue;

                Vector2 gapPos = gap.SimPosition;

                float dist = Vector2.Distance(limbPos, gapPos);

                force += Vector2.Normalize(gap.LerpedFlowForce) * (Math.Max(gap.LerpedFlowForce.Length() - dist, 0.0f) / 500.0f);
            }

            if (force.Length() > 20.0f) return force;
            return force;
        }

        public Limb GetLimb(LimbType limbType)
        {
            Limb limb = null;
            limbDictionary.TryGetValue(limbType, out limb);
            return limb;
        }
        
        public void FindLowestLimb()
        {
            //find the lowest limb
            lowestLimb = null;
            foreach (Limb limb in Limbs)
            {
                if (lowestLimb == null)
                    lowestLimb = limb;
                else if (limb.SimPosition.Y < lowestLimb.SimPosition.Y)
                    lowestLimb = limb;
            }
        }

        public void Remove()
        {
            foreach (Limb l in Limbs) l.Remove();
            foreach (RevoluteJoint joint in limbJoints)
            {
                GameMain.World.RemoveJoint(joint);
            }
        }

    }
}