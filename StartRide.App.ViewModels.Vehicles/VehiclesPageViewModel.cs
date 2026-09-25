using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StartRide.Core;

namespace StartRide.App.ViewModels.Vehicles;

/// <summary>已装车辆条目。</summary>
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

	/// <summary>文件名（含扩展名）。</summary>
	public string FileName { get; }

	public bool IsSelected
	{
		get => isSelected;
		set => SetProperty(ref isSelected, value);
	}

	public VehicleItem(string name, string filePath, string fileSizeText, long bytes = 0)
	{
		Name = name;
		FilePath = filePath;
		DisplayName = name;
		FileSizeText = fileSizeText;
		Bytes = bytes;
		DirectoryPath = Path.GetDirectoryName(filePath) ?? "";
		FileName = Path.GetFileName(filePath);
	}
}

/// <summary>
/// 车辆管理页：扫描本机 BeamNG.drive 的 content/vehicles/*.zip，
/// 展示已装车辆列表。数据真实来自检测到的游戏目录。
///
/// StartRide 自有版式：顶部统计条 + 左列表 + 右详情面板。
/// </summary>
public sealed class VehiclesPageViewModel : ObservableObject
{
	private string statusMessage = "";
	private VehicleItem? selectedVehicle;
	private int sortMode;
	private RelayCommand? openVehiclesFolderCommand;
	private RelayCommand<VehicleItem>? openVehicleLocationCommand;
	private RelayCommand<VehicleItem>? selectVehicleCommand;
	private RelayCommand<VehicleItem>? copyVehiclePathCommand;
	private RelayCommand? sortByNameCommand;
	private RelayCommand? sortBySizeCommand;

	public ObservableCollection<VehicleItem> Vehicles { get; } = new();

	public bool HasVehicles => Vehicles.Count > 0;

	/// <summary>顶部统计条的四格数字。</summary>
	public string StatCountText => summary.CountText;

	public string StatTotalText => summary.TotalText;

	public string StatAverageText => summary.AverageText;

	public string StatMaxText => summary.MaxText;

	public string StatBandText =>
		summary.IsEmpty ? "" : $"大 {summary.LargeCount} · 中 {summary.MediumCount} · 小 {summary.SmallCount}";

	private FileSetSummary summary = new();

	public string StatusMessage
	{
		get => statusMessage;
		private set => SetProperty(ref statusMessage, value);
	}

	public string GameDirectory { get; }

	public string VehiclesDirectory => string.IsNullOrWhiteSpace(GameDirectory)
		? ""
		: Path.Combine(GameDirectory, "content", "vehicles");

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
			}
		}
	}

	public bool HasSelection => selectedVehicle != null;

	public string SelectedTitle => selectedVehicle?.DisplayName ?? "";

	public string SelectedSizeText => selectedVehicle?.FileSizeText ?? "";

	public string SelectedFileName => selectedVehicle?.FileName ?? "";

	public string SelectedDirectoryPath => selectedVehicle?.DirectoryPath ?? "";

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
		LoadVehicles();
	}

	/// <summary>在资源管理器中打开游戏车辆目录（content/vehicles）。</summary>
	public IRelayCommand OpenVehiclesFolderCommand =>
		openVehiclesFolderCommand ?? (openVehiclesFolderCommand = new RelayCommand(OpenVehiclesFolder));

	/// <summary>在资源管理器中定位某辆车的 zip 文件。</summary>
	public IRelayCommand<VehicleItem> OpenVehicleLocationCommand =>
		openVehicleLocationCommand ?? (openVehicleLocationCommand = new RelayCommand<VehicleItem>(OpenVehicleLocation));

	/// <summary>点行 = 选中（右侧详情面板跟着换）。</summary>
	public IRelayCommand<VehicleItem> SelectVehicleCommand =>
		selectVehicleCommand ?? (selectVehicleCommand = new RelayCommand<VehicleItem>(SelectVehicle));

	public IRelayCommand<VehicleItem> CopyVehiclePathCommand =>
		copyVehiclePathCommand ?? (copyVehiclePathCommand = new RelayCommand<VehicleItem>(CopyVehiclePath));

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
		Reload();
	}

	private void Reload()
	{
		LoadVehicles();
	}

	private void OpenVehicleLocation(VehicleItem? item)
	{
		if (item == null || string.IsNullOrWhiteSpace(item.FilePath))
		{
			return;
		}
		try
		{
			Process.Start("explorer.exe", "/select,\"" + item.FilePath + "\"");
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

	private void LoadVehicles()
	{
		string previousSelection = selectedVehicle?.Name ?? "";
		Vehicles.Clear();
		SelectedVehicle = null;

		if (string.IsNullOrWhiteSpace(GameDirectory))
		{
			StatusMessage = "未检测到 BeamNG.drive 安装目录，请在设置中指定游戏目录。";
			RefreshStats(new List<VehicleItem>());
			return;
		}

		string vehiclesDir = Path.Combine(GameDirectory, "content", "vehicles");
		if (!Directory.Exists(vehiclesDir))
		{
			StatusMessage = "游戏目录下未找到 content/vehicles，请确认游戏已安装完整。";
			RefreshStats(new List<VehicleItem>());
			return;
		}

		var entries = new List<VehicleItem>();
		foreach (string file in Directory.GetFiles(vehiclesDir, "*.zip"))
		{
			string name = Path.GetFileNameWithoutExtension(file);
			if (name.StartsWith("___", StringComparison.Ordinal))
			{
				continue; // 跳过 README 类占位文件
			}
			long bytes;
			string size;
			try
			{
				bytes = new FileInfo(file).Length;
				size = FileSizeFormatter.Format(bytes);
			}
			catch
			{
				bytes = 0;
				size = "";
			}
			entries.Add(new VehicleItem(name, file, size, bytes));
		}

		// 排序：0=名称 1=体积从大到小
		entries = (sortMode == 1)
			? entries.OrderByDescending(v => v.Bytes).ThenBy(v => v.Name, StringComparer.OrdinalIgnoreCase).ToList()
			: entries.OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase).ToList();

		foreach (VehicleItem v in entries)
		{
			Vehicles.Add(v);
		}

		StatusMessage = entries.Count == 0
			? "未找到已安装车辆。可以在「模组仓库 → 在线仓库」里下载，或手动放到 content/vehicles。"
			: "";
		OnPropertyChanged(nameof(HasVehicles));
		RefreshStats(entries);

		// 保持上一次选中，避免改排序后详情面板突然空掉
		if (!string.IsNullOrEmpty(previousSelection))
		{
			VehicleItem? again = Vehicles.FirstOrDefault(v => v.Name == previousSelection);
			if (again != null)
			{
				SelectVehicle(again);
			}
		}

		// 云同步：车辆库列表上报云端
		try
		{
			AppState.Current.CloudSync.PushVehicles(entries.Select(v => new { name = v.Name, size = v.FileSizeText }));
		}
		catch
		{
			// 云同步失败不影响本地列表
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
