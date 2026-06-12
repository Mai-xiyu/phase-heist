using Godot;

namespace PhaseHeist;

public interface IInteractable
{
    float InteractRadius { get; }
    bool CanInteract(Node3D actor);
    void Interact(Node3D actor);
    string GetInteractPrompt();
}
