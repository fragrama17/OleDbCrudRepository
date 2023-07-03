namespace OleDbCrudRepository;

public class Program
{
    public static void Main(string[] args)
    {
        var customerRepository = new CustomerRepository();

        customerRepository.Create(new Customer { Email = "may@mail.com" , Name = Guid.NewGuid().ToString(), BirthDate = DateTime.Today});
        customerRepository.Update(8,new Customer { Name = "Ugo", BirthDate = DateTime.Today.AddDays(-10)});
        
        var customers = customerRepository.FindAll();
        Console.WriteLine("All Customers:");
        foreach (var customer in customers)
        {
            Console.WriteLine(customer.Id);
        }
        
        Console.WriteLine($"Find By Id: {customerRepository.FindById(8).Name}");
        
    }
}