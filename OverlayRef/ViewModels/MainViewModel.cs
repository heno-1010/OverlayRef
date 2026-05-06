using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Windows.Media.Imaging;

namespace OverlayRef.ViewModels
{
    class MainViewModel : INotifyPropertyChanged
    {
        private BitmapImage _imageSource;
        private double _scale = 1.0;

        public BitmapImage ImageSource
        {
            get => _imageSource;
            set
            {
                _imageSource = value;
                OnPropertyChanged(nameof(ImageSource));
            }
        }
        public double Scale
        {
            get => _scale;
            set
            {
                _scale = value;
                OnPropertyChanged(nameof(Scale));
            }
        }
        private double _offsetX;
        public double OffsetX
        {
            get => _offsetX;
            set
            {
                _offsetX = value;
                OnPropertyChanged(nameof(OffsetX));
            }
        }

        private double _offsetY;
        public double OffsetY
        {
            get => _offsetY;
            set
            {
                _offsetY = value;
                OnPropertyChanged(nameof(OffsetY));
            }
        }
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
