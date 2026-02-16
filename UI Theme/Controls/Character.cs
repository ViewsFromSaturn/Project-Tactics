using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ProjectTactics.Models
{
    /// <summary>
    /// Represents a player character with stats, bio, and identity
    /// </summary>
    public class Character : INotifyPropertyChanged
    {
        private string _id = string.Empty;
        private string _fullName = string.Empty;
        private string _race = string.Empty;
        private string _city = string.Empty;
        private string _rankTitle = string.Empty;
        private string _bio = string.Empty;
        private string _portraitUrl = string.Empty;
        
        // Training Stats
        private int _strength;
        private int _speed;
        private int _agility;
        private int _endurance;
        private int _stamina;
        private int _ether;
        
        // Identity
        public string Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }
        
        public string FullName
        {
            get => _fullName;
            set { _fullName = value; OnPropertyChanged(); }
        }
        
        public string Race
        {
            get => _race;
            set { _race = value; OnPropertyChanged(); }
        }
        
        public string City
        {
            get => _city;
            set { _city = value; OnPropertyChanged(); }
        }
        
        public string RankTitle
        {
            get => _rankTitle;
            set { _rankTitle = value; OnPropertyChanged(); }
        }
        
        public string Bio
        {
            get => _bio;
            set { _bio = value; OnPropertyChanged(); }
        }
        
        public string PortraitUrl
        {
            get => _portraitUrl;
            set { _portraitUrl = value; OnPropertyChanged(); }
        }
        
        // Training Stats
        public int Strength
        {
            get => _strength;
            set { _strength = value; OnPropertyChanged(); }
        }
        
        public int Speed
        {
            get => _speed;
            set { _speed = value; OnPropertyChanged(); }
        }
        
        public int Agility
        {
            get => _agility;
            set { _agility = value; OnPropertyChanged(); }
        }
        
        public int Endurance
        {
            get => _endurance;
            set { _endurance = value; OnPropertyChanged(); }
        }
        
        public int Stamina
        {
            get => _stamina;
            set { _stamina = value; OnPropertyChanged(); }
        }
        
        public int Ether
        {
            get => _ether;
            set { _ether = value; OnPropertyChanged(); }
        }
        
        public event PropertyChangedEventHandler? PropertyChanged;
        
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
