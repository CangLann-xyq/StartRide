using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StartRide.Core;

namespace StartRide.App.ViewModels.Vehicles;

/// <summary>车辆来源。</summary>
public enum VehicleSource
{
	/// <summary>游戏自带（content/vehicles/*.zip）。</summary>
	BuiltIn,

	/// <summary>模组带的（mods/*.zip 里的 vehicles/&lt;id&gt;/）。</summary>
	Mod,
}

/// <summary>一辆车（内置或模组）。</summary>
public sealed class VehicleItem : ObservableObject
{
	private bool isSelected;

	public string Name { get; }
	public string FilePath { get; }
	public string DisplayName { get; }
	public string FileSizeText { get; }

	/// <summary>原始字节数，排序/统计用（显示走 FileSizeText）。</summary>
	public long Bytes { get; }

	/// <summary>所在目录（详情面板显示）。</summary>
	public string DirectoryPath { get; }

	/// <summary>承载它的包文件名（含扩展名）——内置车是车辆包，模组车是那个模组包。</summary>
	public string FileName { get; }

	/// <summary>来源（内置 / 模组）。</summary>
	public VehicleSource Source { get; }

	/// <summary>车辆 ID（BeamNG 的 vehicles/&lt;id&gt; 目录名）。</summary>
	public string VehicleId { get; }

	public bool IsBuiltIn => Source == VehicleSource.BuiltIn;

	public string SourceText => IsBuiltIn ? "内置" : "模组";

	public bool IsSelected
	{
		get => isSelected;
		set => SetProperty(ref isSelected, value);
	}

	public VehicleItem(string name, string filePath, string fileSizeText, VehicleSource source, string vehicleId, long bytes = 0)
	{
		Name = name;
		FilePath = filePath;
		DisplayName = name;
		FileSizeText = fileSizeText;
		Bytes = bytes;
		Source = source;
		VehicleId = vehicleId;
		DirectoryPath = Path.GetDirectoryName(filePath) ?? "";
		FileName = Path.GetFileName(filePath);
	}
}

/// <summary>
/// 车辆管理页：把**能开的车**完整列出来。
///
/// 两处来源都要扫（只扫第一处曾是"好多车没展示出来"的根因）：
///   ① 游戏自带：content/vehicles/*.zip
///   ② 模组带的：mods/*.zip 内部的 vehicles/&lt;id&gt;/
///      —— 实测本机自带 123 辆、116 个模组里 95 个带车辆，全都没进过这个列表。
///
/// 判据（实测得出，避免把共享目录和"配件包"算成车）：
///   vehicles/&lt;id&gt;/ 下**至少有一个 *.pc 文件**才算一辆车。
///   vehicles/common 只有贴图/材质（无 .pc）→ 排除；
///   CHNLicensePlates 这类只给已有车加车牌的包，里面只有 .jbeam 没有 .pc → 排除。
///
/// 扫描放后台线程：要读 200+ 个 zip 的中央目录，别卡在启动路径上。
/// </summary>
public sealed class VehiclesPageViewModel : ObservableObject
{
	private string statusMessage = "";
	private VehicleItem? selectedVehicle;
	private int sortMode;
	private bool isScanning = true;
	private string selectedSource = "全部";
	private RelayCommand? openVehiclesFolderCommand;
	private RelayCommand<VehicleItem>? openVehicleLocationCommand;
	private RelayCommand<VehicleItem>? selectVehicleCommand;
	private RelayCommand<VehicleItem>? copyVehiclePathCommand;
	private RelayCommand? sortByNameCommand;
	private RelayCommand? sortBySizeCommand;
	private RelayCommand? revealSelectedCommand;

	private FileSetSummary summary = new();
	private List<VehicleItem> allVehicles = new();

	public ObservableCollection<VehicleItem> Vehicles { get; } = new();

	public ObservableCollection<string> SourceOptions { get; } = new();

	public bool HasVehicles => Vehicles.Count > 0;

	public bool IsScanning
	{
		get => isScanning;
		private set => SetProperty(ref isScanning, value);
	}

	/// <summary>顶部统计条的四格数字。</summary>
	public string StatCountText => summary.CountText;

