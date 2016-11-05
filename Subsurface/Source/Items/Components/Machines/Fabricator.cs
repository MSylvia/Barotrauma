﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Barotrauma.Items.Components
{
    class FabricableItem
    {
        public readonly ItemPrefab TargetItem;

        public readonly List<Tuple<ItemPrefab, int>> RequiredItems;

        public readonly float RequiredTime;

        public readonly List<Skill> RequiredSkills;

        public FabricableItem(XElement element)
        {
            string name = ToolBox.GetAttributeString(element, "name", "");

            TargetItem = ItemPrefab.list.Find(ip => ip.Name.ToLowerInvariant() == name.ToLowerInvariant()) as ItemPrefab;
            if (TargetItem == null)
            {
                DebugConsole.ThrowError("Error in fabricable item "+name+"! Item \"" + element.Name + "\" not found.");
                return;
            }

            RequiredSkills = new List<Skill>();

            RequiredTime = ToolBox.GetAttributeFloat(element, "requiredtime", 1.0f);

            RequiredItems = new List<Tuple<ItemPrefab, int>>();
            
            string[] requiredItemNames = ToolBox.GetAttributeString(element, "requireditems", "").Split(',');
            foreach (string requiredItemName in requiredItemNames)
            {
                if (string.IsNullOrWhiteSpace(requiredItemName)) continue;

                ItemPrefab requiredItem = ItemPrefab.list.Find(ip => ip.Name.ToLowerInvariant() == requiredItemName.Trim().ToLowerInvariant()) as ItemPrefab;
                if (requiredItem == null)
                {
                    DebugConsole.ThrowError("Error in fabricable item " + name + "! Required item \"" + requiredItemName + "\" not found.");

                    continue;
                }

                var existing = RequiredItems.Find(r => r.Item1 == requiredItem);

                if (existing == null)
                {

                    RequiredItems.Add(new Tuple<ItemPrefab, int>(requiredItem, 1));
                }
                else
                {
                    RequiredItems.Remove(existing);
                    RequiredItems.Add(new Tuple<ItemPrefab, int>(requiredItem, existing.Item2+1));
                }
            }

            foreach (XElement subElement in element.Elements())
            {
                switch (subElement.Name.ToString().ToLowerInvariant())
                {
                    case "requiredskill":
                        RequiredSkills.Add(new Skill(
                            ToolBox.GetAttributeString(subElement, "name", ""), 
                            ToolBox.GetAttributeInt(subElement, "level", 0)));
                        break;
                }
            }

        }
    }

    class Fabricator : Powered
    {
        private List<FabricableItem> fabricableItems;

        private GUIListBox itemList;

        private GUIFrame selectedItemFrame;

        private GUIProgressBar progressBar;
        private GUIButton activateButton;

        private FabricableItem fabricatedItem;
        private float timeUntilReady;

        //used for checking if contained items have changed 
        //(in which case we need to recheck which items can be fabricated)
        private Item[] prevContainedItems;

        private float lastNetworkUpdate;

        public Fabricator(Item item, XElement element) 
            : base(item, element)
        {
            fabricableItems = new List<FabricableItem>();

            foreach (XElement subElement in element.Elements())
            {
                if (subElement.Name.ToString() != "fabricableitem") continue;

                FabricableItem fabricableItem = new FabricableItem(subElement);
                if (fabricableItem.TargetItem != null) fabricableItems.Add(fabricableItem);                
            }

            GuiFrame.Padding = new Vector4(20.0f, 20.0f, 20.0f, 20.0f);

            itemList = new GUIListBox(new Rectangle(0,0,GuiFrame.Rect.Width/2-20,0), GUI.Style, GuiFrame);
            itemList.OnSelected = SelectItem;

            foreach (FabricableItem fi in fabricableItems)
            {
                Color color = ((itemList.CountChildren % 2) == 0) ? Color.Transparent : Color.Black * 0.3f;
                
                GUIFrame frame = new GUIFrame(new Rectangle(0, 0, 0, 50), Color.Transparent, null, itemList)
                {
                    UserData = fi,
                    Padding = new Vector4(5.0f, 5.0f, 5.0f, 5.0f),
                    HoverColor = Color.Gold * 0.2f,
                    SelectedColor = Color.Gold * 0.5f,
                    ToolTip = fi.TargetItem.Description
                };

                GUITextBlock textBlock = new GUITextBlock(
                    new Rectangle(40, 0, 0, 25),
                    fi.TargetItem.Name,
                    Color.Transparent, Color.White,
                    Alignment.Left, Alignment.Left,
                    null, frame);
                textBlock.ToolTip = fi.TargetItem.Description;
                textBlock.Padding = new Vector4(5.0f, 0.0f, 5.0f, 0.0f);

                if (fi.TargetItem.sprite != null)
                {
                    GUIImage img = new GUIImage(new Rectangle(0, 0, 40, 40), fi.TargetItem.sprite, Alignment.Left, frame);
                    img.Scale = Math.Min(Math.Min(40.0f / img.SourceRect.Width, 40.0f / img.SourceRect.Height), 1.0f);
                    img.Color = fi.TargetItem.SpriteColor;
                    img.ToolTip = fi.TargetItem.Description; 
                }

            }
        }

        private bool SelectItem(GUIComponent component, object obj)
        {
            FabricableItem targetItem = obj as FabricableItem;
            if (targetItem == null) return false;

            if (selectedItemFrame != null) GuiFrame.RemoveChild(selectedItemFrame);

            //int width = 200, height = 150;
            selectedItemFrame = new GUIFrame(new Rectangle(0, 0, (int)(GuiFrame.Rect.Width * 0.4f), 300), Color.Black * 0.8f, Alignment.CenterY | Alignment.Right, null, GuiFrame);

            selectedItemFrame.Padding = new Vector4(10.0f, 10.0f, 10.0f, 10.0f);

            progressBar = new GUIProgressBar(new Rectangle(0, 0, 0, 20), Color.Green, GUI.Style, 0.0f, Alignment.BottomCenter, selectedItemFrame);
            progressBar.IsHorizontal = true;

            if (targetItem.TargetItem.sprite != null)
            {
                int y = 0;

                GUIImage img = new GUIImage(new Rectangle(10, 0, 40, 40), targetItem.TargetItem.sprite, Alignment.TopLeft, selectedItemFrame);
                img.Scale = Math.Min(Math.Min(40.0f / img.SourceRect.Width, 40.0f / img.SourceRect.Height), 1.0f);
                img.Color = targetItem.TargetItem.SpriteColor;

                new GUITextBlock(
                    new Rectangle(60, 0, 0, 25),
                    targetItem.TargetItem.Name,
                    Color.Transparent, Color.White,
                    Alignment.TopLeft,
                    Alignment.TopLeft, null,
                    selectedItemFrame, true);

                y += 40;

                if (!string.IsNullOrWhiteSpace(targetItem.TargetItem.Description))
                {
                    var description = new GUITextBlock(
                        new Rectangle(0, y, 0, 0),
                        targetItem.TargetItem.Description, 
                        GUI.Style, Alignment.TopLeft, Alignment.TopLeft, 
                        selectedItemFrame, true, GUI.SmallFont);

                    y += description.Rect.Height + 10;
                }


                List<Skill> inadequateSkills = new List<Skill>();

                if (Character.Controlled != null)
                {
                    inadequateSkills = targetItem.RequiredSkills.FindAll(skill => Character.Controlled.GetSkillLevel(skill.Name) < skill.Level);
                }

                Color textColor = Color.White;
                string text;
                if (!inadequateSkills.Any())
                {
                    text = "Required items:\n";
                    foreach (Tuple<ItemPrefab, int> ip in targetItem.RequiredItems)
                    {
                        text += "   - " + ip.Item1.Name + " x"+ip.Item2+"\n";
                    }
                    text += "Required time: " + targetItem.RequiredTime + " s";
                }
                else
                {
                    text = "Skills required to calibrate:\n";
                    foreach (Skill skill in inadequateSkills)
                    {
                        text += "   - " + skill.Name + " lvl " + skill.Level + "\n";
                    }

                    textColor = Color.Red;
                }

                new GUITextBlock(
                    new Rectangle(0, y, 0, 25),
                    text,
                    Color.Transparent, textColor,
                    Alignment.TopLeft,
                    Alignment.TopLeft, null,
                    selectedItemFrame);

                activateButton = new GUIButton(new Rectangle(0, -30, 100, 20), "Create", Color.White, Alignment.CenterX | Alignment.Bottom, GUI.Style, selectedItemFrame);
                activateButton.OnClicked = StartButtonClicked;
                activateButton.UserData = targetItem;
                activateButton.Enabled = false;
            }

            return true;
        }

        public override bool Select(Character character)
        {
            CheckFabricableItems(character);
            if (itemList.Selected != null)
            {
                SelectItem(itemList.Selected, itemList.Selected.UserData);                
            }


            return base.Select(character);
        }

        public override bool Pick(Character picker)
        {
            return (picker != null);
        }

        /// <summary>
        /// check which of the items can be fabricated by the character
        /// and update the text colors of the item list accordingly
        /// </summary>
        private void CheckFabricableItems(Character character)
        {
            foreach (GUIComponent child in itemList.children)
            {
                var itemPrefab = child.UserData as FabricableItem;
                if (itemPrefab == null) continue;

                bool canBeFabricated = CanBeFabricated(itemPrefab, character);


                child.GetChild<GUITextBlock>().TextColor = Color.White * (canBeFabricated ? 1.0f : 0.5f);
                child.GetChild<GUIImage>().Color = itemPrefab.TargetItem.SpriteColor * (canBeFabricated ? 1.0f : 0.5f);

            }

            var itemContainer = item.GetComponent<ItemContainer>();
            prevContainedItems = new Item[itemContainer.Inventory.Items.Length];
            itemContainer.Inventory.Items.CopyTo(prevContainedItems, 0);
        }

        private bool StartButtonClicked(GUIButton button, object obj)
        {
            if (fabricatedItem == null)
            {
                StartFabricating(obj as FabricableItem);
            }
            else
            {
                CancelFabricating();
            }
            
            return true;
        }

        private void StartFabricating(FabricableItem selectedItem)
        {
            if (selectedItem == null) return;

            itemList.Enabled = false;

            activateButton.Text = "Cancel";

            fabricatedItem = selectedItem;
            IsActive = true;

            timeUntilReady = fabricatedItem.RequiredTime;

            var containers = item.GetComponents<ItemContainer>();
            containers[0].Inventory.Locked = true;
            containers[1].Inventory.Locked = true;

            currPowerConsumption = powerConsumption;
        }

        private void CancelFabricating()
        {
            itemList.Enabled = true;
            IsActive = false;
            fabricatedItem = null;

            currPowerConsumption = 0.0f;

            if (activateButton != null)
            {
                activateButton.Text = "Create";
            }
            if (progressBar != null) progressBar.BarSize = 0.0f;

            timeUntilReady = 0.0f;

            var containers = item.GetComponents<ItemContainer>();
            containers[0].Inventory.Locked = false;
            containers[1].Inventory.Locked = false;
        }

        public override void Update(float deltaTime, Camera cam)
        {
            if (progressBar!=null)
            {
                progressBar.BarSize = fabricatedItem == null ? 0.0f : (fabricatedItem.RequiredTime - timeUntilReady) / fabricatedItem.RequiredTime;
            }

            if (voltage < minVoltage) return;

            if (powerConsumption == 0) voltage = 1.0f;

            timeUntilReady -= deltaTime*voltage;

            voltage -= deltaTime * 10.0f;

            if (timeUntilReady > 0.0f) return;

            var containers = item.GetComponents<ItemContainer>();
            if (containers.Count<2)
            {
                DebugConsole.ThrowError("Error while fabricating a new item: fabricators must have two ItemContainer components");
                return;
            }

            foreach (Tuple<ItemPrefab,int> ip in fabricatedItem.RequiredItems)
            {
                for (int i = 0; i<ip.Item2; i++)
                {
                    var requiredItem = containers[0].Inventory.Items.FirstOrDefault(it => it != null && it.Prefab == ip.Item1);
                    if (requiredItem == null) continue;

                    Item.Spawner.AddToRemoveQueue(requiredItem);
                    containers[0].Inventory.RemoveItem(requiredItem);
                }
            }
                        
            Item.Spawner.AddToSpawnQueue(fabricatedItem.TargetItem, containers[1].Inventory);

            CancelFabricating();
        }

        public override void DrawHUD(SpriteBatch spriteBatch, Character character)
        {
            GuiFrame.Draw(spriteBatch);
        }

        public override void UpdateHUD(Character character)
        {
            FabricableItem targetItem = itemList.SelectedData as FabricableItem;
            if (targetItem != null)
            {
                activateButton.Enabled = CanBeFabricated(targetItem, character);
            }

            if (character != null)
            {
                bool itemsChanged = false;
                if (prevContainedItems == null)
                {
                    itemsChanged = true;
                }
                else
                {
                    var itemContainer = item.GetComponent<ItemContainer>();
                    for (int i = 0; i < itemContainer.Inventory.Items.Length; i++)
                    {
                        if (prevContainedItems[i] != itemContainer.Inventory.Items[i])
                        {
                            itemsChanged = true;
                            break;
                        }
                    }
                }

                if (itemsChanged) CheckFabricableItems(character);
            }


            GuiFrame.Update((float)Timing.Step);
        }

        private bool CanBeFabricated(FabricableItem fabricableItem, Character user)
        {
            if (fabricableItem == null) return false;

            if (user != null && 
                fabricableItem.RequiredSkills.Any(skill => user.GetSkillLevel(skill.Name) < skill.Level))
            {
                return false;
            }

            ItemContainer container = item.GetComponent<ItemContainer>();
            foreach (Tuple<ItemPrefab, int> ip in fabricableItem.RequiredItems)
            {
                if (Array.FindAll(container.Inventory.Items, it => it != null && it.Prefab == ip.Item1).Count() < ip.Item2) return false;
            }

            return true;
        }
        
    }
}
