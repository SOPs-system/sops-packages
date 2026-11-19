namespace Sops.Device.Models
{
    /// <summary>
    /// Operating system platform detected at runtime.
    /// </summary>
    public enum DevicePlatform
    {
        /// <summary>Unknown or unsupported platform.</summary>
        Unknown = 0,

        /// <summary>Microsoft Windows (7 SP1 through 11, Server).</summary>
        Windows = 1,

        /// <summary>Linux distributions.</summary>
        Linux = 2,

        /// <summary>Apple macOS.</summary>
        MacOS = 3,

        /// <summary>Google Android.</summary>
        Android = 4,

        /// <summary>Apple iOS.</summary>
        IOS = 5,

        /// <summary>Apple Mac Catalyst.</summary>
        MacCatalyst = 6
    }
}
