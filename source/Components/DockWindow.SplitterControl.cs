namespace System.Windows.Forms.DockPanel
{
    public partial class DockWindow
    {
        internal class DefaultSplitterControl : SplitterBase
        {
            protected override int SplitterSize => Measures.SplitterSize;

            protected override void StartDrag()
            {
                var window = Parent as DockWindow;

                window?.DockPanel.BeginDrag(window, window.RectangleToScreen(Bounds));
            }
        }
    }
}
