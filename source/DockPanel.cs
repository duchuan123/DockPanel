using System.Drawing;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

// To simplify the process of finding the toolbox bitmap resource:
// #1 Create an internal class called "resfinder" outside of the root namespace.
// #2 Use "resfinder" in the toolbox bitmap attribute instead of the control name.
// #3 use the "<default namespace>.<resourcename>" string to locate the resource.
// See: http://www.bobpowell.net/toolboxbitmap.htm
internal class resfinder
{
}

namespace System.Windows.Forms.DockPanel
{
    [SuppressMessage("Microsoft.Naming", "CA1720:AvoidTypeNamesInParameters", MessageId = "0#")]
    public delegate IDockContent DeserializeDockContent(string persistString);

    [LocalizedDescription("DockPanel_Description")]
    [Designer("System.Windows.Forms.Design.ControlDesigner, System.Design")]
    [ToolboxBitmap(typeof(resfinder), "System.Windows.Forms.DockPanel.DockPanel.bmp")]
    [DefaultEvent("ActiveContentChanged")]
    public partial class DockPanel : Panel
    {
        private readonly FocusManagerImpl _mFocusManager;

        public DockPanel()
        {
            ShowAutoHideContentOnHover = true;

            _mFocusManager = new FocusManagerImpl(this);
            Extender = new DockPanelExtender(this);
            Panes = new DockPaneCollection();
            FloatWindows = new FloatWindowCollection();

            SuspendLayout();

            AutoHideWindow = Extender.AutoHideWindowFactory.CreateAutoHideWindow(this);
            AutoHideWindow.Visible = false;
            AutoHideWindow.ActiveContentChanged += m_autoHideWindow_ActiveContentChanged;
            SetAutoHideWindowParent();

            DummyControl = new DummyControl { Bounds = new Rectangle(0, 0, 1, 1) };
            Controls.Add(DummyControl);

            LoadDockWindows();

            DummyContent = new DockContent();
            ResumeLayout();

            ContentAdded += ContentAddedForeColor;
        }

        private void ContentAddedForeColor(object sender, DockContentEventArgs e)
        {
            if (ThemeForceForeColor)
                e.Content.DockHandler.Form.BackColor = ThemeForeColor;
        }

        private Color _mBackColor;
        /// <summary>
        /// Determines the color with which the client rectangle will be drawn.
        /// If this property is used instead of the BackColor it will not have any influence on the borders to the surrounding controls (DockPane).
        /// The BackColor property changes the borders of surrounding controls (DockPane).
        /// Alternatively both properties may be used (BackColor to draw and define the color of the borders and ThemeBackColor to define the color of the client rectangle). 
        /// For Backgroundimages: Set your prefered Image, then set the ThemeBackColor and the BackColor to the same Color (Control)
        /// </summary>
        [Description("Determines the color with which the client rectangle will be drawn.\r\n" +
            "If this property is used instead of the BackColor it will not have any influence on the borders to the surrounding controls (DockPane).\r\n" +
            "The BackColor property changes the borders of surrounding controls (DockPane).\r\n" +
            "Alternatively both properties may be used (BackColor to draw and define the color of the borders and ThemeBackColor to define the color of the client rectangle).\r\n" +
            "For Backgroundimages: Set your prefered Image, then set the ThemeBackColor and the BackColor to the same Color (Control).")]
        public Color ThemeBackColor
        {
            get
            {
                return !_mBackColor.IsEmpty ? _mBackColor : BackColor;
            }
            set
            {
                if (_mBackColor != value)
                {
                    _mBackColor = value;
                    Refresh();
                }
            }
        }

