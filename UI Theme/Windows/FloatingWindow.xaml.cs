using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace ProjectTactics.Windows
{
    /// <summary>
    /// Reusable floating window with glass effect and animations
    /// </summary>
    public partial class FloatingWindow : Window
    {
        public FloatingWindow(string title, UIElement content)
        {
            InitializeComponent();
            
            TitleText.Text = title;
            WindowContent.Content = content;
            
            // Start with invisible window
            Opacity = 0;
            
            // Trigger fade-in animation after window is loaded
            Loaded += FloatingWindow_Loaded;
        }
        
        private void FloatingWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Fade-in animation
            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            
            BeginAnimation(OpacityProperty, fadeIn);
        }
        
        /// <summary>
        /// Enable window dragging when clicking on title bar
        /// </summary>
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                try
                {
                    DragMove();
                }
                catch
                {
                    // Ignore exceptions from DragMove (happens if called outside mouse down)
                }
            }
        }
        
        /// <summary>
        /// Fade-out animation before closing
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            var fadeOut = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(150),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            
            fadeOut.Completed += (s, args) => Close();
            
            BeginAnimation(OpacityProperty, fadeOut);
        }
    }
}
