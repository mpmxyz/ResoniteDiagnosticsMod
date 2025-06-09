using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Schedulers;

using ResoniteModLoader;

namespace ResoniteDiagnosticMod;
internal static class LogWriter {
	private static Process? LivePreviewProcess = null;
	private readonly static TaskScheduler OutputScheduler = new WorkStealingTaskScheduler(1, System.Threading.ThreadPriority.Normal, "LogWriter.OutputScheduler");
	public static bool LivePreviewEnabled {
		get => LivePreviewProcess != null && !LivePreviewProcess.HasExited;
		set {
			if (value != LivePreviewEnabled) {
				try {
					if (value) {
						ProcessStartInfo info = new("powershell", "-Command \"& { While (1 -eq 1) { Read-Host >$null ; } }\"") {
							RedirectStandardInput = true,
							CreateNoWindow = false,
							WindowStyle = ProcessWindowStyle.Normal,
							UseShellExecute = false,
							ErrorDialog = true,
						};
						LivePreviewProcess = Process.Start(info);
					} else {
						LivePreviewProcess?.Kill();
					}
				} catch (Exception e) {
					ResoniteMod.Error(e);
				}
			}
		}
	}
	public static void Msg(string message, int skipedStackEntries = 1) {
		try {
			var stack = ResoniteDiagnosticMod.PrintStack ? $"\n{new StackTrace(skipedStackEntries, true)}" : "";
			var thread = Formatter.FormatValue(Thread.CurrentThread);
			message = $"[{thread}] {message}{stack}";
			InternalMsg(message);
		} catch (Exception e){
			ResoniteMod.Error(e);
		}
	}
	public static void InternalMsg(string message) {
			ResoniteMod.Msg(message);
			new Task(() => LivePreviewProcess?.StandardInput?.WriteLine(message)).Start(OutputScheduler);
	}
	public static void InternalError(string message) {
			ResoniteMod.Error(message);
			new Task(() => LivePreviewProcess?.StandardInput?.WriteLine(message)).Start(OutputScheduler);
	}
}
