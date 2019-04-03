using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MLDotNetUWP
{
    /// <summary>
    /// The XAML will display a green background for a Status message, and a red background for an ErrorMessage,
    /// blue/violet for a warning message.  ClearMessage type clears the Status box and sets it to green.
    /// </summary>
    public enum NotifyType
    {
        StatusMessage,
        WarningMessage,
        ErrorMessage,
        ClearMessage
    };

    /// <summary>
    /// For the Prism EventAggregator, MVVMLight Messenger, PubSub or other messaging system.
    /// </summary>
    public class NotifyUserEventMessage
    {
        public string MessagePayload { get; set; }
        public NotifyType MessageType { get; set; }
    }
}