	public string StatTotalText => summary.TotalText;

	public string StatAverageText => summary.AverageText;

	public string StatMaxText => summary.MaxText;

	/// <summary>内置 / 模组 各自多少辆。</summary>
	public string StatBandText
	{
		get
		{
			if (summary.IsEmpty)
			{
				return "";
			}
			int builtIn = allVehicles.Count(v => v.IsBuiltIn);
			int mod = allVehicles.Count - builtIn;
			return $"内置 {builtIn} · 模组 {mod}";
		}
	}

	public string StatusMessage
	{
		get => statusMessage;
		private set => SetProperty(ref statusMessage, value);
	}

	public string GameDirectory { get; }

	public string VehiclesDirectory => string.IsNullOrWhiteSpace(GameDirectory)
		? ""
		: Path.Combine(GameDirectory, "content", "vehicles");

	public string ModsDirectory { get; }

	/// <summary>来源筛选（全部 / 内置 / 模组）。</summary>
	public string SelectedSource
	{
		get => selectedSource;
		set
		{
			if (SetProperty(ref selectedSource, value))
			{
				ApplyView();
			}
		}
	}

	/// <summary>当前选中的车辆，右侧详情面板绑它。</summary>
	public VehicleItem? SelectedVehicle
	{
		get => selectedVehicle;
		private set
		{
			if (SetProperty(ref selectedVehicle, value))
			{
				OnPropertyChanged(nameof(HasSelection));
				OnPropertyChanged(nameof(SelectedTitle));
				OnPropertyChanged(nameof(SelectedSizeText));
				OnPropertyChanged(nameof(SelectedFileName));
				OnPropertyChanged(nameof(SelectedDirectoryPath));
				OnPropertyChanged(nameof(SelectedPercentText));
				OnPropertyChanged(nameof(SelectedSourceText));
				OnPropertyChanged(nameof(SelectedVehicleId));
			}
		}
	}

	public bool HasSelection => selectedVehicle != null;

	public string SelectedTitle => selectedVehicle?.DisplayName ?? "";

	public string SelectedSizeText => selectedVehicle?.FileSizeText ?? "";

	public string SelectedFileName => selectedVehicle?.FileName ?? "";

	public string SelectedDirectoryPath => selectedVehicle?.DirectoryPath ?? "";

	public string SelectedSourceText => selectedVehicle?.SourceText ?? "";

	public string SelectedVehicleId => selectedVehicle?.VehicleId ?? "";

	/// <summary>这一辆占车辆总体积的百分比——一眼看出是不是"重点包"。</summary>
	public string SelectedPercentText
	{
		get
		{
			if (selectedVehicle == null || summary.TotalBytes <= 0)
			{
				return "";
			}
			double pct = selectedVehicle.Bytes * 100.0 / summary.TotalBytes;
			return pct.ToString("0.#") + "% of 车辆总体积";
		}
	}

	public VehiclesPageViewModel()
	{
		GameDirectory = AppSettings.Load().GameDirectory;
		if (string.IsNullOrWhiteSpace(GameDirectory))
		{
			GameDirectory = AppSettings.DetectGameDirectory();
		}
		ModsDirectory = new AppSettings().ResolveModsDirectory();
		foreach (string s in new[] { "全部", "内置", "模组" })
		{
			SourceOptions.Add(s);
		}
		StatusMessage = "正在扫描内置车辆与模组车辆…";
		_ = ScanAsync();
	}

	private async Task ScanAsync()
	{
		List<VehicleItem> found = await Task.Run(ScanAll).ConfigureAwait(true);
		allVehicles = found;
		ApplyView();
		IsScanning = false;

		// 云同步：车辆库列表上报云端
		try
		{
			AppState.Current.CloudSync.PushVehicles(found.Select(v => new { name = v.Name, size = v.FileSizeText, source = v.SourceText }));
		}
		catch
		{
			// 云同步失败不影响本地列表
		}
	}

