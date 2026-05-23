using OverlayRef.ViewModels;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace OverlayRef
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private MainViewModel _vm;
        private bool _isPanning = false;
        private Point _lastMousePos;
        private DispatcherTimer _resizeTimer;
        private bool _isClickThroughEnabled = false;

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_LAYERED = 0x00080000;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const int HOTKEY_ID = 9000;

        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;

        private const uint VK_T = 0x54;
        private const int WM_HOTKEY = 0x0312;

        public MainWindow()
        {
            InitializeComponent();
            _vm = new MainViewModel();
            DataContext = _vm;
            _resizeTimer = new DispatcherTimer();
            _resizeTimer.Interval = TimeSpan.FromMilliseconds(150);
            _resizeTimer.Tick += ResizeTimer_Tick;
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;

            HwndSource source = HwndSource.FromHwnd(hwnd);
            source.AddHook(HwndHook);
            RegisterHotKey(hwnd, HOTKEY_ID, MOD_CONTROL | MOD_ALT, VK_T);
        }

        private IntPtr HwndHook(IntPtr hwnd,int msg,IntPtr wParam,IntPtr lParam,ref bool handled)
        {
            if (msg == WM_HOTKEY)
            {
                if (wParam.ToInt32() == HOTKEY_ID)
                {
                    ToggleClickThrough();
                    handled = true;
                }
            }

            return IntPtr.Zero;
        }
        private void ToggleClickThrough()
        {
            _isClickThroughEnabled = !_isClickThroughEnabled;

            if (_isClickThroughEnabled)
            {
                EnableClickThrough();
            }
            else
            {
                DisableClickThrough();
            }
        }
        protected override void OnClosed(EventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;

            UnregisterHotKey(hwnd, HOTKEY_ID);

            base.OnClosed(e);
        }
        private void EnableClickThrough()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            exStyle |= WS_EX_LAYERED | WS_EX_TRANSPARENT;
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
        }
        private void DisableClickThrough()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            exStyle &= ~(WS_EX_LAYERED | WS_EX_TRANSPARENT);
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
        }
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        private void OpenImage_Click(object sender, RoutedEventArgs e)
        {
            var dig = new Microsoft.Win32.OpenFileDialog();
            dig.Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif";

            if (dig.ShowDialog() == true)
            {
                ResetImageState();

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(dig.FileName);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();

                _vm.ImageSource = bitmap;

                NotImageGrid.Visibility = Visibility.Collapsed;
                ImageGrid.Visibility = Visibility.Visible;

                double imageWidth = bitmap.PixelWidth;
                double imageHeight = bitmap.PixelHeight;

                var maxWidth = SystemParameters.PrimaryScreenWidth * 0.8;
                var maxHeight = SystemParameters.PrimaryScreenHeight * 0.8;

                double scale = Math.Min(maxWidth / imageWidth, maxHeight / imageHeight);
                scale = Math.Min(scale, 1.0);

                this.Width = imageWidth * scale;
                this.Height = imageHeight * scale;

                this.SizeToContent = SizeToContent.Manual;

                _vm.ImageSource = bitmap;

                NotImageGrid.Visibility = Visibility.Collapsed;
                ImageGrid.Visibility = Visibility.Visible;

                Dispatcher.BeginInvoke(() =>
                {
                    ImageControl.UpdateLayout();

                    Dispatcher.BeginInvoke(() =>
                    {
                        _vm.Scale = GetMinScale(ImageControl);
                        _vm.OffsetX = 0;
                        _vm.OffsetY = 0;
                        ClampOffset(ImageControl);

                    }, DispatcherPriority.Loaded);

                }, DispatcherPriority.Render);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Image_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                _vm.Opacity += e.Delta > 0 ? 0.05 : -0.05;
                return;
            }

            var image = sender as FrameworkElement;

            var parent = image.Parent as FrameworkElement;
            var pos = e.GetPosition(parent);

            double zoom = e.Delta > 0 ? 1.1 : 1 / 1.1;

            double oldScale = _vm.Scale;
            double newScale = _vm.Scale * zoom;

            double minScale = GetMinScale(image);
            if (newScale < minScale)
                newScale = minScale;

            _vm.Scale = newScale;

            _vm.OffsetX = (_vm.OffsetX - pos.X) * (newScale / oldScale) + pos.X;
            _vm.OffsetY = (_vm.OffsetY - pos.Y) * (newScale / oldScale) + pos.Y;

            ClampOffset(image);
        }

        private double GetMinScale(FrameworkElement image)
        {
            if (_vm.ImageSource == null)
                return 1.0;

            var parent = image.Parent as FrameworkElement;
            if (parent == null)
                return 1.0;

            double viewW = parent.ActualWidth;
            double viewH = parent.ActualHeight;

            if (viewW <= 1 || viewH <= 1)
                return 1.0;

            double imgW = _vm.ImageSource.PixelWidth;
            double imgH = _vm.ImageSource.PixelHeight;

            return Math.Max(viewW / imgW, viewH / imgH);
        }

        private void ClampOffset(FrameworkElement image)
        {
            if (_vm.ImageSource == null)
                return;

            var parent = image.Parent as FrameworkElement;
            if (parent == null)
                return;

            double viewW = parent.ActualWidth;
            double viewH = parent.ActualHeight;

            if (viewW <= 1 || viewH <= 1)
                return;

            double imgW = _vm.ImageSource.PixelWidth * _vm.Scale;
            double imgH = _vm.ImageSource.PixelHeight * _vm.Scale;

            // X方向
            if (imgW <= viewW)
            {
                _vm.OffsetX = (viewW - imgW) / 2;
            }
            else
            {
                double minX = viewW - imgW;
                double maxX = 0;
                _vm.OffsetX = Math.Clamp(_vm.OffsetX, minX, maxX);
            }

            // Y方向
            if (imgH <= viewH)
            {
                _vm.OffsetY = (viewH - imgH) / 2;
            }
            else
            {
                double minY = viewH - imgH;
                double maxY = 0;
                _vm.OffsetY = Math.Clamp(_vm.OffsetY, minY, maxY);
            }
        }

        private void ResetImageState()
        {
            _vm.Scale = 0;
            _vm.OffsetX = 0;
            _vm.OffsetY = 0;
        }
        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_vm.ImageSource == null)
                return;

            _resizeTimer.Stop();
            _resizeTimer.Start();
        }
        private void ResizeTimer_Tick(object sender, EventArgs e)
        {
            _resizeTimer.Stop();

            if (_vm.ImageSource == null)
                return;

            var image = ImageControl;

            double fit = GetFitScale(image);
            double fill = GetMinScale(image);

            if (_vm.Scale > fit)
            {
                _vm.Scale = fit;
                _vm.OffsetX = 0;
                _vm.OffsetY = 0;
            }
            else if (_vm.Scale < fill)
            {
                _vm.Scale = fill;
            }

            ClampOffset(image);
        }
        private double GetFitScale(FrameworkElement image)
        {
            var parent = image.Parent as FrameworkElement;

            double viewW = parent.ActualWidth;
            double viewH = parent.ActualHeight;

            double imgW = _vm.ImageSource.PixelWidth;
            double imgH = _vm.ImageSource.PixelHeight;

            return Math.Min(viewW / imgW, viewH / imgH);
        }
        private void Image_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.MiddleButton == MouseButtonState.Pressed)
            {
                _isPanning = true;
                _lastMousePos = e.GetPosition(ImageCanvas);
                ImageControl.CaptureMouse();
            }
        }

        private void Image_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isPanning) return;

            var pos = e.GetPosition(ImageCanvas);
            var delta = pos - _lastMousePos;

            _lastMousePos = pos;

            _vm.OffsetX += delta.X;
            _vm.OffsetY += delta.Y;

            ClampOffset(ImageControl);
        }

        private void Image_MouseUp(object sender, MouseEventArgs e)
        {
            if (_isPanning)
            {
                _isPanning = false;
                ImageControl.ReleaseMouseCapture();
            }
        }
        private void Help_Click(object sender, RoutedEventArgs e)
        {
            var helpwindow = new HelpWindow();
            helpwindow.Owner = this;
            helpwindow.Show();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Alt) && e.Key == Key.T)
            {
                _isClickThroughEnabled = !_isClickThroughEnabled;
                if (_isClickThroughEnabled)
                {
                    EnableClickThrough();
                }
                else
                {
                    DisableClickThrough();
                }
                MessageBox.Show(_isClickThroughEnabled ? "Click-through enabled." : "Click-through disabled.");

            }
        }
    }
}