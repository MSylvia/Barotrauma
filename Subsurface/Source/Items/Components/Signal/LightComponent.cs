﻿using FarseerPhysics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Barotrauma.Lights;
using System;
using System.IO;
using System.Xml.Linq;

namespace Barotrauma.Items.Components
{
    class LightComponent : Powered
    {

        private Color lightColor;

        //private Sprite sprite;

        LightSource light;

        float range;

        float lightBrightness;

        private float flicker;

        [Editable, HasDefaultValue(100.0f, true)]
        public float Range
        {
            get { return range; }
            set
            {
                range = MathHelper.Clamp(value, 0.0f, 2048.0f);
            }
        }
        
        [HasDefaultValue(0.0f, false)]
        public float Flicker
        {
            get { return flicker; }
            set
            {
                flicker = MathHelper.Clamp(value, 0.0f, 1.0f);
            }
        }

        [InGameEditable, HasDefaultValue("1.0,1.0,1.0,1.0", true)]
        public string LightColor
        {
            get { return ToolBox.Vector4ToString(lightColor.ToVector4()); }
            set
            {
                Vector4 newColor = ToolBox.ParseToVector4(value);
                newColor.X = MathHelper.Clamp(newColor.X, 0.0f, 1.0f);
                newColor.Y = MathHelper.Clamp(newColor.Y, 0.0f, 1.0f);
                newColor.Z = MathHelper.Clamp(newColor.Z, 0.0f, 1.0f);
                newColor.W = MathHelper.Clamp(newColor.W, 0.0f, 1.0f);
                lightColor = new Color(newColor);
            }
        }

        public override void Move(Vector2 amount)
        {
            light.Position += amount;
        }

        public override bool IsActive
        {
            get
            {
                return base.IsActive;
            }

            set
            {
                if (base.IsActive == value) return;
                base.IsActive = value;
                light.Color = value ? lightColor : Color.Transparent;                
            }
        }

        public LightComponent(Item item, XElement element)
            : base (item, element)
        {
            //foreach (XElement subElement in element.Elements())
            //{
            //    if (subElement.Name.ToString().ToLower() != "sprite") continue;
            //    sprite = new Sprite(subElement);
            //    break;
            //}

            light = new LightSource(item.Position, 100.0f, Color.White);

            IsActive = true;

            //lightColor = new Color(ToolBox.GetAttributeVector4(element, "color", Vector4.One));
        }
        
        public override void Update(float deltaTime, Camera cam)
        {
            base.Update(deltaTime, cam);
            
            if (item.container != null)
            {
                light.Color = Color.Transparent;
                return;
            }

            if (item.body != null)
            {
                light.Position = ConvertUnits.ToDisplayUnits(item.body.SimPosition);
            }
            
            if (powerConsumption == 0.0f)
            {
                voltage = 1.0f;
            }
            else
            {
                currPowerConsumption = powerConsumption;                
            }

            if (Rand.Range(0.0f, 1.0f) < 0.05f && voltage < Rand.Range(0.0f, minVoltage))
            {
                if (voltage > 0.1f) sparkSounds[Rand.Int(sparkSounds.Length)].Play(1.0f, 400.0f, item.Position);
                lightBrightness = 0.0f;
            }
            else
            {
                lightBrightness = MathHelper.Lerp(lightBrightness, Math.Min(voltage, 1.0f), 0.1f);
            }

            light.Color = lightColor * lightBrightness * (1.0f-Rand.Range(0.0f,Flicker));
            light.Range = range * (float)Math.Sqrt(lightBrightness);

            voltage = 0.0f;
        }

        public override void Draw(SpriteBatch spriteBatch, bool editing)
        {
            if (!editing) return;

            //Vector2 center = new Vector2(item.Rect.Center.X, -item.Rect.Y + item.Rect.Height/2.0f);

            //GUI.DrawLine(spriteBatch, center - Vector2.One * range, center + Vector2.One * range, lightColor);
        }

        public override void Remove()
        {
            base.Remove();

            light.Remove();
        }

        public override void ReceiveSignal(string signal, Connection connection, Item sender, float power=0.0f)
        {
            base.ReceiveSignal(signal, connection, sender, power);

            switch (connection.Name)
            {
                case "toggle":
                    IsActive = !IsActive;
                    break;
                case "set_state":           
                    IsActive = (signal != "0");                   
                    break;
            }
        }
    }
}