	private List<VehicleItem> ScanAll()
	{
		var result = new List<VehicleItem>();
		if (string.IsNullOrWhiteSpace(GameDirectory))
		{
			return result;
		}

		string vehiclesDir = Path.Combine(GameDirectory, "content", "vehicles");
		var builtInIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		// ① 游戏自带
		if (Directory.Exists(vehiclesDir))
		{
			foreach (string file in Directory.GetFiles(vehiclesDir, "*.zip"))
			{
				string name = Path.GetFileNameWithoutExtension(file);
				if (name.StartsWith("___", StringComparison.Ordinal))
				{
					continue; // 跳过 README 类占位文件
				}
				builtInIds.Add(name);
				long bytes = SafeLength(file);
				result.Add(new VehicleItem(name, file, FileSizeHelper.Format(bytes), VehicleSource.BuiltIn, name, bytes));

				// 有的内置包内还带别的车（如 BSC 那类）——一并列出，避免漏
				foreach (string extra in VehicleIdsInZip(file))
				{
					builtInIds.Add(extra);
					result.Add(new VehicleItem(extra, file, FileSizeHelper.Format(bytes), VehicleSource.BuiltIn, extra, bytes));
				}
			}
		}

		// ② 模组带的车辆
		if (!string.IsNullOrWhiteSpace(ModsDirectory) && Directory.Exists(ModsDirectory))
		{
			var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (string file in Directory.GetFiles(ModsDirectory, "*.zip"))
			{
				long bytes = SafeLength(file);
				foreach (string id in VehicleIdsInZip(file))
				{
					if (builtInIds.Contains(id) || !seen.Add(id))
					{
						continue; // 内置已有 / 已被别的模组提供 → 不重复列
					}
					string size = VehicleContentSizeInZip(file, id);
					result.Add(new VehicleItem(id, file, size, VehicleSource.Mod, id, bytes));
				}
			}
			// 解压形态的模组（mods/<名字>/vehicles/<id>/）
			foreach (string dir in SafeDirs(ModsDirectory))
			{
				string vroot = Path.Combine(dir, "vehicles");
				if (!Directory.Exists(vroot))
				{
					continue;
				}
				foreach (string vdir in SafeDirs(vroot))
				{
					if (!HasPcFile(vdir))
					{
						continue;
					}
					string id = Path.GetFileName(vdir);
					if (builtInIds.Contains(id) || !seen.Add(id))
					{
						continue;
					}
					long totalBytes = DirectoryContentSize(vdir);
					result.Add(new VehicleItem(id, dir, FileSizeHelper.Format(totalBytes), VehicleSource.Mod, id, totalBytes));
				}
			}
		}

		return result;
	}

	private static IEnumerable<string> SafeDirs(string dir)
	{
		try
		{
			return Directory.GetDirectories(dir);
		}
		catch
		{
			return Array.Empty<string>();
		}
	}

	private static long SafeLength(string path)
	{
		try
		{
			return new FileInfo(path).Length;
		}
		catch
		{
			return 0;
		}
	}

	private static bool HasPcFile(string dir)
	{
		try
		{
			return Directory.EnumerateFiles(dir, "*.pc").Any();
		}
		catch
		{
			return false;
		}
	}

