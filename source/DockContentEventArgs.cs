namespace System.Windows.Forms.DockPanel
{
    public class DockContentEventArgs : EventArgs
    {
        public DockContentEventArgs(IDockContent content)
        {
            Content = content;
        }

        public IDockContent Content { get; }
    }
}
