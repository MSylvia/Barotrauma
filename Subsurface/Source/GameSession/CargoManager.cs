﻿using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Barotrauma
{
    class CargoManager
    {
        private List<MapEntityPrefab> purchasedItems;
        
        public CargoManager()
        {
            purchasedItems = new List<MapEntityPrefab>();
        }

        public void AddItem(MapEntityPrefab item)
        {
            purchasedItems.Add(item);
        }

        public void CreateItems()
        {
            WayPoint wp = WayPoint.GetRandom(SpawnType.Cargo);

            if (wp==null)
            {
                DebugConsole.ThrowError("The submarine must have a waypoint marked as Cargo for bought items to be placed correctly!");
                return;
            }

            Hull cargoRoom = Hull.FindHull(wp.Position);

            if (wp == null)
            {
                DebugConsole.ThrowError("A waypoint marked as Cargo must be placed inside a room!");
                return;
            }

            foreach (MapEntityPrefab prefab in purchasedItems)
            {
                Vector2 position = new Vector2(
                    Rand.Range(cargoRoom.Rect.X + 20, cargoRoom.Rect.Right - 20),
                    Rand.Range(cargoRoom.Rect.Y - cargoRoom.Rect.Height + 20.0f, cargoRoom.Rect.Y));

                new Item(prefab as ItemPrefab, wp.Position);
            }

            purchasedItems.Clear();
        }
    }
}