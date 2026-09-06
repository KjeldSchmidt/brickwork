namespace Brickwork.App.ViewModels;

/// <summary>A tool gesture label and what it does, e.g. ("Click Wall", "Change Wall Type").</summary>
public sealed record MapToolButtonHint(string Button, string Description);
