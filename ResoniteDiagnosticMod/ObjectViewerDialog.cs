using Elements.Core;

using FrooxEngine;
using FrooxEngine.UIX;

namespace ResoniteDiagnosticMod;
internal class ObjectViewerDialog {
	public ObjectViewerDialog(World world) {
		Slot slot = world.LocalUserSpace.AddSlot("Object Viewer Dialog");
		UIBuilder uiBuilder = RadiantUI_Panel.SetupPanel(slot, "Object Viewer Dialog", new float2(800f, 700f), pinButton: false);
		float3 v = slot.LocalScale;
		slot.LocalScale = v * 0.001f;
		RadiantUI_Constants.SetupEditorStyle(uiBuilder);
		uiBuilder.VerticalLayout(4f);
		uiBuilder.Style.MinHeight = 24f;
		uiBuilder.Text("Object Viewer");
		uiBuilder.HorizontalLayout(4f);
		var textField = uiBuilder.TextField();
		var button = uiBuilder.Button("Display");
		uiBuilder.NestOut();
		uiBuilder.PushStyle();
		uiBuilder.Style.FlexibleHeight = 1f;
		uiBuilder.ScrollArea();
		uiBuilder.PopStyle();
		uiBuilder.VerticalLayout();
		uiBuilder.FitContent(SizeFit.Disabled, SizeFit.PreferredSize);
		uiBuilder.PushStyle();
		uiBuilder.Style.TextAutoSizeMax = 24;
		uiBuilder.Style.TextAlignment = Alignment.TopLeft;
		uiBuilder.OverlappingLayout(4f);
		var text = uiBuilder.Text(ResoniteDiagnosticMod.CurrentDefinitions, parseRTF: false);
		uiBuilder.NestOut();
		uiBuilder.PopStyle();
		uiBuilder.NestOut();
		button.LocalPressed += (b, e) => {
			var obj = Formatter.TryGetKnownObject(world, textField.TargetString ?? "");
			text.Content.Value = $"{Formatter.FormatValue(obj)}\n{Formatter.FormatProperties(obj)}";
		};
		slot.PositionInFrontOfUser(float3.Backward, null, 0.6f);
	}
}
