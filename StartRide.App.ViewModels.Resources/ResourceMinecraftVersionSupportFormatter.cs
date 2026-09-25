using System;
using System.Collections.Generic;
using System.Linq;
using StartRide.App.Resources;

namespace StartRide.App.ViewModels.Resources;

public static class ResourceMinecraftVersionSupportFormatter
{
	private readonly record struct NormalizedMinecraftVersion(string Key, string DisplayText);

	private readonly record struct RankedVersion(NormalizedMinecraftVersion Version, int? Rank, int OriginalIndex);

	public static string Format(IReadOnlyList<string> supportedVersions, IReadOnlyList<string>? releaseVersionOrder = null)
	{
		IReadOnlyList<NormalizedMinecraftVersion> readOnlyList = NormalizeVersions(supportedVersions);
		if (readOnlyList.Count == 0)
		{
			return Strings.Resources_ModVersionsUnknown;
		}
		IReadOnlyList<NormalizedMinecraftVersion> readOnlyList2 = NormalizeVersions(releaseVersionOrder ?? Array.Empty<string>());
		if (readOnlyList2.Count == 0)
		{
			return string.Join(", ", readOnlyList.Select((NormalizedMinecraftVersion version) => version.DisplayText));
		}
		Dictionary<string, int> releaseRanks = readOnlyList2.Select((NormalizedMinecraftVersion version, int index) => (Key: version.Key, Index: index)).GroupBy<(string, int), string>(((string Key, int Index) item) => item.Key, StringComparer.OrdinalIgnoreCase).ToDictionary<IGrouping<string, (string, int)>, string, int>((IGrouping<string, (string Key, int Index)> group) => group.Key, (IGrouping<string, (string Key, int Index)> group) => group.First().Index, StringComparer.OrdinalIgnoreCase);
		List<RankedVersion> versions = (from version in readOnlyList.Select((NormalizedMinecraftVersion version, int index) => new RankedVersion(version, releaseRanks.TryGetValue(version.Key, out var value) ? new int?(value) : ((int?)null), index))
			orderby version.Rank ?? int.MaxValue, version.OriginalIndex
			select version).ToList();
		return string.Join(", ", CreateSegments(versions));
	}

	private static IReadOnlyList<string> CreateSegments(IReadOnlyList<RankedVersion> versions)
	{
		List<string> list = new List<string>();
		List<RankedVersion> list2 = new List<RankedVersion>();
		foreach (RankedVersion version in versions)
		{
			if (list2.Count != 0)
			{
				if (!IsConsecutive(list2[list2.Count - 1], version))
				{
					list.Add(FormatSegment(list2));
					list2.Clear();
					list2.Add(version);
					continue;
				}
			}
			list2.Add(version);
		}
		if (list2.Count > 0)
		{
			list.Add(FormatSegment(list2));
		}
		return list;
	}

	private static bool IsConsecutive(RankedVersion previous, RankedVersion current)
	{
		if (previous.Rank.HasValue && current.Rank.HasValue)
		{
			return current.Rank.Value - previous.Rank.Value == 1;
		}
		return false;
	}

	private static string FormatSegment(IReadOnlyList<RankedVersion> segment)
	{
		if (segment.Count <= 1)
		{
			return segment[0].Version.DisplayText;
		}
		return segment[segment.Count - 1].Version.DisplayText + "+";
	}

	private static IReadOnlyList<NormalizedMinecraftVersion> NormalizeVersions(IEnumerable<string> versions)
	{
		return (from @group in (from version in versions.Select(TryNormalizeMinecraftVersion)
				where version.HasValue
				select version.Value).GroupBy<NormalizedMinecraftVersion, string>((NormalizedMinecraftVersion version) => version.Key, StringComparer.OrdinalIgnoreCase)
			select @group.First()).ToList();
	}

	private static NormalizedMinecraftVersion? TryNormalizeMinecraftVersion(string? version)
	{
		if (string.IsNullOrWhiteSpace(version))
		{
			return null;
		}
		string text = version.Trim();
		if (int.TryParse(text, out var result))
		{
			return new NormalizedMinecraftVersion(text, result.ToString());
		}
		string[] array = text.Split('.');
		if (array.Length < 2 || !TryParseLeadingNumber(array[0], out var number) || !TryParseLeadingNumber(array[1], out var number2))
		{
			return null;
		}
		string obj = $"{number}.{number2}";
		return new NormalizedMinecraftVersion(obj, obj);
	}

	private static bool TryParseLeadingNumber(string value, out int number)
	{
		number = 0;
		int i;
		for (i = 0; i < value.Length && char.IsDigit(value[i]); i++)
		{
		}
		if (i > 0)
		{
			return int.TryParse(value.Substring(0, i), out number);
		}
		return false;
	}
}
