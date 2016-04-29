using System.Collections;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace System.Windows.Forms.DockPanel
{
    public abstract class AutoHideStripBase : Control
    {
        [SuppressMessage("Microsoft.Design", "CA1034:NestedTypesShouldNotBeVisible")]
        protected class Tab : IDisposable
        {
            protected internal Tab(IDockContent content)
            {
                Content = content;
            }

            ~Tab()
            {
                Dispose(false);
            }

            public IDockContent Content { get; }

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

            public DockPanel DockPanel => DockPane.DockPanel;

            public int Count => DockPane.DisplayingContents.Count;

            public Tab this[int index]
            {
                get
                {
                    var content = DockPane.DisplayingContents[index];
                    if (content == null)
                        throw new ArgumentOutOfRangeException(nameof(index));
                    if (content.DockHandler.AutoHideTab == null)
                        content.DockHandler.AutoHideTab = DockPanel.AutoHideStripControl.CreateTab(content);
                    return content.DockHandler.AutoHideTab as Tab;
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

                return IndexOf(tab.Content);
            }

            public int IndexOf(IDockContent content)
            {
                return DockPane.DisplayingContents.IndexOf(content);
            }
        }

        [SuppressMessage("Microsoft.Design", "CA1034:NestedTypesShouldNotBeVisible")]
        protected class Pane : IDisposable
        {
            protected internal Pane(DockPane dockPane)
            {
                DockPane = dockPane;
            }

            ~Pane()
            {
                Dispose(false);
            }

            public DockPane DockPane { get; }

            public TabCollection AutoHideTabs
            {
                get
                {
                    if (DockPane.AutoHideTabs == null)
                        DockPane.AutoHideTabs = new TabCollection(DockPane);
                    return DockPane.AutoHideTabs as TabCollection;
                }
            }

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
        protected sealed class PaneCollection : IEnumerable<Pane>
        {
            private class AutoHideState
            {
                private readonly DockState _mDockState;
                private bool _mSelected;

                public AutoHideState(DockState dockState)
                {
                    _mDockState = dockState;
                }

                public DockState DockState => _mDockState;

                public bool Selected
                {
                    get { return _mSelected; }
                    set { _mSelected = value; }
                }
            }

            private class AutoHideStateCollection
            {
                private readonly AutoHideState[] _mStates;

                public AutoHideStateCollection()
                {
                    _mStates = new[]    {
                                                new AutoHideState(DockState.DockTopAutoHide),
                                                new AutoHideState(DockState.DockBottomAutoHide),
                                                new AutoHideState(DockState.DockLeftAutoHide),
                                                new AutoHideState(DockState.DockRightAutoHide)
                                            };
                }

                public AutoHideState this[DockState dockState]
                {
                    get
                    {
                        for (var i = 0; i < _mStates.Length; i++)
                        {
                            if (_mStates[i].DockState == dockState)
                                return _mStates[i];
                        }
                        throw new ArgumentOutOfRangeException(nameof(dockState));
                    }
                }

                public bool ContainsPane(DockPane pane)
                {
                    return !pane.IsHidden && _mStates.Any(t => t.DockState == pane.DockState && t.Selected);
                }
            }

            internal PaneCollection(DockPanel panel, DockState dockState)
            {
                DockPanel = panel;
                States = new AutoHideStateCollection();
                States[DockState.DockTopAutoHide].Selected = dockState == DockState.DockTopAutoHide;
                States[DockState.DockBottomAutoHide].Selected = dockState == DockState.DockBottomAutoHide;
                States[DockState.DockLeftAutoHide].Selected = dockState == DockState.DockLeftAutoHide;
                States[DockState.DockRightAutoHide].Selected = dockState == DockState.DockRightAutoHide;
            }

            public DockPanel DockPanel { get; }

            private AutoHideStateCollection States { get; }

            public int Count
            {
                get
                {
                    return DockPanel.Panes.Count(pane => States.ContainsPane(pane));
                }
            }

            public Pane this[int index]
            {
                get
                {
                    int count = 0;
                    foreach (DockPane pane in DockPanel.Panes)
                    {
                        if (!States.ContainsPane(pane))
                            continue;

                        if (count == index)
                        {
                            if (pane.AutoHidePane == null)
                                pane.AutoHidePane = DockPanel.AutoHideStripControl.CreatePane(pane);
                            return pane.AutoHidePane as Pane;
                        }

                        count++;
                    }
                    throw new ArgumentOutOfRangeException(nameof(index));
                }
            }

            public bool Contains(Pane pane)
            {
                return IndexOf(pane) != -1;
            }

            public int IndexOf(Pane pane)
            {
                if (pane == null)
                    return -1;

                var index = 0;
                foreach (var dockPane in DockPanel.Panes)
                {
                    if (!States.ContainsPane(pane.DockPane))
                        continue;

                    if (Equals(pane, dockPane.AutoHidePane))
                        return index;

                    index++;
                }
                return -1;
            }

            #region IEnumerable Members

            IEnumerator<Pane> IEnumerable<Pane>.GetEnumerator()
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
        }

        protected AutoHideStripBase(DockPanel panel)
        {
            DockPanel = panel;
            PanesTop = new PaneCollection(panel, DockState.DockTopAutoHide);
            PanesBottom = new PaneCollection(panel, DockState.DockBottomAutoHide);
            PanesLeft = new PaneCollection(panel, DockState.DockLeftAutoHide);
            PanesRight = new PaneCollection(panel, DockState.DockRightAutoHide);

            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
            SetStyle(ControlStyles.Selectable, false);
        }

        protected DockPanel DockPanel { get; }

        protected PaneCollection PanesTop { get; }

        protected PaneCollection PanesBottom { get; }

        protected PaneCollection PanesLeft { get; }

        protected PaneCollection PanesRight { get; }

        protected PaneCollection GetPanes(DockState dockState)
        {
            switch (dockState)
            {
                case DockState.DockTopAutoHide:
                    return PanesTop;
                case DockState.DockBottomAutoHide:
                    return PanesBottom;
                case DockState.DockLeftAutoHide:
                    return PanesLeft;
                case DockState.DockRightAutoHide:
                    return PanesRight;
                case DockState.Unknown:
                    break;
                case DockState.Float:
                    break;
                case DockState.Document:
                    break;
                case DockState.DockTop:
                    break;
                case DockState.DockLeft:
                    break;
                case DockState.DockBottom:
                    break;
                case DockState.DockRight:
                    break;
                case DockState.Hidden:
                    break;
            }
            throw new ArgumentOutOfRangeException(nameof(dockState));
        }

        internal int GetNumberOfPanes(DockState dockState)
        {
            return GetPanes(dockState).Count;
        }

        protected Rectangle RectangleTopLeft
        {
            get
            {
                int height = MeasureHeight();
                return PanesTop.Count > 0 && PanesLeft.Count > 0 ? new Rectangle(0, 0, height, height) : Rectangle.Empty;
            }
        }

        protected Rectangle RectangleTopRight
        {
            get
            {
                int height = MeasureHeight();
                return PanesTop.Count > 0 && PanesRight.Count > 0 ? new Rectangle(Width - height, 0, height, height) : Rectangle.Empty;
            }
        }

        protected Rectangle RectangleBottomLeft
        {
            get
            {
                int height = MeasureHeight();
                return PanesBottom.Count > 0 && PanesLeft.Count > 0 ? new Rectangle(0, Height - height, height, height) : Rectangle.Empty;
            }
        }

        protected Rectangle RectangleBottomRight
        {
            get
            {
                int height = MeasureHeight();
                return PanesBottom.Count > 0 && PanesRight.Count > 0 ? new Rectangle(Width - height, Height - height, height, height) : Rectangle.Empty;
            }
        }

        protected internal Rectangle GetTabStripRectangle(DockState dockState)
        {
            int height = MeasureHeight();
            if (dockState == DockState.DockTopAutoHide && PanesTop.Count > 0)
                return new Rectangle(RectangleTopLeft.Width, 0, Width - RectangleTopLeft.Width - RectangleTopRight.Width, height);
            else if (dockState == DockState.DockBottomAutoHide && PanesBottom.Count > 0)
                return new Rectangle(RectangleBottomLeft.Width, Height - height, Width - RectangleBottomLeft.Width - RectangleBottomRight.Width, height);
            else if (dockState == DockState.DockLeftAutoHide && PanesLeft.Count > 0)
                return new Rectangle(0, RectangleTopLeft.Width, height, Height - RectangleTopLeft.Height - RectangleBottomLeft.Height);
            else if (dockState == DockState.DockRightAutoHide && PanesRight.Count > 0)
                return new Rectangle(Width - height, RectangleTopRight.Width, height, Height - RectangleTopRight.Height - RectangleBottomRight.Height);
            else
                return Rectangle.Empty;
        }

        private GraphicsPath _mDisplayingArea;
        private GraphicsPath DisplayingArea => _mDisplayingArea ?? (_mDisplayingArea = new GraphicsPath());

        private void SetRegion()
        {
            DisplayingArea.Reset();
            DisplayingArea.AddRectangle(RectangleTopLeft);
            DisplayingArea.AddRectangle(RectangleTopRight);
            DisplayingArea.AddRectangle(RectangleBottomLeft);
            DisplayingArea.AddRectangle(RectangleBottomRight);
            DisplayingArea.AddRectangle(GetTabStripRectangle(DockState.DockTopAutoHide));
            DisplayingArea.AddRectangle(GetTabStripRectangle(DockState.DockBottomAutoHide));
            DisplayingArea.AddRectangle(GetTabStripRectangle(DockState.DockLeftAutoHide));
            DisplayingArea.AddRectangle(GetTabStripRectangle(DockState.DockRightAutoHide));
            Region = new Region(DisplayingArea);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (e.Button != MouseButtons.Left)
                return;

            IDockContent content = HitTest();
            if (content == null)
                return;

            SetActiveAutoHideContent(content);

            content.DockHandler.Activate();
        }

        protected override void OnMouseHover(EventArgs e)
        {
            base.OnMouseHover(e);

            if (!DockPanel.ShowAutoHideContentOnHover)
                return;

            IDockContent content = HitTest();
            SetActiveAutoHideContent(content);

            // requires further tracking of mouse hover behavior,
            ResetMouseEventArgs();
        }

        private void SetActiveAutoHideContent(IDockContent content)
        {
            if (content != null && DockPanel.ActiveAutoHideContent != content)
                DockPanel.ActiveAutoHideContent = content;
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            RefreshChanges();
            base.OnLayout(levent);
        }

        internal void RefreshChanges()
        {
            if (IsDisposed)
                return;

            SetRegion();
            OnRefreshChanges();
        }

        protected virtual void OnRefreshChanges()
        {
        }

        protected internal abstract int MeasureHeight();

        private IDockContent HitTest()
        {
            Point ptMouse = PointToClient(MousePosition);
            return HitTest(ptMouse);
        }

        protected virtual Tab CreateTab(IDockContent content)
        {
            return new Tab(content);
        }

        protected virtual Pane CreatePane(DockPane dockPane)
        {
            return new Pane(dockPane);
        }

        protected abstract IDockContent HitTest(Point point);

        protected override AccessibleObject CreateAccessibilityInstance()
        {
            return new AutoHideStripsAccessibleObject(this);
        }

        protected abstract Rectangle GetTabBounds(Tab tab);

        internal static Rectangle ToScreen(Rectangle rectangle, Control parent)
        {
            if (parent == null)
                return rectangle;

            return new Rectangle(parent.PointToScreen(new Point(rectangle.Left, rectangle.Top)), new Size(rectangle.Width, rectangle.Height));
        }

        public class AutoHideStripsAccessibleObject : ControlAccessibleObject
        {
            private readonly AutoHideStripBase _strip;

            public AutoHideStripsAccessibleObject(AutoHideStripBase strip)
                : base(strip)
            {
                _strip = strip;
            }

            public override AccessibleRole Role => AccessibleRole.Window;

            public override int GetChildCount()
            {
                // Top, Bottom, Left, Right
                return 4;
            }

            public override AccessibleObject GetChild(int index)
            {
                switch (index)
                {
                    case 0:
                        return new AutoHideStripAccessibleObject(_strip, DockState.DockTopAutoHide, this);
                    case 1:
                        return new AutoHideStripAccessibleObject(_strip, DockState.DockBottomAutoHide, this);
                    case 2:
                        return new AutoHideStripAccessibleObject(_strip, DockState.DockLeftAutoHide, this);
                    default:
                        return new AutoHideStripAccessibleObject(_strip, DockState.DockRightAutoHide, this);
                }
            }

            public override AccessibleObject HitTest(int x, int y)
            {
                Dictionary<DockState, Rectangle> rectangles = new Dictionary<DockState, Rectangle> {
                    { DockState.DockTopAutoHide,    _strip.GetTabStripRectangle(DockState.DockTopAutoHide) },
                    { DockState.DockBottomAutoHide, _strip.GetTabStripRectangle(DockState.DockBottomAutoHide) },
                    { DockState.DockLeftAutoHide,   _strip.GetTabStripRectangle(DockState.DockLeftAutoHide) },
                    { DockState.DockRightAutoHide,  _strip.GetTabStripRectangle(DockState.DockRightAutoHide) },
                };

                var point = _strip.PointToClient(new Point(x, y));
                return (from rectangle in rectangles where rectangle.Value.Contains(point) select new AutoHideStripAccessibleObject(_strip, rectangle.Key, this)).FirstOrDefault();
            }
        }

        public class AutoHideStripAccessibleObject : AccessibleObject
        {
            private readonly AutoHideStripBase _strip;
            private readonly DockState _state;

            public AutoHideStripAccessibleObject(AutoHideStripBase strip, DockState state, AccessibleObject parent)
            {
                _strip = strip;
                _state = state;

                Parent = parent;
            }

            public override AccessibleObject Parent { get; }

            public override AccessibleRole Role => AccessibleRole.PageTabList;

            public override int GetChildCount()
            {
                return _strip.GetPanes(_state).Sum(pane => pane.AutoHideTabs.Count);
            }

            public override AccessibleObject GetChild(int index)
            {
                List<Tab> tabs = new List<Tab>();
                foreach (Pane pane in _strip.GetPanes(_state))
                {
                    tabs.AddRange(pane.AutoHideTabs);
                }

                return new AutoHideStripTabAccessibleObject(_strip, tabs[index], this);
            }

            public override Rectangle Bounds
            {
                get
                {
                    Rectangle rectangle = _strip.GetTabStripRectangle(_state);
                    return ToScreen(rectangle, _strip);
                }
            }
        }

        protected class AutoHideStripTabAccessibleObject : AccessibleObject
        {
            private readonly AutoHideStripBase _strip;
            private readonly Tab _tab;

            internal AutoHideStripTabAccessibleObject(AutoHideStripBase strip, Tab tab, AccessibleObject parent)
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
