using Foundation;
using UIKit;

namespace Endorphins;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    // Mac Catalyst installs a default Edit menu whose Cut/Copy/Paste/Select-All items own
    // ⌘X/⌘C/⌘V/⌘A as menu key-equivalents. UIKit resolves those at the native layer *before*
    // the WebView receives a keydown, so the ink editor's Monaco never sees them and its
    // copy/cut/paste can't run. Removing that command group frees the keys to reach the
    // WebView; the browser handles them in plain text fields, and InkEditorComponent binds
    // them to native-clipboard actions in Monaco.
    //
    // This must `override` MauiUIApplicationDelegate.BuildMenu — MAUI already exports
    // buildMenuWithBuilder:, so a separately-[Export]ed method would be dead code UIKit
    // never calls. Keep the base call so MAUI still builds its own MenuBarItems.
    public override void BuildMenu(IUIMenuBuilder builder)
    {
        base.BuildMenu(builder);

        // Remove only the standard Cut/Copy/Paste/Select-All group, not the whole Edit menu.
        // That group owns ⌘X/⌘C/⌘V/⌘A as menu key-equivalents, which UIKit resolves before the
        // WebView sees the keydown — so freeing just those keys lets Monaco handle them while
        // the Edit menu itself (Undo, Redo, Find, …) stays in the menu bar.
        var standardEdit = UIMenuIdentifier.StandardEdit.GetConstant();
        if (standardEdit is not null)
        {
            builder.RemoveMenu(standardEdit);
        }
    }
}
