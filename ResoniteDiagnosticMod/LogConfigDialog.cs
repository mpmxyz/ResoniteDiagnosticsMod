using Elements.Assets;
using Elements.Core;

using FrooxEngine;
using FrooxEngine.UIX;

namespace ResoniteDiagnosticMod;
internal class LogConfigDialog {
	public LogConfigDialog(World w) {
		Slot slot = w.LocalUserSpace.AddSlot("Log Configuration Dialog");
		UIBuilder uiBuilder = RadiantUI_Panel.SetupPanel(slot, "Log configuration", new float2(800f, 700f), pinButton: false);
		float3 v = slot.LocalScale;
		slot.LocalScale = v * 0.001f;
		RadiantUI_Constants.SetupEditorStyle(uiBuilder);
		uiBuilder.VerticalLayout(4f);
		uiBuilder.Style.MinHeight = 24f;
		uiBuilder.Text("Log configuration");
		uiBuilder.PushStyle();
		uiBuilder.Style.FlexibleHeight = 1f;
		uiBuilder.ScrollArea();
		uiBuilder.PopStyle();
		uiBuilder.VerticalLayout();
		uiBuilder.FitContent(SizeFit.Disabled, SizeFit.PreferredSize);
		uiBuilder.PushStyle();
		uiBuilder.Style.TextAutoSizeMax = 24;
		uiBuilder.Style.TextAlignment = Alignment.TopLeft;
		var textField = uiBuilder.TextField(ResoniteDiagnosticMod.CurrentDefinitions, parseRTF: false);
		textField.Text.HorizontalAlign.Value = TextHorizontalAlignment.Left;
		var textFieldLayout = textField.Slot.AttachComponent<OverlappingLayout>();
		textFieldLayout.PaddingTop.Value = 4f;
		textFieldLayout.PaddingBottom.Value = 4f;
		textFieldLayout.PaddingLeft.Value = 4f;
		textFieldLayout.PaddingRight.Value = 4f;
		uiBuilder.PopStyle();
		uiBuilder.NestOut();
		uiBuilder.Style.MinHeight = 32f;
		uiBuilder.HorizontalLayout(4f);
		uiBuilder.Button("Update", RadiantUI_Constants.Sub.GREEN).LocalPressed += (b, e) => {
			var newConfig = textField.TargetString;
			if (!ResoniteDiagnosticMod.SetDefinitions(ref newConfig, b.World.Types)) {
				textField.TargetString = newConfig;
			}
		};
		uiBuilder.Button("Close", RadiantUI_Constants.Sub.RED).LocalPressed += (b, e) => {
			slot.Destroy();
		};
		slot.PositionInFrontOfUser(float3.Backward, null, 0.6f);
	}
}
