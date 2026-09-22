using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;

namespace Launcher.App.Services;

/// <summary>一根内存条。</summary>
public sealed class MemoryModuleInfo
{
	public ulong CapacityBytes { get; init; }

	public int SpeedMhz { get; init; }

	public string Manufacturer { get; init; } = "";

	public string PartNumber { get; init; } = "";

	/// <summary>插槽位置（如 DIMM_A1 / ChannelA-DIMM0）</summary>
	public string DeviceLocator { get; init; } = "";

	/// <summary>DDR3 / DDR4 / DDR5 …</summary>
	public string Generation { get; init; } = "";

	public string CapacityText => CapacityBytes > 0
		? (CapacityBytes % (1024UL * 1024 * 1024) == 0
			? (CapacityBytes / 1024 / 1024 / 1024) + " GB"
			: Math.Round(CapacityBytes / 1024.0 / 1024 / 1024, 1) + " GB")
		: "?";

	public string SpeedText => SpeedMhz > 0 ? SpeedMhz + " MHz" : "?";

	/// <summary>「容量 · 代数 频率 · 插槽 · 型号」这类一行摘要，由界面拼装。</summary>
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

	/// <summary>主板上的内存插槽总数（读不到时为 0）</summary>
	public int TotalSlots { get; set; }

	public ulong TotalInstalledBytes => Modules.Aggregate(0UL, (sum, m) => sum + m.CapacityBytes);

	public int PopulatedSlots => Modules.Count;

	public string Error { get; set; } = "";
}

/// <summary>
/// 本机内存条硬件信息（WMI / SMBIOS）。
///
/// 启动器里原本只能拿到「总内存 / 可用内存」两个数，用户看不到自己插了几根条、
/// 什么频率。这里直接读 Win32_PhysicalMemory + Win32_PhysicalMemoryArray，
/// 给出每根条的容量/频率/代数/插槽。
///
/// 读不到（虚拟机、被策略限制、WMI 服务异常）时返回空列表 + Error，不抛异常。
/// </summary>
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

		// 插槽总数读不到时，至少按已用数量显示，避免界面出现「共 0 个插槽」
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

	/// <summary>SMBIOS 内存类型编码 → 可读代数。参考 SMBIOS 规范 Memory Device — Type。</summary>
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

		// Win32_PhysicalMemory.MemoryType（旧字段，部分机型只有它有值）
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
