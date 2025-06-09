using System;
using System.Reflection;

using Elements.Assets;
using Elements.Core;

using FrooxEngine;
using FrooxEngine.UIX;

using HarmonyLib;

using ResoniteModLoader;

namespace ResoniteDiagnosticMod;

public class ResoniteDiagnosticMod : ResoniteMod {
	internal const string VERSION_CONSTANT = "1.0.0";
	public override string Name => "ResoniteDiagnosticMod";
	public override string Author => "mpmxyz";
	public override string Version => VERSION_CONSTANT;
	public override string Link => "https://github.com/mpmxyz/ResoniteDiagnosticMod/";

	[AutoRegisterConfigKey]
	private static readonly ModConfigurationKey<string> STARTUP_DEFINITIONS = new(
		"startup_definitions",
		"startup config",
		computeDefault: () => "FrooxEngine.Collider UpdateCollider\n" +
			"//ddd\n" +
			"\n" +
			"\n" +
			"BoxCollider UnregisterShape");

	[AutoRegisterConfigKey]
	private static readonly ModConfigurationKey<bool> PERSIST_DEFINITIONS = new(
		"persist_definitions",
		"reapply runtime config on next startup",
		computeDefault: () => false);

	[AutoRegisterConfigKey]
	private static readonly ModConfigurationKey<bool> PRINT_STACK = new(
		"print_stack",
		"print stack on every log entry",
		computeDefault: () => false);

	[AutoRegisterConfigKey]
	private static readonly ModConfigurationKey<bool> PRINT_OBJECT_TOSTRING = new(
		"print_object_tostring",
		"print ToString() of objects (id + type otherwise)",
		computeDefault: () => false);

	[AutoRegisterConfigKey]
	private static readonly ModConfigurationKey<bool> PRINT_OBJECT_PROPERTIES = new(
		"print_object_values",
		"prints properties of objects",
		computeDefault: () => false);

	[AutoRegisterConfigKey]
	private static readonly ModConfigurationKey<bool> PRINT_SLOT_HIERARCHY_EVENT_HANDLERS_RECURSIVELY = new(
		"print_slot_hierarchy_event_handlers_recursively",
		"prints all contained event handlers with their properties",
		computeDefault: () => false);

	[AutoRegisterConfigKey]
	private static readonly ModConfigurationKey<bool> LOGGING_ENABLED = new(
		"logging_enabled",
		"global toggle to enable/disable custom logging",
		computeDefault: () => true);

	[AutoRegisterConfigKey]
	private static readonly ModConfigurationKey<bool> LIVE_PREVIEW = new(
		"live_preview",
		"global toggle to enable/disable companion process displaying the log output",
		computeDefault: () => false);

	[AutoRegisterConfigKey]
	private static readonly ModConfigurationKey<bool> PATCH_GENERIC_METHODS = new(
		"patch_generic_methods",
		"!!!DANGER: allows patching generic methods which can lead to bugs or crashes!!!",
		computeDefault: () => false);



	private static ModConfiguration? Config = null;

	internal static string CurrentDefinitions = "";

	public static bool PrintStack => Config?.GetValue(PRINT_STACK) ?? false;
	public static bool PrintFullObjects => Config?.GetValue(PRINT_OBJECT_TOSTRING) ?? false;
	public static bool PrintObjectProperties => Config?.GetValue(PRINT_OBJECT_PROPERTIES) ?? false;
	public static bool PrintSlotHierarchyEventHandlersRecursively => Config?.GetValue(PRINT_SLOT_HIERARCHY_EVENT_HANDLERS_RECURSIVELY) ?? false;
	public static bool LoggingEnabled => Config?.GetValue(LOGGING_ENABLED) ?? true;
	public static bool PatchGenericMethods => Config?.GetValue(PATCH_GENERIC_METHODS) ?? true;

