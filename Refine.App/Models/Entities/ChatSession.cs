using SQLite;
using SQLiteNetExtensions.Attributes;

namespace Refine.App.Models.Entities;

public class ChatSession
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string Title { get; set; } = "New Chat";
    public bool IsPinned { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [OneToMany(CascadeOperations = CascadeOperation.All)]
    public List<ChatMessage> Messages { get; set; } = new();
}
