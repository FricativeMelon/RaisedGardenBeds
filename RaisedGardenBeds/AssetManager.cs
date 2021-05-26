﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using StardewModdingAPI;
using StardewValley;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RaisedGardenBeds
{
	public class AssetManager : IAssetLoader, IAssetEditor
	{
		private IModHelper _helper;
		private ITranslationHelper i18n => _helper.Translation;

		internal const string AssetPrefix = "blueberry.rgb.Assets";

		internal static readonly string GameContentItemDefinitionsPath = Path.Combine(AssetPrefix, "ItemDefinitions");
		internal static readonly string GameContentEventDataPath = Path.Combine(AssetPrefix, "EventData");
		internal static readonly string GameContentSpritesPath = Path.Combine(AssetPrefix, "Sprites");

		internal static readonly string LocalItemDefinitionsPath = Path.Combine("assets", "itemDefinitions.json");
		internal static readonly string LocalEventDataPath = Path.Combine("assets", "eventData.json");
		internal static readonly string LocalSpritesPath = Path.Combine("assets", "sprites.png");

		internal static readonly string ContentPackPath = Path.Combine("assets", "ContentPack");


		public AssetManager(IModHelper helper)
		{
			this._helper = helper;
		}

		public bool CanLoad<T>(IAssetInfo asset)
		{
			return asset.AssetNameEquals(GameContentSpritesPath)
				|| asset.AssetNameEquals(GameContentItemDefinitionsPath)
				|| asset.AssetNameEquals(GameContentEventDataPath);
		}

		public T Load<T>(IAssetInfo asset)
		{
			if (asset.AssetNameEquals(GameContentSpritesPath))
				return (T)(object)_helper.Content.Load<Texture2D>(LocalSpritesPath);
			if (asset.AssetNameEquals(GameContentItemDefinitionsPath))
				return (T)(object)_helper.Content.Load<Dictionary<string, Dictionary<string, string>>>(LocalItemDefinitionsPath);
			if (asset.AssetNameEquals(GameContentEventDataPath))
				return (T)(object)_helper.Content.Load<Dictionary<string, string>>(LocalEventDataPath);
			return (T)(object)null;
		}

		public bool CanEdit<T>(IAssetInfo asset)
		{
			bool isReady = OutdoorPot.Sprites != null && ModEntry.JsonAssets != null && !(Game1.activeClickableMenu is StardewValley.Menus.TitleMenu);
			bool isEventAsset = asset.AssetName.StartsWith(Path.Combine("Data", "Events"))
						&& Path.GetFileNameWithoutExtension(asset.AssetName) is string where
						&& ModEntry.Events != null && ModEntry.Events.Any(e => e["Where"] == where);
			return isReady
				&& (asset.AssetNameEquals(GameContentItemDefinitionsPath)
					|| asset.AssetNameEquals(GameContentEventDataPath)
					|| asset.AssetNameEquals(Path.Combine("TileSheets", "Craftables"))
					|| asset.AssetNameEquals(Path.Combine("Data", "BigCraftablesInformation"))
					|| asset.AssetNameEquals(Path.Combine("Data", "CraftingRecipes"))
					|| isEventAsset);
		}

		public void Edit<T>(IAssetData asset)
		{
			// Local data
			if (asset.AssetNameEquals(GameContentItemDefinitionsPath))
			{
				// Remove items until number is within the reserved set
				string itemDataPath = Path.Combine(ContentPackPath, "BigCraftables", "Raised Bed", "big-craftable.json");
				var itemData = _helper.Content.Load<Dictionary<string, object>>(itemDataPath);
				var data = asset.AsDictionary<string, Dictionary<string, string>>().Data;
				while (data.Count > (int)itemData["ReserveExtraIndexCount"] || data.Count > OutdoorPot.Sprites.Height / Game1.smallestTileSize * 2)
				{
					string key = ModEntry.ItemDefinitions.Last().Key;
					if (data.Remove(key))
						Log.W("Removing excess raised bed: " + key);
					else
						break;
				}
				return;
			}
			if (asset.AssetNameEquals(GameContentEventDataPath))
			{
				var events = JsonConvert.DeserializeObject(asset.AsDictionary<string, string>().Data["Events"])
					as List<Dictionary<string, string>>;
				ModEntry.Events = events;

				return;
			}

			// Game data
			int id = ModEntry.JsonAssets == null ? -1 : ModEntry.JsonAssets.GetBigCraftableId(OutdoorPot.GenericName);
			if (id < 0)
				return;
			if (asset.AssetNameEquals(Path.Combine("TileSheets", "Craftables")))
			{
				// Patch basic object sprites to game craftables sheet for all variants
				for (int variant = 0; variant < ModEntry.ItemDefinitions.Count; ++variant)
				{
					Rectangle destination = StardewValley.Object.getSourceRectForBigCraftable(id + variant);
					Rectangle source;
					string variantName = OutdoorPot.GetVariantKeyFromVariantIndex(variant: variant);
					int soilOffsetY = int.Parse(ModEntry.ItemDefinitions[variantName]["SoilHeightAboveGround"]);
					// soil
					source = new Rectangle(Game1.smallestTileSize * OutdoorPot.SoilIndexInSheet, variant * Game1.smallestTileSize * 2, Game1.smallestTileSize, Game1.smallestTileSize);
					asset.AsImage().PatchImage(
						source: OutdoorPot.Sprites,
						sourceArea: source,
						targetArea: new Rectangle(destination.X, destination.Y + Game1.smallestTileSize - soilOffsetY, source.Width, source.Height),
						patchMode: PatchMode.Overlay);
					// object
					source = new Rectangle(0, variant * Game1.smallestTileSize * 2, destination.Width, destination.Height);
					asset.AsImage().PatchImage(
						source: OutdoorPot.Sprites,
						sourceArea: source,
						targetArea: destination,
						patchMode: PatchMode.Overlay);
				}
				return;
			}
			if (asset.AssetNameEquals(Path.Combine("Data", "BigCraftablesInformation")))
			{
				string[] fields;
				var data = asset.AsDictionary<int, string>().Data;

				// Patch dummy entries into bigcraftables file
				for (int i = 0; i < ModEntry.ItemDefinitions.Count; ++i)
				{
					string varietyName = ModEntry.ItemDefinitions.Keys.ToArray()[i];
					fields = data[id].Split('/');
					fields[8] = i18n.Get("item.name." + varietyName);
					data[id + i] = string.Join("/", fields);
				}

				// Patch object display name and description from localisations file
				fields = data[id].Split('/');
				fields[4] = i18n.Get("item.description" + (ModEntry.Config.CanBePlacedInBuildings ? ".indoors" : ""));
				fields[8] = i18n.Get("item.name");
				data[id] = string.Join("/", fields);

				return;
			}
			if (asset.AssetNameEquals(Path.Combine("Data", "CraftingRecipes")))
			{
				// Add crafting recipes for all object variants
				var data = asset.AsDictionary<string, string>().Data;
				string[] fields = data[OutdoorPot.GenericName].Split('/');
				foreach (KeyValuePair<string, Dictionary<string, string>> idAndFields in ModEntry.ItemDefinitions)
				{
					string[] newFields = new string[]
					{
						idAndFields.Value["RecipeItems"],			// Crafting ingredients
						fields[1],									// Unused field
						fields[2],									// Crafted item ID and quantity
						idAndFields.Value["RecipeIsDefault"],		// Recipe always available
						idAndFields.Value["RecipeConditions"] ?? "null",	// Recipe availability conditions
						i18n.Get("item.name." + idAndFields.Key)	// Recipe display name
					};
					data[OutdoorPot.GenericName + "." + idAndFields.Key] = string.Join("/", newFields);
				}
				return;
			}
			if ((asset.AssetName.StartsWith(Path.Combine("Data", "Events"))
				&& Path.GetFileNameWithoutExtension(asset.AssetName) is string where
				&& ModEntry.Events != null
				&& ModEntry.Events.FirstOrDefault(e => e["Where"] == where) is Dictionary<string, string> eventData))
			{
				string key = ModEntry.EventRoot + ModEntry.Events.IndexOf(eventData) + "/" + eventData["Conditions"];
				string script = string.Format(
					format: eventData["Script"],
					eventData["Who"],
					i18n.Get("event.0.dialogue"));
				asset.AsDictionary<string, string>().Data[key] = script;

				return;
			}
		}
	}
}