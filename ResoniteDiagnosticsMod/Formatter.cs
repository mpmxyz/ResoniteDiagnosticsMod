
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

using Elements.Core;

using FrooxEngine;

using HarmonyLib;

using MonoMod.Utils;

namespace ResoniteDiagnosticsMod;
internal class Formatter {
	private static readonly ConditionalWeakTable<object, string> objectIDs = new();
	private static readonly ConcurrentDictionary<string, WeakReference> reverseLookup = new();
	private static long idCounter = 0;

	internal static object? TryGetKnownObject(World w, string key) {
		if (reverseLookup.TryGetValue(key, out var o)) {
			return o.SafeGetTarget();
		}
		if (RefID.TryParse(key, out var id)) {
			return w.ReferenceController.GetObjectOrNull(id);
		}
		return null;
	}

	internal static string FormatKey(object? o) {
		return o switch {
			null => "null",
			string s => s,
			ValueType v => v.ToString(),
			_ => objectIDs.GetValue(o, (_) => {
				var id = $"O{Interlocked.Increment(ref idCounter)}";
				reverseLookup.TryAdd(id, new WeakReference(o));
				return id;
			})
		};
	}

	internal static string FormatValue(object? o) {
		return o switch {
			null => "null",
			string _ => string.Format("\"{0}\"", o),
			char _ => string.Format("'{0}'", o),
			ValueType v => v.ToString(),
			PhysicsMovedHierarchyEventHandler h => FormatSlotHierarchyEventHandler(h),
			GeneralMovedHierarchyEventHandler h => FormatSlotHierarchyEventHandler(h),
			ChangedHierarchyEventHandler h => FormatSlotHierarchyEventHandler(h),
			IWorldElement e => ResoniteDiagnosticsMod.PrintFullObjects
				? $"{e.ReferenceID}/{FormatKey(o)}: {o}"
				: $"{e.ReferenceID}/{FormatKey(o)}: {o.GetType().FullName} \"{e.Name}\"",
			_ => ResoniteDiagnosticsMod.PrintFullObjects
				? $"{FormatKey(o)}: {o}"
				: $"{FormatKey(o)}: {o.GetType().FullName}",
		};
	}

	internal static string FormatSlotHierarchyEventHandler<H, M>(SlotHierarchyEventHandler<H, M> h) where M : SlotHierarchyEventManger<H, M> where H : SlotHierarchyEventHandler<H, M> {
		return ResoniteDiagnosticsMod.PrintSlotHierarchyEventHandlersRecursively
			? $"{FormatKey(h)}: {h.GetType().FullName}\n{h.HierarchyToString()}"
			: $"{FormatKey(h)}: {h.GetType().FullName}";
	}

	internal static string? FormatProperties(object? o) {
		if (o == null) return null;
		StringBuilder sb = new("  ");
		sb.Append(FormatKey(o));
		sb.Append(":");
		var originalType = o.GetType();
		Dictionary<MemberInfo,string> keyValues = new();
		for (var type = originalType; type != null; type = type.BaseType) {
			foreach (var member in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)) {
				var declaredMember = AccessTools.GetDeclaredMember(member);
				if (declaredMember.CanRead && !keyValues.ContainsKey(declaredMember)) {
					try {
						keyValues.Add(declaredMember, FormatValue(member.GetValue(o)));
					} catch (Exception e) {
						keyValues.Add(declaredMember, e.Message);
					}
				}
			}
			foreach (var member in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)) {
				var declaredMember = AccessTools.GetDeclaredMember(member);
				if (!keyValues.ContainsKey(declaredMember)) {
					try {
						keyValues.Add(declaredMember, FormatValue(member.GetValue(o)));
					} catch (Exception e) {
						keyValues.Add(declaredMember, e.Message);
					}
				}
			}
		}
		keyValues
			.OrderBy(it => it.Key.DeclaringType == originalType ? "" : it.Key.DeclaringType.FullName)
			.ThenBy(it => it.Key.Name)
			.Do(it => {
				sb.Append("\n    ");
				if (it.Key.DeclaringType != originalType) {
					sb.Append(it.Key.DeclaringType.Namespace);
					sb.Append(".");
					sb.Append(it.Key.DeclaringType.Name);
					sb.Append(".");
				}
				sb.Append(it.Key.Name);
				sb.Append("=");
				sb.Append(it.Value);
			});
		if (keyValues.Any()) {
			return sb.ToString();
		} else {
			return null;
		}
	}
}