        /// <summary>
        /// "Force ThemeColor on all DockContent attached to this panel."
        /// </summary>
        [Description("Force ThemeColor on all DockContent attached to this panel.")]
        public bool ThemeForceForeColor { get; set; } = true;
        private Color _mForeColor;
        /// <summary>
        /// Determines the color all DockContent backcolors will change to.
        /// </summary>
        [Description("Determines the color all DockContent backcolors will change to.")]
        public Color ThemeForeColor
        {
            get
            {
                return !_mForeColor.IsEmpty ? _mForeColor : BackColor;
            }
            set
            {
                if (_mForeColor != value)
                {
                    _mForeColor = value;

                    if (ThemeForceForeColor)
                        foreach (var c in this.Contents)
                            c.DockHandler.Form.BackColor = ThemeForeColor;

                    Refresh();
                }
            }
        }

        private bool ShouldSerializeThemeBackColor()
        {
            return !_mBackColor.IsEmpty;
        }

        private void ResetThemeBackColor()
        {
            ThemeBackColor = Color.Empty;
        }

        private AutoHideStripBase _mAutoHideStripControl;
        internal AutoHideStripBase AutoHideStripControl
        {
            get
            {
                if (_mAutoHideStripControl == null)
                {
                    _mAutoHideStripControl = AutoHideStripFactory.CreateAutoHideStrip(this);
                    Controls.Add(_mAutoHideStripControl);
                }
                return _mAutoHideStripControl;
            }
        }
        internal void ResetAutoHideStripControl()
        {
            _mAutoHideStripControl?.Dispose();

            _mAutoHideStripControl = null;
        }

        private void MdiClientHandleAssigned(object sender, EventArgs e)
        {
            SetMdiClient();
            PerformLayout();
        }

