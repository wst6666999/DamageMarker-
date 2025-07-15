using System.Windows.Input;

namespace DamageMaker.Models
{
    public class MenuItem
    {
        public string? Header { get; set; }
        public string? CommandParameter { get; set; }
        public ICommand? Command { get; set; }
    }
}
