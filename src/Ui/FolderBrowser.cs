using PixieEditor.Editing;
using PixieEditor.Ui.Browsing;

namespace PixieEditor.Ui;

// Screens retain their navigation session; the reusable view owns all browser widgets.
internal sealed class FolderBrowserView(FolderBrowser browser)
{
    private readonly FileBrowserView View = new(browser.Model);
    public FolderBrowser Browser => browser;
    public void Draw(string id, float height) => View.Draw(id, height);
}
