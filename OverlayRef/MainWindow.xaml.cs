using OverlayRef.ViewModels;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace OverlayRef
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private MainViewModel _vm;

        public MainWindow()
        {
            InitializeComponent();
            _vm = new MainViewModel();
            DataContext = _vm;
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if(e.ButtonState == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }
        private void OpenImage_Click(object sender, RoutedEventArgs e)
        {
            var dig = new Microsoft.Win32.OpenFileDialog();
            dig.Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif";
            if(dig.ShowDialog() == true)
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(dig.FileName);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();

                _vm.ImageSource = bitmap;

                var maxWidth = SystemParameters.PrimaryScreenWidth * 0.8;
                var maxHeight = SystemParameters.PrimaryScreenHeight * 0.8;

                double imageWidth = bitmap.PixelWidth;
                double imageHeight = bitmap.PixelHeight;

                double scale = Math.Min(maxWidth / imageWidth, maxHeight / imageHeight);
                scale = Math.Min(scale, 1.0);

                this.Width = imageWidth * scale;
                this.Height = imageHeight * scale;

                this.SizeToContent = SizeToContent.Manual;

                NotImageGrid.Visibility = Visibility.Collapsed;
                ImageGrid.Visibility = Visibility.Visible;
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}