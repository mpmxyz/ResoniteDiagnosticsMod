using System.Text.RegularExpressions;

namespace ResoniteDiagnosticMod;
internal class Utils {
	internal static bool IsFullMatch(Regex regex, string tested) {
		var match = regex.Match(tested);
		return match.Success && match.Length == tested.Length;
	}
}
