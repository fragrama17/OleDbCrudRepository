using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OleDbCrudRepository;

[Table("TblCustomers")]
public class Customer
{
    [Key]
    [Column("CustomerId")]
    public long Id { get; set; }
    [Column("CustomerName")] public string Name { get; set; }
    public string PostalAddress { get; set; }
    public string Email { get; set; }
    public DateTime BirthDate { get; set; }
}