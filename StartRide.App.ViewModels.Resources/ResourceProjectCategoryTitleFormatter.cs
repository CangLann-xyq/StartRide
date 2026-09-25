using System.Collections.Generic;
using System.Linq;
using StartRide.App.Resources;
using Launcher.Domain.Models;

namespace StartRide.App.ViewModels.Resources;

internal static class ResourceProjectCategoryTitleFormatter
{
	public static IReadOnlyList<string> Format(ResourceProjectKind kind, IEnumerable<ResourceProjectCategory> categories)
	{
		return (from category in categories.Distinct()
			select Resolve(kind, category) into title
			where !string.IsNullOrWhiteSpace(title)
			select (title)).ToArray();
	}

	private static string? Resolve(ResourceProjectKind kind, ResourceProjectCategory category)
	{
		switch (kind)
		{
		case ResourceProjectKind.Mod:
			switch (category)
			{
			case ResourceProjectCategory.Optimization:
				return Strings.Resources_ModFilterTypeOptimization;
			case ResourceProjectCategory.Utility:
				return Strings.Resources_ModFilterTypeUtility;
			case ResourceProjectCategory.Adventure:
				return Strings.Resources_ModFilterTypeAdventure;
			case ResourceProjectCategory.Decoration:
				return Strings.Resources_ModFilterTypeDecoration;
			case ResourceProjectCategory.Equipment:
				return Strings.Resources_ModFilterTypeEquipment;
			case ResourceProjectCategory.Technology:
				return Strings.Resources_ModFilterTypeTechnology;
			case ResourceProjectCategory.Magic:
				return Strings.Resources_ModFilterTypeMagic;
			case ResourceProjectCategory.Mobs:
				return Strings.Resources_ModFilterTypeMobs;
			case ResourceProjectCategory.WorldGeneration:
				return Strings.Resources_ModFilterTypeWorldGeneration;
			case ResourceProjectCategory.Storage:
				return Strings.Resources_ModFilterTypeStorage;
			case ResourceProjectCategory.Library:
				return Strings.Resources_ModFilterTypeLibrary;
			}
			break;
		case ResourceProjectKind.ResourcePack:
			switch (category)
			{
			case ResourceProjectCategory.Simplistic:
				return Strings.Resources_ResourcePackFilterTypeSimplistic;
			case ResourceProjectCategory.Themed:
				return Strings.Resources_ResourcePackFilterTypeThemed;
			case ResourceProjectCategory.Realistic:
				return Strings.Resources_ResourcePackFilterTypeRealistic;
			case ResourceProjectCategory.VanillaLike:
				return Strings.Resources_ResourcePackFilterTypeVanillaLike;
			case ResourceProjectCategory.Audio:
				return Strings.Resources_ResourcePackFilterTypeAudio;
			}
			break;
		case ResourceProjectKind.ShaderPack:
			switch (category)
			{
			case ResourceProjectCategory.Cartoon:
				return Strings.Resources_ShaderPackFilterTypeCartoon;
			case ResourceProjectCategory.Cursed:
				return Strings.Resources_ShaderPackFilterTypeCursed;
			case ResourceProjectCategory.Fantasy:
				return Strings.Resources_ShaderPackFilterTypeFantasy;
			case ResourceProjectCategory.Realistic:
				return Strings.Resources_ShaderPackFilterTypeRealistic;
			case ResourceProjectCategory.SemiRealistic:
				return Strings.Resources_ShaderPackFilterTypeSemiRealistic;
			case ResourceProjectCategory.VanillaLike:
				return Strings.Resources_ShaderPackFilterTypeVanillaLike;
			}
			break;
		}
		return null;
	}
}
