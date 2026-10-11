using TaskManagement.Domain.Enums;

namespace TaskManagement.Domain.Entities;

public class User
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public UserRole Role { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    public ICollection<RefreshToken> RefreshTokens { get; private set; } = new List<RefreshToken>();
    public ICollection<TaskItem> Tasks { get; private set; } = new List<TaskItem>();

    private User()
    {
    }

    public User(string name, string email, string passwordHash, UserRole role)
    {
        Id = Guid.NewGuid();
        Name = name;
        Email = email;
        PasswordHash = passwordHash;
        Role = role;
        CreatedAt = DateTime.UtcNow;
    }

    public void ChangePasswordHash(string passwordHash) => PasswordHash = passwordHash;

    public void ChangeRole(UserRole role) => Role = role;

    public void MarkAsDeleted()
    {
        if (IsDeleted)
        {
            return;
        }

        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
    }
}
