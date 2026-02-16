using System;
using System.Windows;
using System.Windows.Input;
using ProjectTactics.Controls.CharacterSheet;
using ProjectTactics.Models;
using ProjectTactics.Services;

namespace ProjectTactics.Windows
{
    /// <summary>
    /// Main application window
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly TaskadeClient _taskadeClient;
        
        public MainWindow()
        {
            InitializeComponent();
            _taskadeClient = new TaskadeClient();
        }
        
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }
        
        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }
        
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
        
        private async void OpenCharacterSheet_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Fetch character data from Taskade
                var character = await _taskadeClient.GetCharacterAsync();
                
                if (character == null)
                {
                    // Create sample data if API call fails
                    character = CreateSampleCharacter();
                }
                
                // Create character sheet control and bind data
                var characterSheet = new CharacterSheetControl
                {
                    DataContext = character
                };
                
                // Open in floating window
                var window = new FloatingWindow("Character Sheet", characterSheet);
                window.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading character: {ex.Message}", 
                              "Error", 
                              MessageBoxButton.OK, 
                              MessageBoxImage.Error);
            }
        }
        
        private void OpenInventory_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Inventory system coming soon!", 
                          "Info", 
                          MessageBoxButton.OK, 
                          MessageBoxImage.Information);
        }
        
        private void OpenQuestLog_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Quest log coming soon!", 
                          "Info", 
                          MessageBoxButton.OK, 
                          MessageBoxImage.Information);
        }
        
        private void OpenGuildManagement_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Guild management coming soon!", 
                          "Info", 
                          MessageBoxButton.OK, 
                          MessageBoxImage.Information);
        }
        
        private void ExitGame_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Are you sure you want to exit?", 
                                       "Exit Game", 
                                       MessageBoxButton.YesNo, 
                                       MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                Application.Current.Shutdown();
            }
        }
        
        /// <summary>
        /// Create sample character for demo purposes
        /// </summary>
        private Character CreateSampleCharacter()
        {
            return new Character
            {
                FullName = "Kael Ironbound",
                Race = "Human",
                City = "Ironhaven",
                RankTitle = "Warden of the Northern March",
                Bio = "Born in the frozen wastes beyond the Wall, Kael rose through the ranks of the City Guard through sheer determination and martial prowess. Known for his unyielding defense of the city's northern gates.",
                PortraitUrl = "https://via.placeholder.com/128",
                Strength = 85,
                Speed = 72,
                Agility = 68,
                Endurance = 90,
                Stamina = 78,
                Ether = 45
            };
        }
    }
}