        private bool _mDisposed;
        protected override void Dispose(bool disposing)
        {
            if (!_mDisposed && disposing)
            {
                _mFocusManager.Dispose();
                if (_mMdiClientController != null)
                {
                    _mMdiClientController.HandleAssigned -= MdiClientHandleAssigned;
                    _mMdiClientController.MdiChildActivate -= ParentFormMdiChildActivate;
                    _mMdiClientController.Dispose();
                }
                FloatWindows.Dispose();
                Panes.Dispose();
                DummyContent.Dispose();

                _mDisposed = true;
            }

            base.Dispose(disposing);
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IDockContent ActiveAutoHideContent
        {
            get { return AutoHideWindow.ActiveContent; }
            set { AutoHideWindow.ActiveContent = value; }
        }

        private bool _mAllowEndUserDocking = !Win32Helper.IsRunningOnMono;
        [LocalizedCategory("Category_Docking")]
        [LocalizedDescription("DockPanel_AllowEndUserDocking_Description")]
        [DefaultValue(true)]
        public bool AllowEndUserDocking
        {
            get
            {
                if (Win32Helper.IsRunningOnMono && _mAllowEndUserDocking)
                    _mAllowEndUserDocking = false;

                return _mAllowEndUserDocking;
            }
            set
            {
                if (Win32Helper.IsRunningOnMono && value)
                    throw new InvalidOperationException("AllowEndUserDocking can only be false if running on Mono");

                _mAllowEndUserDocking = value;
            }
        }

        private bool _mAllowEndUserNestedDocking = !Win32Helper.IsRunningOnMono;
        [LocalizedCategory("Category_Docking")]
        [LocalizedDescription("DockPanel_AllowEndUserNestedDocking_Description")]
        [DefaultValue(true)]
        public bool AllowEndUserNestedDocking
        {
            get
            {
                if (Win32Helper.IsRunningOnMono && _mAllowEndUserDocking)
                    _mAllowEndUserDocking = false;
                return _mAllowEndUserNestedDocking;
            }
            set
            {
                if (Win32Helper.IsRunningOnMono && value)
                    throw new InvalidOperationException("AllowEndUserNestedDocking can only be false if running on Mono");

                _mAllowEndUserNestedDocking = value;
            }
        }

        [Browsable(false)]
        public DockContentCollection Contents { get; } = new DockContentCollection();

        internal DockContent DummyContent { get; }

        private bool _mRightToLeftLayout;
        [DefaultValue(false)]
        [LocalizedCategory("Appearance")]
        [LocalizedDescription("DockPanel_RightToLeftLayout_Description")]
        public bool RightToLeftLayout
        {
            get { return _mRightToLeftLayout; }
            set
            {
                if (_mRightToLeftLayout == value)
                    return;

                _mRightToLeftLayout = value;
                foreach (var floatWindow in FloatWindows)
                    floatWindow.RightToLeftLayout = value;
            }
        }

        protected override void OnRightToLeftChanged(EventArgs e)
        {
            base.OnRightToLeftChanged(e);
            foreach (var floatWindow in FloatWindows.Where(floatWindow => floatWindow.RightToLeft != RightToLeft))
            {
                floatWindow.RightToLeft = RightToLeft;
            }
        }

        private bool _mShowDocumentIcon;
        [DefaultValue(false)]
        [LocalizedCategory("Category_Docking")]
        [LocalizedDescription("DockPanel_ShowDocumentIcon_Description")]
        public bool ShowDocumentIcon
        {
            get { return _mShowDocumentIcon; }
            set
            {
                if (_mShowDocumentIcon == value)
                    return;

                _mShowDocumentIcon = value;
                Refresh();
            }
        }

        [DefaultValue(DocumentTabStripLocation.Top)]
        [LocalizedCategory("Category_Docking")]
        [LocalizedDescription("DockPanel_DocumentTabStripLocation")]
        public DocumentTabStripLocation DocumentTabStripLocation { get; set; } = DocumentTabStripLocation.Top;

        [Browsable(false)]
        public DockPanelExtender Extender { get; }

        [Browsable(false)]
        public DockPanelExtender.IDockPaneFactory DockPaneFactory => Extender.DockPaneFactory;

        [Browsable(false)]
        public DockPanelExtender.IFloatWindowFactory FloatWindowFactory => Extender.FloatWindowFactory;

        [Browsable(false)]
        public DockPanelExtender.IDockWindowFactory DockWindowFactory => Extender.DockWindowFactory;

        internal DockPanelExtender.IDockPaneCaptionFactory DockPaneCaptionFactory => Extender.DockPaneCaptionFactory;

        internal DockPanelExtender.IDockPaneStripFactory DockPaneStripFactory => Extender.DockPaneStripFactory;

        internal DockPanelExtender.IAutoHideStripFactory AutoHideStripFactory => Extender.AutoHideStripFactory;

        [Browsable(false)]
        public DockPaneCollection Panes { get; }

        public Rectangle DockArea => new Rectangle(DockPadding.Left, DockPadding.Top,
            ClientRectangle.Width - DockPadding.Left - DockPadding.Right,
            ClientRectangle.Height - DockPadding.Top - DockPadding.Bottom);

        private double _mDockBottomPortion = 0.25;
        [LocalizedCategory("Category_Docking")]
        [LocalizedDescription("DockPanel_DockBottomPortion_Description")]
        [DefaultValue(0.25)]
        public double DockBottomPortion
        {
            get { return _mDockBottomPortion; }
            set
            {
                if (value <= 0)
                    throw new ArgumentOutOfRangeException(nameof(value));

                if (value == _mDockBottomPortion)
                    return;

                _mDockBottomPortion = value;

                if (_mDockBottomPortion < 1 && _mDockTopPortion < 1)
                {
                    if (_mDockTopPortion + _mDockBottomPortion > 1)
                        _mDockTopPortion = 1 - _mDockBottomPortion;
                }

                PerformLayout();
            }
        }

        private double _mDockLeftPortion = 0.25;
        [LocalizedCategory("Category_Docking")]
        [LocalizedDescription("DockPanel_DockLeftPortion_Description")]
        [DefaultValue(0.25)]
        public double DockLeftPortion
        {
            get { return _mDockLeftPortion; }
            set
            {
                if (value <= 0)
                    throw new ArgumentOutOfRangeException(nameof(value));

                if (value == _mDockLeftPortion)
                    return;

                _mDockLeftPortion = value;

                if (_mDockLeftPortion < 1 && _mDockRightPortion < 1)
                {
                    if (_mDockLeftPortion + _mDockRightPortion > 1)
                        _mDockRightPortion = 1 - _mDockLeftPortion;
                }
                PerformLayout();
            }
        }

        private double _mDockRightPortion = 0.25;
        [LocalizedCategory("Category_Docking")]
        [LocalizedDescription("DockPanel_DockRightPortion_Description")]
        [DefaultValue(0.25)]
        public double DockRightPortion
        {
            get { return _mDockRightPortion; }
            set
            {
                if (value <= 0)
                    throw new ArgumentOutOfRangeException(nameof(value));

                if (value == _mDockRightPortion)
                    return;

                _mDockRightPortion = value;

                if (_mDockLeftPortion < 1 && _mDockRightPortion < 1)
                {
                    if (_mDockLeftPortion + _mDockRightPortion > 1)
                        _mDockLeftPortion = 1 - _mDockRightPortion;
                }
                PerformLayout();
            }
        }

        private double _mDockTopPortion = 0.25;
        [LocalizedCategory("Category_Docking")]
        [LocalizedDescription("DockPanel_DockTopPortion_Description")]
        [DefaultValue(0.25)]
        public double DockTopPortion
        {
            get { return _mDockTopPortion; }
            set
            {
                if (value <= 0)
                    throw new ArgumentOutOfRangeException(nameof(value));

                if (value == _mDockTopPortion)
                    return;

                _mDockTopPortion = value;

                if (_mDockTopPortion < 1 && _mDockBottomPortion < 1)
                {
                    if (_mDockTopPortion + _mDockBottomPortion > 1)
                        _mDockBottomPortion = 1 - _mDockTopPortion;
                }
                PerformLayout();
            }
        }

        [Browsable(false)]
        public DockWindowCollection DockWindows { get; private set; }

        public void UpdateDockWindowZOrder(DockStyle dockStyle, bool fullPanelEdge)
        {
            switch (dockStyle)
            {
                case DockStyle.Left:
                    if (fullPanelEdge)
                        DockWindows[DockState.DockLeft].SendToBack();
                    else
                        DockWindows[DockState.DockLeft].BringToFront();
                    break;
                case DockStyle.Right:
                    if (fullPanelEdge)
                        DockWindows[DockState.DockRight].SendToBack();
                    else
                        DockWindows[DockState.DockRight].BringToFront();
                    break;
                case DockStyle.Top:
                    if (fullPanelEdge)
                        DockWindows[DockState.DockTop].SendToBack();
                    else
                        DockWindows[DockState.DockTop].BringToFront();
                    break;
                case DockStyle.Bottom:
                    if (fullPanelEdge)
                        DockWindows[DockState.DockBottom].SendToBack();
                    else
                        DockWindows[DockState.DockBottom].BringToFront();
                    break;
                case DockStyle.None:
                    break;
                case DockStyle.Fill:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(dockStyle), dockStyle, null);
            }
        }

