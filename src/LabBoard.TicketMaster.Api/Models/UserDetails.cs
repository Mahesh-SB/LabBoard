namespace LabBoard.TicketMaster.Api.Models;

public class UserDetails
{
    public Guid   Id       { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Gender   { get; set; } = string.Empty;
    public string Email    { get; set; } = string.Empty;
    public string Phone    { get; set; } = string.Empty;
}
