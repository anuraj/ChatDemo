using System.Collections.Generic;

namespace ChatDemo.ViewModels
{
    public class ChatRequest
    {
        public string? ConversationId { get; set; }
        public Message? Message { get; set; }
    }
}