        [Browsable(false)]
        public int DocumentsCount => Documents.Count();

        public IDockContent[] DocumentsToArray()
        {
            int count = DocumentsCount;
            IDockContent[] documents = new IDockContent[count];
            int i = 0;
            foreach (IDockContent content in Documents)
            {
                documents[i] = content;
                i++;
            }

            return documents;
        }

        [Browsable(false)]
        public IEnumerable<IDockContent> Documents
        {
            get { return Contents.Where(content => content.DockHandler.DockState == DockState.Document); }
        }

        private Control DummyControl { get; }

        [Browsable(false)]
        public FloatWindowCollection FloatWindows { get; }

        [Category("Layout")]
        [LocalizedDescription("DockPanel_DefaultFloatWindowSize_Description")]
        public Size DefaultFloatWindowSize { get; set; } = new Size(300, 300);

        private bool ShouldSerializeDefaultFloatWindowSize()
        {
            return DefaultFloatWindowSize != new Size(300, 300);
        }

        private void ResetDefaultFloatWindowSize()
        {
            DefaultFloatWindowSize = new Size(300, 300);
        }

        [LocalizedCategory("Category_Performance")]
        [LocalizedDescription("DockPanel_SupportDeeplyNestedContent_Description")]
        [DefaultValue(false)]
        public bool SupportDeeplyNestedContent { get; set; }

