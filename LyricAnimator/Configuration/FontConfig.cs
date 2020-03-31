namespace LyricAnimator.Configuration
{
    internal sealed class FontConfig
    {
        public string Family { get; set; } = "Open Sans";
        public float Size { get; set; } = 72f;
        public string HexColor { get; set; } = "#ffffff";
        public float LineMargin { get; set; } = 20f;

        public FontConfig(float size, string hexColor)
        {
            Size = size;
            HexColor = hexColor;
        }

        public FontConfig()
        {
        }
    }
}