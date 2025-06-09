using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

using HarmonyLib;

namespace ResoniteDiagnosticMod;

internal readonly struct LoggingFilter {
	private const BindingFlags LENIENT_FLAGS = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

	readonly Type type;
	readonly Regex method;
	readonly IEnumerable<Type> typeArgs;
	readonly ExecutionFilter filter;

	public ExecutionFilter ExecutionFilter { get { return filter; } }

	public LoggingFilter(Type type, Regex method, IEnumerable<Type> typeArgs, ExecutionFilter filter) {
		this.type = type;
		this.method = method;
		this.typeArgs = typeArgs;
		this.filter = filter;
	}


	public IEnumerable<MethodInfo> GetMatches() {
		var method = this.method;
		var typeArgs = this.typeArgs;
		return type.GetMethods(LENIENT_FLAGS)
			.Where(it => Utils.IsFullMatch(method, it.Name))
			.Select(it => it.IsGenericMethod ? it.MakeGenericMethod(typeArgs.ToArray()) : it)
			.Select(it => AccessTools.GetDeclaredMember(it));
	}

	public override string ToString() {
		return $"LoggingFilter(type={type.FullName}, method={method}, typeArgs=[{typeArgs.Join(t => t.FullName)}], filter={filter})";
	}
}