        [LocalizedCategory("Category_Docking")]
        [LocalizedDescription("DockPanel_ShowAutoHideContentOnHover_Description")]
        [DefaultValue(true)]
        public bool ShowAutoHideContentOnHover { get; set; }

        public int GetDockWindowSize(DockState dockState)
        {
            if (dockState == DockState.DockLeft || dockState == DockState.DockRight)
            {
                int width = ClientRectangle.Width - DockPadding.Left - DockPadding.Right;
                int dockLeftSize = _mDockLeftPortion >= 1 ? (int)_mDockLeftPortion : (int)(width * _mDockLeftPortion);
                int dockRightSize = _mDockRightPortion >= 1 ? (int)_mDockRightPortion : (int)(width * _mDockRightPortion);

                if (dockLeftSize < MeasurePane.MinSize)
                    dockLeftSize = MeasurePane.MinSize;
                if (dockRightSize < MeasurePane.MinSize)
                    dockRightSize = MeasurePane.MinSize;

                if (dockLeftSize + dockRightSize > width - MeasurePane.MinSize)
                {
                    int adjust = dockLeftSize + dockRightSize - (width - MeasurePane.MinSize);
                    dockLeftSize -= adjust / 2;
                    dockRightSize -= adjust / 2;
                }

                return dockState == DockState.DockLeft ? dockLeftSize : dockRightSize;
            }
            else if (dockState == DockState.DockTop || dockState == DockState.DockBottom)
            {
                int height = ClientRectangle.Height - DockPadding.Top - DockPadding.Bottom;
                int dockTopSize = _mDockTopPortion >= 1 ? (int)_mDockTopPortion : (int)(height * _mDockTopPortion);
                int dockBottomSize = _mDockBottomPortion >= 1 ? (int)_mDockBottomPortion : (int)(height * _mDockBottomPortion);

                if (dockTopSize < MeasurePane.MinSize)
                    dockTopSize = MeasurePane.MinSize;
                if (dockBottomSize < MeasurePane.MinSize)
                    dockBottomSize = MeasurePane.MinSize;

                if (dockTopSize + dockBottomSize > height - MeasurePane.MinSize)
                {
                    int adjust = dockTopSize + dockBottomSize - (height - MeasurePane.MinSize);
                    dockTopSize -= adjust / 2;
                    dockBottomSize -= adjust / 2;
                }

                return dockState == DockState.DockTop ? dockTopSize : dockBottomSize;
            }
            else
                return 0;
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            SuspendLayout(true);

            AutoHideStripControl.Bounds = ClientRectangle;

            CalculateDockPadding();

            DockWindows[DockState.DockLeft].Width = GetDockWindowSize(DockState.DockLeft);
            DockWindows[DockState.DockRight].Width = GetDockWindowSize(DockState.DockRight);
            DockWindows[DockState.DockTop].Height = GetDockWindowSize(DockState.DockTop);
            DockWindows[DockState.DockBottom].Height = GetDockWindowSize(DockState.DockBottom);

            AutoHideWindow.Bounds = AutoHideWindowRectangle;

            DockWindows[DockState.Document].BringToFront();
            AutoHideWindow.BringToFront();

            base.OnLayout(levent);
            ResumeLayout(true, true);
        }

        internal Rectangle GetTabStripRectangle(DockState dockState)
        {
            return AutoHideStripControl.GetTabStripRectangle(dockState);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (ThemeBackColor == BackColor) return;

            Graphics g = e.Graphics;
            SolidBrush bgBrush = new SolidBrush(ThemeBackColor);
            g.FillRectangle(bgBrush, ClientRectangle);
        }

