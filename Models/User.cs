public class User
{
    public int Id { get; set; } // Explicit ID for dictionary key
    public string FirstName { get; set; } = ""; // Name
    public string LastName { get; set; } = ""; // Surname
    public int Age { get; set; } // Age
    public bool IsCustomer { get; set; } // Customer status
}

