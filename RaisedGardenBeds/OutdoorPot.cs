﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Netcode;
using StardewValley;
using StardewValley.Locations;
using StardewValley.TerrainFeatures;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace RaisedGardenBeds
{
	[XmlType("Mods_Blueberry_RaisedGardenBeds_OutdoorPot")]	// SpaceCore serialisation signature
	public class OutdoorPot : StardewValley.Objects.IndoorPot
	{
		[Flags]
		public enum Axis
		{
			None = 0,
			Vertical = 1,
			Horizontal = 2,
			Diagonal = 4
		}

		/**********
		Identifiers
		**********/

		/// <summary>
		/// Overrides default object displayName accessors to ensure it uses the values we set.
		/// </summary>
		public override string DisplayName
		{
			get => this.displayName;
			set => this.displayName = value;
		}
		/// <summary>
		/// Serialised wrapper for synchronised variant key string.
		/// Required for unique object persistence.
		/// </summary>
		public string VariantKey
		{
			get => this._variantKey.Value;
			set
			{
				this.SetForVariant(variantKey: value);
			}
		}

		/******
		Breakage
		*******/

		/// <summary>
		/// Counter in days, counting down towards and below 0, used for object breakage logic.
		/// </summary>
		public NetInt BreakageTimer = new NetInt();
		/// <summary>
		/// Whether object breakage is enabled.
		/// </summary>
		public bool CanBreak => ModEntry.Config.RaisedBedsMayBreakWithAge;
		/// <summary>
		/// Whether the object is marked as broken, preventing it from planting or growing seeds and crops.
		/// </summary>
		public bool IsBroken => this.CanBreak && this.BreakageTimer.Value <= OutdoorPot.BreakageDefinite;
		/// <summary>
		/// Whether the object is ready to be broken at the end of the season.
		/// </summary>
		public bool IsReadyToBreak => this.CanBreak && this.BreakageTimer.Value <= OutdoorPot.BreakageTarget && this.BreakageTimer.Value > OutdoorPot.BreakageDefinite;

		/**********
		Temp values
		**********/

		/// <summary>
		/// Dynamic dummy index in game content craftables spritesheet for the generic OutdoorPot object.
		/// </summary>
		[XmlIgnore]
		public static int BaseParentSheetIndex = -1;
		/// <summary>
		/// Visual Y-offset of soil sprites from object tile Y-position.
		/// </summary>
		[XmlIgnore]
		public NetInt SoilHeightAboveGround = new NetInt();
		/// <summary>
		/// Default number of days before the object can be broken at the end of the season.
		/// </summary>
		[XmlIgnore]
		public NetInt BreakageStart = new NetInt();
		/// <summary>
		/// Name of key for this object's texture in the <see cref="OutdoorPot.Sprites"/> spritesheet list.
		/// </summary>
		[XmlIgnore]
		public string SpriteKey { get; set; }
		/// <summary>
		/// Index of this object within its texture in the <see cref="OutdoorPot.Sprites"/> spritesheet list.
		/// </summary>
		[XmlIgnore]
		public int SpriteIndex { get; set; }
		/// <summary>
		/// Array of axes that contain a neighbouring OutdoorPot object, projecting outwards from each corner of the object tile.
		/// </summary>
		[XmlIgnore]
		public readonly NetArray<int, NetInt> Neighbours = new NetArray<int, NetInt>(size: 4);
		/// <summary>
		/// Name of key for the current object variant in the <see cref="ModEntry.ItemDefinitions"/> dictionary.
		/// </summary>
		[XmlIgnore]
		private readonly NetString _variantKey = new NetString();
		/// <summary>
		/// Temporary one-tick variable used in <see cref="OutdoorPot.AdjustAllOnNextTick(GameLocation)"/> 
		/// in order to indirectly provide the locations to check to the <see cref="AdjustAll(GameLocation)"/> method.
		/// </summary>
		[XmlIgnore]
		private static GameLocation LocationToIdentifyOutdoorPots;

		/***********
		Const values
		***********/

		/// <summary>
		/// Horizontal index of object sprite for menu and held views in the shared <see cref="OutdoorPot.Sprites"/> spritesheet.
		/// </summary>
		internal const int PreviewIndexInSheet = 9;
		/// <summary>
		/// Horizontal index of broken object sprite in the shared <see cref="OutdoorPot.Sprites"/> spritesheet.
		/// </summary>
		internal const int BrokenIndexInSheet = 8;
		/// <summary>
		/// Horizontal index of object endpiece corner sprite in the shared <see cref="OutdoorPot.Sprites"/> spritesheet.
		/// </summary>
		internal const int EndpieceIndexInSheet = 6;
		/// <summary>
		/// Horizontal index of soil sprites, arranged vertically, in the shared <see cref="OutdoorPot.Sprites"/> spritesheet.
		/// </summary>
		internal const int SoilIndexInSheet = 4;
		/// <summary>
		/// Value for <see cref="OutdoorPot.BreakageTimer"/> where the object is marked for breakage.
		/// </summary>
		internal const int BreakageTarget = 0;
		/// <summary>
		/// When broken, <see cref="OutdoorPot.BreakageTimer"/> is set to this value.
		/// </summary>
		internal const int BreakageDefinite = -300;
		/// <summary>
		/// When object breaks, tool actions will destroy it, rather than popping it.
		/// Breaking the object will refund the primary resource by this ratio, rounding down.
		/// </summary>
		internal const float RefundRatio = 0.25f;
		/// <summary>
		/// Milliseconds duration for object shaking when hit with an invalid action.
		/// </summary>
		internal const int ShakeDuration = 300;
		/// <summary>
		/// Common name or prefix for all garden bed objects.
		/// </summary>
		internal const string GenericName = "blueberry.rgb.raisedbed";


		public OutdoorPot() : this(variantKey: null, tileLocation: Vector2.Zero) {}

		public OutdoorPot(string variantKey, Vector2 tileLocation)
		{
			// Code copied from base constructors rather than calling them
			// since fields would inevitably fail to populate in order.

			// Object ()
			this.initNetFields();
			
			// Object (Vector2, int, bool) : Object ()
			this.ParentSheetIndex = OutdoorPot.BaseParentSheetIndex;
			this.TileLocation = tileLocation;
			this.CanBeSetDown = true;
			this.bigCraftable.Value = true;
			this.boundingBox.Value = new Rectangle((int)tileLocation.X * Game1.tileSize, (int)tileLocation.Y * Game1.tileSize, Game1.tileSize, Game1.tileSize);

			Game1.bigCraftablesInformation.TryGetValue(this.ParentSheetIndex, out string objectInformation);
			if (objectInformation != null)
			{
				string[] objectInfoArray = objectInformation.Split('/');
				this.Name = objectInfoArray[0];
				this.Price = int.Parse(objectInfoArray[1]);
				this.Edibility = int.Parse(objectInfoArray[2]);
				string[] typeAndCategory = objectInfoArray[3].Split(' ');
				this.Type = typeAndCategory[0];
				if (typeAndCategory.Length > 1)
				{
					this.Category = Convert.ToInt32(typeAndCategory[1]);
				}
				this.setOutdoors.Value = bool.Parse(objectInfoArray[5]);
				this.setIndoors.Value = bool.Parse(objectInfoArray[6]);
				this.Fragility = int.Parse(objectInfoArray[7]);
				this.isLamp.Value = false;
				this.IsRecipe = false;
			}

			// IndoorPot (Vector2) : Object (Vector2, int, bool)
			this.hoeDirt.Value = new HoeDirt();
			if (Game1.isRaining && Game1.currentLocation.IsOutdoors)
			{
				this.hoeDirt.Value.state.Value = 1;
			}
			this.showNextIndex.Value = this.hoeDirt.Value.state.Value == 1;

			// this (string, Vector2) : IndoorPot (Vector2)
			this.VariantKey = variantKey;
		}

		/// <summary>
		/// Reset all variant-specific values based on the variant key in this object's name.
		/// </summary>
		public void SetForVariant(string variantKey = null)
		{
			if (ModEntry.ItemDefinitions == null || !ModEntry.ItemDefinitions.Any())
			{
				Log.W($"Did not set {this.GetType().Name} ({this.Name}) variant: {nameof(ModEntry.ItemDefinitions)} null or empty.");
				return;
			}
			bool resetBreakage = variantKey == null;
			this._variantKey.Value = variantKey
				?? this._variantKey.Value
				?? ModEntry.ItemDefinitions.Keys.FirstOrDefault(key => key.StartsWith(ModEntry.Instance.ModManifest.Author))
				?? ModEntry.ItemDefinitions.Keys.First();
			this.SoilHeightAboveGround.Value = ModEntry.ItemDefinitions[this.VariantKey].SoilHeightAboveGround;
			this.BreakageStart.Value = ModEntry.ItemDefinitions[this.VariantKey].DaysToBreak;
			this.DisplayName = this.loadDisplayName();
			if (resetBreakage)
			{
				this.BreakageTimer.Value = this.BreakageStart.Value;
			}
			this.SpriteKey = ModEntry.ItemDefinitions[this.VariantKey].SpriteKey;
			this.SpriteIndex = ModEntry.ItemDefinitions[this.VariantKey].SpriteIndex;
		}

		protected override void initNetFields()
		{
			base.initNetFields();
			this.NetFields.AddFields(this._variantKey, this.SoilHeightAboveGround, this.BreakageStart, this.BreakageTimer, this.Neighbours);
		}

		public static string GetVariantKeyFromParentSheetIndex(int index)
		{
			return ModEntry.ItemDefinitions.Keys.ToList().ElementAt(index - OutdoorPot.BaseParentSheetIndex);
		}

		public static string GetVariantKeyFromVariantIndex(int variantIndex)
		{
			return ModEntry.ItemDefinitions.Keys.ToList().ElementAt(variantIndex);
		}

		public static int GetVariantIndexFromName(string name)
		{
			return OutdoorPot.GetVariantIndexFromVariantKey(name.Split('.').Last());
		}

		public static string GetVariantKeyFromName(string name)
		{
			string[] splitName = name.Split(new char[] { '.' }, 4);
			return splitName.Length > 3 ? splitName.Last() : null;
		}

		public static int GetParentSheetIndexFromName(string name)
		{
			return OutdoorPot.GetVariantIndexFromName(name) + OutdoorPot.BaseParentSheetIndex;
		}

		public static string GetDisplayNameFromName(string name)
		{
			return OutdoorPot.GetDisplayNameFromVariantKey(OutdoorPot.GetVariantKeyFromName(name));
		}

		public static int GetVariantIndexFromVariantKey(string variantKey)
		{
			return ModEntry.ItemDefinitions.Keys.ToList().IndexOf(variantKey);
		}

		public static string GetDisplayNameFromVariantKey(string variantKey)
		{
			return Translations.GetNameTranslation(data: ModEntry.ItemDefinitions[variantKey]);
		}

		public static string GetNameFromVariantKey(string variantKey)
		{
			return $"{OutdoorPot.GenericName}.{variantKey}";
		}

		public static string GetDisplayNameFromRecipeName(string recipeName)
		{
			return OutdoorPot.GetDisplayNameFromName(recipeName.Split('.').Last());
		}

		protected override string loadDisplayName()
		{
			return OutdoorPot.GetDisplayNameFromVariantKey(this.VariantKey);
		}

		public override string getDescription()
		{
			string description = Translations.GetTranslation($"item.description{(ModEntry.Config.CanBePlacedInBuildings ? ".indoors" : "")}");
			return Game1.parseText(text: description, whichFont: Game1.smallFont, width: this.getDescriptionWidth());
		}

		public override bool placementAction(GameLocation location, int x, int y, Farmer who = null)
		{
			Vector2 tileLocation = new Vector2(x, y) / Game1.tileSize;
			location.Objects[tileLocation] = new OutdoorPot(variantKey: this.VariantKey, tileLocation: tileLocation);
			OutdoorPot.AdjustWithNeighbours(location: location, tileLocation: tileLocation);
			if (Game1.player.ActiveObject == this)
			{
				Game1.player.reduceActiveItemByOne();
				Game1.playSound("Ship");
			}
			return false;
		}

		public override void performRemoveAction(Vector2 tileLocation, GameLocation environment)
		{
			if (this.TossHeldItem())
			{
				base.performRemoveAction(tileLocation, environment);
				OutdoorPot.AdjustAllOnNextTick(specificLocation: environment);
			}
		}

		public override bool performToolAction(Tool t, GameLocation location)
		{
			bool isHeavy = t.isHeavyHitter();

			if (this.IsBroken)
			{
				if (t is StardewValley.Tools.MeleeWeapon)
				{
					base.performToolAction(t, location);
					return false;
				}

				// Broken objects will not return to the inventory when hit, but will be destroyed
				// and provide a small refund of the primary resource used in crafting.

				location.playSound("axchop");

				// Remove object without adjusting neighbours, as neighbours have already adjusted to ignore the broken object
				if (this.TossHeldItem(pop: true))
				{
					// visual debris
					Game1.createRadialDebris(
						location: location, debrisType: 12,
						xTile: (int)this.TileLocation.X, yTile: (int)this.TileLocation.Y,
						numberOfChunks: Game1.random.Next(4, 10), resource: false);
					Multiplayer multiplayer = ModEntry.Instance.Helper.Reflection.GetField<Multiplayer>(typeof(Game1), "multiplayer").GetValue();
					multiplayer.broadcastSprites(
						location: location,
						sprites: new TemporaryAnimatedSprite(
							rowInAnimationTexture: 12,
							position: new Vector2(this.TileLocation.X * Game1.tileSize, this.TileLocation.Y * Game1.tileSize),
							color: Color.White,
							animationLength: 8,
							flipped: Game1.random.NextDouble() < 0.5,
							animationInterval: 50));

					// refund debris
					string recipeRaw = StardewValley.CraftingRecipe.craftingRecipes
						[OutdoorPot.GetNameFromVariantKey(variantKey: this.VariantKey)];
					string[] recipeSplit = recipeRaw.Split('/')[0].Split(' ');
					List<int> recipe = recipeSplit.ToList().ConvertAll(int.Parse);
					int refundItem = recipe[0];
					int refundQuantity = (int)(recipe[1] * OutdoorPot.RefundRatio);
					if (refundQuantity > 0)
					{
						Game1.createRadialDebris(
							location: location,
							debrisType: refundItem,
							xTile: (int)this.TileLocation.X - 1,
							yTile: (int)this.TileLocation.Y - 1,
							numberOfChunks: refundQuantity,
							resource: false,
							groundLevel: -1,
							item: true);
					}

					// destroy object
					location.Objects.Remove(this.TileLocation);
				}
			}
			else
			{
				// Attempt to pop object or its held objects if any
				bool isValidAction = base.performToolAction(t, location);
				if (isValidAction)
				{
					if (this.TossHeldItem()
						&& Game1.createItemDebris(this, Game1.player.getStandingPosition(), Game1.player.FacingDirection) is Debris debris && debris != null
						&& location.Objects.Remove(this.TileLocation))
					{
						OutdoorPot.AdjustWithNeighbours(location: location, tileLocation: this.TileLocation);
					}
				}
			}

			// Don't perform any usual fall-through behaviours for vanilla objects, we do all we need here
			return false;
		}

		public override bool performObjectDropInAction(Item dropInItem, bool probe, Farmer who)
		{
			if (dropInItem == null)
			{
				return false;
			}

			// Block seed, crop, fertiliser, and object actions when breakage is enabled and object is broken
			if (this.IsBroken)
			{
				// Accept objects of the same variant to repair if broken
				if (this.canStackWith(dropInItem))
				{
					if (!probe)
					{
						this.Unbreak(adjust: true);
						who.currentLocation.playSound("Ship");
					}
					return true;
				}
			}
			else if (this.IsOpenForPlacement(ignoreObjects: true) && OutdoorPot.IsItemPlaceableNoCrops(dropInItem))
			{
				// Accept objects if not holding any seeds or crops
				if (!probe)
				{
					if (this.heldObject.Value != null && this.heldObject.Value.ParentSheetIndex == dropInItem.ParentSheetIndex)
					{
						return false;
					}
					else if (this.TossHeldItem())
					{
						this.HoldItem(dropInItem);
					}
					else
					{
						return false;
					}
				}
				return true;
			}
			return this.IsOpenForPlacement() && base.performObjectDropInAction(dropInItem, probe, who);
		}

		public override bool canBePlacedHere(GameLocation l, Vector2 tile)
		{
			bool okGreenHouse = ModEntry.Config.CanBePlacedInGreenHouse && l.IsGreenhouse;
			bool okFarmHouse = ModEntry.Config.CanBePlacedInFarmHouse && (l is FarmHouse || l is IslandFarmHouse);
			bool okFarm = (l.IsOutdoors && l.IsFarm) || (!l.IsOutdoors && ModEntry.Config.CanBePlacedInBuildings && l.isStructure.Value);
			
			bool noTiles = l.isTileLocationTotallyClearAndPlaceableIgnoreFloors(tile);
			bool noObjects = !l.Objects.ContainsKey(tile);
			bool noCrops = (!l.terrainFeatures.ContainsKey(tile) || l.terrainFeatures[tile] is Flooring || (l.terrainFeatures[tile] is HoeDirt hoeDirt && hoeDirt.crop == null));
			bool noFoliage = l.getLargeTerrainFeatureAt((int)tile.X, (int)tile.Y) == null;
			bool noStumpsAndBoulders = l.resourceClumps.All(r => !r.occupiesTile((int)tile.X, (int)tile.Y));

			bool okLocation = okGreenHouse || okFarmHouse || okFarm;
			bool noObstructions = noTiles && noObjects && noCrops && noFoliage && noStumpsAndBoulders;

			return !l.isTemp() && okLocation && noObstructions;
		}

		public override bool canStackWith(ISalable other)
		{
			// Objects must be of the same variant (wood and wood, stone and stone, ..) in order to stack
			return other is OutdoorPot o && o != null && o.VariantKey == this.VariantKey;
		}

		public override void ApplySprinklerAnimation(GameLocation location)
		{
			if (this.GetSprinklerRadius() is int radius && radius < 1)
			{
				return;
			}

			Vector2 position = (this.TileLocation * Game1.tileSize) - new Vector2(0, this.SoilHeightAboveGround.Value * Game1.pixelZoom);
			int delay = Game1.random.Next(1000);
			float id = (tileLocation.X * 4000) + tileLocation.Y;
			Color colour = Color.White * 0.4f;
			const int frames = 4;
			const int interval = 60;
			const int loops = 100;
			const int index = 29;

			switch (radius)
			{
				case 0:
				{
					Vector2[] offsets = new [] { new Vector2(0, -48), new Vector2(48, 0), new Vector2(0, 48), new Vector2(-48, 0) };
					float[] rotations = new [] { 0, (float)(Math.PI / 2), (float)Math.PI, (float)(Math.PI + (Math.PI / 2)) };
					for (int i = 0; i < 4; ++i)
					{
						location.temporarySprites.Add(
							new TemporaryAnimatedSprite(
								rowInAnimationTexture: index,
								position + offsets[i],
								colour,
								animationLength: frames,
								flipped: false,
								animationInterval: interval,
								numberOfLoops: loops)
							{
								rotation = rotations[i],
								delayBeforeAnimationStart = delay,
								id = id
							});
					}
					break;
				}
				case 1:
					location.temporarySprites.Add(
						new TemporaryAnimatedSprite(
							"TileSheets\\animations", new Rectangle(0, 1984, 192, 192),
							animationInterval: interval,
							animationLength: 3,
							numberOfLoops: loops,
							position: position + new Vector2(-Game1.tileSize),
							flicker: false,
							flipped: false)
						{
							color = colour,
							delayBeforeAnimationStart = delay,
							id = id
						});
					break;
				default:
				{
					float scale = radius / 2f;
					location.temporarySprites.Add(
						new TemporaryAnimatedSprite(
							"TileSheets\\animations",
							new Rectangle(0, 2176, 320, 320),
							animationInterval: interval,
							animationLength: frames,
							numberOfLoops: loops,
							position + new Vector2(Game1.tileSize / 2) + (new Vector2(-160f) * scale),
							flicker: false,
							flipped: false)
						{
							color = colour,
							delayBeforeAnimationStart = delay,
							id = id,
							scale = scale
						});
					break;
				}
			}
		}

		public override void DayUpdate(GameLocation location)
		{
			if (ModEntry.Config.RaisedBedsMayBreakWithAge)
			{
				if (this.IsBroken)
				{
					// If this object is broken, its crop shouldn't be allowed to grow
					this.hoeDirt.Value.crop?.ResetPhaseDays();
				}
				--this.BreakageTimer.Value;
			}
			else if (this.IsBroken)
			{
				// Ignore breakage timer when disabled
				this.Unbreak(location: location, adjust: true);
			}
			if (!this.IsBroken && this.heldObject.Value != null)
			{
				bool isSprinkler = this.heldObject.Value.IsSprinkler();
				int sprinklerRadius = isSprinkler ? this.heldObject.Value.GetModifiedRadiusForSprinkler() : -1;
				if (ModEntry.Config.SprinklersEnabled
					&& isSprinkler && sprinklerRadius >= 0
					&& (!Game1.IsRainingHere(location) || !location.IsOutdoors))
				{
					location.postFarmEventOvernightActions.Add(delegate
					{
						if (!Game1.player.team.SpecialOrderRuleActive("NO_SPRINKLER"))
						{
							foreach (Vector2 current in this.heldObject.Value.GetSprinklerTiles())
							{
								this.heldObject.Value.ApplySprinkler(location, current);
							}
							this.ApplySprinklerAnimation(location); // We don't call heldObject.DayUpdate() so as to use custom animation logic
						}
					});
				}
			}
			base.DayUpdate(location);
		}

		public override void updateWhenCurrentLocation(GameTime time, GameLocation environment)
		{
			base.updateWhenCurrentLocation(time, environment);
			this.heldObject.Value?.updateWhenCurrentLocation(time, environment);
		}

		public override bool minutesElapsed(int minutes, GameLocation environment)
		{
			return false;
		}

		public override void addWorkingAnimation(GameLocation environment) {}

		public override void onReadyForHarvest(GameLocation environment) {}

		public override void drawWhenHeld(SpriteBatch spriteBatch, Vector2 objectPosition, Farmer f)
		{
			spriteBatch.Draw(
				texture: ModEntry.Sprites[this.SpriteKey],
				position: objectPosition,
				sourceRectangle: this.GetSourceRectangle(),
				color: Color.White,
				rotation: 0f,
				origin: Vector2.Zero,
				scale: Game1.pixelZoom,
				effects: SpriteEffects.None,
				layerDepth: Math.Max(0f, (f.getStandingY() + 3) / 10000f));
		}

		public override void drawInMenu(SpriteBatch spriteBatch, Vector2 location, float scaleSize, float transparency, float layerDepth, StackDrawType drawStackNumber, Color color, bool drawShadow)
		{
			if (this.IsRecipe)
			{
				transparency = 0.5f;
				scaleSize *= 0.75f;
			}

			spriteBatch.Draw(
				texture: ModEntry.Sprites[this.SpriteKey],
				position: location + new Vector2(Game1.tileSize / 2, Game1.tileSize / 2),
				sourceRectangle: this.GetSourceRectangle(),
				color: color * transparency,
				rotation: 0f,
				origin: new Vector2(Game1.smallestTileSize / 2, Game1.smallestTileSize),
				scale: Game1.pixelZoom * ((scaleSize < 0.2) ? scaleSize : (scaleSize / 2)),
				effects: SpriteEffects.None,
				layerDepth: layerDepth);

			const float tinyScale = 3f;
			bool shouldDrawStackNumber = drawStackNumber == StackDrawType.Draw && this.Stack > 1 && this.Stack <= 999 && scaleSize > 0.3;
			if (shouldDrawStackNumber)
			{
				Utility.drawTinyDigits(
					toDraw: stack,
					b: spriteBatch,
					position: location + new Vector2(
						Game1.tileSize - Utility.getWidthOfTinyDigitString(this.Stack, tinyScale * scaleSize) + (tinyScale * scaleSize),
						Game1.tileSize - (18f * scaleSize) + 2f),
					scale: tinyScale * scaleSize,
					layerDepth: 1f,
					c: color);
			}
		}

		public override void draw(SpriteBatch spriteBatch, int x, int y, float alpha = 1)
		{
			Vector2 position = Game1.GlobalToLocal(Game1.viewport, new Vector2(x * Game1.tileSize, (y * Game1.tileSize) - Game1.tileSize));
			Rectangle destination = new Rectangle(
				(int)position.X + ((this.shakeTimer > 0) ? Game1.random.Next(-1, 2) : 0),
				(int)position.Y + ((this.shakeTimer > 0) ? Game1.random.Next(-1, 2) : 0),
				Game1.tileSize,
				Game1.tileSize * 2);
			Color colour = Color.White * alpha;

			Rectangle[] source = new Rectangle[4];
			const int w = Game1.smallestTileSize / 2;
			const int h = Game1.smallestTileSize;
			int yOffset = this.SpriteIndex * Game1.smallestTileSize * 2;

			// Layer depth used in base game calculations for illusion of depth when rendering world objects
			float layerDepth = Math.Max(0f, (((y + 1f) * Game1.tileSize) - (this.SoilHeightAboveGround.Value * Game1.pixelZoom)) / 10000f) + (1 / 10000f);
			float layerDepth2 = Math.Max(0f, ((y * Game1.tileSize) - (this.SoilHeightAboveGround.Value * Game1.pixelZoom)) / 10000f) + (1 / 10000f);

			// Broken OutdoorPot
			if (this.IsBroken)
			{
				spriteBatch.Draw(
					texture: ModEntry.Sprites[this.SpriteKey],
					destinationRectangle: destination,
					sourceRectangle: this.GetSourceRectangle(),
					color: colour,
					rotation: 0f,
					origin: Vector2.Zero,
					effects: SpriteEffects.None,
					layerDepth: layerDepth);
			}
			// Held OutdoorPot placement preview
			else if (alpha < 0.6f)
			{
				spriteBatch.Draw(
					texture: ModEntry.Sprites[this.SpriteKey],
					position: position + (new Vector2(Game1.smallestTileSize / 2, Game1.smallestTileSize) * Game1.pixelZoom),
					sourceRectangle: this.GetSourceRectangle(),
					color: colour,
					rotation: 0f,
					origin: new Vector2(Game1.smallestTileSize / 2, Game1.smallestTileSize),
					scale: Game1.pixelZoom,
					effects: SpriteEffects.None,
					layerDepth: layerDepth);

			}
			// Regular OutdoorPot
			else
			{
				// Soil
				spriteBatch.Draw(
					texture: ModEntry.Sprites[this.SpriteKey],
					destinationRectangle: new Rectangle(destination.X, destination.Y + ((Game1.smallestTileSize - this.SoilHeightAboveGround.Value) * Game1.pixelZoom), Game1.tileSize, Game1.tileSize),
					sourceRectangle: new Rectangle(Game1.smallestTileSize * OutdoorPot.SoilIndexInSheet, yOffset + (this.showNextIndex.Value ? Game1.smallestTileSize : 0), Game1.smallestTileSize, Game1.smallestTileSize),
					color: colour,
					rotation: 0f,
					origin: Vector2.Zero,
					effects: SpriteEffects.None,
					layerDepth: ((((Axis)this.Neighbours[0]).HasFlag(Axis.Vertical)) ? layerDepth2 : layerDepth) - (1 / 10000f));

				// Neutral OutdoorPot
				if (this.TileLocation == Vector2.Zero)
				{
					spriteBatch.Draw(
						texture: ModEntry.Sprites[this.SpriteKey],
						destinationRectangle: destination,
						sourceRectangle: new Rectangle(0, yOffset, Game1.smallestTileSize, Game1.smallestTileSize * 2),
						color: colour,
						rotation: 0f,
						origin: Vector2.Zero,
						effects: SpriteEffects.None,
						layerDepth: layerDepth);
					return;
				}

				// Source rectangles for corners are based on their neighbouring OutdoorPots
				source[0] = new Rectangle((this.Neighbours[0] * w * 2), yOffset, w, h);
				source[1] = new Rectangle(w + (this.Neighbours[1] * w * 2), yOffset, w, h);
				source[2] = new Rectangle((this.Neighbours[2] * w * 2), yOffset + h, w, h);
				source[3] = new Rectangle(w + ((this.Neighbours[2] == (int)Axis.Horizontal && this.Neighbours[3] == (int)Axis.None ? OutdoorPot.EndpieceIndexInSheet : this.Neighbours[3]) * w * 2), yOffset + h, w, h);

				// Corners are drawn individually to allow for all placement permutations
				for (int i = 0; i < 4; ++i)
				{
					Rectangle cornerDestination = new Rectangle(
							destination.X + (i % 2 == 1 ? destination.Width / 2 : 0),
							destination.Y + (i > 1 ? destination.Height / 2 : 0),
							destination.Width / 2,
							destination.Height / 2);

					spriteBatch.Draw(
						texture: ModEntry.Sprites[this.SpriteKey],
						destinationRectangle: cornerDestination,
						sourceRectangle: source[i],
						color: colour,
						rotation: 0f,
						origin: Vector2.Zero,
						effects: SpriteEffects.None,
						layerDepth: layerDepth + ((i + 1) / 10000f));
				}
			}

			// Fertiliser
			if (this.hoeDirt.Value.fertilizer.Value != 0)
			{
				Rectangle fertilizer_rect = this.hoeDirt.Value.GetFertilizerSourceRect(this.hoeDirt.Value.fertilizer.Value);
				fertilizer_rect.Width = 13;
				fertilizer_rect.Height = 13;

				spriteBatch.Draw(
					texture: Game1.mouseCursors,
					position: Game1.GlobalToLocal(Game1.viewport, new Vector2((this.TileLocation.X * Game1.tileSize) + (1 * Game1.pixelZoom), (this.TileLocation.Y * Game1.tileSize) - (this.SoilHeightAboveGround.Value * 2) - (2 * Game1.pixelZoom))),
					sourceRectangle: fertilizer_rect,
					color: Color.White,
					rotation: 0f,
					origin: Vector2.Zero,
					scale: Game1.pixelZoom,
					effects: SpriteEffects.None,
					layerDepth: ((this.TileLocation.Y + 0.65f) * Game1.tileSize / 10000f) + (1 / 10000f));
			}

			// Seeds and crops
			if (this.hoeDirt.Value.crop != null)
			{
				this.hoeDirt.Value.crop.drawWithOffset(
					spriteBatch,
					tileLocation: this.TileLocation,
					toTint: (this.hoeDirt.Value.state.Value == 1 && this.hoeDirt.Value.crop.currentPhase.Value == 0 && !this.hoeDirt.Value.crop.raisedSeeds.Value)
						? (new Color(180, 100, 200) * 1f)
						: Color.White,
					rotation: this.hoeDirt.Value.getShakeRotation(),
					offset: new Vector2(Game1.tileSize / 2, 0f));
			}

			// Objects (eg. Sprinkler)
			if (this.heldObject.Value != null)
			{
				int objectOffset = (4 * Game1.pixelZoom) + (this.heldObject.Value.IsSprinkler() ? 0 : Game1.tileSize);
				this.heldObject.Value.draw(
					spriteBatch,
					xNonTile: x * Game1.tileSize,
					yNonTile: (y * Game1.tileSize) - objectOffset - (this.SoilHeightAboveGround.Value * Game1.pixelZoom),
					layerDepth: ((this.TileLocation.Y + 0.66f) * Game1.tileSize / 10000f) + (1 / 10000f),
					alpha: 1f);
			}

			// Plantable bushes (eg. Tea)
			if (this.bush.Value != null)
			{
				this.bush.Value.draw(
					spriteBatch,
					tileLocation: new Vector2(x, y),
					yDrawOffset: -(this.SoilHeightAboveGround.Value * Game1.pixelZoom));
			}
		}

		public override Item getOne()
		{
			return new OutdoorPot(variantKey: this.VariantKey, tileLocation: this.TileLocation);
		}

		public Rectangle GetSourceRectangle()
		{
			return new Rectangle(Game1.smallestTileSize * OutdoorPot.PreviewIndexInSheet, this.SpriteIndex * Game1.smallestTileSize * 2, Game1.smallestTileSize, Game1.smallestTileSize * 2);
		}

		public bool CanPlantHere(Item item)
		{
			return this.hoeDirt.Value.canPlantThisSeedHere(item.ParentSheetIndex, (int)this.TileLocation.X, (int)this.TileLocation.Y);
		}

		public bool IsOpenForPlacement(bool ignoreCrops = false, bool ignoreObjects = false)
		{
			ignoreObjects |= this.heldObject.Value == null;
			ignoreCrops |= (this.hoeDirt.Value.crop == null && this.bush.Value == null);
			return !this.IsBroken && ignoreObjects && ignoreCrops;
		}

		public bool IsHoldingSprinkler()
		{
			return this.heldObject.Value != null && this.heldObject.Value.IsSprinkler();
		}

		public int GetSprinklerRadius()
		{
			return this.IsHoldingSprinkler() ? this.heldObject.Value.GetModifiedRadiusForSprinkler() : -1;
		}

		public void HoldItem(Item item)
		{
			this.heldObject.Value = item.getOne() as StardewValley.Object;
			this.heldObject.Value.TileLocation = this.TileLocation;
		}

		public bool TossHeldItem(bool pop = false)
		{
			bool tossed = false;

			// Toss crops
			if (this.hoeDirt.Value.crop != null)
			{
				if (pop && this.hoeDirt.Value.crop.harvest(xTile: (int)this.TileLocation.X, yTile: (int)this.TileLocation.Y, soil: this.hoeDirt.Value))
				{
					tossed = true;
				}
			}
			
			// Toss held objects
			if (this.heldObject.Value != null)
			{
				if (pop && Game1.createItemDebris(item: heldObject.Value, origin: this.TileLocation * Game1.tileSize, direction: -1) != null)
				{
					this.heldObject.Value.TileLocation = Vector2.Zero;
					this.heldObject.Value = null;
					tossed = true;
				}
				else
				{
					this.heldObject.Value.shakeTimer = OutdoorPot.ShakeDuration;
				}
			}

			return tossed || (this.hoeDirt.Value.crop == null && this.heldObject.Value == null);
		}

		public void Water()
		{
			this.hoeDirt.Value.state.Value = 1;
			this.showNextIndex.Value = this.hoeDirt.Value.state.Value == 1;
		}

		public void Unbreak(GameLocation location = null, bool adjust = false)
		{
			this.BreakageTimer.Value = this.BreakageStart.Value;
			if (adjust)
			{
				OutdoorPot.AdjustWithNeighbours(location: location, tileLocation: this.TileLocation);
			}
		}

		/// <summary>
		/// Check all objects to qualify for breakage, and mark for breakage if qualified.
		/// </summary>
		public static void BreakAll(GameLocation specificLocation = null)
		{
			foreach (GameLocation location in specificLocation != null ? new[] { specificLocation } : OutdoorPot.GetValidLocations())
			{
				List<OutdoorPot> pots = location.Objects.Values.OfType<OutdoorPot>().Where(o => o.IsReadyToBreak).ToList();
				pots.ForEach(pot => pot.Break(location: location, adjust: false));
				OutdoorPot.AdjustAll(specificLocation: location);
			}
		}

		/// <summary>
		/// Set values to mark the object as broken, leaving it unable to continue to grow crops.
		/// </summary>
		/// <param name="adjust">Whether to call <see cref="OutdoorPot.AdjustAll(GameLocation)"/> after breaking.</param>
		public void Break(GameLocation location, bool adjust)
		{
			this.BreakageTimer.Value = OutdoorPot.BreakageDefinite;
			if (adjust)
			{
				OutdoorPot.AdjustWithNeighbours(location: location, tileLocation: this.TileLocation);
			}
		}

		/// <summary>
		/// Calls <see cref="OutdoorPot.AdjustAll(GameLocation)"/> on the next tick.
		/// Useful when adjusting sprites after some base game checks, or when removing multiple objects simultaneously.
		/// </summary>
		public static void AdjustAllOnNextTick(GameLocation specificLocation = null)
		{
			ModEntry.Instance.Helper.Events.GameLoop.UpdateTicked += OutdoorPot.Event_AdjustAllOnNextTick;
			OutdoorPot.LocationToIdentifyOutdoorPots = specificLocation;
		}

		private static void Event_AdjustAllOnNextTick(object sender, StardewModdingAPI.Events.UpdateTickedEventArgs e)
		{
			ModEntry.Instance.Helper.Events.GameLoop.UpdateTicked -= OutdoorPot.Event_AdjustAllOnNextTick;
			OutdoorPot.AdjustAll(specificLocation: OutdoorPot.LocationToIdentifyOutdoorPots);
			OutdoorPot.LocationToIdentifyOutdoorPots = null;
		}

		public static void ResetAll(GameLocation specificLocation = null)
		{
			foreach (GameLocation location in specificLocation != null ? new [] { specificLocation } : OutdoorPot.GetValidLocations())
				location.Objects.Values.OfType<OutdoorPot>().ToList().ForEach(o => o.SetForVariant());
		}

		/// <summary>
		/// Adjusts sprites of all objects by reconfirming the positions of other nearby objects.
		/// Required to form complete shapes using the four-corners method of building raised bed areas from objects.
		/// </summary>
		public static void AdjustAll(GameLocation specificLocation = null)
		{
			foreach (GameLocation location in specificLocation != null ? new[] { specificLocation } : OutdoorPot.GetValidLocations())
				location.Objects.Values.OfType<OutdoorPot>().ToList().ForEach(o => o.Adjust(location: location));
		}

		public void Adjust(GameLocation location)
		{
			for (int i = 0; i < 4; ++i)
			{
				Axis n = Axis.None;
				if (new Vector2(this.TileLocation.X, this.TileLocation.Y + (i > 1 ? 1 : -1)) is Vector2 v1
					&& location.Objects.ContainsKey(v1) && location.Objects[v1] is StardewValley.Object o1
					&& OutdoorPot.CheckNeighbour(p: this, o: o1))
					n |= Axis.Vertical;
				if (new Vector2(this.TileLocation.X + (i % 2 == 1 ? 1 : -1), this.TileLocation.Y) is Vector2 v2
					&& location.Objects.ContainsKey(v2) && location.Objects[v2] is StardewValley.Object o2
					&& OutdoorPot.CheckNeighbour(p: this, o: o2))
					n |= Axis.Horizontal;
				if ((n & (Axis.Vertical | Axis.Horizontal)) != Axis.None
					&& new Vector2(this.TileLocation.X + (i % 2 == 1 ? 1 : -1), this.TileLocation.Y + (i > 1 ? 1 : -1)) is Vector2 v3
					&& location.Objects.ContainsKey(v3) && location.Objects[v3] is StardewValley.Object o3
					&& OutdoorPot.CheckNeighbour(p: this, o: o3))
					n |= Axis.Diagonal;
				if (n == (Axis.Diagonal | Axis.Horizontal))
					n = Axis.Horizontal;
				this.Neighbours[i] = (int)n;
			}
		}

		public static void AdjustWithNeighbours(GameLocation location, Vector2 tileLocation, int radius = 1)
		{
			if (location == null)
				location = Game1.currentLocation;
			if (radius < 1)
				return;

			Point origin = Utility.Vector2ToPoint(tileLocation);
			Point start = new Point(
				Math.Max(0, origin.X - radius),
				Math.Max(0, origin.Y - radius));
			Point end = new Point(
				Math.Min(location.Map.GetLayer("Back").DisplayWidth / Game1.tileSize, origin.X + radius),
				Math.Min(location.Map.GetLayer("Back").DisplayHeight / Game1.tileSize, origin.Y + radius));

			for (int x = start.X; x <= end.X; ++x)
			{
				for (int y = start.Y; y <= end.Y; ++y)
				{
					Vector2 tile = new Vector2(x, y);
					if (location.Objects.ContainsKey(tile) && location.Objects[tile] != null && location.Objects[tile] is OutdoorPot op)
					{
						op.Adjust(location: location);
					}
				}
			}
		}

		private static bool CheckNeighbour(OutdoorPot p, StardewValley.Object o)
		{
			bool facts = o is OutdoorPot op && op != null && op.canStackWith(p) && !op.IsBroken;
			return facts;
		}

		public static bool IsItemPlantable(Item item)
		{
			return item != null && !(item is Tool)
				&& !StardewValley.Object.isWildTreeSeed(item.ParentSheetIndex)
				&& (item.Category == -19 || item.Category == -74 || (item is StardewValley.Object o && o.isSapling()));
		}

		public static bool IsItemPlaceableNoCrops(Item item)
		{
			return !IsItemPlantable(item) && item is StardewValley.Object o && ModEntry.Config.SprinklersEnabled && o.IsSprinkler();
		}

		public static IEnumerable<GameLocation> GetValidLocations()
		{
			var locations = new List<GameLocation> { Game1.getFarm() };
			if (ModEntry.Config.CanBePlacedInFarmHouse)
				locations.AddRange(new[] { Game1.getLocationFromName("FarmHouse"), Game1.getLocationFromName("IslandFarmHouse") });
			if (ModEntry.Config.CanBePlacedInGreenHouse)
				locations.Add(Game1.getLocationFromName("Greenhouse"));
			if (ModEntry.Config.CanBePlacedInBuildings)
				locations.AddRange(Game1.getFarm().buildings.Where(b => b.indoors.Value != null).Select(b => b.indoors.Value));
			return locations;
		}
	}
}