        internal void AddContent(IDockContent content)
        {
            if (content == null)
                throw new ArgumentNullException();

            if (!Contents.Contains(content))
            {
                Contents.Add(content);
                OnContentAdded(new DockContentEventArgs(content));
            }
        }

        internal void AddPane(DockPane pane)
        {
            if (Panes.Contains(pane))
                return;

            Panes.Add(pane);
        }

        internal void AddFloatWindow(FloatWindow floatWindow)
        {
            if (FloatWindows.Contains(floatWindow))
                return;

            FloatWindows.Add(floatWindow);
        }

        private void CalculateDockPadding()
        {
            DockPadding.All = 0;

            int height = AutoHideStripControl.MeasureHeight();

            if (AutoHideStripControl.GetNumberOfPanes(DockState.DockLeftAutoHide) > 0)
                DockPadding.Left = height;
            if (AutoHideStripControl.GetNumberOfPanes(DockState.DockRightAutoHide) > 0)
                DockPadding.Right = height;
            if (AutoHideStripControl.GetNumberOfPanes(DockState.DockTopAutoHide) > 0)
                DockPadding.Top = height;
            if (AutoHideStripControl.GetNumberOfPanes(DockState.DockBottomAutoHide) > 0)
                DockPadding.Bottom = height;
        }

        internal void RemoveContent(IDockContent content)
        {
            if (content == null)
                throw new ArgumentNullException();

            if (Contents.Contains(content))
            {
                Contents.Remove(content);
                OnContentRemoved(new DockContentEventArgs(content));
            }
        }

        internal void RemovePane(DockPane pane)
        {
            if (!Panes.Contains(pane))
                return;

            Panes.Remove(pane);
        }

        internal void RemoveFloatWindow(FloatWindow floatWindow)
        {
            if (!FloatWindows.Contains(floatWindow))
                return;

            FloatWindows.Remove(floatWindow);
            if (FloatWindows.Count != 0)
                return;

            ParentForm?.Focus();
        }

        public void SetPaneIndex(DockPane pane, int index)
        {
            int oldIndex = Panes.IndexOf(pane);
            if (oldIndex == -1)
                throw new ArgumentException(Strings.DockPanel_SetPaneIndex_InvalidPane);

            if (index < 0 || index > Panes.Count - 1)
                if (index != -1)
                    throw new ArgumentOutOfRangeException(Strings.DockPanel_SetPaneIndex_InvalidIndex);

            if (oldIndex == index)
                return;
            if (oldIndex == Panes.Count - 1 && index == -1)
                return;

            Panes.Remove(pane);
            if (index == -1)
                Panes.Add(pane);
            else if (oldIndex < index)
                Panes.AddAt(pane, index - 1);
            else
                Panes.AddAt(pane, index);
        }

        public void SuspendLayout(bool allWindows)
        {
            FocusManager.SuspendFocusTracking();
            SuspendLayout();
            if (allWindows)
                SuspendMdiClientLayout();
        }

        public void ResumeLayout(bool performLayout, bool allWindows)
        {
            FocusManager.ResumeFocusTracking();
            ResumeLayout(performLayout);
            if (allWindows)
                ResumeMdiClientLayout(performLayout);
        }

        internal Form ParentForm => GetMdiClientController().ParentForm;

        protected override void OnParentChanged(EventArgs e)
        {
            SetAutoHideWindowParent();
            GetMdiClientController().ParentForm = Parent as Form;
            base.OnParentChanged(e);
        }

        private void SetAutoHideWindowParent()
        {
            if (AutoHideWindow.Parent == this) return;
            AutoHideWindow.Parent = this;
            AutoHideWindow.BringToFront();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);

            if (Visible)
                SetMdiClient();
        }

