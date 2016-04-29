using System.Drawing;
using System.Drawing.Drawing2D;
using System.Collections;
using System.Collections.Generic;
using System.Security.Permissions;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace System.Windows.Forms.DockPanel
{
    public abstract class DockPaneStripBase : Control
    {
        [SuppressMessage("Microsoft.Design", "CA1034:NestedTypesShouldNotBeVisible")]        
        protected internal class Tab : IDisposable
        {
            public Tab(IDockContent content)
            {
                Content = content;
            }

            ~Tab()
            {
                Dispose(false);
            }

            public IDockContent Content { get; }

            public Form ContentForm => Content as Form;

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            protected virtual void Dispose(bool disposing)
            {
            }
        }

        [SuppressMessage("Microsoft.Design", "CA1034:NestedTypesShouldNotBeVisible")]        
        protected sealed class TabCollection : IEnumerable<Tab>
        {
            #region IEnumerable Members
            IEnumerator<Tab> IEnumerable<Tab>.GetEnumerator()
            {
                for (int i = 0; i < Count; i++)
                    yield return this[i];
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                for (int i = 0; i < Count; i++)
                    yield return this[i];
            }
            #endregion

            internal TabCollection(DockPane pane)
            {
                DockPane = pane;
            }

            public DockPane DockPane { get; }

            public int Count => DockPane.DisplayingContents.Count;

            public Tab this[int index]
            {
                get
                {
                    IDockContent content = DockPane.DisplayingContents[index];
                    if (content == null)
                        throw new ArgumentOutOfRangeException(nameof(index));
                    return content.DockHandler.GetTab(DockPane.TabStripControl);
                }
            }

            public bool Contains(Tab tab)
            {
                return IndexOf(tab) != -1;
            }

            public bool Contains(IDockContent content)
            {
                return IndexOf(content) != -1;
            }

            public int IndexOf(Tab tab)
            {
                if (tab == null)
                    return -1;

                return DockPane.DisplayingContents.IndexOf(tab.Content);
            }

            public int IndexOf(IDockContent content)
            {
                return DockPane.DisplayingContents.IndexOf(content);
            }
        }

        protected DockPaneStripBase(DockPane pane)
        {
            DockPane = pane;

            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
            SetStyle(ControlStyles.Selectable, false);
        }

        protected DockPane DockPane { get; }

        protected DockPane.AppearanceStyle Appearance => DockPane.Appearance;

        private TabCollection _mTabs;
        protected TabCollection Tabs => _mTabs ?? (_mTabs = new TabCollection(DockPane));

        internal void RefreshChanges()
        {
            if (IsDisposed)
                return;

            OnRefreshChanges();
        }

        protected virtual void OnRefreshChanges()
        {
        }

        protected internal abstract int MeasureHeight();

        protected internal abstract void EnsureTabVisible(IDockContent content);

        protected int HitTest()
        {
            return HitTest(PointToClient(MousePosition));
        }

        protected internal abstract int HitTest(Point point);

        protected virtual bool MouseDownActivateTest(MouseEventArgs e)
        {
            return true;
        }

        public abstract GraphicsPath GetOutline(int index);

        protected internal virtual Tab CreateTab(IDockContent content)
        {
            return new Tab(content);
        }

        private Rectangle _dragBox = Rectangle.Empty;
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            int index = HitTest();
            if (index != -1)
            {
                if (e.Button == MouseButtons.Middle)
                {
                    // Close the specified content.
                    TryCloseTab(index);
                }
                else
                {
                    IDockContent content = Tabs[index].Content;
                    if (DockPane.ActiveContent != content)
                    {
                        // Test if the content should be active
                        if (MouseDownActivateTest(e))
                            DockPane.ActiveContent = content;
                    }

                }
            }

            if (e.Button == MouseButtons.Left)
            {
                var dragSize = SystemInformation.DragSize;
                _dragBox = new Rectangle(new Point(e.X - dragSize.Width / 2,
                                                e.Y - dragSize.Height / 2), dragSize);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (e.Button != MouseButtons.Left || _dragBox.Contains(e.Location)) 
                return;

            if (DockPane.ActiveContent == null)
                return;

            if (DockPane.DockPanel.AllowEndUserDocking && DockPane.AllowDockDragAndDrop && DockPane.ActiveContent.DockHandler.AllowEndUserDocking)
                DockPane.DockPanel.BeginDrag(DockPane.ActiveContent.DockHandler);
        }

        protected bool HasTabPageContextMenu => DockPane.HasTabPageContextMenu;

        protected void ShowTabPageContextMenu(Point position)
        {
            DockPane.ShowTabPageContextMenu(this, position);
        }

        protected bool TryCloseTab(int index)
        {
            if (index >= 0 || index < Tabs.Count)
            {
                // Close the specified content.
                IDockContent content = Tabs[index].Content;
                DockPane.CloseContent(content);
                return true;
            }
            return false;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            if (e.Button == MouseButtons.Right)
                ShowTabPageContextMenu(new Point(e.X, e.Y));
        }

        [SecurityPermission(SecurityAction.LinkDemand, Flags = SecurityPermissionFlag.UnmanagedCode)]
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == (int)Win32.Msgs.WM_LBUTTONDBLCLK)
            {
                base.WndProc(ref m);

                int index = HitTest();
                if (DockPane.DockPanel.AllowEndUserDocking && index != -1)
                {
                    IDockContent content = Tabs[index].Content;
                    if (content.DockHandler.CheckDockState(!content.DockHandler.IsFloat) != DockState.Unknown)
                        content.DockHandler.IsFloat = !content.DockHandler.IsFloat;	
                }

                return;
            }

            base.WndProc(ref m);
        }

        protected override void OnDragOver(DragEventArgs drgevent)
        {
            base.OnDragOver(drgevent);

            int index = HitTest();
            if (index != -1)
            {
                IDockContent content = Tabs[index].Content;
                if (DockPane.ActiveContent != content)
                    DockPane.ActiveContent = content;
            }
        }

        protected abstract Rectangle GetTabBounds(Tab tab);

        internal static Rectangle ToScreen(Rectangle rectangle, Control parent)
        {
            if (parent == null)
                return rectangle;

            return new Rectangle(parent.PointToScreen(new Point(rectangle.Left, rectangle.Top)), new Size(rectangle.Width, rectangle.Height));
        }

        protected override AccessibleObject CreateAccessibilityInstance()
        {
            return new DockPaneStripAccessibleObject(this);
        }

        public class DockPaneStripAccessibleObject : ControlAccessibleObject
        {
            private readonly DockPaneStripBase _strip;

            public DockPaneStripAccessibleObject(DockPaneStripBase strip)
                : base(strip)
            {
                _strip = strip;
            }

            public override AccessibleRole Role => AccessibleRole.PageTabList;

            public override int GetChildCount()
            {
                return _strip.Tabs.Count;
            }

            public override AccessibleObject GetChild(int index)
            {
                return new DockPaneStripTabAccessibleObject(_strip, _strip.Tabs[index], this);
            }

            public override AccessibleObject HitTest(int x, int y)
            {
                var point = new Point(x, y);
                return (from tab in _strip.Tabs let rectangle = _strip.GetTabBounds(tab) where ToScreen(rectangle, _strip).Contains(point) select new DockPaneStripTabAccessibleObject(_strip, tab, this)).FirstOrDefault();
            }
        }

        protected class DockPaneStripTabAccessibleObject : AccessibleObject
        {
            private readonly DockPaneStripBase _strip;
            private readonly Tab _tab;

            internal DockPaneStripTabAccessibleObject(DockPaneStripBase strip, Tab tab, AccessibleObject parent)
            {
                _strip = strip;
                _tab = tab;

                Parent = parent;
            }

            public override AccessibleObject Parent { get; }

            public override AccessibleRole Role => AccessibleRole.PageTab;

            public override Rectangle Bounds
            {
                get
                {
                    Rectangle rectangle = _strip.GetTabBounds(_tab);
                    return ToScreen(rectangle, _strip);
                }
            }

            public override string Name
            {
                get
                {
                    return _tab.Content.DockHandler.TabText;
                }
                set
                {
                    //base.Name = value;
                }
            }
        }
 
    }
}
