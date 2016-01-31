using System;
using System.Drawing;
using System.Windows.Forms;

namespace AdvancedUIControls
{
    public enum SnapMode
    {
        Normal = 0,
        Half = 1
    }

    public delegate void TranslationHandler(RenderBox control);
    public delegate void ZoomHandler(RenderBox control, float zoom);
    public delegate void RenderHandler(RenderBox control, Graphics graphics);
    public partial class RenderBox : UserControl
    {
        #region GridCellSize
        private readonly int gridCellWidth;
        public int GridCellWidth { get { return gridCellWidth; } }

        private readonly int gridCellHeight;
        public int GridCellHeight { get { return gridCellHeight; } }
        #endregion

        #region Pens
        public Pen GridPen { set; get; }
        public Pen GridHoverPen { set; get; }
        #endregion

        #region Zoom
        public float MinZoom { get; set; }
        public float MaxZoom { get; set; }
        public float DeltaZoom { get; set; }
        private float zoom = 1.0f;
        public float Zoom { get { return zoom; } }
        public readonly ZoomHandler OnZoomChanged;
        #endregion

        #region Translation
        private bool translationAllowed = true;
        public bool TranslationAllowed { get { return translationAllowed; } }
        private bool isTranslating;
        private Point translatingStartPoint = Point.Empty;

        public readonly TranslationHandler OnTranslationStarted;
        public readonly TranslationHandler OnTranslationStopped;
        #endregion

        private Point renderOffset = Point.Empty;
        private Point mousePosition = Point.Empty;
        public readonly RenderHandler OnRender;

        public RenderBox(int gridCellWidth = 32, int gridCellHeight = 32)
        {
            InitializeComponent();

            this.gridCellWidth = gridCellWidth;
            this.gridCellHeight = gridCellHeight;

            GridPen = new Pen(Color.Black, 2.0f);
            GridHoverPen = new Pen(Color.Red, 2.0f)
            {
                DashStyle = System.Drawing.Drawing2D.DashStyle.Dash
            };

            MinZoom = 0.5f;
            MaxZoom = 1.0f;
            DeltaZoom = 0.25f;
            this.MouseWheel += handleZoom;

            setSnapPositionStatus(Point.Empty);
            setZoomStatus(zoom);
        }

        public void ToggleTranslation()
        {
            if (translationAllowed)
                stopTranslation();

            translationAllowed = !translationAllowed;
        }
        private void startTranslation()
        {
            if (!translationAllowed || isTranslating)
                return;

            isTranslating = true;
            translatingStartPoint = ToRelativePoint(mousePosition);

            OnTranslationStarted?.Invoke(this);
        }
        private void stopTranslation()
        {
            isTranslating = false;
            translatingStartPoint = Point.Empty;

            Cursor = Cursors.Default;

            OnTranslationStopped?.Invoke(this);
        }

        private void setSnapPositionStatus(Point snappedMousePosition)
        {
            snapPositionStatus.Text = $"X: {snappedMousePosition.X} Y: {snappedMousePosition.Y}";
        }
        private void setZoomStatus(float zoom)
        {
            zoomStatus.Text = $"Z: {(int)(zoom * 100)}%";
        }
        private void pictureBox_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Middle)
                return;
            startTranslation();
        }
        private void pictureBox_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Middle)
                return;
            stopTranslation();
        }
        private void pictureBox_MouseMove(object sender, MouseEventArgs e)
        {
            mousePosition = new Point((int)(e.X / zoom), (int)(e.Y / zoom));
            if (isTranslating)
                renderOffset = new Point(mousePosition.X - translatingStartPoint.X, mousePosition.Y - translatingStartPoint.Y);
            pictureBox.Invalidate();
        }
        private void handleZoom(object sender, MouseEventArgs e)
        {
            if (e.Delta > 0 && zoom < MaxZoom)
                zoom += DeltaZoom;
            else if (e.Delta < 0 && zoom > MinZoom)
                zoom -= DeltaZoom;

            setZoomStatus(zoom);
            pictureBox.Invalidate();

            OnZoomChanged?.Invoke(this, zoom);
        }
        private void RenderBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F1)
                ToggleTranslation();
        }
        private void pictureBox_SizeChanged(object sender, EventArgs e)
        {
            pictureBox.Invalidate();
        }

        private void drawGrid(Graphics graphics)
        {
            var width = pictureBox.Width;
            var height = pictureBox.Height;

            for (int x = 0; x < ((width / zoom) / GridCellWidth) + 1; x++)
                graphics.DrawLine(GridPen,
                    x * GridCellWidth + (renderOffset.X % GridCellWidth), 0,
                    x * GridCellWidth + (renderOffset.X % GridCellWidth), height / zoom);
            for (int y = 0; y < (height / zoom) / GridCellHeight; y++)
                graphics.DrawLine(GridPen,
                    0, y * GridCellHeight + (renderOffset.Y % GridCellHeight),
                    width / zoom, y * GridCellHeight + (renderOffset.Y % GridCellHeight));
        }
        private void drawSelectionBox(Graphics graphics)
        {
            var grid = GetSnappedPoint(mousePosition);

            setSnapPositionStatus(ToRelativePoint(grid));
            graphics.DrawRectangle(GridHoverPen, grid.X, grid.Y, GridCellWidth, GridCellHeight);

            //Debug lines
            graphics.DrawLine(GridHoverPen, mousePosition, grid);
            graphics.DrawLine(GridHoverPen, mousePosition, renderOffset);
        }
        private void pictureBox_Paint(object sender, PaintEventArgs e)
        {
            //e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.;
            //e.Graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
            e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

            e.Graphics.ScaleTransform(zoom, zoom);

            drawGrid(e.Graphics);
            drawSelectionBox(e.Graphics);
            OnRender?.Invoke(this, e.Graphics);

            e.Graphics.TranslateTransform(renderOffset.X, renderOffset.Y);
        }

        public Point ToRelativePoint(Point point)
        {
            return new Point(point.X - renderOffset.X, point.Y - renderOffset.Y);
        }
        public Point ToAbsolutePoint(Point point)
        {
            return new Point(point.X + renderOffset.X, point.Y + renderOffset.Y);
        }
        public Point GetSnappedPoint(Point point, SnapMode snapMode = SnapMode.Normal)
        {
            var relativePoint = ToRelativePoint(point);
            var relativeX = relativePoint.X;
            var relativeY = relativePoint.Y;

            var closestGridX = 0;
            var closestGridY = 0;
            switch (snapMode)
            {
                case SnapMode.Normal:
                    closestGridX = relativeX - (relativeX % GridCellWidth) - (relativeX < 0 ? GridCellWidth : 0);
                    closestGridY = relativeY - (relativeY % GridCellHeight) - (relativeY < 0 ? GridCellHeight : 0);
                    break;
                case SnapMode.Half:
                    var halfCellWidth = GridCellWidth / 2;
                    var halfCellHeight = GridCellHeight / 2;
                    closestGridX = relativeX - (relativeX % halfCellWidth) - (relativeX < 0 ? halfCellWidth : 0);
                    closestGridY = relativeY - (relativeY % halfCellHeight) - (relativeY < 0 ? halfCellHeight : 0);
                    break;
            }
            return ToAbsolutePoint(new Point(closestGridX, closestGridY));
        }

        public Point GetSnappedMouse(SnapMode snapMode = SnapMode.Normal)
        {
            return GetSnappedPoint(mousePosition, snapMode);
        }
    }
}
