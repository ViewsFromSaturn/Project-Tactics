using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ProjectTactics.Models
{
    /// <summary>
    /// Item rarity levels
    /// </summary>
    public enum ItemRarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary
    }
    
    /// <summary>
    /// Item categories
    /// </summary>
    public enum ItemCategory
    {
        Weapon,
        Armor,
        Accessory,
        Consumable,
        Material,
        Quest
    }
    
    /// <summary>
    /// Represents an inventory item
    /// </summary>
    public class Item : INotifyPropertyChanged
    {
        private string _id = string.Empty;
        private string _name = string.Empty;
        private string _description = string.Empty;
        private ItemRarity _rarity;
        private ItemCategory _category;
        private string _iconUrl = string.Empty;
        private int _quantity;
        private int _level;
        private bool _isEquipped;
        
        public string Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }
        
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }
        
        public string Description
        {
            get => _description;
            set { _description = value; OnPropertyChanged(); }
        }
        
        public ItemRarity Rarity
        {
            get => _rarity;
            set { _rarity = value; OnPropertyChanged(); OnPropertyChanged(nameof(RarityColor)); }
        }
        
        public ItemCategory Category
        {
            get => _category;
            set { _category = value; OnPropertyChanged(); }
        }
        
        public string IconUrl
        {
            get => _iconUrl;
            set { _iconUrl = value; OnPropertyChanged(); }
        }
        
        public int Quantity
        {
            get => _quantity;
            set { _quantity = value; OnPropertyChanged(); }
        }
        
        public int Level
        {
            get => _level;
            set { _level = value; OnPropertyChanged(); }
        }
        
        public bool IsEquipped
        {
            get => _isEquipped;
            set { _isEquipped = value; OnPropertyChanged(); }
        }
        
        /// <summary>
        /// Gets the color associated with the item's rarity
        /// </summary>
        public string RarityColor
        {
            get
            {
                return Rarity switch
                {
                    ItemRarity.Common => "#9090A0",
                    ItemRarity.Uncommon => "#50C878",
                    ItemRarity.Rare => "#6496FF",
                    ItemRarity.Epic => "#8B5CF6",
                    ItemRarity.Legendary => "#D4A843",
                    _ => "#9090A0"
                };
            }
        }
        
        public event PropertyChangedEventHandler? PropertyChanged;
        
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
