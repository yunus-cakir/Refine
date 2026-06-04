namespace Refine.App.Models;

public class RfDropdownOption<TValue>
{
    public TValue Value { get; set; } = default!;
    public string Text { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
}
