namespace Dodorassik.Core.Domain;

/// <summary>
/// A reusable clue printed on a card or hidden in the field. Kids find them
/// physically and the adult types the code into the phone, which lets the
/// app reveal the next part of the story or unlock a step.
/// </summary>
public class Clue
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HuntId { get; set; }
    public Hunt? Hunt { get; set; }

    /// <summary>Short code printed on the physical clue (e.g. "K3-42").</summary>
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Reveal { get; set; } = string.Empty;
    public int Points { get; set; } = 5;
}
