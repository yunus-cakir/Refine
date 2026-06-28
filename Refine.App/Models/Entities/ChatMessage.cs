using SQLite;
using SQLiteNetExtensions.Attributes;

namespace Refine.App.Models.Entities;

public class ChatMessage
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [ForeignKey(typeof(ChatSession))]
    public int ChatSessionId { get; set; }

    /// <summary>
    /// Role can be 'user', 'model', or 'system'
    /// </summary>
    public string Role { get; set; } = "user";
    
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ManyToOne]
    public ChatSession? Session { get; set; }
}
