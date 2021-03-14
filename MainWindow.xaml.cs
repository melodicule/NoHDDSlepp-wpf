using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.IO;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Threading;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Rectangle = System.Windows.Shapes.Rectangle;
using System.Windows.Media.Animation;

namespace NoHDDSleep
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        #region 属性变更通知
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        #endregion

        private const string NAME = "NoSleep.empty";
        private const int BYTE_PER_MB = 1024 * 1024;
        private const int BLOCKS = 144;
        private const double CANVAS_HEIGHT = 128;
        private const double CANVAS_WIDTH = 128;

        DispatcherTimer timer = new DispatcherTimer();
        FileStream stream;
        Rectangle[] rectangles = new Rectangle[BLOCKS];
        Random random = new Random();
        byte[] buffer = new byte[BYTE_PER_MB];
        string[] symbols;
        int defaultIndex = 0;
        public int SelectedDriver 
        {
            get => defaultIndex;
            set
            {
                defaultIndex = value;
                OnPropertyChanged();
            }
        }
        ColorAnimationUsingKeyFrames write;
        ColorAnimationUsingKeyFrames read;

        public MainWindow()
        {
            InitializeComponent();
            int blocksPerRow = (int)Math.Sqrt(BLOCKS);
            int rows = (int)Math.Ceiling(BLOCKS * 1.0 / blocksPerRow);

            for (int i = 0; i < BLOCKS; i++)
            {
                rectangles[i] = new Rectangle()
                {
                    Width = CANVAS_WIDTH / blocksPerRow,
                    Height = CANVAS_HEIGHT / rows,
                    Stroke = Brushes.Transparent,
                    StrokeThickness = 0.5,
                    Fill = new SolidColorBrush(Colors.WhiteSmoke)
                };
                canvas.Children.Add(rectangles[i]);
                rectangles[i].SetValue(Canvas.LeftProperty, (i % blocksPerRow) * (CANVAS_WIDTH / blocksPerRow));
                rectangles[i].SetValue(Canvas.TopProperty, (i / blocksPerRow) * (CANVAS_HEIGHT / rows));
            }

            #region 创建关键帧动画
            CubicEase easeOut = new CubicEase();
            CubicEase easeIn = new CubicEase();
            easeOut.EasingMode = EasingMode.EaseOut;
            easeIn.EasingMode = EasingMode.EaseIn;

            write = new ColorAnimationUsingKeyFrames();
            EasingColorKeyFrame frame11 = new EasingColorKeyFrame()
            {
                KeyTime = KeyTime.FromPercent(0.2),
                Value = Colors.Orange,
                EasingFunction = easeIn
            };
            EasingColorKeyFrame frame12 = new EasingColorKeyFrame()
            {
                KeyTime = KeyTime.FromPercent(1.0),
                Value = Colors.WhiteSmoke,
                EasingFunction = easeOut
            };
            write.KeyFrames.Add(frame11);
            write.KeyFrames.Add(frame12);
            read = new ColorAnimationUsingKeyFrames();
            EasingColorKeyFrame frame21 = new EasingColorKeyFrame()
            {
                KeyTime = KeyTime.FromPercent(0.2),
                Value = Colors.LimeGreen,
                EasingFunction = easeIn
            };
            EasingColorKeyFrame frame22 = new EasingColorKeyFrame()
            {
                KeyTime = KeyTime.FromPercent(1.0),
                Value = Colors.WhiteSmoke,
                EasingFunction = easeOut
            };
            read.KeyFrames.Add(frame21);
            read.KeyFrames.Add(frame22);

            write.Duration = TimeSpan.FromSeconds(2);
            read.Duration = TimeSpan.FromSeconds(2);
            #endregion
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //获取所有盘符
            var drivers = DriveInfo.GetDrives();
            symbols = new string[drivers.Length];
            for (int i = 0; i < drivers.Length; i++)
            {
                symbols[i] = drivers[i].Name;
                if (symbols[i].StartsWith("E"))
                {
                    defaultIndex = i;
                }
            }
            driversList.ItemsSource = symbols;
            OnPropertyChanged("SelectedDriver");

            //创建文件
            try
            {
                stream = new FileStream(Path.Combine(symbols[SelectedDriver], NAME), FileMode.Create);
                //设置基本参数
                timer.Tick += Timer_Tick;
                timer.Interval = TimeSpan.FromSeconds(0.5);

                await Task.Run(() =>
                {
                    for (int i = 0; i < BLOCKS; i++)
                    {
                        stream.Write(buffer, 0, buffer.Length);
                    }

                    Dispatcher.Invoke(() =>
                    {
                        timer.Start();
                    });
                });
                //开始
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (stream != null)
            {
                int pos = random.Next(0, BLOCKS);
                stream.Seek(pos * BYTE_PER_MB, SeekOrigin.Begin);
                if (random.NextDouble() < 0.5)
                {
                    stream.ReadAsync(buffer, 0, BYTE_PER_MB);
                    rectangles[pos].Fill.BeginAnimation(SolidColorBrush.ColorProperty, read);
                }
                else
                {
                    stream.WriteAsync(buffer, 0, BYTE_PER_MB);
                    rectangles[pos].Fill.BeginAnimation(SolidColorBrush.ColorProperty, write);
                }

            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Button bt = sender as Button;
            bt.Visibility = Visibility.Collapsed;
            if (bt.Tag.ToString() == "开始")
            {
                timer.IsEnabled = true;
                btPause.Visibility = Visibility.Visible;
            }
            else
            {
                timer.IsEnabled = false;
                btStart.Visibility = Visibility.Visible;
            }
        }

        private void driversList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            timer.Stop();
            if (stream != null && stream.CanWrite)
            {
                stream.Dispose();
                stream = null;
                stream = new FileStream(Path.Combine(symbols[SelectedDriver], NAME), FileMode.Create);
                Task.Run(() =>
                {
                    for (int i = 0; i < BLOCKS; i++)
                    {
                        stream.Write(buffer, 0, buffer.Length);
                    }

                    Dispatcher.Invoke(() =>
                    {
                        timer.Start();
                    });
                });
            }
            //创建文件
            
            
        }

        private void Win_Closing(object sender, CancelEventArgs e)
        {
            if (timer != null)
            {
                timer.Stop();
            }
            if (stream != null && stream.CanWrite)
            {
                stream.Dispose();
                try
                {
                    File.Delete(Path.Combine(symbols[SelectedDriver], NAME));
                }
                catch (Exception)
                {

                }
            }
        }
    }
}
