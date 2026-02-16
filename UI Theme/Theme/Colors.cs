using System.Windows.Media;

namespace ProjectTactics.Theme
{
    /// <summary>
    /// Centralized color palette for the dystopian fantasy UI
    /// </summary>
    public static class Colors
    {
        // Background Colors
        public static readonly Color BackgroundDark = Color.FromArgb(255, 8, 8, 18);
        public static readonly Color BackgroundDarkTransparent = Color.FromArgb(217, 8, 8, 18); // 85% opacity
        public static readonly Color CardBackground = Color.FromArgb(26, 60, 65, 80); // 10% opacity
        public static readonly Color CardBackgroundHover = Color.FromArgb(38, 60, 65, 80); // 15% opacity
        
        // Border Colors
        public static readonly Color BorderSubtle = Color.FromArgb(89, 60, 65, 80); // 35% opacity
        public static readonly Color BorderMedium = Color.FromArgb(128, 60, 65, 80); // 50% opacity
        public static readonly Color BorderBright = Color.FromArgb(179, 60, 65, 80); // 70% opacity
        
        // Text Colors
        public static readonly Color TextPrimary = Color.FromArgb(255, 212, 210, 204); // #D4D2CC
        public static readonly Color TextSecondary = Color.FromArgb(255, 144, 144, 160); // #9090A0
        public static readonly Color TextDim = Color.FromArgb(255, 100, 100, 122); // #64647A
        public static readonly Color TextBright = Color.FromArgb(255, 238, 238, 232); // #EEEEE8
        
        // Accent Colors
        public static readonly Color AccentViolet = Color.FromArgb(255, 139, 92, 246); // #8B5CF6
        public static readonly Color AccentVioletDim = Color.FromArgb(77, 139, 92, 246); // 30% opacity
        
        public static readonly Color AccentEmerald = Color.FromArgb(255, 80, 200, 120); // #50C878
        public static readonly Color AccentEmeraldDim = Color.FromArgb(77, 80, 200, 120); // 30% opacity
        
        public static readonly Color AccentGold = Color.FromArgb(255, 212, 168, 67); // #D4A843
        public static readonly Color AccentGoldDim = Color.FromArgb(77, 212, 168, 67); // 30% opacity
        
        public static readonly Color AccentRuby = Color.FromArgb(255, 200, 80, 80); // #C85050
        public static readonly Color AccentRubyDim = Color.FromArgb(51, 200, 80, 80); // 20% opacity
        
        // Status Colors
        public static readonly Color StatusOnline = Color.FromArgb(255, 80, 200, 120);
        public static readonly Color StatusAway = Color.FromArgb(255, 212, 168, 67);
        public static readonly Color StatusOffline = Color.FromArgb(255, 100, 100, 122);
        
        // Rarity Colors
        public static readonly Color RarityCommon = Color.FromArgb(255, 144, 144, 160);
        public static readonly Color RarityUncommon = Color.FromArgb(255, 80, 200, 120);
        public static readonly Color RarityRare = Color.FromArgb(255, 100, 150, 255);
        public static readonly Color RarityEpic = Color.FromArgb(255, 139, 92, 246);
        public static readonly Color RarityLegendary = Color.FromArgb(255, 212, 168, 67);
        
        // Helper method to create brush from color
        public static SolidColorBrush CreateBrush(Color color)
        {
            return new SolidColorBrush(color);
        }
    }
}
