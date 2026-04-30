using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using RetroVU.Services;
using System.IO;
using System.Text.Json;
using IoPath = System.IO.Path;

namespace RetroVU.Views;

public partial class MainWindow : Window
{
    private AudioAnalyzer _analyzer; // 声明我们刚刚写的分析器
    
    private DispatcherTimer _timer;
    private double _currentDb = -20;
    private double _targetDb = -20;
    private double _currentAngle = -45.0; // 改为记录指针实际的角度
    private double _rawVU = -20.0;
    //private Random _random = new Random();

    private double _gainOffset = 26.0;
    private readonly string _settingFilePath;
    
    private DateTime _lastAudioTime = DateTime.Now;

    // === 新增物理模拟变量 ===
    private double _velocity = 0;// 表针当前的运动速度
    private double _stiffness = 0.29; //刚度（弹簧拉力）：值越大响应越快，(范围 0.05 - 0.3)
    private double _friction = 0.42;  // 阻尼（摩擦力）：值越大停下得越快。 (范围 0.1 - 0.5)
    
    
    public MainWindow()
    {
        
        InitializeComponent();

        string appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string appFolder = IoPath.Combine(appDataFolder, "RetroVU");

        if (!Directory.Exists(appFolder))  // 检查并创建配置文件夹（万一这是用户第一次运行程序还没文件夹的话）
        {
            Directory.CreateDirectory(appFolder);
        }
        
        _settingFilePath = IoPath.Combine(appFolder, "settings.json");

        LoadSettings();
        
        DrawScale();

        // ======= 1. 启动音频监听链路 =======
        _analyzer = new AudioAnalyzer(); // 1. 初始化 Analyzer
        _analyzer.AudioDataAvailable += OnAudioDataAvailable;
        _analyzer.StartCapturing();

        StartSimulation();
    }

