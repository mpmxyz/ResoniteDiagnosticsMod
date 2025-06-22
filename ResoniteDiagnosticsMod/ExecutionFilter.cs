using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ResoniteDiagnosticsMod;
internal readonly struct ExecutionFilter {
	private static readonly ConcurrentDictionary<MethodInfo, ExecutionFilter> methodOptions = new();

	private readonly Predicate<object?>? InstanceFilter = null;
	private readonly Predicate<object?>?[] ArgumentFilters = { };

	public ExecutionFilter(
		Predicate<object?>? InstanceFilter,
		IEnumerable<Predicate<object?>?> ArgumentFilters
	) {
		this.InstanceFilter = InstanceFilter;
		this.ArgumentFilters = ArgumentFilters.ToArray();
	}

	public static void Set(MethodInfo method, ExecutionFilter? filter) {
		if (filter != null) {
			methodOptions[method] = (ExecutionFilter)filter;
		} else {
			methodOptions.TryRemove(method, out var _);
		}
	}

	public static bool Check(MethodInfo method, object? instance, object?[] args) {
		if (!ResoniteDiagnosticsMod.LoggingEnabled) {
			return false;
		}
		if (methodOptions.TryGetValue(method, out var filter)) {
			if (filter.InstanceFilter != null && !filter.InstanceFilter(instance)) {
				return false;
			}
			int count = Math.Min(args.Length, filter.ArgumentFilters.Length);
			for (int i = 0; i < count; i++) {
				var argFilter = filter.ArgumentFilters[i];
				if (argFilter != null && !argFilter(args[i])) {
					return false;
				}
			}
		}
		return true;
	}
}
