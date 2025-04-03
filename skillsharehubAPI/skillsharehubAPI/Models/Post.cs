namespace skillsharehubAPI.Models
{
    public class Post
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Content { get; set; }
        public string MediaUrl { get; set; }
        public string MediaType { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}