	public override void OnEngineInit() {
		UniLog.FlushEveryMessage = true;
		Config = GetConfiguration() ?? throw new Exception("Expected Configuration but got null!");
		LogWriter.LivePreviewEnabled = Config?.GetValue(LIVE_PREVIEW) ?? false;
		LIVE_PREVIEW.OnChanged += (_) => {
			LogWriter.LivePreviewEnabled = Config?.GetValue(LIVE_PREVIEW) ?? false;
		};

		var harmony = new Harmony("mpmxyz.ResoniteDiagnosticMod");
		harmony.PatchAll();
		
		LogWriter.Msg("Patched!");

		var text = Config?.GetValue(STARTUP_DEFINITIONS) ?? "";

		if (!SetDefinitions(ref text, null)) {
			SetDefinitions(ref text, null);
		}

		PERSIST_DEFINITIONS.OnChanged += (_) => {
			if (Config?.GetValue(PERSIST_DEFINITIONS) ?? false) {
				Config?.Set(STARTUP_DEFINITIONS, CurrentDefinitions);
			}
		};
	}

	public override void DefineConfiguration(ModConfigurationDefinitionBuilder builder) {
		builder.AutoSave(false);
	}

	public static bool SetDefinitions(ref string definitions, TypeManager? typeManager) {
		var lines = definitions.Split('\n');
		var updatedDefinitions = "";
		bool first = true;
		bool noFailures = true;
		var methodsToUnpatch = LoggingPatches.PatchedMethods();
		foreach (var line in lines) {
			if (!first) {
				updatedDefinitions += "\n";
			}
			switch (LoggingFilterParser.TryParse(line, typeManager, out var filter)) {
				case LoggingFilterParser.ParseResult.Success:
					Msg($"Applying definition {filter}");
					bool none = true;
					bool success = false;
					foreach (var method in filter?.GetMatches() ?? EmptyEnumerator<MethodInfo>.Instance) {
						ExecutionFilter.Set(method, filter?.ExecutionFilter);
						Msg($"Patching {method}...");
						methodsToUnpatch.Remove(method);
						none = false;
						if (LoggingPatches.TryPatch(method) == null) {
							success = true;
						}
					}
					if (!success) {
						if (none) {
							Error("No matches found!");
						}
						noFailures = false;
						updatedDefinitions += LoggingFilterParser.PREFIX_COMMENT;
					}
					break;
				case LoggingFilterParser.ParseResult.Failed:
					Error($"Failed to parse \"{line}\"!");
					noFailures = false;
					updatedDefinitions += LoggingFilterParser.PREFIX_COMMENT;
					break;
			}
			updatedDefinitions += line;
			first = false;
		}
		methodsToUnpatch.Do(it => LoggingPatches.TryUnpatch(it));
		definitions = updatedDefinitions;
		if (noFailures) {
			CurrentDefinitions = updatedDefinitions;
			if (Config?.GetValue(PERSIST_DEFINITIONS) ?? false) {
				Config?.Set(STARTUP_DEFINITIONS, definitions);
			}
		}
		return noFailures;
	}

	[HarmonyPatch(typeof(DevTool), "GenerateMenuItems")]
	public static class DevToolPatches {
		public static void Postfix(InteractionHandler tool, ContextMenu menu) {
			menu.AddItem("Edit Logging...", null).Button.LocalPressed += (b, e) => new LogConfigDialog(b.World);
			menu.AddItem("View Object...", null).Button.LocalPressed += (b, e) => new ObjectViewerDialog(b.World);
			var grabberHolder = tool.Grabber?.HolderSlot;
			if (grabberHolder != null && grabberHolder.ChildrenCount == 1) {
				var grabbedSlot = grabberHolder[0];
				var reference = grabbedSlot.GetComponent<ReferenceProxy>()?.Reference.Target ?? grabbedSlot;
				menu.AddItem("Copy RefID", null).Button.LocalPressed += (b, e) => {
					tool.InputInterface.Clipboard.SetText(Formatter.FormatKey(reference));
				};
			}
		}
	}
}
