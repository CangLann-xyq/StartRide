using System;
using System.Threading;
using System.Threading.Tasks;
using Launcher.Application.Services;
using Launcher.Domain.Models;

namespace StartRide.App.Services;

/// <summary>
/// 顶替原版的 Terracotta 下载器。
///
/// StartRide 的联机走自建中继，不需要从 GitHub 下载任何第三方穿透模块，
/// 所以这里直接报告「模块已就绪」，联机页前面那道
/// 「联机功能使用须知 + 正在下载联机模块…」的闸门就不会再出现。
///
/// 判定入口在 TerracottaAgreementDialogViewModel.EnsureReadyAsync：
/// TryGetAvailable() 非空即立即放行，不会弹窗，也不会发起网络请求。
/// </summary>
public sealed class StartRideProvisioningService : ITerracottaProvisioningService
{
	private static readonly TerracottaModule Ready =
		new TerracottaModule("startride-relay", "x64", string.Empty, string.Empty);

	public TerracottaModule? TryGetAvailable() => Ready;

	public Task<TerracottaModule> EnsureAvailableAsync(
		IProgress<LauncherProgress>? progress = null,
		CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		return Task.FromResult(Ready);
	}
}
