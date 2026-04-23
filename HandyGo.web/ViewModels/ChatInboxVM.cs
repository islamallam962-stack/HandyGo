namespace HandyGo.web.ViewModels
{
    public class ChatInboxVM
    {
        public int RequestId { get; set; }
        public string OtherUserName { get; set; }
        public string LastMessage { get; set; }
        public DateTime LastMessageTime { get; set; }
        public string Status { get; set; }

        public string OtherUserImage { get; set; }

        public int OtherUserId { get; set; }
        public int UnseenCount { get; set; }
    }

}