	private static long DirectoryContentSize(string dir)
	{
		try
		{
			return Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).Sum(SafeLength);
		}
		catch
		{
			return 0;
		}
	}

	/// <summary>
	/// 从 zip 中央目录里挑出真正的车辆 ID。
	/// 判据：vehicles/&lt;id&gt;/ 下存在 *.pc（车体部件配置）。
	/// 只读中央目录，不解压 —— 实测 116 个模组包（19GB）扫完 0.19s。
	/// </summary>
	private static List<string> VehicleIdsInZip(string zipPath)
	{
		var ids = new List<string>();
		try
		{
			using ZipArchive zip = ZipFile.OpenRead(zipPath);
			var hit = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (ZipArchiveEntry entry in zip.Entries)
			{
				string full = entry.FullName.Replace('\\', '/');
				if (full.Length == 0 || full[full.Length - 1] == '/')
				{
					continue;
				}
				// 只认根级 vehicles/<id>/<file>.pc，不认 vehicles/<id>/<子目录>/...
				if (!full.StartsWith("vehicles/", StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}
				string[] parts = full.Split('/');
				if (parts.Length != 3 || parts[1].Length == 0)
				{
					continue;
				}
				if (parts[2].EndsWith(".pc", StringComparison.OrdinalIgnoreCase) &&
					!parts[1].Equals("common", StringComparison.OrdinalIgnoreCase) &&
					hit.Add(parts[1]))
				{
					ids.Add(parts[1]);
				}
			}
		}
		catch
		{
			// 损坏 / 加密 / 非 zip → 当作没有车辆
		}
		return ids;
	}

	/// <summary>该车辆在这个 zip 里的解压后体积（各条目 Length 求和，走中央目录，不解压）。</summary>
	private static string VehicleContentSizeInZip(string zipPath, string vehicleId)
	{
		string prefix = "vehicles/" + vehicleId + "/";
		try
		{
			using ZipArchive zip = ZipFile.OpenRead(zipPath);
			long total = 0;
			foreach (ZipArchiveEntry entry in zip.Entries)
			{
				if (entry.FullName.Replace('\\', '/').StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
				{
					total += entry.Length;
				}
			}
			return total > 0 ? FileSizeHelper.Format(total) : "";
		}
		catch
		{
			return "";
		}
	}

	/// <summary>在资源管理器中打开游戏车辆目录（content/vehicles）。</summary>
	public IRelayCommand OpenVehiclesFolderCommand =>
		openVehiclesFolderCommand ?? (openVehiclesFolderCommand = new RelayCommand(OpenVehiclesFolder));

	/// <summary>在资源管理器中定位承载这辆车的包文件。</summary>
	public IRelayCommand<VehicleItem> OpenVehicleLocationCommand =>
		openVehicleLocationCommand ?? (openVehicleLocationCommand = new RelayCommand<VehicleItem>(OpenVehicleLocation));

	/// <summary>点行 = 选中（右侧详情面板跟着换）。</summary>
	public IRelayCommand<VehicleItem> SelectVehicleCommand =>
		selectVehicleCommand ?? (selectVehicleCommand = new RelayCommand<VehicleItem>(SelectVehicle));

	public IRelayCommand<VehicleItem> CopyVehiclePathCommand =>
		copyVehiclePathCommand ?? (copyVehiclePathCommand = new RelayCommand<VehicleItem>(CopyVehiclePath));

	/// <summary>右侧面板的「在资源管理器中定位」。</summary>
	public IRelayCommand RevealSelectedCommand =>
		revealSelectedCommand ?? (revealSelectedCommand = new RelayCommand(() => OpenVehicleLocation(selectedVehicle)));

	public IRelayCommand SortByNameCommand =>
		sortByNameCommand ?? (sortByNameCommand = new RelayCommand(() => ApplySort(0)));

	public IRelayCommand SortBySizeCommand =>
		sortBySizeCommand ?? (sortBySizeCommand = new RelayCommand(() => ApplySort(1)));

	public bool IsSortByName => sortMode == 0;

	public bool IsSortBySize => sortMode == 1;

	private void SelectVehicle(VehicleItem? item)
	{
		if (item == null)
		{
			return;
		}
		foreach (VehicleItem v in Vehicles)
		{
			v.IsSelected = ReferenceEquals(v, item);
		}
		SelectedVehicle = item;
	}

	private void CopyVehiclePath(VehicleItem? item)
	{
		string path = item?.FilePath ?? "";
		if (string.IsNullOrWhiteSpace(path))
		{
			return;
		}
		try
		{
			Clipboard.SetText(path);
			AppState.Current.Notify("路径已复制");
		}
		catch (Exception ex)
		{
			MessageBox.Show("复制路径失败：" + ex.Message, "StartRide", MessageBoxButton.OK, MessageBoxImage.Error);
		}
	}

	private void ApplySort(int mode)
	{
		if (sortMode == mode)
		{
			return;
		}
		sortMode = mode;
		OnPropertyChanged(nameof(IsSortByName));
		OnPropertyChanged(nameof(IsSortBySize));
		ApplyView();
	}

	/// <summary>把 allVehicles 按当前筛选 + 排序灌进可见集合。</summary>
	private void ApplyView()
	{
		string previousSelection = selectedVehicle?.Name + "|" + selectedVehicle?.VehicleId;

		IEnumerable<VehicleItem> query = allVehicles;
		if (SelectedSource == "内置")
		{
			query = query.Where(v => v.IsBuiltIn);
		}
		else if (SelectedSource == "模组")
		{
			query = query.Where(v => !v.IsBuiltIn);
		}

		List<VehicleItem> entries = (sortMode == 1)
			? query.OrderByDescending(v => v.Bytes).ThenBy(v => v.Name, StringComparer.OrdinalIgnoreCase).ToList()
			: query.OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase).ToList();

		Vehicles.Clear();
		SelectedVehicle = null;
		foreach (VehicleItem v in entries)
		{
			Vehicles.Add(v);
		}

		if (IsScanning)
		{
			// 扫描中，状态文案由扫描结束统一给
		}
		else if (allVehicles.Count == 0)
		{
			StatusMessage = string.IsNullOrWhiteSpace(GameDirectory)
				? "未检测到 BeamNG.drive 安装目录，请在设置中指定游戏目录。"
				: "未找到已安装车辆。可以在「模组仓库 → 在线仓库」里下载，或手动放到 mods 文件夹。";
		}
		else if (entries.Count == 0)
		{
			StatusMessage = "当前筛选下没有车辆。";
		}
		else
		{
			StatusMessage = "";
		}

		OnPropertyChanged(nameof(HasVehicles));
		OnPropertyChanged(nameof(StatBandText));
		RefreshStats(entries);

		// 保持上一次选中，避免改排序/筛选后详情面板突然空掉
		if (!string.IsNullOrEmpty(previousSelection))
		{
			VehicleItem? again = Vehicles.FirstOrDefault(v => v.Name + "|" + v.VehicleId == previousSelection);
			if (again != null)
			{
				SelectVehicle(again);
			}
		}
	}

	private void OpenVehicleLocation(VehicleItem? item)
	{
		if (item == null || string.IsNullOrWhiteSpace(item.FilePath))
		{
			return;
		}
		try
		{
			// 模组车定位到承载它的模组包（目录形态就打开那个目录）
			if (Directory.Exists(item.FilePath))
			{
				Process.Start(new ProcessStartInfo { FileName = item.FilePath, UseShellExecute = true });
			}
			else
			{
				Process.Start("explorer.exe", "/select,\"" + item.FilePath + "\"");
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show("无法打开文件位置：" + ex.Message, "StartRide", MessageBoxButton.OK, MessageBoxImage.Error);
		}
	}

	private void OpenVehiclesFolder()
	{
		try
		{
			if (string.IsNullOrWhiteSpace(GameDirectory))
			{
				MessageBox.Show("未检测到 BeamNG.drive 安装目录。", "StartRide", MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}
			string dir = Path.Combine(GameDirectory, "content", "vehicles");
			if (!Directory.Exists(dir))
			{
				MessageBox.Show("未找到车辆目录：" + dir, "StartRide", MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}
			Process.Start(new ProcessStartInfo
			{
				FileName = dir,
				UseShellExecute = true,
			});
		}
		catch (Exception ex)
		{
			MessageBox.Show("无法打开车辆目录：" + ex.Message, "StartRide", MessageBoxButton.OK, MessageBoxImage.Error);
		}
	}

	private void RefreshStats(List<VehicleItem> items)
	{
		summary = FileSetSummary.From(items.Select(v => (v.DisplayName, v.Bytes)));
		OnPropertyChanged(nameof(StatCountText));
		OnPropertyChanged(nameof(StatTotalText));
		OnPropertyChanged(nameof(StatAverageText));
		OnPropertyChanged(nameof(StatMaxText));
		OnPropertyChanged(nameof(StatBandText));
		OnPropertyChanged(nameof(SelectedPercentText));
	}
}

/// <summary>体积格式化（与 FileSizeFormatter 同口径，扫描期在后台线程用，避免依赖 UI 绑定）。</summary>
internal static class FileSizeHelper
{
	public static string Format(long bytes)
	{
		try
		{
			return FileSizeFormatter.Format(bytes);
		}
		catch
		{
			return "";
		}
	}
}
