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

public enum VehicleSource
{
	BuiltIn,

	Mod,
}

public sealed class VehicleItem : ObservableObject
{
	private bool isSelected;

	public string Name { get; }
	public string FilePath { get; }
	public string DisplayName { get; }
	public string FileSizeText { get; }

	public long Bytes { get; }

	public string DirectoryPath { get; }

	public string FileName { get; }

	public VehicleSource Source { get; }

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

public sealed class VehiclesPageViewModel : ObservableObject
{
	private string statusMessage = "";
	private VehicleItem? selectedVehicle;
	private int sortMode;
	private bool isScanning = true;
	private string selectedSource = "全部";
	private string searchText = "";
	private RelayCommand? openVehiclesFolderCommand;
	private RelayCommand<VehicleItem>? openVehicleLocationCommand;
	private RelayCommand<VehicleItem>? selectVehicleCommand;
	private RelayCommand<VehicleItem>? copyVehiclePathCommand;
	private RelayCommand? sortByNameCommand;
	private RelayCommand? sortBySizeCommand;
	private RelayCommand? revealSelectedCommand;
	private RelayCommand? refreshCommand;
	private RelayCommand? openModsFolderCommand;
	private RelayCommand<VehicleItem>? copyVehicleIdCommand;

	private FileSetSummary summary = new();
	private List<VehicleItem> allVehicles = new();

	public ObservableCollection<VehicleItem> Vehicles { get; } = new();

	public ObservableCollection<string> SourceOptions { get; } = new();

	public bool HasVehicles => Vehicles.Count > 0;

	public bool IsScanning
	{
		get => isScanning;
		private set
		{
			if (SetProperty(ref isScanning, value))
			{
				OnPropertyChanged(nameof(StatBandText));
			}
		}
	}

	public string StatCountText => summary.CountText;

	public string StatTotalText => summary.TotalText;

	public string StatAverageText => summary.AverageText;

	public string StatMaxText => summary.MaxText;

	public string StatBandText
	{
		get
		{
			if (IsScanning)
			{
				return "正在扫描…";
			}
			if (summary.IsEmpty)
			{
				return "";
			}
			if (IsViewFiltered)
			{
				return $"显示 {Vehicles.Count} / 共 {allVehicles.Count}";
			}
			int builtIn = allVehicles.Count(v => v.IsBuiltIn);
			int mod = allVehicles.Count - builtIn;
			return $"内置 {builtIn} · 模组 {mod}";
		}
	}

	private bool IsViewFiltered => !string.IsNullOrWhiteSpace(searchText) || SelectedSource != "全部";

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

	public string SearchText
	{
		get => searchText;
		set
		{
			if (SetProperty(ref searchText, value))
			{
				ApplyView();
			}
		}
	}

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
		_ = RescanAsync();
	}

	private void Refresh()
	{
		if (IsScanning)
		{
			return;
		}
		IsScanning = true;
		StatusMessage = "正在重新扫描内置车辆与模组车辆…";
		AppState.Current.Notify("正在重新扫描车辆…");
		_ = RescanAsync(announce: true);
	}