    private void LoadSettings()
    {
        try
        {
            if (File.Exists(_settingFilePath)) // 读取 JSON 文件文本
            {
                string json = File.ReadAllText(_settingFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json); // 把字符串解析成 AppSettings 对象
                if (settings != null)
                {
                    _gainOffset = settings.GainOffset; //把读取到的值赋给内部增益
                }

                Console.WriteLine($"LoadSuccess!{_gainOffset}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"LoadError:{ex.Message}");
        }
    }

    private void SaveSettings()
    {
        try
        {
            var settings = new AppSettings { GainOffset = _gainOffset };
            string json = JsonSerializer.Serialize(settings);
            
            File.WriteAllText(_settingFilePath,json);

            Console.WriteLine($"SaveSuccess!{_gainOffset}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SaveError:{ex.Message}");
            
        }
    }

    //当关闭窗口时，切断系统麦克风/内录流，防止内存泄漏
    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _analyzer?.StopCapturing();
        _analyzer?.Dispose();
    }

    // ======= 核心处理：把音频信号变作指针运动 =======
    private void OnAudioDataAvailable(object? sender, AudioDateEventArgs e)
    {
        //提取在 AudioAnalyzer 里算好的 均方(Power)，此时对它开根号，转成 RMS (均方根)振幅
        double rms = Math.Sqrt(e.Power);
        
        // 将振幅转换为对数分贝 dBFS 
        // 声音在程序里的原本值是在 0.0 到 1.0 之间，用常用公式 20*log10(rms) 算出分贝数
        double db = 20 * Math.Log10(rms);
        if(double.IsInfinity(db) || double.IsNaN(db))
        {
            db = -60; // 极度安静时的托底值，防止计算出错
        }
        
        // 校准偏差 (Calibration Offset)
        // 电脑内最高分贝是 0 dBFS，通常音乐都在 -10dBFS 均值徘徊。
        //double gainOffest = 26;
        
        double vu = db + _gainOffset; 
        
        // 将结果限制在表盘允许的最小值到最大值范围内 (-20 到 +3)
        vu = Math.Clamp(vu, -20.0, 3.0);

        _rawVU = vu;
        _lastAudioTime = DateTime.Now;// 每次收到声音，就刷新时间戳

        //_targetDb = vu;
        
        
    }
    
    private void InputElement_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            this.BeginMoveDrag(e);
        }
    }
    
    private void PinButton_OnClick(object? sender, RoutedEventArgs e)
    {
        this.Topmost = !this.Topmost;

        if (this.Topmost)
        {
            
            PinButtonPathIcon.Foreground = SolidColorBrush.Parse("#382b33");
            PinButton.Foreground = SolidColorBrush.Parse("#222325");
            PinButton.Opacity = 1.0;
        }
        else
        {
            PinButtonPathIcon.Foreground = SolidColorBrush.Parse("#666666");
            PinButton.Foreground = SolidColorBrush.Parse("#666666");// 取消置顶时恢复灰色
            PinButton.Opacity = 0.5;
        }
    }
    
    private void CloseButton_OnClick(object? sender, RoutedEventArgs e)
    {
        this.Close();
    }
    
    // ==========================================
    // 动画与物理模拟逻辑
    // ==========================================
    
    // =======动画与UI渲染渲染 (在主线程) =======
    private void StartSimulation()
    {
        _timer = new DispatcherTimer()
        {
            // 设定刷新率为 16 毫秒（约每秒 60 帧）
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _timer.Tick += OnTimerTick;
        _timer.Start();
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if ((DateTime.Now - _lastAudioTime).TotalMilliseconds > 100)// 如果超过 150 毫秒没有收到新的音频数据包，说明系统音频已经断流（静音）
        {
            _rawVU -= 0.23;
            if (_rawVU < -20) _rawVU = -20;
            //_targetDb = -20;
        }

        if (_targetDb > _rawVU)
        {
            _targetDb -= 1.0;
            if(_targetDb < _rawVU) _targetDb = _rawVU;
        }
        else
        {
            _targetDb = _rawVU;
        }

        //物理模拟 (二阶弹簧阻尼系统)
        // a. 计算弹簧拉力 (目标 dB 和 当前 dB 的差值即为拉伸距离，乘上刚度即为拉力)
        double force = (_targetDb - _currentDb) * _stiffness;
        // b. 根据牛顿第二定律（假设质量为1），拉力转化为加速度累加到速度上
        _velocity += force;
        // c. 空气与机械摩擦力阻尼会消耗速度
        _velocity *= (1 - _friction);
        // d. 速度改变指针当前位置
        _currentDb += _velocity;
        
        
        
        
        // 2. 平滑插值 (Easing)：让物理指针有一个“追赶”目标值的过程，而不是瞬间移动
        //double damping = 0.1;// 阻尼系数：越小感觉指针越重，越大响应越快,（0.05 极慢，0.5 极快）
        /*double attackDamping = 0.15; // 弹起速度（较大 = 快，灵敏）
        double releaseDamping = 0.02; // 回落速度（较小 = 带有惯性的平滑回落）
        
        
        if (_targetDb > _currentDb)
        {
            _currentDb += (_targetDb - _currentDb) * attackDamping; // 当来了一个强音，迅速追随（Attack）
        }
        else
        {
            _currentDb += (_targetDb - _currentDb) * releaseDamping;// 当声音渐渐消失，缓慢优雅地回落（Release）
        }*/

        // _currentDb += (_targetDb - _currentDb) * damping;

        double disPlayDb = Math.Clamp(_currentDb, -20.0, 3.0);
        
        // 将dB值映射到表盘的角度
        double targetAngle = DbToAngle(disPlayDb);
        // 旋转指针容器
        // 注意：这里需要确保 XAML 中 NeedleCanvas 的 RenderTransformOrigin 是 "50%,100%"
        NeedleCanvas.RenderTransform = new RotateTransform(targetAngle);
        
        // 更新下方的数字读数
        if (_currentDb >= 3.0)
        {
            // 超过最大量程，显示 CLIP 警告，并把字变成红色
            ReadoutText.Text = "CLIP";
            ReadoutText.Foreground = SolidColorBrush.Parse("#ff3333"); 
        }
        else if (_currentDb <= -19.5)
        {
            // 极低音量显示 -∞ dB，颜色恢复深灰
            ReadoutText.Text = "-∞ dB";
            ReadoutText.Foreground = SolidColorBrush.Parse("#666666"); 
        }
        else
        {
            // 正常显示数字，颜色恢复深灰
            ReadoutText.Text = $"{_currentDb:F1} dB"; 
            ReadoutText.Foreground = SolidColorBrush.Parse("#666666"); 
        }
        
        // 6. 控制 PEAK 指示灯
        if (disPlayDb >= -0.5)
        {
            // 超过 0 VU，点亮红灯
            PeakLight.Fill = SolidColorBrush.Parse("#ff3333");
        }
        else
        {
            // 安全区间，恢复暗红
            PeakLight.Fill = SolidColorBrush.Parse("#5a0000"); 
        }
    }

    
    //将非线性的DB刻度转换为对应的旋转角度
    
    private double DbToAngle(double db)
    {
        // 处理边界值情况
        if (db <= -20) return -45.0;
        if (db >= 3) return 45.0;

        // 这里是原先定义的非线性映射表
        var vuScale = new[]
        {
            new { vu = -20.0, angle = -45.0 },
            new { vu = -10.0, angle = -20.0 },
            new { vu = -7.0, angle = -5.0 },
            new { vu = -5.0, angle = 5.0 },
            new { vu = -3.0, angle = 15.0 },
            new { vu = -1.0, angle = 25.0 },
            new { vu = 0.0, angle = 30.0 },
            new { vu = 1.0, angle = 35.0 },
            new { vu = 2.0, angle = 40.0 },
            new { vu = 3.0, angle = 45.0 }
        };
        
        // 寻找当前值属于哪两个区间之间，并进行线性插值
        for (int i = 0; i < vuScale.Length - 1; i++)
        {
            if (db >= vuScale[i].vu && db <= vuScale[i + 1].vu)
            {
                double fraction = (db - vuScale[i].vu) / (vuScale[i + 1].vu - vuScale[i].vu);
                return vuScale[i].angle + fraction * (vuScale[i + 1].angle - vuScale[i].angle);
            }
        }
        return -45.0;
    }


    private void DrawScale()
    {
        // 表盘的圆心和半径
        double cx = 160;
        double cy = 240;
        double r = 160;

        var vuScale = new[]
        {
            //预先定义好的经典 VU 表非线性刻度和对应的角度
            new { vu = -20, angle = -45.0 },
            new { vu = -10, angle = -20.0 },
            new { vu = -7,  angle = -5.0 },
            new { vu = -5,  angle =  5.0 },
            new { vu = -3,  angle =  15.0 },
            new { vu = -1,  angle =  25.0 },
            new { vu = 0,   angle =  30.0 },
            new { vu = 1,   angle =  35.0 },
            new { vu = 2,   angle =  40.0 },
            new { vu = 3,   angle =  45.0 },
        };

        // ==========================================
        // 画主刻度线和刻度文字
        // ==========================================
        foreach (var mark in vuScale)
        {
            // 大于等于 0 VU 的是警戒红区
            bool isRed = mark.vu >= 0;
            var brush = isRed ? Brushes.Crimson : Brushes.DimGray;
            
            // 角度转为弧度
            double rad = (mark.angle - 90) * Math.PI / 180.0;
            
            // 线条长度：部分主要刻度画长一点(10)，次要的画短一点(6)
            double length = (mark.vu == -20 || mark.vu == -10 || mark.vu == 0 || mark.vu == 3) ? 10 : 6;
            
            double x1 = cx + r * Math.Cos(rad);
            double y1 = cy + r * Math.Sin(rad);
                
            double x2 = cx + (r - length) * Math.Cos(rad);
            double y2 = cy + (r - length) * Math.Sin(rad);
            
            var line = new Line
            {
                StartPoint = new Point(x1, y1),
                EndPoint = new Point(x2, y2),
                Stroke = brush,
                StrokeThickness = mark.vu == 0 ? 3 : 2  // 0 VU 加粗
            };
            
            ScaleCanvas.Children.Add(line);

            // 画刻度文字
            int[] textMarks = { -20, -10, -7, -5, -3, 0, 3 };
            if (Array.IndexOf(textMarks, mark.vu) >= 0 )
            {
                var text = new TextBlock
                {
                    Text = mark.vu > 0 ? $"+{mark.vu}" : mark.vu.ToString(),
                    Foreground = brush,
                    FontSize = 11,
                    FontWeight = FontWeight.Bold,
                    FontFamily = new FontFamily("Consolas")
                };
    
                double textR = r - 24;
                double tx = cx + textR * Math.Cos(rad);
                double ty = cy + textR * Math.Sin(rad);
                
                Canvas.SetLeft(text, tx - 8);
                Canvas.SetTop(text, ty - 8);

                ScaleCanvas.Children.Add(text);
            }
        } 

        
        // 单独画中间的细刻度线
        
        for (int i = 0; i < vuScale.Length - 1; i++)
        {
            var start = vuScale[i];
            var end = vuScale[i + 1];
            
            int dbDiff = (int)(end.vu - start.vu);
            int steps = dbDiff;

            if (dbDiff == 10) 
            {
                steps = 5;
            }

            for (int j = 1; j < steps; j++)
            {
                double fraction = (double)j / steps;
                
                double currentAngle = start.angle + (end.angle - start.angle) * fraction;

                bool isRed = start.vu >= 0;
                var brush = isRed ? Brushes.Crimson : Brushes.DimGray;
                
                double rad = (currentAngle - 90) * Math.PI / 180.0;

                // 使用一个独立变量来控制细线的长度
                double subLineLength = 4; 
                
                double x1 = cx + r * Math.Cos(rad);
                double y1 = cy + r * Math.Sin(rad);
                
                // 这里用上面定义的 subLineLength，而非外部大循环的 length
                double x2 = cx + (r - subLineLength) * Math.Cos(rad);
                double y2 = cy + (r - subLineLength) * Math.Sin(rad);

                var subLine = new Line
                {
                    StartPoint = new Point(x1, y1),
                    EndPoint = new Point(x2, y2),
                    Stroke = brush,
                    StrokeThickness = 1 // 更细
                };
            
                ScaleCanvas.Children.Add(subLine);
            }
        }
    }


    public void IncreaseGainButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _gainOffset += 2.0;
        if (_gainOffset > 45) _gainOffset = 45;

        Console.WriteLine($"Increased:{_gainOffset}");
        
        SaveSettings();

    }

    public void DecreaseGainButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _gainOffset -= 2.0;
        
        if(_gainOffset < 0) _gainOffset = 0;

        Console.WriteLine($"Decreased:{_gainOffset}");
        
        SaveSettings();

    }


    private void ResetButton_Onclick(object? sender, RoutedEventArgs e)
    {
        _gainOffset = 26.0;
        Console.WriteLine($"Reset:{_gainOffset}");
        SaveSettings();
    }
}

public class AppSettings
{
    public double GainOffset { get; set; } = 26.0;

}