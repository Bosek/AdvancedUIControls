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

    public delegate void MouseHandler(MouseEventArgs mouse);
    public delegate void TranslationHandler(RenderBox control);
    public delegate void ZoomHandler(RenderBox control, float zoom);
    public delegate void RenderHandler(RenderBox control, Graphics graphics);
    public partial class RenderBox : UserControl
    {
        public int CanvasWidth { get { return pictureBox.Width; } }
        public int CanvasHeight { get { return pictureBox.Height; } }

        #region GridCellSize
        readonly int gridCellWidth;
        public int GridCellWidth { get { return gridCellWidth; } }

        readonly int gridCellHeight;
        public int GridCellHeight { get { return gridCellHeight; } }
        #endregion

        #region Pens
        public Pen GridPen { set; get; } = new Pen(Color.Black, 1.0f);
        public Pen GridHoverPen { set; get; } = new Pen(Color.Red, 1.0f);
        #endregion

        #region Zoom
        public float MinZoom { get; set; } = .5f;
        public float MaxZoom { get; set; } = 1f;
        public float DeltaZoom { get; set; } = .25f;
        float zoom = 1.0f;
        public float Zoom { get { return zoom; } }
        public event ZoomHandler OnZoomChanged;
        #endregion

        #region Translation
        bool translationAllowed = true;
        public bool TranslationAllowed { get { return translationAllowed; } }
        bool isTranslating;
        Point translationStartPoint = Point.Empty;

        public event TranslationHandler OnTranslationStarted;
        public event TranslationHandler OnTranslationStopped;
        #endregion

        Point renderOffset = Point.Empty;
        Point mousePosition = Point.Empty;
        public event RenderHandler OnRender;
        new public event MouseHandler OnMouseDown;
        new public event MouseHandler OnMouseUp;

        public RenderBox(int gridCellWidth = 32, int gridCellHeight = 32)
        {
            InitializeComponent();

            this.gridCellWidth = gridCellWidth;
            this.gridCellHeight = gridCellHeight;

            MouseWheel += handleZoom;

            setZoomStatus(zoom);
        }

        public void ToggleTranslation()
        {
            if (translationAllowed)
                stopTranslation();

            translationAllowed = !translationAllowed;
        }
        void startTranslation()
        {
            if (!translationAllowed || isTranslating)
                return;

            isTranslating = true;
            translationStartPoint = ToAbsolutePoint(mousePosition);

            OnTranslationStarted?.Invoke(this);
        }
        void stopTranslation()
        {
            isTranslating = false;
            translationStartPoint = Point.Empty;

            Cursor = Cursors.Default;

            OnTranslationStopped?.Invoke(this);
        }

        void setSnapPositionStatus(Point snappedMousePosition)
        {
            snapPositionStatus.Text = $"X: {snappedMousePosition.X} Y: {snappedMousePosition.Y}";
        }
        void setZoomStatus(float zoom)
        {
            zoomStatus.Text = $"Z: {(int)(zoom * 100)}%";
        }
        void pictureBox_MouseDown(object sender, MouseEventArgs e)
        {
            OnMouseDown?.Invoke(e);
            if (e.Button != MouseButtons.Middle)
                return;
            startTranslation();
        }
        void pictureBox_MouseUp(object sender, MouseEventArgs e)
        {
            OnMouseUp?.Invoke(e);
            if (e.Button != MouseButtons.Middle)
                return;
            stopTranslation();
        }
        void pictureBox_MouseMove(object sender, MouseEventArgs e)
        {
            mousePosition = new Point((int)(e.X / zoom), (int)(e.Y / zoom));
            if (isTranslating)
                renderOffset = new Point(mousePosition.X - translationStartPoint.X, mousePosition.Y - translationStartPoint.Y);
            pictureBox.Invalidate();
        }
        void handleZoom(object sender, MouseEventArgs e)
        {
            if (e.Delta > 0 && zoom < MaxZoom)
                zoom += DeltaZoom;
            else if (e.Delta < 0 && zoom > MinZoom)
                zoom -= DeltaZoom;

            setZoomStatus(zoom);
            pictureBox.Invalidate();

            OnZoomChanged?.Invoke(this, zoom);
        }
        void RenderBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F1)
                ToggleTranslation();
        }
        void pictureBox_SizeChanged(object sender, EventArgs e)
        {
            pictureBox.Invalidate();
        }

        void drawGrid(Graphics graphics)
        {
            var width = CanvasWidth;
            var height = CanvasHeight;

            for (int x = 0; x < ((width / zoom) / GridCellWidth) + 1; x++)
                graphics.DrawLine(GridPen,
                    x * GridCellWidth + (renderOffset.X % GridCellWidth), 0,
                    x * GridCellWidth + (renderOffset.X % GridCellWidth), height / zoom);
            for (int y = 0; y < (height / zoom) / GridCellHeight; y++)
                graphics.DrawLine(GridPen,
                    0, y * GridCellHeight + (renderOffset.Y % GridCellHeight),
                    width / zoom, y * GridCellHeight + (renderOffset.Y % GridCellHeight));
        }
        void drawSelectionBox(Graphics graphics)
        {
            var grid = GetSnappedPoint(mousePosition);

            setSnapPositionStatus(GetSnappedMouse());
            graphics.DrawRectangle(GridHoverPen, grid.X, grid.Y, GridCellWidth, GridCellHeight);
        }
        void pictureBox_Paint(object sender, PaintEventArgs e)
        {
            //e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.;
            //e.Graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
            e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

            e.Graphics.ScaleTransform(zoom, zoom);

            OnRender?.Invoke(this, e.Graphics);
            drawGrid(e.Graphics);
            drawSelectionBox(e.Graphics);

            e.Graphics.TranslateTransform(renderOffset.X, renderOffset.Y);
        }

        public void GoTo(Point point)
        {
            point.X = -point.X;
            point.Y = -point.Y;

            renderOffset = point;
            pictureBox.Invalidate();
        }

        public Point ToAbsolutePoint(Point point)
        {
            return new Point(point.X - renderOffset.X, point.Y - renderOffset.Y);
        }
        public Point ToRelativePoint(Point point)
        {
            return new Point(point.X + renderOffset.X, point.Y + renderOffset.Y);
        }
        public Point GetSnappedPoint(Point point, SnapMode snapMode = SnapMode.Normal)
        {
            var absolutePoint = ToAbsolutePoint(point);
            var absoluteX = absolutePoint.X;
            var absoluteY = absolutePoint.Y;

            var closestGridX = 0;
            var closestGridY = 0;
            switch (snapMode)
            {
                case SnapMode.Normal:
                    closestGridX = absoluteX - (absoluteX % GridCellWidth) - (absoluteX < 0 ? GridCellWidth : 0);
                    closestGridY = absoluteY - (absoluteY % GridCellHeight) - (absoluteY < 0 ? GridCellHeight : 0);
                    break;
                case SnapMode.Half:
                    var halfCellWidth = GridCellWidth / 2;
                    var halfCellHeight = GridCellHeight / 2;
                    closestGridX = absoluteX - (absoluteX % halfCellWidth) - (absoluteX < 0 ? halfCellWidth : 0);
                    closestGridY = absoluteY - (absoluteY % halfCellHeight) - (absoluteY < 0 ? halfCellHeight : 0);
                    break;
            }
            return ToRelativePoint(new Point(closestGridX, closestGridY));
        }

        public Point GetSnappedMouse(SnapMode snapMode = SnapMode.Normal)
        {
            var snapped = ToAbsolutePoint(GetSnappedPoint(mousePosition, snapMode));
            return new Point(snapped.X / GridCellWidth, snapped.Y / GridCellHeight);
        }

        public void CenterToCell(int x, int y)
        {
            var newLocation = new Point(
                (x * GridCellWidth) - (CanvasWidth / 2) + (GridCellWidth / 2),
                (y * GridCellHeight) - (CanvasHeight / 2) + (GridCellHeight / 2)
                );
            GoTo(newLocation);
        }
    }
}