        public Rectangle DocumentWindowBounds
        {
            get
            {
                var rectDocumentBounds = DisplayRectangle;
                if (DockWindows[DockState.DockLeft].Visible)
                {
                    rectDocumentBounds.X += DockWindows[DockState.DockLeft].Width;
                    rectDocumentBounds.Width -= DockWindows[DockState.DockLeft].Width;
                }
                if (DockWindows[DockState.DockRight].Visible)
                    rectDocumentBounds.Width -= DockWindows[DockState.DockRight].Width;
                if (DockWindows[DockState.DockTop].Visible)
                {
                    rectDocumentBounds.Y += DockWindows[DockState.DockTop].Height;
                    rectDocumentBounds.Height -= DockWindows[DockState.DockTop].Height;
                }
                if (DockWindows[DockState.DockBottom].Visible)
                    rectDocumentBounds.Height -= DockWindows[DockState.DockBottom].Height;

                return rectDocumentBounds;
            }
        }

        private static readonly object ActiveAutoHideContentChangedEvent = new object();

        [LocalizedCategory("Category_DockingNotification")]
        [LocalizedDescription("DockPanel_ActiveAutoHideContentChanged_Description")]
        public event EventHandler ActiveAutoHideContentChanged
        {
            add { Events.AddHandler(ActiveAutoHideContentChangedEvent, value); }
            remove { Events.RemoveHandler(ActiveAutoHideContentChangedEvent, value); }
        }

        protected virtual void OnActiveAutoHideContentChanged(EventArgs e)
        {
            EventHandler handler = (EventHandler)Events[ActiveAutoHideContentChangedEvent];
            handler?.Invoke(this, e);
        }

        private void m_autoHideWindow_ActiveContentChanged(object sender, EventArgs e)
        {
            OnActiveAutoHideContentChanged(e);
        }


        private static readonly object ContentAddedEvent = new object();

        [LocalizedCategory("Category_DockingNotification")]
        [LocalizedDescription("DockPanel_ContentAdded_Description")]
        public event EventHandler<DockContentEventArgs> ContentAdded
        {
            add { Events.AddHandler(ContentAddedEvent, value); }
            remove { Events.RemoveHandler(ContentAddedEvent, value); }
        }

        protected virtual void OnContentAdded(DockContentEventArgs e)
        {
            EventHandler<DockContentEventArgs> handler = (EventHandler<DockContentEventArgs>)Events[ContentAddedEvent];
            handler?.Invoke(this, e);
        }

        private static readonly object ContentRemovedEvent = new object();

        [LocalizedCategory("Category_DockingNotification")]
        [LocalizedDescription("DockPanel_ContentRemoved_Description")]
        public event EventHandler<DockContentEventArgs> ContentRemoved
        {
            add { Events.AddHandler(ContentRemovedEvent, value); }
            remove { Events.RemoveHandler(ContentRemovedEvent, value); }
        }

        protected virtual void OnContentRemoved(DockContentEventArgs e)
        {
            EventHandler<DockContentEventArgs> handler = (EventHandler<DockContentEventArgs>)Events[ContentRemovedEvent];
            handler?.Invoke(this, e);
        }

        internal void ReloadDockWindows()
        {
            var old = DockWindows;
            LoadDockWindows();
            foreach (var dockWindow in old)
            {
                Controls.Remove(dockWindow);
                dockWindow.Dispose();
            }
        }

        internal void LoadDockWindows()
        {
            DockWindows = new DockWindowCollection(this);
            foreach (var dockWindow in DockWindows)
            {
                Controls.Add(dockWindow);
            }
        }

        public void ResetAutoHideStripWindow()
        {
            var old = AutoHideWindow;
            AutoHideWindow = Extender.AutoHideWindowFactory.CreateAutoHideWindow(this);
            AutoHideWindow.Visible = false;
            SetAutoHideWindowParent();

            old.Visible = false;
            old.Parent = null;
            old.Dispose();
        }
    }
}
