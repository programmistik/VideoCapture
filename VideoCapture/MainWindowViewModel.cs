using Accord.Video.FFMPEG;
using AForge.Video;
using AForge.Video.DirectShow;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.CommandWpf;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace VideoCapture
{
    class MainWindowViewModel : ViewModelBase, IDisposable
    {
        public ObservableCollection<FilterInfo> VideoDevices { get; set; }

        private BitmapImage image;
        public BitmapImage Image { get => image; set => Set(ref image, value); }

        private bool isDesktopSource;
        public bool IsDesktopSource { get => isDesktopSource; set => Set(ref isDesktopSource, value); }

        private bool isWebcamSource;
        public bool IsWebcamSource { get => isWebcamSource; set => Set(ref isWebcamSource, value); }

        private IVideoSource videoSource;
        private VideoFileWriter writer;

        private FilterInfo currentDevice;
        public FilterInfo CurrentDevice { get => currentDevice; set => Set(ref currentDevice, value); }

        private bool recording;
        private DateTime? firstFrameTime;

        public MainWindowViewModel()
        {
            VideoDevices = new ObservableCollection<FilterInfo>();
            GetVideoDevices();
            IsDesktopSource = true;
        }

        public void Dispose()
        {
            if (videoSource != null && videoSource.IsRunning)
                videoSource.SignalToStop();

            writer?.Dispose();
        }
        private void SaveSnapshot()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog();
            dialog.FileName = "Snapshot1";
            dialog.DefaultExt = ".png";
            var dialogResult = dialog.ShowDialog();
            if (dialogResult != true)
            {
                return;
            }
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(Image));
            using (var fileStream = new FileStream(dialog.FileName, FileMode.Create))
            //var FileName = "G:\\Snapshot.png";
            //using (var fileStream = new FileStream(FileName, FileMode.Create))
            {
                encoder.Save(fileStream);
            }
        }

        private void StartRecording()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog();
            dialog.FileName = "Video1";
            dialog.DefaultExt = ".avi";
            dialog.AddExtension = true;
            var dialogresult = dialog.ShowDialog();
            if (dialogresult != true)
            {
                return;
            }
            firstFrameTime = null;
            writer = new VideoFileWriter();
            writer.Open(dialog.FileName, (int)Math.Round(Image.Width, 0), (int)Math.Round(Image.Height, 0));
            recording = true;
        }

        private void GetVideoDevices()
        {
            var devices = new FilterInfoCollection(FilterCategory.VideoInputDevice);

            foreach (FilterInfo device in devices)
                VideoDevices.Add(device);

            if (VideoDevices.Any())
                CurrentDevice = VideoDevices[0];
            else
                System.Windows.MessageBox.Show("No webcam found");
        }

        private void video_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            try
            {
                if (recording)
                {
                    using (var bitmap = (Bitmap)eventArgs.Frame.Clone())
                    {
                        if (firstFrameTime != null)
                            writer.WriteVideoFrame(bitmap, DateTime.Now - firstFrameTime.Value);
                        else
                        {
                            writer.WriteVideoFrame(bitmap);
                            firstFrameTime = DateTime.Now;
                        }
                    }
                }
                using (var bitmap = (Bitmap)eventArgs.Frame.Clone())
                {
                    var bi = new BitmapImage();
                    bi.BeginInit();
                    var ms = new MemoryStream();
                    bitmap.Save(ms, ImageFormat.Bmp);
                    ms.Seek(0, SeekOrigin.Begin);
                    bi.StreamSource = ms;
                    bi.EndInit();

                    //var bi = bitmap.ToBitmapImage();
                    bi.Freeze();
                    Dispatcher.CurrentDispatcher.Invoke(() => Image = bi);
                }
            }
            catch (Exception exc)
            {
                System.Windows.MessageBox.Show("Error on:\n" + exc.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StopCamera();
            }
        }

        private void StopCamera()
        {
            if (videoSource != null && videoSource.IsRunning)
            {
                videoSource.SignalToStop();
                videoSource.NewFrame -= video_NewFrame;
            }
            Image = null;
        }        

        private RelayCommand startVideoCommand;
        public RelayCommand StartVideoCommand
        {
            get => startVideoCommand ?? (startVideoCommand = new RelayCommand(
                () =>
                {
                    if (IsDesktopSource)
                    {
                        var rectangle = new Rectangle();
                        foreach (var screen in Screen.AllScreens)
                        {
                            rectangle = Rectangle.Union(rectangle, screen.Bounds);
                        }
                        videoSource = new ScreenCaptureStream(rectangle);
                        videoSource.NewFrame += video_NewFrame;
                        videoSource.Start();
                    }
                    else if (IsWebcamSource)
                    {
                        if (CurrentDevice != null)
                        {
                            videoSource = new VideoCaptureDevice(CurrentDevice.MonikerString);
                            videoSource.NewFrame += video_NewFrame;
                            videoSource.Start();
                        }
                        else
                            System.Windows.MessageBox.Show("Current device can't be null");

                    }
                }
            ));
        }

        private RelayCommand saveSnapshotCommand;
        public RelayCommand SaveSnapshotCommand
        {
            get => saveSnapshotCommand ?? (saveSnapshotCommand = new RelayCommand(
                () =>
                {
                    SaveSnapshot();
                }
            ));
        }

        private RelayCommand startRecordingCommand;
        public RelayCommand StartRecordingCommand
        {
            get => startRecordingCommand ?? (startRecordingCommand = new RelayCommand(
                () =>
                {
                    var dialog = new Microsoft.Win32.SaveFileDialog();
                    dialog.FileName = "Video1";
                    dialog.DefaultExt = ".avi";
                    dialog.AddExtension = true;
                    var dialogresult = dialog.ShowDialog();
                    if (dialogresult != true)
                    {
                        return;
                    }
                    firstFrameTime = null;
                    writer = new VideoFileWriter();
                    writer.Open(dialog.FileName, (int)Math.Round(Image.Width, 0), (int)Math.Round(Image.Height, 0));
                    recording = true;
                }
            ));
        }

        private RelayCommand stopRecordingCommand;
        public RelayCommand StopRecordingCommand
        {
            get => stopRecordingCommand ?? (stopRecordingCommand = new RelayCommand(
                () =>
                {
                    recording = false;
                    writer.Close();
                    writer.Dispose();
                }
            ));
        }


    }
}
