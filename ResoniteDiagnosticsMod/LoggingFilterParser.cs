using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using Elements.Core;

using FrooxEngine;

using HarmonyLib;

using ResoniteModLoader;

namespace ResoniteDiagnosticsMod;
internal class LoggingFilterParser {
	public static readonly string PREFIX_COMMENT = "//";
	public static readonly Regex LINE_REGEX = new("^(\\S+) +([^<\\s]+)(?:<(\\S+)>)?(?: +(\"(?:[^\\\\]|\\.)*\"|\\*))*$", RegexOptions.CultureInvariant);
	private static readonly TimeSpan MAX_FILTER_TIME = TimeSpan.FromMilliseconds(100);

	public enum ParseResult {
		Success,
		Failed,
		Ignored
	}

	private static Type? TryParseType(string typeName, TypeManager? typeManager) {
		try {
			return NiceTypeParser.TryParse(typeName, (s) => {
				Type? type = null;
				try {
					type = typeManager?.GetDataModelType(s, true);
					if (type != null) {
						return type;
					} else {
						ResoniteMod.Warn($"Could not resolve with type manager: \"{s}\"");
					}
				} catch (Exception e) {
					LogWriter.InternalError($"Error parsing \"{s}\" with with type manager: {e}");
				}
				type = TypeHelper.FindType(s);
				if (type != null) {
					return type;
				} else {
					ResoniteMod.Warn($"Could not resolve type: \"{s}\"");
					return null;
				}
			});
		} catch (Exception e) {
			LogWriter.InternalError($"Error parsing type \"{typeName}\": {e}");
			return null;
		}
	}

	private static bool TryParseTypeList(string typeList, TypeManager? typeManager, out IEnumerable<Type> output) {
		var outputList = new List<Type>();
		output = outputList;
		int depth = 0;
		int nProcessed = 0;
		//AddUntil(int i): consumes characters up to/including the separator at index i
		bool AddUntil(int i) {
			var item = typeList.Substring(nProcessed, i - nProcessed).Trim();
			nProcessed = i + 1;
			if (string.IsNullOrEmpty(item)) {
				if (i != typeList.Length || outputList.Count > 0) {
					LogWriter.InternalError("Empty type!");
					return false;
				} else {
					return true;
				}
			}
			var type = TryParseType(item, typeManager);
			if (type == null) {
				LogWriter.InternalError($"Failed to parse type argument \"{item}\"!");
				return false;
			}
			outputList.Add(type);
			return true;
		}
		for (int i = 0; i < typeList.Length; i++) {
			switch (typeList[i]) {
				case '[' or '(' or '<':
					depth++;
					break;
				case ']' or ')' or '>':
					depth--;
					//only a very basic check, ignores mismatched brackets
					//(fails type conversion anyway)
					if (depth < 0) {
						LogWriter.InternalError("Unbalanced brackets!");
						return false;
					}
					break;
				case ',':
					if (depth == 0) {
						if (!AddUntil(i)) {
							return false;
						}
					}
					break;
			}
		}
		//add remaining text (There has to be some after the last ','!)
		return depth == 0 && AddUntil(typeList.Length);
	}

	public static ParseResult TryParse(string line, TypeManager? typeManager, out LoggingFilter? filter) {
		line = line.Trim();
		if (string.IsNullOrEmpty(line) || line.StartsWith(PREFIX_COMMENT)) {
			filter = null;
			return ParseResult.Ignored;
		}
		var match = LINE_REGEX.Match(line);
		if (!match.Success) {
			LogWriter.InternalError("Line not match regular expression!");
			filter = null;
			return ParseResult.Failed;
		}
		var typeName = match.Groups[1].Value;
		var methodName = match.Groups[2].Value;
		var typeArgNames = match.Groups[3].Value;
		var executionFilterItems = match.Groups[4].Captures;

		var type = TryParseType(typeName, typeManager);
		if (type == null) {
			LogWriter.InternalError("Could not parse object's type!");
			filter = null;
			return ParseResult.Failed;
		}
		if (!TryParseTypeList(typeArgNames, typeManager, out var typeArgs)) {
			LogWriter.InternalError("Failed to parse type arguments!");
			filter = null;
			return ParseResult.Failed;
		}
		if (!TryParseExecutionFilter(executionFilterItems, out ExecutionFilter executionFilter)) {
			LogWriter.InternalError("Failed to parse execution filter!");
			filter = null;
			return ParseResult.Failed;
		}
		Regex method;
		try {
			method = new Regex(methodName, RegexOptions.Compiled | RegexOptions.CultureInvariant);
		} catch (Exception e) {
			LogWriter.InternalError($"Failed to parse method filter \"{methodName}\": {e}");
			filter = null;
			return ParseResult.Failed;
		}

		filter = new LoggingFilter(type, method, typeArgs, executionFilter);
		return ParseResult.Success;
	}


	private static bool TryParseExecutionFilter(CaptureCollection executionFilterResults, out ExecutionFilter executionFilter) {
		Predicate<object?>? instanceFilter = null;
		List<Predicate<object?>?> argumentFilters = new();
		for (int i = 0; i < executionFilterResults.Count; i++) {
			var code = executionFilterResults[i].Value;
			if (TryParseValueFilter(code, out var filter)) {
				instanceFilter = filter;
				argumentFilters.Add(filter);
			} else {
				executionFilter = new();
				return false;
			}
		}
		executionFilter = new(instanceFilter, argumentFilters);
		return true;
	}

	private static bool TryParseValueFilter(string code, out Predicate<object?>? filter) {
		filter = null;
		if (code == "*") {
			return true;
		}
		try {
			var regexCode = Regex.Unescape(code.Substring(1, code.Length - 2));
			var regex = new Regex(regexCode, RegexOptions.CultureInvariant | RegexOptions.Compiled, MAX_FILTER_TIME);
			filter = (o) => {
				try {
					return Utils.IsFullMatch(regex, Formatter.FormatKey(o)) || (o is IWorldElement e && Utils.IsFullMatch(regex, e.ReferenceID.ToString()));
				} catch (RegexMatchTimeoutException e) {
					ResoniteMod.Warn($"Failed log filter due to exceeding timeout of {e.MatchTimeout}");
					return false;
				}
			};
			return true;
		} catch (Exception e) {
			LogWriter.InternalError($"Unable to parse value filter {code}:{e}");
			return false;
		}
	}
}
