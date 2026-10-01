using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Threading;

namespace StartRide.App.Diagnostics;

internal sealed class DispatcherBusyProbe : IDisposable
{
	internal const string NoOperationDetail = "none";

	private static FieldInfo? operationMethodField;

	private static bool hasResolvedOperationMethodField;

	private readonly DispatcherHooks hooks;

	private int nestingDepth;

	private bool hasOutermostOperation;

	private long outermostStartedAt;

	private DispatcherOperation? outermostOperation;

	private bool isDisposed;

	internal double FrameBusyMs { get; private set; }

	internal int FrameOperationCount { get; private set; }

	internal double FrameLongestOperationMs { get; private set; }

	internal string FrameLongestOperationDetail { get; private set; }

	internal double TotalBusyMs { get; private set; }

	internal int TotalOperationCount { get; private set; }

	internal double WorstOperationMs { get; private set; }

	internal string WorstOperationDetail { get; private set; }

	private DispatcherBusyProbe(DispatcherHooks hooks)
	{

		FrameLongestOperationDetail = "none";
		WorstOperationDetail = "none";
		this.hooks = hooks;
		hooks.OperationStarted += new DispatcherHookEventHandler(Hooks_OperationStarted);
		hooks.OperationCompleted += new DispatcherHookEventHandler(Hooks_OperationCompleted);
		hooks.OperationAborted += new DispatcherHookEventHandler(Hooks_OperationAborted);
	}

	internal static DispatcherBusyProbe? TryAttach(Dispatcher? dispatcher)
	{
		if (dispatcher == null)
		{
			return null;
		}
		try
		{
			return new DispatcherBusyProbe(dispatcher.Hooks);
		}
		catch (Exception)
		{
			return null;
		}
	}

	internal void ResetFrame()
	{
		FrameBusyMs = 0.0;
		FrameOperationCount = 0;
		FrameLongestOperationMs = 0.0;
		FrameLongestOperationDetail = "none";
	}

	public void Dispose()
	{

		if (!isDisposed)
		{
			isDisposed = true;
			hooks.OperationStarted -= new DispatcherHookEventHandler(Hooks_OperationStarted);
			hooks.OperationCompleted -= new DispatcherHookEventHandler(Hooks_OperationCompleted);
			hooks.OperationAborted -= new DispatcherHookEventHandler(Hooks_OperationAborted);
		}
	}

	private void Hooks_OperationStarted(object? sender, DispatcherHookEventArgs e)
	{
		if (nestingDepth == 0)
		{
			outermostStartedAt = Stopwatch.GetTimestamp();
			outermostOperation = e.Operation;
			hasOutermostOperation = true;
		}
		nestingDepth++;
	}

	private void Hooks_OperationCompleted(object? sender, DispatcherHookEventArgs e)
	{
		CompleteOperation();
	}

	private void Hooks_OperationAborted(object? sender, DispatcherHookEventArgs e)
	{
		CompleteOperation();
	}

	private void CompleteOperation()
	{
		if (nestingDepth == 0)
		{
			return;
		}
		nestingDepth--;
		if (nestingDepth > 0 || !hasOutermostOperation)
		{
			return;
		}
		double totalMilliseconds = Stopwatch.GetElapsedTime(outermostStartedAt).TotalMilliseconds;
		DispatcherOperation operation = outermostOperation;
		hasOutermostOperation = false;
		outermostOperation = null;
		FrameBusyMs += totalMilliseconds;
		FrameOperationCount++;
		TotalBusyMs += totalMilliseconds;
		TotalOperationCount++;
		if (!(totalMilliseconds <= FrameLongestOperationMs) || !(totalMilliseconds <= WorstOperationMs))
		{
			string text = Describe(operation);
			if (totalMilliseconds > FrameLongestOperationMs)
			{
				FrameLongestOperationMs = totalMilliseconds;
				FrameLongestOperationDetail = text;
			}
			if (totalMilliseconds > WorstOperationMs)
			{
				WorstOperationMs = totalMilliseconds;
				WorstOperationDetail = text;
			}
		}
	}

	private static string Describe(DispatcherOperation? operation)
	{

		if (operation == null)
		{
			return "none";
		}
		string text = ((object)operation.Priority                                   ).ToString();
		string text2 = TryGetMethodName(operation);
		if (text2 != null)
		{
			return text + "/" + text2;
		}
		return text;
	}

	private static string? TryGetMethodName(DispatcherOperation operation)
	{
		if (!hasResolvedOperationMethodField)
		{
			hasResolvedOperationMethodField = true;
			operationMethodField = typeof(DispatcherOperation).GetField("_method", BindingFlags.Instance | BindingFlags.NonPublic);
		}
		if ((object)operationMethodField == null)
		{
			return null;
		}
		try
		{
			if (!(operationMethodField.GetValue(operation) is Delegate obj))
			{
				return null;
			}
			MethodInfo method = obj.Method;
			Type declaringType = method.DeclaringType;
			return ((declaringType?.DeclaringType ?? declaringType)?.Name ?? "?") + "." + method.Name;
		}
		catch (Exception)
		{
			operationMethodField = null;
			return null;
		}
	}
}
