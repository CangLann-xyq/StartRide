using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;

namespace StartRide.App.Services;

public sealed class MemoryModuleInfo
{
	public ulong CapacityBytes { get; init; }

	public int SpeedMhz { get; init; }

	public string Manufacturer { get; init; } = "";

	public string PartNumber { get; init; } = "";

	public string DeviceLocator { get; init; } = "";

	public string Generation { get; init; } = "";

	public string CapacityText => CapacityBytes > 0
		? (CapacityBytes % (1024UL * 1024 * 1024) == 0
			? (CapacityBytes / 1024 / 1024 / 1024) + " GB"
			: Math.Round(CapacityBytes / 1024.0 / 1024 / 1024, 1) + " GB")
		: "?";

	public string SpeedText => SpeedMhz > 0 ? SpeedMhz + " MHz" : "?";

	public string SlotText => string.IsNullOrWhiteSpace(DeviceLocator) ? "?" : DeviceLocator;

	public string ModelText
	{
		get
		{
			var parts = new List<string>();
			if (!string.IsNullOrWhiteSpace(Manufacturer)) parts.Add(Manufacturer.Trim());
			if (!string.IsNullOrWhiteSpace(PartNumber)) parts.Add(PartNumber.Trim());
			return parts.Count > 0 ? string.Join(" ", parts) : "?";
		}
	}
}

public sealed class HardwareMemorySnapshot
{
	public List<MemoryModuleInfo> Modules { get; } = new List<MemoryModuleInfo>();

	public int TotalSlots { get; set; }

	public ulong TotalInstalledBytes => Modules.Aggregate(0UL, (sum, m) => sum + m.CapacityBytes);

	public int PopulatedSlots => Modules.Count;

	public string Error { get; set; } = "";
}

public sealed class StartRideHardwareService
{
	public HardwareMemorySnapshot GetMemorySnapshot()
	{
		var snapshot = new HardwareMemorySnapshot();
		try
		{
			using var searcher = new ManagementObjectSearcher(
				"SELECT Capacity, Speed, ConfiguredClockSpeed, Manufacturer, PartNumber, DeviceLocator, BankLabel, SMBIOSMemoryType, MemoryType FROM Win32_PhysicalMemory");
			foreach (ManagementBaseObject item in searcher.Get())
			{
				using (item)
				{
					var module = new MemoryModuleInfo
					{
						CapacityBytes = ToUlong(item["Capacity"]),
						SpeedMhz = ToInt(item["ConfiguredClockSpeed"]) != 0 ? ToInt(item["ConfiguredClockSpeed"]) : ToInt(item["Speed"]),
						Manufacturer = ToText(item["Manufacturer"]),
						PartNumber = ToText(item["PartNumber"]),
						DeviceLocator = ToText(item["DeviceLocator"]),
						Generation = DescribeMemoryType(ToInt(item["SMBIOSMemoryType"]), ToInt(item["MemoryType"])),
					};
					if (module.CapacityBytes > 0 || !string.IsNullOrWhiteSpace(module.DeviceLocator))
					{
						snapshot.Modules.Add(module);
					}
				}
			}
		}
		catch (Exception ex)
		{
			snapshot.Error = ex.Message;
		}

		try
		{
			using var searcher = new ManagementObjectSearcher(
				"SELECT MemoryDevices FROM Win32_PhysicalMemoryArray");
			foreach (ManagementBaseObject item in searcher.Get())
			{
				using (item)
				{
					int slots = ToInt(item["MemoryDevices"]);
					if (slots > snapshot.TotalSlots) snapshot.TotalSlots = slots;
				}
			}
		}
		catch { }

		if (snapshot.TotalSlots < snapshot.Modules.Count) snapshot.TotalSlots = snapshot.Modules.Count;
		return snapshot;
	}

	private static ulong ToUlong(object? value)
	{
		try { return value == null ? 0UL : Convert.ToUInt64(value); }
		catch { return 0UL; }
	}

	private static int ToInt(object? value)
	{
		try { return value == null ? 0 : Convert.ToInt32(value); }
		catch { return 0; }
	}

	private static string ToText(object? value) => value?.ToString()?.Trim() ?? "";

	private static string DescribeMemoryType(int smbiosType, int wmiMemoryType)
	{
		string text = smbiosType switch
		{
			20 => "DDR",
			21 => "DDR2",
			24 => "DDR3",
			26 => "DDR4",
			27 => "LPDDR",
			28 => "LPDDR2",
			29 => "LPDDR3",
			30 => "LPDDR4",
			34 => "DDR5",
			35 => "LPDDR5",
			_ => "",
		};
		if (text.Length > 0) return text;

		return wmiMemoryType switch
		{
			20 => "DDR",
			21 => "DDR2",
			24 => "DDR3",
			26 => "DDR4",
			0 => "",
			_ => "",
		};
	}
}
