using System;

namespace StartRide.Core
{

    public static class BrandingIcons
    {
        private const string ComponentPrefix = "/StartRide;component/";

        public const string BeamNgLogo = ComponentPrefix + "Assets/Branding/beamng_logo_256.png";

        public const string BeamNgLogoSmall = ComponentPrefix + "Assets/Branding/beamng_logo_50x50.png";

        public const string StartRideIcon = ComponentPrefix + "Assets/Branding/startride_icon_256.png";

        public const string BeamNgSvgKey = "beamng/beamng";

        public static string Normalize(string? source, string fallback = BeamNgLogo)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return fallback;
            }

            string value = source!.Trim().Trim('"').Replace('\\', '/');
            if (value.StartsWith(ComponentPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
            if (value.StartsWith("pack://", StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
            if (value.StartsWith("/", StringComparison.Ordinal))
            {
                return ComponentPrefix + value.TrimStart('/');
            }
            if (value.IndexOf(':') > 0)
            {
                return source!.Trim();
            }
            return ComponentPrefix + value;
        }
    }
}
