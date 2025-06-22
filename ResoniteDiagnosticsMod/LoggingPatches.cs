using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;

using HarmonyLib;

using MonoMod.Utils;

using ResoniteModLoader;

namespace ResoniteDiagnosticsMod;
//TODO: add protection against recursive logging
internal static class LoggingPatches {
	private static readonly Harmony harmony = new("mpmxyz.ResoniteDiagnosticsMod.Logging");
	private static readonly HashSet<MethodInfo> patched = new();
	private static long callCounter = 0;

	public static Exception? TryPatch(MethodInfo method) {
		try {
			if (!patched.Contains(method)){
				method = AccessTools.GetDeclaredMember(method);
				if (!ResoniteDiagnosticsMod.PatchGenericMethods && (method.IsGenericMethod || method.DeclaringType.IsGenericType)) {
					throw new Exception($"Cannot patch generic methods like \"{method}\"!");
				}
				harmony.Patch(
					method,
					new HarmonyMethod(
						typeof(LoggingPatches)
							.GetMethod(nameof(Prefix))
					),
					null,
					null,
					new HarmonyMethod(
						typeof(LoggingPatches)
							.GetMethod(method.ReturnType != typeof(void)
								? nameof(FinalizerWithResult)
								: nameof(Finalizer)
							)
					)
				);
				patched.Add(method);
				ResoniteMod.Msg( $"Patched {method}");
			}
			return null;
		} catch (Exception e){
			ResoniteMod.Error( e );
			return e;
		}
	}
	public static Exception? TryUnpatch(MethodInfo method) {
		try {
			patched.Remove(method);
			harmony.Unpatch(method, HarmonyPatchType.All, harmony.Id);
			ResoniteMod.Msg($"Unpatched {method}");
			return null;
		} catch (Exception e){
			ResoniteMod.Error(e);
			return e;
		}
	}

	internal static HashSet<MethodInfo> PatchedMethods() {
		return new(patched);
	}

	public static void Prefix(out string? __state, object __instance, MethodInfo __originalMethod, object[] __args) {
		__state = null;
		try {
			if (ExecutionFilter.Check(__originalMethod, __instance, __args)) {
				__state = Interlocked.Increment(ref callCounter).ToString();
				HashSet<object?>? objects = null;
				if (ResoniteDiagnosticsMod.PrintObjectProperties) {
					objects = new(__args) {
						__instance
					};
				}
				FormattedMessage($"Enter call {__state}", __instance, __originalMethod, __args, objects);
			}
		} catch (Exception e) {
			ResoniteMod.Error(e);
		}
	}

	public static void Finalizer(string? __state, object __instance, MethodInfo __originalMethod, object?[] __args, object? __exception) {
		try {
			if (__state != null) {
				var msg = __exception == null ? $"Exit call {__state}" : $"Exit call {__state} with exception {__exception}";
				HashSet<object?>? objects = null;
				if (ResoniteDiagnosticsMod.PrintObjectProperties) {
					objects = new(__args) {
						__instance
					};
				}
				FormattedMessage(msg, __instance, __originalMethod, __args, objects);
			}
		} catch (Exception e) {
			ResoniteMod.Error(e);
		}
	}

	public static void FinalizerWithResult(string? __state, object __instance, MethodInfo __originalMethod, object?[] __args, object? __result, object? __exception) {
		try {
			if (__state != null) {
				var msg = __exception == null ? $"Exit call {__state} with result {Formatter.FormatValue(__result)}" : $"Exit call {__state} with exception {__exception}";
				HashSet<object?>? objects = null;
				if (ResoniteDiagnosticsMod.PrintObjectProperties) {
					objects = new(__args) {
						__instance,
						__result
					};
				}
				FormattedMessage(msg, __instance, __originalMethod, __args, objects);
			}
		} catch (Exception e) {
			ResoniteMod.Error(e);
		}
	}

	private static void FormattedMessage(string msg, object __instance, MethodInfo __originalMethod, object?[] __args, IEnumerable<object?>? objects) {
		string method = $"{__originalMethod.GetRealDeclaringType().FullName}.{__originalMethod.Name}";
		string instance = Formatter.FormatValue(__instance);
		string args;
		if (__args.Length > 0) {
			args = $", {__args.Join(it => Formatter.FormatValue(it))}";
		} else {
			args = "" ;
		}
		string properties;
		if (objects != null) {
			properties = $"\n{objects.Select(Formatter.FormatProperties).Where(it => it != null).Join(delimiter: "\n")}";
		} else {
			properties = "";
		}

		LogWriter.Msg($"{msg}\n{method}({instance}{args}){properties}", 2);
	}
}
