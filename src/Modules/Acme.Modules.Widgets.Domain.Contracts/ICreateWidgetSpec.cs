namespace Acme.Modules.Widgets.Domain.Contracts;

/// <summary>Inputs for <c>Widget.Create</c>. Implemented by the Application command model.</summary>
public interface ICreateWidgetSpec
{
    string Name { get; }
    int Quantity { get; }
}