	private async Task RescanAsync(bool announce = false)
	{
		List<VehicleItem> found = await Task.Run(ScanAll).ConfigureAwait(true);
		allVehicles = found;
		ApplyView();
		IsScanning = false;

		if (announce)
		{
			AppState.Current.Notify($"已重新扫描：{allVehicles.Count} 辆车");
		}

		try
		{
			AppState.Current.CloudSync.PushVehicles(found.Select(v => new { name = v.Name, size = v.FileSizeText, source = v.SourceText }));
		}
		catch
		{
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

		if (Directory.Exists(vehiclesDir))
		{
			foreach (string file in Directory.GetFiles(vehiclesDir, "*.zip"))
			{
				string name = Path.GetFileNameWithoutExtension(file);
				if (name.StartsWith("___", StringComparison.Ordinal))
				{
					continue;
				}
				builtInIds.Add(name);
				long bytes = SafeLength(file);
				result.Add(new VehicleItem(name, file, FileSizeHelper.Format(bytes), VehicleSource.BuiltIn, name, bytes));

				foreach (string extra in VehicleIdsInZip(file))
				{
					builtInIds.Add(extra);
					result.Add(new VehicleItem(extra, file, FileSizeHelper.Format(bytes), VehicleSource.BuiltIn, extra, bytes));
				}
			}
		}

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
						continue;
					}
					string size = VehicleContentSizeInZip(file, id);
					result.Add(new VehicleItem(id, file, size, VehicleSource.Mod, id, bytes));
				}
			}
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

	private static bool MatchesToken(VehicleItem v, string token)
	{
		return v.Name.Contains(token, StringComparison.OrdinalIgnoreCase)
			|| v.VehicleId.Contains(token, StringComparison.OrdinalIgnoreCase)
			|| v.FileName.Contains(token, StringComparison.OrdinalIgnoreCase);
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
		}
		return ids;
	}

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

	public IRelayCommand RefreshCommand =>
		refreshCommand ?? (refreshCommand = new RelayCommand(Refresh));

	public IRelayCommand OpenVehiclesFolderCommand =>
		openVehiclesFolderCommand ?? (openVehiclesFolderCommand = new RelayCommand(OpenVehiclesFolder));

	public IRelayCommand<VehicleItem> OpenVehicleLocationCommand =>
		openVehicleLocationCommand ?? (openVehicleLocationCommand = new RelayCommand<VehicleItem>(OpenVehicleLocation));

	public IRelayCommand<VehicleItem> SelectVehicleCommand =>
		selectVehicleCommand ?? (selectVehicleCommand = new RelayCommand<VehicleItem>(SelectVehicle));

	public IRelayCommand<VehicleItem> CopyVehiclePathCommand =>
		copyVehiclePathCommand ?? (copyVehiclePathCommand = new RelayCommand<VehicleItem>(CopyVehiclePath));

	public IRelayCommand RevealSelectedCommand =>
		revealSelectedCommand ?? (revealSelectedCommand = new RelayCommand(() => OpenVehicleLocation(selectedVehicle)));

	public IRelayCommand<VehicleItem> CopyVehicleIdCommand =>
		copyVehicleIdCommand ?? (copyVehicleIdCommand = new RelayCommand<VehicleItem>(CopyVehicleId));

	public IRelayCommand OpenModsFolderCommand =>
		openModsFolderCommand ?? (openModsFolderCommand = new RelayCommand(OpenModsFolder));

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

	private void CopyVehicleId(VehicleItem? item)
	{
		string id = item?.VehicleId ?? "";
		if (string.IsNullOrWhiteSpace(id))
		{
			return;
		}
		try
		{
			Clipboard.SetText(id);
			AppState.Current.Notify("车辆ID 已复制：" + id);
		}
		catch (Exception ex)
		{
			MessageBox.Show("复制车辆ID失败：" + ex.Message, "StartRide", MessageBoxButton.OK, MessageBoxImage.Error);
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

		string[] tokens = (searchText ?? "").Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
		if (tokens.Length > 0)
		{
			query = query.Where(v => tokens.All(t => MatchesToken(v, t)));
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
		}
		else if (allVehicles.Count == 0)
		{
			StatusMessage = string.IsNullOrWhiteSpace(GameDirectory)
				? "未检测到 BeamNG.drive 安装目录，请在设置中指定游戏目录。"
				: "未找到已安装车辆。可以在「模组仓库 → 在线仓库」里下载，或手动放到 mods 文件夹。";
		}
		else if (entries.Count == 0)
		{
			string keyword = (searchText ?? "").Trim();
			StatusMessage = keyword.Length > 0
				? "没有匹配「" + keyword + "」的车辆。换个关键词，或把工具条上的来源切回「全部」。"
				: "当前筛选下没有车辆。";
		}
		else
		{
			StatusMessage = "";
		}

		OnPropertyChanged(nameof(HasVehicles));
		OnPropertyChanged(nameof(StatBandText));
		RefreshStats(entries);

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

	private void OpenModsFolder()
	{
		try
		{
			if (string.IsNullOrWhiteSpace(ModsDirectory) || !Directory.Exists(ModsDirectory))
			{
				MessageBox.Show("未找到模组目录：" + (ModsDirectory ?? ""), "StartRide", MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}
			Process.Start(new ProcessStartInfo
			{
				FileName = ModsDirectory,
				UseShellExecute = true,
			});
		}
		catch (Exception ex)
		{
			MessageBox.Show("无法打开模组目录：" + ex.Message, "StartRide", MessageBoxButton.OK, MessageBoxImage.Error);
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
