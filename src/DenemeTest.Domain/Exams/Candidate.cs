using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace DenemeTest.Exams;

public class Candidate : FullAuditedAggregateRoot<Guid>
{
    public string FirstName { get; protected set; }
    public string LastName { get; protected set; }
    public string Email { get; protected set; }

    // ✅ Yeni alan
    public string Status { get; protected set; } = "Pending"; // Varsayılan: beklemede

    protected Candidate() { }

    public Candidate(Guid id, string firstName, string lastName, string email, string status = "Pending")
        : base(id)
    {
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        Status = status;
    }

    public void Update(string firstName, string lastName, string email, string status)
    {
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        Status = status;
    }
}
