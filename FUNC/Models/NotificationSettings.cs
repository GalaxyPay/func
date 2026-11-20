using System.Text.Json;

namespace FUNC.Models
{
    // GET / PUT func/notifications.
    public class NotificationSettings
    {
        internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        // Notify when this node's block is added to the chain. Off until the user turns it on.
        public bool Blocks { get; set; }
        // Read-only: whether this platform has a tray / menu-bar helper to show notifications.
        public bool Tray { get; set; }
    }
}
